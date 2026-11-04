# Bizcord

Bizcord is a semester project for an enterprise communication platform. Our chosen domain is channel management, implemented by the `Channel Service`.

## Links

- [Project description](docs/ProjectDescription.md)
- [Domain boundaries and container diagram](docs/DomainBoundariesAndServices.md)
- [Week 43: API gateway, routing, and authorization](docs/Week43ApiGatewayAndAuthorization.md)
- [Week 44, task 01: Channel Service failure points](docs/FAILURE_POINTS.md)
- [Week 44, task 02: Reliability measures and tests](docs/Week44ReliabilityMeasures.md)
- [Week 45, task 01: East-west security](docs/Week45EastWestSecurity.md)
- [Channel Service implementation](apps/channel-microservice)

## Start

Create a `.env` file in the repository root. The API gateway clone must use the
same `JWT_SIGNING_KEY` value:

```env
RABBITMQ_USER=guest
RABBITMQ_PASSWORD=replace-with-a-local-password
JWT_SIGNING_KEY=replace-with-at-least-32-random-characters
```

Then start the service and RabbitMQ:

```bash
docker compose up --build
```

The Channel Service is deliberately not published on a host port. Start the
separate API gateway after this stack; it attaches to the `bizcord-backend`
network and exposes the public entry point at <http://localhost:8080>. RabbitMQ
management remains available at <http://localhost:15672>.

Start Bizcord before the gateway because this Compose stack owns the external
Docker network used by both repositories.

## Test

```bash
dotnet test
```
