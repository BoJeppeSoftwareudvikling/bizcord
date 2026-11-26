# Channel microservice

ASP.NET Core Web API for Bizcord channel management.

## Course tasks

[Week 48 reviewer guide for all ten tasks](../../docs/Week48Review.md)

- [Week 44, task 01: Identify failure points](../../docs/FAILURE_POINTS.md)
- [Week 44, task 02: Implement reliability measures](../../docs/Week44ReliabilityMeasures.md)
- [Week 45, task 01: East-west security](../../docs/Week45EastWestSecurity.md)
- [Week 45, task 02: HashiCorp Vault integration](../../docs/Week45VaultIntegration.md)

## Run

The service requires a running, unsealed Vault and a scoped `VAULT_TOKEN`. Follow
the [Vault bootstrap and manual test guide](../../docs/Week45VaultIntegration.md),
then run the service through the repository Compose stack.

## Test

```bash
dotnet test tests
```

## Docker

The Dockerfile references projects and packages outside this service directory, so
the build context must be the Bizcord repository root.

From the repository root, either start the complete local stack:

```bash
docker compose up --build
```

Or build only the Channel Service image:

```bash
docker build -f apps/channel-microservice/Dockerfile -t channel-microservice .
```

Compose does not publish the service on a host port. Client traffic enters through
the separate API gateway on port `8080`; gateway-to-service traffic uses the
internal `bizcord-backend` network and `channel-microservice:8080`.

At startup, Channel Service reads its RabbitMQ connection string and the shared
JWT signing key from Vault. It exits when Vault is sealed, the application token
is missing or denied, or a required secret is absent.
