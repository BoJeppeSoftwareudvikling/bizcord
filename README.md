# Bizcord

Bizcord is a semester project for an enterprise communication platform. Our chosen domain is channel management, implemented by the `Channel Service`.

## Links

- [Project description](docs/ProjectDescription.md)
- [Domain boundaries and container diagram](docs/DomainBoundariesAndServices.md)
- [Week 43: API gateway, routing, and authorization](docs/Week43ApiGatewayAndAuthorization.md)
- [Week 44, task 01: Channel Service failure points](docs/FAILURE_POINTS.md)
- [Week 44, task 02: Reliability measures and tests](docs/Week44ReliabilityMeasures.md)
- [Week 45, task 01: East-west security](docs/Week45EastWestSecurity.md)
- [Week 45, task 02: HashiCorp Vault integration](docs/Week45VaultIntegration.md)
- [Week 46, tasks 01 and 02: Attachment upload saga and demo](docs/Week46AttachmentSaga.md)
- [Week 47, task 01: Fluentd logging sidecar](docs/Week47LoggingSidecar.md)
- [Week 47, task 02: GraphQL presentation layer](docs/Week47GraphQLPresentation.md)
- [Channel Service implementation](apps/channel-microservice)

## Start

Create a `.env` file in the repository root:

```env
RABBITMQ_USER=guest
RABBITMQ_PASSWORD=replace-with-a-local-password
VAULT_TOKEN=replace-with-a-short-lived-channel-service-token
```

Bootstrap and unseal Vault, store the RabbitMQ connection string and shared JWT
signing key, and create the scoped application tokens by following
[the week 45 Vault guide](docs/Week45VaultIntegration.md). The API gateway uses a
different token from Channel Service.

Then start the local stack:

```bash
docker compose up --build
```

The Channel Service is deliberately not published on a host port. Start the
separate API gateway after this stack; it attaches to the `bizcord-backend`
network and exposes the public entry point at <http://localhost:8080>. Vault's UI
is available at <http://localhost:8200>, and RabbitMQ management remains available
at <http://localhost:15672>.

Start Bizcord before the gateway because this Compose stack owns the external
Docker network used by both repositories.

## Logs

Channel Service writes to stdout using ASP.NET Core's default console logger.
Docker forwards its output to the Fluentd companion, which prints collected
records to its own console:

```bash
docker compose logs -f fluentd
```

See [the logging guide](docs/Week47LoggingSidecar.md) for verification, transport
choices and delivery limits.

## GraphQL

POST GraphQL queries and mutations to `http://localhost:8080/graphql` through the
API gateway with a bearer token. `channels` and `channel(id)` require
`channel.read`; `createChannel(input)` requires `channel.write`. REST remains
available. Rebuild the gateway too, so its new route is loaded.

See [the GraphQL guide](docs/Week47GraphQLPresentation.md) for runnable requests,
architecture decisions and verification.

## Test

```bash
dotnet test
```
