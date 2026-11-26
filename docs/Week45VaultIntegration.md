# Week 45, task 02: HashiCorp Vault integration

This document records the implemented secret boundary, the design decisions behind
it, and the manual and automated verification for Bizcord.

## Result

The RabbitMQ connection string and the JWT signing key no longer come from source
controlled settings or ordinary Compose environment variables. Both are stored as
static secrets in a local HashiCorp Vault 1.21.0 KV v2 engine and loaded before the
applications register the components that consume them.

| Workload | Allowed KV v2 path | Loaded configuration |
| --- | --- | --- |
| Channel Service | `secret/channel-service/messaging` | `Messaging:ConnectionString` |
| Channel Service | `secret/shared/jwt` | `Authentication:SigningKey` |
| API gateway | `secret/shared/jwt` | `AuthenticationProviderKey` |

Channel Service and the gateway receive different one-hour Vault tokens. The
gateway cannot read the RabbitMQ credential, and neither application token can
write secrets.

## Design decisions and reasoning

### Vault is the source of truth for both values

The application-facing RabbitMQ connection string contains a password, and the JWT signing key can
mint trusted identities. Leaving either value in `appsettings`, `.env.example` or
Compose would preserve a second secret-distribution path. Storing both in Vault
gives the applications one explicit source of truth. The local RabbitMQ container
still needs bootstrap username/password values from the ignored Compose `.env`;
removing that infrastructure bootstrap path would require a separate broker-
provisioning mechanism and is outside this task.

The JWT key is intentionally one shared secret document because the local faucet
signs tokens that Channel Service validates. Access to that document is shared;
the application tokens are not. The gateway policy only permits the JWT path,
whereas Channel Service can also read its own messaging path.

### Each workload has its own least-privilege token

A single token shared by both containers was rejected because it would allow the
gateway to read the RabbitMQ credential. Separate policies make compromise of the
gateway token less useful and make the intended ownership visible in configuration.

The root token is used only to bootstrap mounts, policies, secrets and application
tokens. It is never supplied to either application.

### Secrets are loaded before dependent services are registered

Both applications retrieve Vault data immediately after
`WebApplication.CreateBuilder`. The resulting values are added as an in-memory
configuration provider before JWT authentication, token signing or messaging is
registered. Existing consumers therefore continue to use normal .NET
configuration keys and do not depend directly on Vault.

Startup fails when the token is absent, access is denied, Vault is sealed, the
response is not valid KV v2 JSON, or a required key is missing or empty. Continuing
with partial configuration would hide a security or operational error.

### Direct HTTP API instead of a Vault client package

The implementation uses `HttpClient` and Vault's documented HTTP API. This keeps
the assignment's KV v2 envelope and `X-Vault-Token` flow visible and avoids adding
a client-library abstraction for two startup reads. A client library would become
more attractive if the system added token renewal, dynamic secrets, leases or
automatic rotation.

### Startup-only loading is deliberate

The values remain in process memory after startup. A new secret version therefore
requires an application restart. This is sufficient for the task and avoids a
second mutable configuration mechanism, but it is not automatic rotation.

### Manual unseal is part of the local exercise

Vault uses persistent file storage and Shamir unseal keys. Restarting the container
preserves the secrets but leaves Vault sealed. This demonstrates the operational
difference between persistence and availability. A production platform would
normally use TLS, protected storage, audit devices and an appropriate auto-unseal
mechanism.

### Network placement

Vault and RabbitMQ join both Compose's normal network and the internal
`bizcord-backend` network. Their management ports remain bound to localhost, while
Channel Service joins only the internal network and has no published host port.
The API gateway joins its public/default network and `bizcord-backend`.

Attaching Vault only to the internal network was rejected because the local UI and
initialization API would not be reachable at `localhost:8200` in this Docker setup.

## Main implementation locations

### Bizcord

- `vault/config.hcl` owns local file storage, the HTTP listener and UI setting.
- `docker-compose.yml` owns the pinned Vault container, persistent volume,
  dual-network placement and runtime `VAULT_ADDR`/`VAULT_TOKEN` delivery.
- `apps/channel-microservice/src/ChannelMicroservice.Presentation/Infrastructure/Secrets/VaultSecretReader.cs`
  owns authenticated KV v2 reads and safe error handling.
- `apps/channel-microservice/src/ChannelMicroservice.Presentation/Infrastructure/Secrets/VaultConfigurationExtensions.cs`
  maps the two Vault values into the existing .NET configuration keys.
- `apps/channel-microservice/src/ChannelMicroservice.Presentation/Program.cs`
  performs retrieval before authentication and messaging registration.
- `apps/channel-microservice/tests/Infrastructure/Secrets/VaultSecretReaderTests.cs`
  verifies the request, success parsing, denied access, sealed state and a missing
  key without using real secret values.

### API gateway (paths under `apps/api-gateway/`)

- `src/Infrastructure/Secrets/VaultSecretReader.cs` owns its KV v2 read.
- `src/Infrastructure/Secrets/VaultConfigurationExtensions.cs` maps the signing
  key to the gateway's existing `AuthenticationProviderKey` configuration key.
- `src/Program.cs` loads the key before JWT services are registered.
- The root `docker-compose.yml` delivers only the gateway's own Vault token.
- `tests/smoke.py` uses a temporary fake Vault to prove startup retrieval while
  retaining the isolated gateway routing and authentication suite.

## Manual setup and verification

Run the commands from the Bizcord repository root in Bash. Initialization and token creation display credentials once. Store them privately.

### 1. Start the infrastructure

Create the local file if needed:

```bash
cp -n .env.example .env
```

Set `RABBITMQ_USER` to a local user such as `bizcord` and choose a password. Leave the token placeholders until
the scoped Channel Service token has been created. Then start only the infrastructure:

```bash
docker compose config --quiet
docker compose up -d rabbitmq vault
docker compose ps
```

Vault's UI is available at <http://localhost:8200> and RabbitMQ management at
<http://localhost:15672>.

### 2. Initialize and unseal Vault

Initialize only when the named `vault-data` volume is new:

```bash
docker compose exec vault vault operator init \
  -key-shares=3 \
  -key-threshold=2
```

Store all three unseal shares and the initial root token outside the repository.
Enter two different shares without putting them in shell history:

```bash
docker compose exec vault vault operator unseal
docker compose exec vault vault operator unseal
docker compose exec vault vault login
```

The last command prompts for the root token. Check the state:

```bash
docker compose exec vault vault status
```

Expected: `Initialized true` and `Sealed false`.

### 3. Enable KV v2 and store both secrets

Enable the mount once:

```bash
docker compose exec vault vault secrets enable -path=secret kv-v2
```

Read the values silently and send them through standard input instead of command
arguments:

```bash
read -rsp 'RabbitMQ connection string: ' RABBITMQ_CONNECTION
printf '%s' "$RABBITMQ_CONNECTION" | \
  docker compose exec -T vault vault kv put \
  secret/channel-service/messaging ConnectionString=-
unset RABBITMQ_CONNECTION

read -rsp 'JWT signing key: ' JWT_KEY
printf '%s' "$JWT_KEY" | \
  docker compose exec -T vault vault kv put \
  secret/shared/jwt SigningKey=-
unset JWT_KEY
```

Use a RabbitMQ value shaped like
`host=rabbitmq;username=<user>;password=<password>`. Use a randomly generated JWT
key of at least 32 bytes. Store both values in Vault.

### 4. Create the read-only policies

```bash
docker compose exec -T vault vault policy write channel-service - <<'EOF'
path "secret/data/channel-service/messaging" {
  capabilities = ["read"]
}
path "secret/data/shared/jwt" {
  capabilities = ["read"]
}
EOF

docker compose exec -T vault vault policy write api-gateway - <<'EOF'
path "secret/data/shared/jwt" {
  capabilities = ["read"]
}
EOF
```

The policy paths contain KV v2's `data` segment. The CLI paths used when storing
the values do not.

### 5. Create and deliver separate application tokens

Create a non-renewable, one-hour token for each workload:

```bash
docker compose exec vault vault token create \
  -policy=channel-service -no-default-policy -ttl=1h -renewable=false

docker compose exec vault vault token create \
  -policy=api-gateway -no-default-policy -ttl=1h -renewable=false
```

Edit the existing root `.env`, keeping the RabbitMQ settings used in step 1.
Set `VAULT_TOKEN` to the Channel Service token and `API_GATEWAY_VAULT_TOKEN` to the gateway token.
Compose passes each service its own token as `VAULT_TOKEN`.

Do not reuse the root token. The application tokens expire after one hour; create
new ones before a later test session.

### 6. Verify the policies directly

Load the tokens without echoing them:

```bash
read -rsp 'Channel Service Vault token: ' CHANNEL_VAULT_TOKEN
read -rsp 'API gateway Vault token: ' GATEWAY_VAULT_TOKEN
```

Allowed reads should return `200`:

```bash
curl -sS -o /dev/null -w '%{http_code}\n' \
  -H "X-Vault-Token: $CHANNEL_VAULT_TOKEN" \
  http://localhost:8200/v1/secret/data/channel-service/messaging

curl -sS -o /dev/null -w '%{http_code}\n' \
  -H "X-Vault-Token: $GATEWAY_VAULT_TOKEN" \
  http://localhost:8200/v1/secret/data/shared/jwt
```

The following calls should each return `403`:

```bash
curl -sS -o /dev/null -w '%{http_code}\n' \
  -H 'X-Vault-Token: invalid' \
  http://localhost:8200/v1/secret/data/shared/jwt

curl -sS -o /dev/null -w '%{http_code}\n' \
  -H "X-Vault-Token: $CHANNEL_VAULT_TOKEN" \
  http://localhost:8200/v1/secret/data/unrelated

curl -sS -o /dev/null -w '%{http_code}\n' -X POST \
  -H "X-Vault-Token: $CHANNEL_VAULT_TOKEN" \
  -H 'Content-Type: application/json' \
  -d '{"data":{"ConnectionString":"must-not-be-written"}}' \
  http://localhost:8200/v1/secret/data/channel-service/messaging

curl -sS -o /dev/null -w '%{http_code}\n' \
  -H "X-Vault-Token: $GATEWAY_VAULT_TOKEN" \
  http://localhost:8200/v1/secret/data/channel-service/messaging
```

Finish by clearing the shell variables:

```bash
unset CHANNEL_VAULT_TOKEN GATEWAY_VAULT_TOKEN
```

### 7. Start both applications and test the shared JWT key

```bash
docker compose up -d --build
docker compose ps
```

Request a write token, create a channel, and call the internal route with a service
identity:

```bash
WRITE_TOKEN="$(
  curl -sS -X POST http://localhost:8080/faucet \
    -H 'Content-Type: application/json' \
    -d '{"claims":{"sub":"student-123","role":"member","scope":"channel.write"}}' |
  python3 -c 'import json,sys; print(json.load(sys.stdin)["value"])'
)"

CHANNEL_ID="$(
  curl -sS -X POST http://localhost:8080/channels \
    -H "Authorization: Bearer $WRITE_TOKEN" \
    -H 'Content-Type: application/json' \
    -d '{"name":"Vault test","description":"Manual week 45 test"}' |
  python3 -c 'import json,sys; print(json.load(sys.stdin)["id"])'
)"

SERVICE_TOKEN="$(
  curl -sS -X POST http://localhost:8080/faucet \
    -H 'Content-Type: application/json' \
    -d '{"claims":{"sub":"message-service","role":"internal-service"}}' |
  python3 -c 'import json,sys; print(json.load(sys.stdin)["value"])'
)"

curl -i "http://localhost:8080/internal/channels/$CHANNEL_ID/exists" \
  -H "Authorization: Bearer $SERVICE_TOKEN"
```

Expected: `201 Created` for channel creation and `200 OK` with `exists: true` for
the final call. This proves that the gateway signed and Channel Service validated
with the same Vault-managed key.

### 8. Verify seal, startup failure and persistence

Stop both applications, restart Vault, and confirm it is sealed:

```bash
docker compose stop api-gateway channel-microservice
docker compose restart vault
docker compose exec vault vault status
```

A direct secret read returns `503` while sealed. Starting Channel Service at this
point should fail fast rather than running without its required secrets:

```bash
docker compose up -d channel-microservice
docker compose ps -a channel-microservice
docker compose logs channel-microservice
```

Unseal with two original shares and verify that the existing secret still returns
`200`:

```bash
docker compose exec vault vault operator unseal
docker compose exec vault vault operator unseal

read -rsp 'Channel Service Vault token: ' CHANNEL_VAULT_TOKEN
curl -sS -o /dev/null -w '%{http_code}\n' \
  -H "X-Vault-Token: $CHANNEL_VAULT_TOKEN" \
  http://localhost:8200/v1/secret/data/channel-service/messaging
unset CHANNEL_VAULT_TOKEN
```

Start both applications again and repeat step 7. Persistence is proven only when
the old data can be read after restart and unseal; `docker compose down -v` would
delete the named Vault volume.

### 9. Stop the environment

```bash
docker compose down
```

Do not add `-v` unless both the Vault and RabbitMQ named volumes should be deleted.

## Verification record

Verification performed on 2026-11-04:

- Bizcord test suite: `48` passed, `0` failed.
- API gateway build: `0` warnings and `0` errors.
- API gateway smoke suite passed and retrieved its signing key from a temporary
  Vault-compatible HTTP server.
- Both Compose files passed `docker compose config --quiet`.
- HashiCorp Vault `1.21.0` was initialized with three shares and threshold two.
- Allowed reads succeeded; invalid token, unrelated path, write attempt and gateway
  access to the RabbitMQ path each returned `403`.
- Both applications built and started with separate one-hour Vault tokens.
- A token signed by the gateway was accepted by Channel Service end to end.
- Application logs contained neither secret value nor either Vault token.
- After Vault restart, it was sealed, secret reads returned `503`, and Channel
  Service failed fast.
- After two unseal shares, both existing secret values remained available and the
  end-to-end application flow passed again.

The temporary application containers were stopped. The temporary `vault-data`
volume created for verification was deleted; the pre-existing RabbitMQ data volume
was preserved.

## Deliberate limitations

- Local Vault uses HTTP, manual unseal and `disable_mlock`; this is not a production
  deployment model.
- The JWT signing key and RabbitMQ credential are KV v2 static secrets, not dynamic
  credentials.
- RabbitMQ itself is still initialized from username/password values in Compose's
  ignored `.env`; Vault owns the connection string consumed by Channel Service,
  not the broker's bootstrap lifecycle.
- Applications load secrets only at startup and do not renew Vault tokens or reload
  rotated values.
- The one-hour token must currently be provisioned manually into each ignored
  `.env` file. This is the local secret-zero mechanism, not workload identity.
- The local faucet remains a teaching tool rather than an identity provider.
