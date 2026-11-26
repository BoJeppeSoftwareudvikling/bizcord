# Week 43: API gateway, routing, and authorization

## Current setup

Run the [Vault bootstrap and stack](Week45VaultIntegration.md#manual-setup-and-verification) from the Bizcord root.
Ocelot accepts requests on `localhost:8080` and forwards them to `channel-microservice:8080` on the private Docker network. See the [route definitions](../apps/api-gateway/src/Configuration/ocelot.example.json) and [gateway startup](../apps/api-gateway/src/Program.cs).

The original week 43 exercise ran the teaching gateway from a separate checkout and routed through host port 8000. Week 45 moved the service onto the private network. The submission includes our configured gateway in the same checkout.

## Task 01: routing configuration

The gateway contains explicit routes for the real Channel Service endpoints:

| Upstream route | Methods | Downstream route |
| --- | --- | --- |
| `/channels` | `GET`, `POST` | `/channels` |
| `/channels/{id}` | `GET`, `PUT`, `DELETE` | `/channels/{id}` |
| `/channels/me` | `GET` | `/channels/me` |

The `/channels/me` route belongs to the authorization work described below. It has
a higher Ocelot priority than `/channels/{id}` so that the literal value `me` is
not interpreted as a channel ID.

For `POST /channels`, the downstream `Location` response header is rewritten from
the internal service address to the gateway base URL. A client therefore receives
a public URL such as `http://localhost:8080/channels/{id}` and does not need to know
the Channel Service host or port.

### Routing decisions

- Explicit routes were chosen instead of a broad catch-all route. Only known paths
  and HTTP methods are exposed through the gateway.
- The gateway is the client's single entry point. Internal service addresses must
  not leak through response headers.
- The Channel Service API contract is unchanged; Ocelot maps the public routes to
  the service's existing routes.

## Task 02: authentication and authorization

Ocelot validates JWT bearer tokens before forwarding protected requests. Route
authorization is based on scopes:

| Capability | Required scope | Routes |
| --- | --- | --- |
| Read channels | `channel.read` | `GET /channels`, `GET /channels/{id}`, `GET /channels/me` |
| Change channels | `channel.write` | `POST /channels`, `PUT /channels/{id}`, `DELETE /channels/{id}` |

Read and write routes are separate Ocelot entries even when they share a path. This
allows each HTTP method to have the smallest required permission.

Scopes were chosen instead of roles for endpoint authorization because they express
API capabilities directly. The token's role is still forwarded as user context,
but a role alone does not grant access to a route.

The starter gateway's `/faucet` endpoint issues locally signed teaching tokens. The
faucet and its demo signing key are suitable for this exercise only; they are not a
replacement for a real identity provider or login flow.

## Forwarded identity and spoofing protection

After a token has been validated, Ocelot maps these claims to downstream request
headers:

| JWT claim | Downstream header |
| --- | --- |
| `sub` | `X-UserId` |
| `role` | `X-Role` |

Before Ocelot performs that mapping, gateway middleware removes any `X-UserId` and
`X-Role` headers supplied by the client. Ocelot then recreates them from the
validated token. This prevents a caller from impersonating another user by sending
trusted-looking headers.

The Channel Service exposes `GET /channels/me` as a narrow verification endpoint.
It returns the forwarded identity as `CurrentUserDto` and rejects requests where
either header is missing. The endpoint was added to prove the complete claim flow;
it does not introduce invented per-user channel behavior into the domain API.

```text
Client token
  -> gateway validates signature and required scope
  -> gateway removes client-supplied identity headers
  -> Ocelot creates identity headers from JWT claims
  -> Channel Service reads X-UserId and X-Role
```

### Trust boundary

Public REST actions read identity headers supplied by the gateway. The Channel Service port is private. The [internal existence endpoint](Week45EastWestSecurity.md) and [GraphQL endpoint](Week47GraphQLPresentation.md) also validate JWTs in the service.

## Manual verification

Start the stack as described above.

Obtain read and write tokens from the teaching faucet. Replace the example token
values in later commands with the returned values.

```bash
curl -sS -X POST http://localhost:8080/faucet \
  -H 'Content-Type: application/json' \
  -d '{"claims":{"sub":"student-123","role":"member","scope":"channel.read"}}'

curl -sS -X POST http://localhost:8080/faucet \
  -H 'Content-Type: application/json' \
  -d '{"claims":{"sub":"student-123","role":"member","scope":"channel.write"}}'
```

Verify authentication, authorization, and identity forwarding:

```bash
# No token: 401 Unauthorized
curl -i http://localhost:8080/channels

# Read token: 200 OK
curl -i http://localhost:8080/channels \
  -H 'Authorization: Bearer PASTE_READ_TOKEN'

# Read token on a write route: 403 Forbidden
curl -i -X POST http://localhost:8080/channels \
  -H 'Authorization: Bearer PASTE_READ_TOKEN' \
  -H 'Content-Type: application/json' \
  -d '{"name":"Gateway test","description":"Week 43"}'

# Client-supplied identity is replaced with claims from the token
curl -i http://localhost:8080/channels/me \
  -H 'Authorization: Bearer PASTE_READ_TOKEN' \
  -H 'X-UserId: spoofed-user' \
  -H 'X-Role: admin'
```

The final response must contain the identity from the token, not the spoofed
headers:

```json
{"userId":"student-123","role":"member"}
```

Also compare a real endpoint directly and through the gateway. The response body
should match, while gateway access additionally requires the correct token:

```bash
docker run --rm --network bizcord-backend curlimages/curl:8.10.1 -i http://channel-microservice:8080/channels
curl -i http://localhost:8080/channels \
  -H 'Authorization: Bearer PASTE_READ_TOKEN'
```

## Original week 43 verification record

The completed implementation was tested end to end with both Compose stacks
running:

- Direct and gateway collection requests returned `200 OK` with the same body.
- Create returned `201 Created`; its `Location` header pointed back to the gateway.
- Read by ID returned the same resource directly and through the gateway.
- Update returned `200 OK`; delete returned `204 No Content`; a later read returned
  `404 Not Found`.
- Missing and invalid tokens returned `401 Unauthorized`.
- A valid token with the wrong scope returned `403 Forbidden`.
- A read token could read but could not create or update resources.
- A write token could create, update, and delete but could not read resources.
- `/channels/me` returned the token's `sub` and `role`, even when spoofed identity
  headers were supplied by the client.
- The full Bizcord test suite passed: 18 of 18 tests.

The HTTP-only Compose setup can log an ASP.NET Core `UseHttpsRedirection` warning
because no HTTPS port is configured. This did not prevent the HTTP routes from
working and is not part of the gateway authorization behavior.

## Implementation references

Bizcord:

- Commit `0369c3a` — expose forwarded Channel Service user identity.
- `apps/channel-microservice/src/ChannelMicroservice.Presentation/Controllers/ChannelsController.cs`
- `apps/channel-microservice/dtos/Channels/ChannelDtos.cs`
- `apps/channel-microservice/tests/Presentation/Controllers/ChannelsControllerIdentityTests.cs`

API gateway:

- Commit `0daa554` — configure Channel Service gateway routes.
- Commit `e5d2e57` — rewrite the Channel Service `Location` header.
- Commit `35f2467` — protect Channel Service routes with JWT scopes.
- `apps/api-gateway/src/Configuration/ocelot.example.json`
- `apps/api-gateway/src/Program.cs`

## Resources

- [Ocelot routing](https://ocelot.readthedocs.io/en/latest/features/routing.html)
- [Ocelot authentication](https://ocelot.readthedocs.io/en/latest/features/authentication.html)
- [Ocelot authorization](https://ocelot.readthedocs.io/en/latest/features/authorization.html)
- [Ocelot claims transformation](https://ocelot.readthedocs.io/en/latest/features/claimstransformation.html)
- [Ocelot header transformation](https://ocelot.readthedocs.io/en/25.0/features/headerstransformation.html)
- [Teaching API gateway repository](https://github.com/patrickstolc/api-gateway)
