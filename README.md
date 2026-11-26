# Bizcord

Bizcord's Channel Service manages channels for an enterprise communication platform.

**Reviewing week 48? Start with [Compulsory assignment #2](docs/Week48Review.md).** The guide maps all ten Moodle tasks to their design decisions, implementation and expected results.

## Start

From this checkout, follow the [Vault bootstrap](docs/Week45VaultIntegration.md#manual-setup-and-verification) to configure RabbitMQ and issue separate tokens for Channel Service and the gateway. Then start the stack from the repository root:

```bash
docker compose up -d --build
docker compose ps
```

Use the gateway at `http://localhost:8080` for REST and GraphQL. Channel Service runs on the private Docker network. The [week 43 guide](docs/Week43ApiGatewayAndAuthorization.md#manual-verification) shows how to obtain a read token and make the first request.

## Test

With the .NET 10 SDK and Docker running, execute the service suite (including RabbitMQ integration tests) and the gateway smoke suite from the repository root:

```bash
dotnet test Bizcord.slnx
python3 apps/api-gateway/tests/smoke.py
```

## Project

- [Project description](docs/ProjectDescription.md)
- [Domain boundaries and container diagram](docs/DomainBoundariesAndServices.md)
- [Channel Service](apps/channel-microservice) · [API gateway](apps/api-gateway) · [Upload workflow](apps/upload-workflow)
