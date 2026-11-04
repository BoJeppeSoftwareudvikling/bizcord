# Week 45, task 01: East-west security

This document records the implemented security boundary, the decisions behind it,
and the verification performed for the Channel Service.

## Result

The Channel Service now exposes one explicit service-to-service contract:

```http
GET /internal/channels/{channelId}/exists
```

The endpoint returns `200 OK` with `exists: true` for an existing channel and
`404 Not Found` with `exists: false` for an unknown channel. Access requires a
valid JWT for audience `channel-service` containing `role: internal-service`.

The contract intentionally answers only whether a channel exists. The domain
documentation says that Message Service will eventually ask whether a user can
post, but the current application has no complete membership-management flow.
Inventing a positive permission result would therefore create a false security
guarantee. The existence check is a narrow first integration contract; user-level
posting permission remains future domain work.

## Security ownership

Authentication and authorization are enforced twice for different reasons:

1. Ocelot validates the bearer token and rejects callers without the required
   `internal-service` role before proxying.
2. Channel Service validates the signature, issuer, audience, lifetime and role
   again before executing the controller action.

Channel Service is the final security boundary. A gateway-only check was rejected
because another workload on the internal network could call the service directly
and bypass Ocelot. The gateway check remains useful as early rejection and keeps
invalid traffic away from the downstream service.

The local teaching faucet issues tokens with:

- issuer: `bizcord-local-gateway`
- audience: `channel-service`
- caller identity in `sub`
- service authorization in `role`
- a one-hour lifetime

The symmetric signing key is no longer committed or copied into both Compose
stacks. Week 45 task 02 stores one shared key at `secret/shared/jwt` in Vault.
The gateway and Channel Service read it with different scoped application tokens,
so the key remains shared while the Vault identities and policies remain separate.

## Network boundary

The Channel Service no longer publishes `8000:8080`. It is attached only to the
internal Docker network `bizcord-backend`. The gateway attaches to both its normal
network and `bizcord-backend`, so it can:

- accept client traffic on host port `8080`
- reach Channel Service at `http://channel-microservice:8080`

The Bizcord Compose stack owns the internal network, while the separate gateway
Compose stack declares it as external. Bizcord must therefore be started first.

Removing the public port reduces the exposed perimeter, but network placement is
not treated as identity. The JWT policy is still enforced inside Channel Service.

## Main implementation locations

### Bizcord

- `docker-compose.yml` — owns the internal network, removes the Channel Service
  host port and supplies the scoped Vault token at runtime.
- `apps/channel-microservice/src/ChannelMicroservice.Presentation/Authentication/ServiceAuthenticationExtensions.cs`
  — owns JWT validation and the `InternalService` authorization policy.
- `apps/channel-microservice/src/ChannelMicroservice.Presentation/Controllers/InternalChannelsController.cs`
  — owns the internal HTTP contract and applies the policy.
- `apps/channel-microservice/src/ChannelMicroservice.Presentation/Program.cs`
  — registers authentication and places authentication before authorization.
- `apps/channel-microservice/tests/Presentation/ServiceAuthenticationTests.cs`
  — verifies the accepted role, denied role and audience validation.
- `apps/channel-microservice/tests/Presentation/Controllers/InternalChannelsControllerTests.cs`
  — verifies the endpoint contract and policy annotation.

### API gateway

- `src/Configuration/ocelot.example.json` — routes to the Docker service name and
  requires `role: internal-service` on the internal route.
- `src/Application/Services/JwtTokenService.cs` — adds issuer and audience to
  locally issued teaching tokens.
- `docker-compose.yml` — joins `bizcord-backend` and receives a gateway-specific
  Vault token at runtime.

## Manual verification

The following flow verifies the public gateway path, the downstream service
policy and the network boundary. It uses Python only to extract values from JSON,
so `jq` is not required.

### 1. Bootstrap Vault and start both Compose stacks

First complete the setup in [Week45VaultIntegration.md](Week45VaultIntegration.md).
The two repositories require different one-hour Vault tokens and Vault must be
unsealed before either application starts.

Start Bizcord first because it owns the shared `bizcord-backend` network:

```bash
cd /home/jklh/studies/projects/bizcord
docker compose up -d --build
docker compose ps
```

Then start the gateway from a second terminal:

```bash
cd /home/jklh/studies/projects/api-gateway
docker compose up -d --build
docker compose ps
```

The Channel Service should be running without a published host port. The gateway
should publish `127.0.0.1:8080`.

### 2. Create a channel through the public gateway

Obtain a token with the existing public write scope:

```bash
WRITE_TOKEN="$(
  curl -sS -X POST http://localhost:8080/faucet \
    -H 'Content-Type: application/json' \
    -d '{"claims":{"sub":"student-123","role":"member","scope":"channel.write"}}' |
  python3 -c 'import json,sys; print(json.load(sys.stdin)["value"])'
)"
```

Create a channel and retain its ID:

```bash
CHANNEL_ID="$(
  curl -sS -X POST http://localhost:8080/channels \
    -H "Authorization: Bearer $WRITE_TOKEN" \
    -H 'Content-Type: application/json' \
    -d '{"name":"Security test","description":"Manual week 45 test"}' |
  python3 -c 'import json,sys; print(json.load(sys.stdin)["id"])'
)"

echo "$CHANNEL_ID"
```

The last command should print a GUID.

### 3. Verify `401 Unauthorized`

Call the internal route without a token:

```bash
curl -i "http://localhost:8080/internal/channels/$CHANNEL_ID/exists"
```

Expected result: `401 Unauthorized`. The caller has not authenticated.

### 4. Verify `403 Forbidden`

Obtain a valid token with the wrong role:

```bash
MEMBER_TOKEN="$(
  curl -sS -X POST http://localhost:8080/faucet \
    -H 'Content-Type: application/json' \
    -d '{"claims":{"sub":"student-123","role":"member"}}' |
  python3 -c 'import json,sys; print(json.load(sys.stdin)["value"])'
)"

curl -i "http://localhost:8080/internal/channels/$CHANNEL_ID/exists" \
  -H "Authorization: Bearer $MEMBER_TOKEN"
```

Expected result: `403 Forbidden`. The identity is valid but lacks
`role: internal-service`.

### 5. Verify the service identity

Obtain a token representing Message Service:

```bash
SERVICE_TOKEN="$(
  curl -sS -X POST http://localhost:8080/faucet \
    -H 'Content-Type: application/json' \
    -d '{"claims":{"sub":"message-service","role":"internal-service"}}' |
  python3 -c 'import json,sys; print(json.load(sys.stdin)["value"])'
)"

curl -i "http://localhost:8080/internal/channels/$CHANNEL_ID/exists" \
  -H "Authorization: Bearer $SERVICE_TOKEN"
```

Expected result: `200 OK` and a body shaped like:

```json
{
  "channelId": "<the created channel ID>",
  "exists": true
}
```

### 6. Verify the resource result separately from authorization

Use a valid service identity with an unknown channel ID:

```bash
UNKNOWN_ID="$(python3 -c 'import uuid; print(uuid.uuid4())')"

curl -i "http://localhost:8080/internal/channels/$UNKNOWN_ID/exists" \
  -H "Authorization: Bearer $SERVICE_TOKEN"
```

Expected result: `404 Not Found` with `exists: false`. Authentication and
authorization succeeded; only the resource lookup failed.

### 7. Prove that Channel Service enforces the policy

The next calls bypass Ocelot by running a temporary curl container directly on
`bizcord-backend`.

No token:

```bash
docker run --rm --network bizcord-backend \
  curlimages/curl:8.10.1 \
  -i "http://channel-microservice:8080/internal/channels/$CHANNEL_ID/exists"
```

Expected result: `401 Unauthorized`.

Valid token with the wrong role:

```bash
docker run --rm --network bizcord-backend \
  curlimages/curl:8.10.1 \
  -i \
  -H "Authorization: Bearer $MEMBER_TOKEN" \
  "http://channel-microservice:8080/internal/channels/$CHANNEL_ID/exists"
```

Expected result: `403 Forbidden`.

Valid service identity:

```bash
docker run --rm --network bizcord-backend \
  curlimages/curl:8.10.1 \
  -i \
  -H "Authorization: Bearer $SERVICE_TOKEN" \
  "http://channel-microservice:8080/internal/channels/$CHANNEL_ID/exists"
```

Expected result: `200 OK`. These three checks prove that Channel Service enforces
the policy independently of the gateway.

### 8. Verify removal of the public service port

```bash
curl -i --max-time 2 http://localhost:8000/health
```

A connection failure is the expected successful result. Channel Service should
not be reachable through the old host port.

### 9. Stop the temporary environment

Stop the gateway first because it is attached to Bizcord's network:

```bash
cd /home/jklh/studies/projects/api-gateway
docker compose down
```

Then stop Bizcord:

```bash
cd /home/jklh/studies/projects/bizcord
docker compose down
```

Do not add `-v` unless the RabbitMQ data volume should also be deleted.

## Verification record

Verification performed on 2026-11-04:

- Bizcord test suite: `44` passed, `0` failed.
- API gateway build: succeeded with `0` warnings and `0` errors.
- API gateway smoke suite: all routing, authentication, authorization, claim,
  expiry and restart checks passed.
- Both Compose files passed `docker compose config --quiet`.
- Both Docker images built and started successfully.
- Gateway call without a token returned `401 Unauthorized`.
- Gateway call with `role: member` returned `403 Forbidden`.
- Gateway call with `role: internal-service` returned `200 OK` for an existing
  channel.
- Direct calls from a temporary container on `bizcord-backend` produced the same
  `401`, `403` and `200` results, proving that Channel Service enforces the policy.
- `localhost:8000` was unreachable, confirming removal of the host port.

The temporary verification containers were stopped after the checks. Named
RabbitMQ data was preserved.

## Deliberate limitations

- The faucet and shared HMAC key are local teaching mechanisms, not a production
  identity provider.
- Transport on the internal Docker network is HTTP, not mTLS.
- The endpoint proves service identity and channel existence; it does not yet
  implement user membership or `CanPostMessages` evaluation.
- The Vault task adds central storage but still uses a startup-loaded static key;
  automatic rotation and reload remain future work.
