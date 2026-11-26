# Week 48 · Compulsory assignment #2

## Run from a fresh checkout

Use Docker with Compose v2, the .NET 10 SDK, Bash, curl and Python 3. Run commands from the Bizcord root. The configured Ocelot gateway is included under [apps/api-gateway](../apps/api-gateway), based on the [course starter](https://github.com/patrickstolc/api-gateway).

1. Follow [Vault bootstrap, steps 1–5](Week45VaultIntegration.md#manual-setup-and-verification). Start RabbitMQ and Vault first, unseal Vault, store the two secrets, then set the two scoped tokens in the root `.env`.
2. Run `docker compose up -d --build` and `docker compose ps`. The gateway should publish `127.0.0.1:8080`. The Channel Service HTTP port should have no host mapping.
3. Once the gateway is ready, run the first command below. A **401** confirms that the protected route is listening. Use the [week 43 token examples](Week43ApiGatewayAndAuthorization.md#manual-verification) to get a **200** for the same request. Review the tasks in the table while the stack is running.
4. Run the two test commands below. The .NET suite uses a disposable RabbitMQ container. The gateway smoke suite starts an isolated gateway with test routes. Use the live checks in the table to exercise the submitted route configuration.
5. When finished, run `docker compose down`. This preserves Vault and RabbitMQ volumes. On a later run, start the infrastructure, unseal the existing Vault and issue fresh one-hour application tokens. Channel data is held in memory and resets when its process stops.

```bash
curl -i http://localhost:8080/channels
dotnet test Bizcord.slnx
python3 apps/api-gateway/tests/smoke.py
```

## Find and verify each task

| Moodle task | Design and implementation | What to verify |
| --- | --- | --- |
| 1.1 · W43 Routing | [Route decisions](Week43ApiGatewayAndAuthorization.md#task-01-routing-configuration) · [Ocelot routes](../apps/api-gateway/src/Configuration/ocelot.example.json) | Compare a direct request on the Docker network with the [authenticated gateway request](Week43ApiGatewayAndAuthorization.md#manual-verification). Both return the channel list. |
| 1.2 · W43 Authorization | [Scopes and identity forwarding](Week43ApiGatewayAndAuthorization.md#task-02-authentication-and-authorization) · [header handling](../apps/api-gateway/src/Program.cs) | A missing token gives 401, a read token on a write route gives 403, and `GET /channels/me` returns the token identity. |
| 2.1 · W44 Failure points | [Nine failure points, impacts and priorities](FAILURE_POINTS.md) | Trace FP01 (slow broker), FP02 (write succeeds before publish) and FP06 (concurrent updates) to the [mitigation decisions](Week44ReliabilityMeasures.md). |
| 2.2 · W44 Reliability measures | [Timeouts, capacity limits and concurrency](Week44ReliabilityMeasures.md) · [publish tests](../apps/channel-microservice/tests/Messaging/BoundedMessageClientTests.cs) | Run the [failure simulations](Week44ReliabilityMeasures.md#tests-og-fejlsimulering). Check that a slow publish times out, excess publishes are rejected and a stale save fails. |
| 3.1 · W45 East/west security | [Service boundary and internal endpoint](Week45EastWestSecurity.md) · [service authorization](../apps/channel-microservice/src/ChannelMicroservice.Presentation/Authentication/ServiceAuthenticationExtensions.cs) | Follow the [gateway and direct checks](Week45EastWestSecurity.md#manual-verification). No token, member role and service role produce 401, 403 and 200 for an existing channel. |
| 3.2 · W45 HashiCorp Vault | [Secrets, policies and setup](Week45VaultIntegration.md) · [Channel Service secret loading](../apps/channel-microservice/src/ChannelMicroservice.Presentation/Infrastructure/Secrets) | Run the [policy checks](Week45VaultIntegration.md#6-verify-the-policies-directly). Both workloads can read the JWT key. Only Channel Service can read the broker credential. |
| 4.1 · W46 Saga scenario | [Attachment upload and compensation diagram](Week46AttachmentSaga.md#saga-design) · [participant simulators](../apps/upload-workflow/SimulatedParticipants.cs) | Follow the [two-upload demo](Week46AttachmentSaga.md#run-the-workflow). The first completes. The second exceeds quota and retracts its own provisional changes. |
| 4.2 · W46 Saga implementation | [MessageClient/Rebus decision and state ownership](Week46AttachmentSaga.md#saga-design) · [UploadCoordinator](../apps/upload-workflow/UploadCoordinator.cs) | Run the [saga tests](Week46AttachmentSaga.md#verification) for success, compensation, duplicate delivery and recovery after a lost reply. |
| 5.1 · W47 Logging sidecar | [Log transport and delivery limits](Week47LoggingSidecar.md) · [Fluentd configuration](../fluentd/fluent.conf) | Run `docker compose logs fluentd`. Look for [records tagged `bizcord.channel`](Week47LoggingSidecar.md#run-and-verify) with Channel Service stdout. |
| 5.2 · W47 GraphQL presentation | [Resolvers, field policies and REST reuse](Week47GraphQLPresentation.md) · [GraphQL code](../apps/channel-microservice/src/ChannelMicroservice.Presentation/GraphQL) | Run the [mutation and query](Week47GraphQLPresentation.md#run-the-example). The created channel appears in the result with the requested fields and is also available through REST. |

The W46 title names Rebus. The detailed Moodle task asks for the existing MessageClient and hand-written coordination. Bizcord implements that version with EasyNetQ and SQLite.

## Verification record

On 26 November 2026, all 69 .NET tests passed, including the RabbitMQ workflow tests. The gateway smoke suite also passed. An isolated run of the submitted Compose services used fresh Vault storage and separate workload tokens. The gateway received 403 when reading the broker credential. Live requests confirmed REST scope enforcement, forwarded identity, the internal service role, REST and GraphQL reading each other's writes, and Channel Service stdout reaching Fluentd.

The test restore reported NU1903 for the upload workflow's transitive `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 dependency.
