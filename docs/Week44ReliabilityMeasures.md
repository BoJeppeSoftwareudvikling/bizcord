# W44: Reliability-tiltag i Channel Service

System Integration, Task 02: *Implement reliability measures in your microservices*.
Implementeret som opfølgning på [Task 01](FAILURE_POINTS.md).

Opgaven beder om at afhjælpe nogle af de identificerede fejlpunkter og begrunde
arkitekturlag, policy og test. Denne iteration prioriterer FP01 og FP06 samt den
del af FP04, hvor en ældre besked overskriver nyere aktivitet.

## Tiltag og placering

### Timeout og bulkhead ved publicering, FP01

[BoundedMessageClient](../packages/MessageClient/src/BoundedMessageClient.cs)
implementerer `IMessageClient` og ligger som singleton omkring
`EasyNetQMessageClient` i infrastrukturlaget. Alle applikationens publishers
bruger derfor samme kapacitetsgrænse uden at kende RabbitMQ eller timeout-koden.
DI-registreringen ligger i
[MessagingServiceCollectionExtensions](../packages/MessageClient/src/MessagingServiceCollectionExtensions.cs).

Et publish får som standard fem sekunders budget og et cancellation token, der
kombinerer callerens cancellation med timeout. `WaitAsync` afgrænser ventetiden,
også hvis det underliggende asynkrone kald ignorerer cancellation. Dette er et
budget for publiceringen, ikke en garanti for hele HTTP-requestens varighed eller
for opstart af forbindelsen.

En `SemaphoreSlim` tillader højst 16 igangværende publishes pr. proces. Er alle
pladser optaget, afvises et nyt publish straks. Der findes ingen ekstra ventekø.
Hvis et publish fortsætter efter timeout eller caller-cancellation, beholdes dets
plads, indtil den underliggende task faktisk afsluttes. Ellers ville gentagne
timeouts kunne omgå grænsen og skabe stadig flere baggrundskald. En sen fejl
observeres, og pladsen frigives, når tasken slutter.

Timeout beskytter callerens ventetid. Bulkhead og load shedding begrænser de
igangværende brokerkald. En vedvarende fejl kan optage alle pladser og afvise nye
writes; det er en kontrolleret begrænsning frem for ubegrænset ophobning.
Reads publicerer ikke og kan fortsætte efter vellykket serviceopstart.

Wrapperen foretager ét publish-forsøg og lader adapterens øvrige fejl gå videre.
Den ændrer ikke EasyNetQ's interne connection recovery. `SubscribeAsync` bliver
videresendt uændret, så opstartsafhængigheden i FP03 består.

### Kopier og optimistic concurrency, FP06

[InMemoryChannelRepository](../apps/channel-microservice/src/ChannelMicroservice.Presentation/Infrastructure/InMemoryChannelRepository.cs)
gemmer og returnerer kopier af kanaler og deres mutable medlemsobjekter.
En caller kan derfor ikke ændre lagrede data blot ved at ændre et returneret
objekt eller det oprindelige input.

Repositoryet tildeler en intern `Version`. `SaveAsync` kræver, at den læste
version stadig er aktuel, og bruger `ConcurrentDictionary.TryUpdate` til atomisk
udskiftning. Har en anden operation gemt eller slettet kanalen, kastes
`ChannelConcurrencyException`. En save genindsætter ikke en slettet kanal.
Versionen er intern og ændrer ikke den eksisterende HTTP-DTO. Dette beskytter
overlappende serveroperationer; en gammel klients redigering opdages ikke via
ETag eller `If-Match`, da API'et endnu ikke udstiller en version.

[ChannelManagementService.UpdateAsync](../apps/channel-microservice/src/ChannelMicroservice.Presentation/Application/Channels/ChannelManagementService.cs)
validerer både navn og beskrivelse, før objektet ændres. En beskrivelse på over
500 tegn kan dermed ikke give et fejlsvar og samtidig ændre kanalens navn.
Valideringen hører til applikationslaget, mens kopiering og atomisk save
hører til repository-implementationen.

### Ældre aktivitet ignoreres, FP04 delvist

`RecordMessageActivityAsync` sammenligner beskedens tidspunkt med kanalens
`LastActivityAt`. Er beskeden ældre, afsluttes behandlingen uden save eller et
nyt activity-event. Versionskontrollen beskytter også mod et konkurrerende save
mellem læsning og opdatering.

Dette er en regel om seneste aktivitet, ikke fuld deduplication. Beskeder med samme
tidspunkt afvises ikke. En genleveret besked kan derfor stadig publicere igen,
hvilket også lader en gentagelse forsøge publicering efter en tidligere publish-fejl.
Consumers skal fortsat kunne tåle dubletter. Konflikter i en consumer kastes
videre til EasyNetQ; der er ikke tilføjet automatisk retry eller en ny fejlkø-policy.

### Kontrollerede fejl ved HTTP-grænsen

[ReliabilityExceptionHandler](../apps/channel-microservice/src/ChannelMicroservice.Presentation/ReliabilityExceptionHandler.cs)
er registreret i `Program.cs` og oversætter de nye exceptions til Problem Details:

- `409 Conflict` ved en samtidig ændring eller sletning.
- `504 Gateway Timeout` ved wrapperens publish-timeout.
- `503 Service Unavailable` når alle publish-pladser er optaget.

Caller-cancellation bliver ikke lavet om til publish-timeout. Andre exception-typer
bliver overladt til den almindelige exception handling. Fejlsvarene for publicering
oplyser, at kanaltilstanden allerede kan være ændret. Et timeout beviser heller
ikke, at brokeren aldrig modtog eventet.

## Konfiguration og policyvalg

Standarderne står i
[appsettings.json](../apps/channel-microservice/src/ChannelMicroservice.Presentation/appsettings.json):

```json
{
  "Messaging": {
    "PublishTimeout": "00:00:05",
    "MaxConcurrentPublishes": 16
  }
}
```

Miljøvariablerne `Messaging__PublishTimeout` og
`Messaging__MaxConcurrentPublishes` kan overskrive dem. Timeout skal være positiv
og højst fem minutter. Kapacitetsgrænsen skal være mellem 1 og 1024.
Ugyldige værdier afvises ved opstart. Fem sekunder og 16 samtidige kald er
startværdier til øvelsen, ikke målte kapacitetsgrænser eller lærerfastsatte SLO'er.

Tiltagene bruger .NET's cancellation og semaphore uden nye package-afhængigheder.
Lærerens Polly-eksempel viser retry ved `RedisConnectionException` og fallback
til en anden datakilde. Bizcord har ikke den samme dependency eller datakilde.
Vi gentager derfor ikke automatisk writes og returnerer ikke en falsk succes
ved brokerfejl. En sikker retry af business-operationen kræver først en løsning
på FP02 med holdbar lagring, outbox og idempotency.

Circuit breaker er ikke valgt i denne iteration. Timeout og bulkhead afgrænser
ventetid og samtidige publishes, men stopper ikke nye forsøg efter en historik af
fejl, sådan som en breaker ville gøre.

## Tests og fejlsimulering

Kør fra repositoryets rod:

```bash
dotnet test Bizcord.slnx --no-restore --verbosity minimal
```

- [BoundedMessageClientTests](../apps/channel-microservice/tests/Messaging/BoundedMessageClientTests.cs)
  simulerer et langsomt brokerkald, cancellation fra caller, fuld kapacitet,
  en synkron adapterfejl og et kald, der ignorerer cancellation og senere fejler.
  De kontrollerer timeout, ét forsøg, frigivelse af pladser og genoptaget publicering.
- [InMemoryChannelRepositoryTests](../apps/channel-microservice/tests/Infrastructure/InMemoryChannelRepositoryTests.cs)
  kontrollerer isolerede kopier, en forældet save og save efter delete.
- [ChannelReliabilityTests](../apps/channel-microservice/tests/Application/Channels/ChannelReliabilityTests.cs)
  kontrollerer ugyldig opdatering uden delvis mutation, ældre beskeder og reads
  under en blokeret publish. Testen viser også den resterende FP02-risiko:
  kanalen findes stadig efter publish-timeout.
- [ReliabilityExceptionHandlerTests](../apps/channel-microservice/tests/Presentation/ReliabilityExceptionHandlerTests.cs)
  kontrollerer Problem Details og statuskoderne 409, 503 og 504.
- [MessagingServiceCollectionExtensionsTests](../apps/channel-microservice/tests/Messaging/MessagingServiceCollectionExtensionsTests.cs)
  kontrollerer singleton-registrering, standardkonfiguration og afvisning af ugyldige grænser.
- [MessagePostedFlowTests](../apps/channel-microservice/tests/Integration/MessagePostedFlowTests.cs)
  bruger en rigtig, isoleret RabbitMQ-testcontainer og kontrollerer det normale
  eventflow gennem den nye wrapper.

Fejltests bruger kontrollerede adaptersimuleringer. De er ikke målinger af
RabbitMQ under netværksudfald eller en loadtest af produktionskapaciteten.

Verificeret 29. oktober 2026: alle 38 tests bestod, 0 fejlede og 0 blev sprunget
over. Heraf er 20 nye tests for de valgte reliability-tiltag. RabbitMQ-testen
bekræftede også, at DI leverer den nye wrapper. Buildet gav ingen advarsler.

## Resterende risici

- FP02: lokal write og publish er stadig to operationer. Timeout og overbelastning
  ruller ikke en write tilbage. Durable outbox og request-idempotency mangler.
- FP04: beskyttelsen mod ældre aktivitet er implementeret, men fuld deduplication,
  atomisk eventlevering og en regel for forskellige beskeder med samme tidspunkt mangler.
- FP05: data og versionsnumre ligger stadig i hukommelsen. Genstart mister data,
  og flere instanser har hver sin tilstand.
- FP03, FP07 og FP08: opstartsafhængighed, fejlkø/replay og dependency-readiness
  er fortsat åbne. `/health` er stadig kun et statisk liveness-svar.
- FP09: publishes er afgrænset pr. proces, men kanallister, datamængde og
  RabbitMQ-backlog er stadig uden projektspecifikke kapacitetsgrænser.

## Kilder

- Opgaveskærmbilledet *Task 02: Implement reliability measures in your microservices*.
- [Task 01: Fejlpunkter](FAILURE_POINTS.md), især FP01, FP04 og FP06.
- [[00 Subjects/System Integration/W44 - Reliability in microservices/Kapitel 12 - Resiliency - TLDR|Kapitel 12 - Resiliency - TLDR]], om tidsbudget, bulkheads, load shedding og sikre retries.
- [[00 Subjects/System Integration/W44 - Reliability in microservices/Retry og fallback med Polly i distributed-counter|Retry og fallback med Polly i distributed-counter]], om lærerens konkrete policyvalg og begrænsninger.

Undervisningskilderne er Obsidian-links til vaulten. Implementationsvalg og
standardværdier ovenfor er projektets egne valg.
