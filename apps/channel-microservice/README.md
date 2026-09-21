# Channel microservice

ASP.NET Core Web API for Bizcord channel management.

The project follows the layered structure used in the course example:

- `Controllers` contains the REST presentation layer.
- `Service` contains business logic.
- `Data` contains the EF Core context and repository layer.
- `Models` contains the private channel domain model and DTO converter.
- The repository-root `SharedModels` project contains contracts that other microservices can reference without depending on the private domain model.

## Channel API

| Method | Route | Result |
| --- | --- | --- |
| `GET` | `/Channel` | Returns all channels |
| `GET` | `/Channel/{id}` | Returns one channel or `404 Not Found` |
| `POST` | `/Channel` | Creates a channel and returns `201 Created` |
| `PUT` | `/Channel/{id}` | Updates a channel or returns `404 Not Found` |
| `DELETE` | `/Channel/{id}` | Deletes a channel and returns `204 No Content`, or `404 Not Found` |

`Name` is required and limited to 100 characters. `Description` is limited to 500 characters. ASP.NET Core returns `400 Bad Request` when these validation rules fail.

## Run

```bash
dotnet run --project src
```

## Test

```bash
dotnet test tests
```

## Docker

Build from the repository root so Docker can include the shared-model project:

```bash
docker build -f apps/channel-microservice/Dockerfile -t channel-microservice .
```
