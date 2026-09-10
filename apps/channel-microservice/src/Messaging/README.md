# Messaging

Denne mappe indkapsler kommunikationen med RabbitMQ, så resten af Channel Service ikke behøver at afhænge direkte af EasyNetQ.

## Filer

- `MessagingServiceCollectionExtensions.cs` samler DI-registreringen. `Program.cs` aktiverer den med `AddMessageClient(...)`.
- `IMessageClient.cs` er det teknologi-uafhængige interface til publish og subscribe. Hvis RabbitMQ senere udskiftes, kan en ny adapter implementere samme interface.
- `EasyNetQMessageClient.cs` er RabbitMQ-adapteren. Den oversætter kald til `IMessageClient` til EasyNetQ's Pub/Sub-API.
- `MessagingOptions.cs` repræsenterer sektionen `Messaging` fra `appsettings.json` som et C#-objekt. Den leverer RabbitMQ-forbindelsesstrengen til DI-opsætningen.

## Konfiguration

Standardværdien findes i `appsettings.json`:

```json
{
  "Messaging": {
    "ConnectionString": "host=localhost"
  }
}
```

Connection string kan overskrives med miljøvariablen `Messaging__ConnectionString`.

## Samlet flow

```text
appsettings.json
    -> MessagingOptions
    -> AddMessageClient()
    -> EasyNetQ IBus
    -> EasyNetQMessageClient
    -> IMessageClient i applikationskoden
    -> RabbitMQ
```

Hvis en anden message broker skal bruges, tilføjes en ny implementation af `IMessageClient`, og DI-registreringen ændres til den nye adapter. Resten af applikationskoden kan fortsætte med at bruge `IMessageClient`.
