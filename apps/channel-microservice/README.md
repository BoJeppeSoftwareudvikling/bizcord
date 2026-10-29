# Channel microservice

ASP.NET Core Web API for Bizcord channel management.

## Course tasks

- [Week 44, task 01: Identify failure points](../../docs/FAILURE_POINTS.md)
- [Week 44, task 02: Implement reliability measures](../../docs/Week44ReliabilityMeasures.md)

## Run

```bash
dotnet run --project src
```

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
