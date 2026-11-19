# Week 47, task 02: GraphQL presentation layer

## Result

Channel Service now exposes a HotChocolate 16.7.6 GraphQL endpoint at `/graphql`
alongside its existing REST controllers. A frontend can request a channel list
and selected-channel details in one operation, choosing only the fields it needs.
The `createChannel` mutation reuses the same creation rules and event publication
as REST.

```text
Client → API gateway POST /graphql → HotChocolate resolver ─┐
Client → API gateway /channels     → REST controller ──────┤
                                                         ↓
                                            ChannelManagementService
                                                         ↓
                                           Repository + IMessageClient
```

The gateway repository also needs the new `channel-graphql` route in
`src/Configuration/ocelot.example.json`. Rebuild both repositories when running
this feature. The service's HTTP port remains internal to `bizcord-backend`.

## API contract

| Field | Existing application method | Required scope |
| --- | --- | --- |
| `channels` | `ListAsync` | `channel.read` |
| `channel(id: UUID!)` | `GetAsync` | `channel.read` |
| `createChannel(input: CreateChannelInput!)` | `CreateAsync` | `channel.write` |

`Channel` exposes `id`, `name`, `description`, `createdAt` and `updatedAt`.
`CreateChannelInput` requires `name`; `description` is optional. A missing channel
returns `null`. An empty GUID returns a validation error. A blank name reaches the
existing application validation; null/missing required GraphQL input is rejected
by schema validation first.

`channels` lists the same channels as REST. It is not a user's membership list.
The assignment's illustrative `GetUserChannelsAsync` does not exist in Bizcord's
service layer. Adding it would require defining and implementing membership
behavior separately. Update/delete mutations, subscriptions and cross-service
aggregation are outside this task.

## Run the example

Prepare Vault and application tokens using the existing
[Vault guide](Week45VaultIntegration.md). Then build/start Bizcord followed by the
gateway:

```bash
# From the Bizcord repository
docker compose up -d --build

# From the api-gateway repository
docker compose up -d --build
```

The local teaching faucet can issue a token with both GraphQL scopes:

```bash
TOKEN="$(curl -fsS http://localhost:8080/faucet \
  -H 'Content-Type: application/json' \
  -d '{"claims":{"sub":"student-123","role":"member","scope":"channel.read channel.write"}}' \
  | python3 -c 'import json,sys; print(json.load(sys.stdin)["value"])')"

curl -sS http://localhost:8080/graphql \
  -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' \
  -d '{"query":"mutation { createChannel(input: { name: \"Engineering\", description: \"Team chat\" }) { id name } }"}'

curl -sS http://localhost:8080/graphql \
  -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' \
  -d '{"query":"{ channels { id name } }"}'

unset TOKEN
```

The faucet remains a local exercise tool that lets the caller choose claims; it
is not a production identity provider. The gateway exposes POST `/graphql`.
No browser IDE or GET GraphQL route is added to the gateway.

For a channel-panel UI, submit this query and the selected channel ID as a variable:

```graphql
query ChannelPanel($id: UUID!) {
  channels {
    id
    name
  }
  selected: channel(id: $id) {
    id
    name
    description
  }
}
```

Previously a client would combine `GET /channels` and `GET /channels/{id}` and
receive every DTO property. GraphQL can return both selections in one response.
It still invokes both application methods; fewer HTTP requests do not imply
fewer repository operations or a consistent transactional snapshot.

## Ownership and alternatives

- `GraphQL/ChannelQuery.cs` and `ChannelMutation.cs` translate GraphQL fields into
  existing service calls, passing cancellation through. They do not query the
  repository or implement business rules.
- `GraphQL/ChannelType.cs` explicitly selects the output fields from `ChannelDto`.
  Domain members, attachment state and concurrency versions are not exposed;
  adding a DTO property does not silently add a GraphQL field.
- `GraphQL/ServiceCollectionExtensions.cs` registers the schema, authorization
  integration and error filter. `Program.cs` maps the authenticated HTTP endpoint.
- `Authentication/ServiceAuthenticationExtensions.cs` owns read/write scope
  policies. Scope matching accepts individual claims or a space-separated claim
  and uses exact, case-sensitive values.
- `GraphQL/ChannelErrorFilter.cs` translates application failures into GraphQL
  errors. The existing REST exception handler remains responsible for HTTP
  Problem Details.

A separate BFF host could evolve or scale independently and aggregate other
services. For this task it would add deployment, authentication and HTTP failure
paths. Reusing this service's in-memory repository in a separate process would
also create separate state. Hosting both presentations together satisfies the
explicit service-reuse requirement without those additions. This is a BFF-style
interface within Channel Service, not a separately deployed BFF.

The example's keyed DI is unnecessary here: `AddChannelApplication` already
registers one scoped `ChannelManagementService`. Resolver method parameters use
`[Service]` to resolve that registration. Conventional non-static query/mutation
classes work with `AddQueryType<ChannelQuery>()` and
`AddMutationType<ChannelMutation>()`; no source generator is needed.

## Authentication and errors

The gateway authenticates requests and forwards the bearer token. Channel
Service validates its signature, issuer, audience and lifetime. HotChocolate
checks `ChannelRead` on query fields and `ChannelWrite` on mutation fields.
A route-level HTTP POST scope rule cannot distinguish queries from mutations,
so field policies enforce the distinction inside GraphQL. Existing REST routes
retain their existing gateway policies.

Missing/invalid credentials produce HTTP 401. Authenticated callers lacking the
field's scope receive a GraphQL authorization error, before the application
method runs. Resolver errors use the GraphQL `errors` array; clients must inspect
it even when the HTTP response is successful. A failed nullable field may coexist
with successful sibling fields.

| Code | Meaning |
| --- | --- |
| `CHANNEL_VALIDATION` | Existing application validation rejected the input. |
| `CHANNEL_CONFLICT` | A concurrency exception requires reloading state. |
| `PUBLISH_TIMEOUT` | State may already be changed; event delivery is uncertain. |
| `PUBLISH_CAPACITY_EXCEEDED` | This publish was rejected, but state may already be changed. |

The filter also recognizes the existing concurrency exception, although the
initial read/create API does not expose update operations that normally cause it.
Unexpected errors retain HotChocolate's generic response. Exception details are
disabled even in Development. GraphQL does not add rollback or retries: repository
write and event publication remain non-atomic, just as they are through REST.

## Verification

- Application build passed with zero warnings and zero errors.
- All 69 Bizcord tests passed, including 16 new HTTP tests and the existing
  RabbitMQ integration tests.
- The new tests use ASP.NET TestServer with real HotChocolate, JWT authentication,
  application service and in-memory repository. Only `IMessageClient` is mocked.
  They verify selected fields, REST/GraphQL shared state, exact scopes, invalid
  credentials, validation without side effects, partial errors, hidden domain
  fields and publication-failure uncertainty without exception leakage.
- Fresh Channel Service and gateway Docker images passed an isolated live run
  with real RabbitMQ, a development Vault and Fluentd. Requests went through the
  gateway on temporary port 38080 using tokens from its faucet. The checks proved
  anonymous/invalid-token rejection, read-only mutation rejection, write-only
  query rejection, composed query field selection, domain validation and shared
  state in both REST → GraphQL and GraphQL → REST directions. GraphQL request
  logs also reached Fluentd.
- The temporary stack used separate networks, disposable infrastructure storage
  and test-only credentials. It was removed after testing; existing application
  containers and data volumes were not used or changed.

The test restore reports an existing NU1903 advisory for
`SQLitePCLRaw.lib.e_sqlite3` 2.1.11 through the upload workflow. It is unrelated to
the GraphQL packages and is not changed by this task.

## Reflection and limits

Both APIs now exercise the same application behavior while offering different
client contracts. No domain logic, persistence or messaging implementation was
copied. The principal costs are another API contract and GraphQL-specific error
and authorization behavior to maintain.

The list remains unpaginated and fully materialized, matching the existing
application method. Large datasets would need service-level pagination and a
corresponding schema. Membership filtering, production identity issuance,
load testing and persistent storage are separate concerns. A one-request query
also does not eliminate consistency differences between concurrently resolved
fields.

## Resources

- [HotChocolate package 16.7.6](https://www.nuget.org/packages/HotChocolate.AspNetCore/16.7.6)
- [HotChocolate dependency injection](https://chillicream.com/docs/hotchocolate/resolvers/dependency-injection)
- [HotChocolate authorization](https://chillicream.com/docs/hotchocolate/security/authorization)
- [HotChocolate error handling](https://chillicream.com/docs/hotchocolate/resolvers/errors)
