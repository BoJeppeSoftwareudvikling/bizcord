# MessageClient

Denne pakke indkapsler kommunikationen med RabbitMQ, så resten af Channel Service ikke behøver at afhænge direkte af EasyNetQ.

Event-kontrakterne ligger i `packages/Messages`. Denne pakke kender ikke de konkrete beskeder, fordi `IMessageClient` er generisk.

## Filer

- `MessagingServiceCollectionExtensions.cs` samler DI-registreringen. `Program.cs` aktiverer den med `AddMessageClient(...)`.
- `IMessageClient.cs` er det teknologi-uafhængige interface til publish og subscribe. Hvis RabbitMQ senere udskiftes, kan en ny adapter implementere samme interface.
- `BoundedMessageClient.cs` afgrænser publish-ventetid og samtidige publish-kald med timeout og bulkhead.
- `EasyNetQMessageClient.cs` er RabbitMQ-adapteren bag `BoundedMessageClient`. Den kalder EasyNetQ's Pub/Sub-API.
- `MessagingOptions.cs` repræsenterer sektionen `Messaging` fra `appsettings.json` som et C#-objekt. Den leverer RabbitMQ-forbindelsesstrengen til DI-opsætningen.

## Konfiguration

Standardværdien findes i `appsettings.json`:

```json
{
  "Messaging": {
    "ConnectionString": "host=localhost",
    "PublishTimeout": "00:00:05",
    "MaxConcurrentPublishes": 16
  }
}
```

Connection string kan overskrives med miljøvariablen `Messaging__ConnectionString`.
Grænserne kan ændres med `Messaging__PublishTimeout` og `Messaging__MaxConcurrentPublishes`.
Timeout skal være positiv og højst fem minutter. Antal samtidige kald skal være
mellem 1 og 1024. Ugyldig konfiguration afvises ved opstart.

## Samlet flow

```text
appsettings.json
    -> MessagingOptions
    -> AddMessageClient()
    -> registrerer IMessageClient som BoundedMessageClient

Applikationskode
    -> IMessageClient / BoundedMessageClient
    -> EasyNetQMessageClient
    -> EasyNetQ IBus
    -> RabbitMQ
```

Hvis en anden message broker skal bruges, tilføjes en ny implementation af `IMessageClient`, og DI-registreringen ændres til den nye adapter. Resten af applikationskoden kan fortsætte med at bruge `IMessageClient`.

Bulkheaden afviser straks ekstra publishes uden at oprette en ventekø. Ved timeout
anmoder klienten om cancellation. Ignorerer adapteren cancellation, beholder det
igangværende kald sin plads indtil afslutning. Wrapperen gentager aldrig en publish.
Subscribe videresendes uændret og har ikke denne timeout-policy.

Et timeout betyder ikke, at eventet ikke blev leveret. Se
[W44-tiltag og begrænsninger](../../docs/Week44ReliabilityMeasures.md).
