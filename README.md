# Bizcord

Bizcord is a semester project for an enterprise communication platform. Our chosen domain is channel management, implemented by the `Channel Service`.

## Links

- [Project description](docs/ProjectDescription.md)
- [Domain boundaries and container diagram](docs/DomainBoundariesAndServices.md)
- [Week 43: API gateway, routing, and authorization](docs/Week43ApiGatewayAndAuthorization.md)
- [Week 44, task 01: Channel Service failure points](docs/FAILURE_POINTS.md)
- [Week 44, task 02: Reliability measures and tests](docs/Week44ReliabilityMeasures.md)
- [Channel Service implementation](apps/channel-microservice)

## Start

Create a `.env` file in the repository root:

```env
RABBITMQ_USER=guest
RABBITMQ_PASSWORD=guest
```

Then start the service and RabbitMQ:

```bash
docker compose up --build
```

The API and Swagger UI are available at <http://localhost:8000> and <http://localhost:8000/swagger>. RabbitMQ management is available at <http://localhost:15672>.

## Test

```bash
dotnet test
```
