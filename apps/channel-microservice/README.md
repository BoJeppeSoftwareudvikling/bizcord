# Channel microservice

ASP.NET Core Web API for Bizcord channel management.

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
