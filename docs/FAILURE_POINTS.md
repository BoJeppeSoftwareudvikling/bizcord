# W44: Fejlpunkter i Channel Service

System Integration, Task 01: *Identify failure points in your microservice*.
Gennemgået 29. oktober 2026 mod Bizcord-commit `23d3e96`.

Dette er analysen før ændringerne. Se [Task 02: Implementerede reliability-tiltag](Week44ReliabilityMeasures.md) for aktuel afhjælpning og resterende risici.

Opgaven kræver en gennemgang af serviceafhængigheder, netværksantagelser og
datalagre samt dokumentation i microservicens mappe. Nedenstående fund er vores
analyse af Bizcords kode. Forslagene til afhjælpning er grundlag for Task 02 og er
ikke implementeret i denne opgave.

## Det aktuelle system

- HTTP-klienter kan læse og ændre kanaler. Writes venter på publicering til RabbitMQ.
- `MessagePostedSubscription` modtager `MessagePostedEvent`, opdaterer kanalens
  seneste aktivitet og publicerer `ChannelActivityRecordedEventDto`.
- `InMemoryChannelRepository` er en singleton med et `ConcurrentDictionary` i
  processen. Der findes ingen database eller Redis-cache i denne implementation.
- [Compose](../docker-compose.yml) starter én Channel Service og én RabbitMQ
  på samme Docker-host. Kun RabbitMQ har et persistent volume og et healthcheck.
- Ocelot-gatewayen ligger i et separat repository ifølge
  [W43-dokumentationen](../docs/Week43ApiGatewayAndAuthorization.md).
  Channel Service har ingen udgående HTTP-kald til andre services eller eksterne
  betalingsudbydere i den gennemgåede kode.

De øvrige services i [domænebeskrivelsen](../docs/DomainBoundariesAndServices.md)
er foreslåede samarbejdspartnere. Den implementerede event-kontrakt hedder
`MessagePostedEvent`, mens domænebeskrivelsen bruger navnet `MessageCreated`.
Fundene nedenfor tager udgangspunkt i den implementerede kontrakt.

## Serviceafhængigheder og netværk

### FP01: En langsom eller utilgængelig broker rammer writes og eventbehandling

[ChannelManagementService](../apps/channel-microservice/src/ChannelMicroservice.Presentation/Application/Channels/ChannelManagementService.cs)
venter på `PublishAsync` i `CreateAsync`, `UpdateAsync`, `DeleteAsync` og
`RecordMessageActivityAsync`.
[EasyNetQMessageClient](../packages/MessageClient/src/EasyNetQMessageClient.cs)
videresender kald og `CancellationToken` til EasyNetQ. Der er ingen eksplicit
operationstimeout, retry-policy, circuit breaker eller bulkhead i adapteren.

Hvis netværket eller RabbitMQ fejler, kan writes fejle eller vente på bibliotekets
timeout. Mange ventende kald kan binde hukommelse og forbindelser og skabe
cascading failure. `await` blokerer ikke i sig selv en worker-tråd. Efter en
vellykket opstart kan reads bruge den lokale repository uden at publicere, hvilket
giver mulighed for at bevare læseadgang under en brokerfejl.

Cancellation bliver allerede videresendt, men er ikke et selvstændigt tidsbudget.
Skal verificeres: EasyNetQ 7.8.0's effektive timeouts, publisher confirms og
connection recovery i den aktuelle opsætning. Koden beviser ikke, at kald kan
vente uendeligt, eller at biblioteket slet ikke forsøger at genoprette forbindelsen.

Forslag til Task 02: mål fejlvarighed, fastlæg et samlet tidsbudget og begræns
samtidige publish-kald. Retry skal være selektiv og sikker i forhold til FP02 og
FP04. Afprøv både forbindelsesbrud og en broker, der svarer langsomt, og mål også
læsekald under belastningen. Prioritet: høj.

### FP02: Lokal ændring og publicering kan ende med forskellige resultater

I [ChannelManagementService](../apps/channel-microservice/src/ChannelMicroservice.Presentation/Application/Channels/ChannelManagementService.cs)
ændrer `CreateAsync`, `UpdateAsync`, `DeleteAsync` og `RecordMessageActivityAsync`
repositoryet før publicering. Der findes ingen transaktion eller outbox, som
knytter de to handlinger sammen.

Fejler publiceringen, består den lokale ændring, mens eventet kan mangle hos
modtagerne. [ChannelsController](../apps/channel-microservice/src/ChannelMicroservice.Presentation/Controllers/ChannelsController.cs)
fanger kun `ChannelValidationException`; en brokerfejl bliver ikke et kontrolleret
fejlsvar fra controlleren. Klienten kan derfor få en fejl efter en gennemført
lokal write. Et tabt svar kan også efterlade afsenderen uden viden om, hvorvidt
brokeren modtog beskeden.

En gentaget `CreateAsync` opretter et nyt `Guid` og kan give en ekstra kanal.
En gentaget delete finder derimod ikke kanalen og returnerer uden at publicere det
manglende `ChannelDeletedEventDto`. Blind retry af hele operationen reparerer derfor
ikke nødvendigvis fejlen.

Forslag til Task 02: brug holdbar lagring og en transactional outbox til tilstand
og event samt idempotency keys for gentagne requests. En fallback må ikke kvittere
for en write, hvis resultatet ikke kan bevares og leveres. Afprøv med en
`IMessageClient`, der fejler efter repository-opdateringen, og kontrollér både
tilstand, events og klientens gentagelse. Prioritet: høj.

### FP03: Brokerafhængigheden kan forhindre opstart af hele API'et

[MessagePostedSubscription.StartAsync](../apps/channel-microservice/src/ChannelMicroservice.Presentation/Infrastructure/Messaging/MessagePostedSubscription.cs)
venter på `SubscribeAsync` og håndterer ikke en opstartsfejl. Servicen registreres
som hosted service i
[AddChannelInfrastructure](../apps/channel-microservice/src/ChannelMicroservice.Presentation/Infrastructure/ServiceCollectionExtensions.cs).
En kastet subscription-fejl kan derfor afbryde hostens opstart. En ventende
subscription kan forsinke opstart, også selv om HTTP-reads kun behøver lokal tilstand.

[Compose](../docker-compose.yml) venter på RabbitMQ's healthcheck før start.
Det reducerer risikoen for at starte for tidligt, men beviser ikke, at appens
credentials eller adgang til køer er korrekte. Det håndterer heller ikke senere
brokerfejl. Compose-filen har ingen restart-policy for de to services.

Forslag til Task 02: vælg eksplicit mellem kontrolleret opstartsfejl og en API,
som starter med degraderet messaging og forsøger at abonnere igen. Afprøv separat
med utilgængelig broker og forkerte credentials. Konfigurationsfejl skal ikke have
samme retry-policy som transient netværksfejl. Prioritet: mellem.

## Datalagre og beskedbehandling

### FP04: Genlevering og forkert rækkefølge kan give forkerte aktivitetsdata

[RecordMessageActivityAsync](../apps/channel-microservice/src/ChannelMicroservice.Presentation/Application/Channels/ChannelManagementService.cs)
overskriver altid `LastMessageId` og `LastActivityAt` og publicerer et nyt
`ChannelActivityRecordedEventDto`. `MessageId` findes i kontrakten, men metoden
gemmer ikke behandlede ID'er og sammenligner ikke tidspunktet med seneste aktivitet.

Den samme besked kan derfor udløse flere udgående events. Kommer en ældre besked
efter en nyere, flytter den kanalens seneste aktivitet baglæns. En publish-fejl
efter lokal opdatering giver også risiko for gentagen behandling ved genlevering.
Skal verificeres: brokerens konkrete genleveringsadfærd i dette setup.

Forslag til Task 02: deduplication med et stabilt `MessageId`, en aftalt regel for
eventrækkefølge og atomisk opdatering af aktivitet, behandlet ID og outbox.
Afprøv samme besked to gange og to beskeder i omvendt tidsrækkefølge. Kontrollér
både kanalens tilstand og antallet af udgående events. Prioritet: høj.

### FP05: Genstart mister kanaldata, og flere instanser får hver sin tilstand

[InMemoryChannelRepository](../apps/channel-microservice/src/ChannelMicroservice.Presentation/Infrastructure/InMemoryChannelRepository.cs)
gemmer alle kanaler i proceshukommelsen. Ved procesnedbrud eller genstart forsvinder
kanaler, medlemsdata og aktivitetsdata. RabbitMQ's volume indeholder ikke dette
repository og kan ikke gendanne det.

Hver serviceinstans får sit eget repository. Ekstra instanser kan derfor returnere
forskellige kanallister, og en instans kan modtage en `MessagePostedEvent` for en
kanal, som kun findes hos en anden instans. Efter genstart kan ventende events
også referere til nu manglende kanaler og ramme FP07.

Forslag til Task 02: afklar krav til durability og recovery, indfør persistent
lagring ejet af Channel Service og test gendannelse. Afprøv genstart med
midlertidige testkanaler og læsning fra to instanser. Når en database tilføjes,
skal forbindelsesfejl, langsomme queries og database-timeouts undersøges særskilt.
En ekstra in-memory kopi er ikke en holdbar fallback. Prioritet: høj.

### FP06: Delte mutable objekter tillader delvise og samtidige ændringer

[InMemoryChannelRepository.GetAsync](../apps/channel-microservice/src/ChannelMicroservice.Presentation/Infrastructure/InMemoryChannelRepository.cs)
returnerer selve det gemte `Channel`-objekt. `ConcurrentDictionary` beskytter
dictionary-operationer, men gør ikke en sekvens af ændringer på objektet atomisk.
`SaveAsync` bruger en assignment, der også kan indsætte en manglende kanal.

Et konkret eksempel findes i
[UpdateAsync](../apps/channel-microservice/src/ChannelMicroservice.Presentation/Application/Channels/ChannelManagementService.cs):
metoden tildeler `Name`, før den validerer `Description`. Et gyldigt nyt navn
og en beskrivelse på over 500 tegn kan derfor ændre det gemte navn, selv om
controlleren returnerer `400` og intet update-event publiceres. Samtidige writes
kan desuden overskrive hinanden. En operation, der har læst en kanal før en
sletning, kan indsætte kanalen igen ved et efterfølgende `SaveAsync`.

Forslag til Task 02: valider hele kommandoen før ændringer, undgå at udlevere
mutable lagringsobjekter, og brug atomiske writes med concurrency-kontrol.
Afprøv den ugyldige beskrivelse og en styret rækkefølge af read, delete og save.
Prioritet: høj.

### FP07: Ugyldige events kræver en plan for fejlkø og recovery

[MessagePostedHandler](../apps/channel-microservice/src/ChannelMicroservice.Presentation/Infrastructure/Messaging/MessagePostedHandler.cs)
afviser `PostedAt`, hvis tidspunktet ikke er UTC.
[RecordMessageActivityAsync](../apps/channel-microservice/src/ChannelMicroservice.Presentation/Application/Channels/ChannelManagementService.cs)
afviser tomme ID'er og kaster, hvis kanalen ikke findes.
[MessagePostedSubscription](../apps/channel-microservice/src/ChannelMicroservice.Presentation/Infrastructure/Messaging/MessagePostedSubscription.cs)
lader exceptionen gå tilbage til EasyNetQ.

Valideringen forhindrer ugyldig aktivitet i at blive behandlet, men projektet har
ingen eksplicit politik for permanente kontraktfejl, midlertidigt manglende data
eller manuel genbehandling. Skal verificeres: EasyNetQ's standard error queue,
acknowledgement og genlevering. Det er ikke påvist, at en ugyldig besked blokerer
hele køen eller retriedes uendeligt.

Forslag til Task 02: adskil transient fejl fra poison messages, overvåg fejlkøen
og dokumentér, hvordan beskeder rettes eller genbehandles. Afprøv ugyldig UTC,
ukendt kanal og derefter en gyldig besked. Kontrollér fejlkø og fortsat fremdrift.
Prioritet: mellem.

## Drift og kapacitet

### FP08: Health-endpointet opdager ikke, om messaging virker

[Program.cs](../apps/channel-microservice/src/ChannelMicroservice.Presentation/Program.cs) returnerer altid
`200` med `Status = "Healthy"` fra `/health`, når endpointet kan kaldes. Det tjekker
ikke forbindelsen til RabbitMQ eller subscriptionens tilstand. Et positivt svar
viser derfor HTTP-liveness, men ikke om writes og eventbehandling fungerer.

Forslag til Task 02: adskil liveness og readiness efter den aftalte degradering.
En brokerfejl bør ikke automatisk starte en endeløs genstartscyklus og dermed
miste in-memory data. Mål publish-fejl, behandlingstid og køernes længde. Afprøv
`/health` under en brokerfejl efter succesfuld opstart. Prioritet: mellem.

### FP09: Voksende data og køer kan udtømme fælles ressourcer

[ListAsync](../apps/channel-microservice/src/ChannelMicroservice.Presentation/Infrastructure/InMemoryChannelRepository.cs)
sorterer og kopierer alle kanaler. Applikationsservicen opretter derefter endnu
en liste med DTO'er. Der er ingen pagination eller grænse for antallet af gemte
kanaler. Mange kanaler og samtidige læsninger kan derfor øge CPU- og hukommelsesbrug.

Langsom eventbehandling kan samtidig opbygge backlog hos RabbitMQ. Der er ingen
eksplicitte kapacitetsgrænser i [Compose](../docker-compose.yml) eller
projektspecifik prefetch-konfiguration i
[EasyNetQMessageClient](../packages/MessageClient/src/EasyNetQMessageClient.cs).
Begge containere deler hostens ressourcer. Skal verificeres: effektive
brokergrænser, prefetch og belastningen, hvor svartid eller hukommelse bliver
uacceptabel. Der er ikke udført en loadtest.

Forslag til Task 02: pagination, relevante ressourcegrænser, kontrolleret
consumer-concurrency og load shedding ud fra målte behov. Afprøv voksende
kanallister og en langsom consumer, mens API-svartid, memory og backlog måles.
Prioritet: mellem.

## Gateway og øvrige dependencies

Gatewayen er klientens dokumenterede indgangspunkt. En utilgængelig gateway eller
fejl i dens route kan derfor afskære klientadgang, selv om Channel Service virker.
Skal verificeres i gateway-repositoryet: downstream-timeout, retry, circuit breaker
og deploymentkonfiguration. W43-noten dokumenterer routing og authorization,
men er ikke bevis for den aktuelle gatewayproces eller dens resilience-politikker.

Der er ingen ekstern database eller tredjepartsservice at fejlinjicere i den
nuværende Channel Service. Datalagets aktuelle risiko er især tab og inkonsistens
i hukommelsen. Foreslåede fremtidige servicekald skal have deres egen vurdering,
når de implementeres.

## Prioritering til næste opgave

1. Afklar accepteret datatab, recovery-tid og et samlet tidsbudget for writes.
   Dette er krav, som teamet skal vælge, ikke tal opgivet i opgaven.
2. Prioritér FP02, FP04, FP05 og FP06, så lagring og gentagelse giver korrekte
   resultater. Outbox kræver et holdbart datalager og gør ikke consumers automatisk
   idempotente.
3. Vælg timeouts, begrænset retry med backoff og eventuelt circuit breaker for
   konkrete transient brokerfejl. Bevar læseadgang, hvis den er forretningsmæssigt
   brugbar, og giv tydelig fejl, når en write ikke kan gennemføres sikkert.
4. Verificér opstart, readiness, fejlkøer og belastning med de foreslåede scenarier.

Lærerens `distributed-counter` illustrerer retry ved `RedisConnectionException`
og fallback fra Redis til en lokal database. Bizcord bruger RabbitMQ og et
in-memory repository. Exception-typerne, tidsbudgettet og fallback-valget skal
derfor begrundes ud fra Bizcords egne operationer. Retry kan ikke i sig selv
løse tab af data eller forskellen mellem en gemt ændring og et manglende event.

## Verifikation

Kildekode og konfiguration er gennemgået statisk. Den eksisterende suite blev
kørt den 29. oktober 2026:

```bash
dotnet test Bizcord.slnx --no-restore --verbosity minimal
```

Resultat: 18 tests bestod, 0 fejlede, 0 blev sprunget over. Det inkluderer
[MessagePostedFlowTests](../apps/channel-microservice/tests/Integration/MessagePostedFlowTests.cs), som starter
sin egen RabbitMQ-container og kontrollerer det normale eventflow. De eksisterende
tests dækker også validering og videresendelse af cancellation tokens.

Testresultatet dokumenterer udgangspunktet. Brokerudfald, latency, genstart,
genlevering, samtidighed og load er ikke afprøvet i denne opgave. Scenarierne under
de enkelte fund er forslag til næste verifikation, ikke udførte tests.

## Undervisningskilder

- Opgaveskærmbilledet *Task 01: Identify failure points in your microservice*,
  leveret sammen med implementeringsønsket.
- [[00 Subjects/System Integration/W44 - Reliability in microservices/W44 - Reliability in microservices|W44 - Reliability in microservices]].
- [[00 Subjects/System Integration/W44 - Reliability in microservices/Kapitel 12 - Resiliency - TLDR|Kapitel 12 - Resiliency - TLDR]], om fejl i dependencies, degradering, tidsbudgetter og stability patterns.
- [[00 Subjects/System Integration/W44 - Reliability in microservices/Retry og fallback med Polly i distributed-counter|Retry og fallback med Polly i distributed-counter]], om lærerens policies og deres begrænsninger.
- [[00 Subjects/System Integration/W44 - Reliability in microservices/Resources/distributed-counter/projects/CounterService/Core/Services/NewLikeService.cs|Lærerens NewLikeService.cs]] og [[00 Subjects/System Integration/W44 - Reliability in microservices/Resources/distributed-counter/projects/CounterService/Core/Services/LikeAggregationService.cs|LikeAggregationService.cs]], gennemgået sammen med noterne.

Kildehenvisningerne til undervisningsmaterialet er Obsidian-links til den lokale
vault. Kodehenvisningerne ovenfor er relative links til filer i Bizcord.
