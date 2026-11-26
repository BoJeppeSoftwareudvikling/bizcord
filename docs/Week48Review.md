# Compulsory assignment 2 — Weeks 43–47

For the week 48 submission, we extended the Channel Service from assignment 1. A client now enters through the gateway and can use REST or GraphQL. The attachment upload is a separate message-driven workflow. Each week below links the design decision to the code that implements it. To try the examples, use the [setup instructions](#run-from-a-fresh-checkout).

## Week 43 — API gateway

**Task 01 — Routing.** Clients use one public address for channel operations. We listed each path and HTTP method in Ocelot so adding a new controller action does not automatically expose it. Look at the `POST /channels` route in particular. It rewrites the creation response’s `Location` header so the client can fetch its new channel through the gateway. [Route configuration](../apps/api-gateway/src/Configuration/ocelot.example.json) · [Routing decisions](Week43ApiGatewayAndAuthorization.md#task-01-routing-configuration)

**Task 02 — Authorization.** We used separate read and write scopes because permission depends on the operation. The same client can list channels and still be denied permission to create one. The gateway also removes incoming identity headers and passes on the identity from the validated token. Try a read token on a write route for a 403, then call `GET /channels/me` to see the forwarded identity. [Gateway middleware](../apps/api-gateway/src/Program.cs) · [Scope decisions and runnable requests](Week43ApiGatewayAndAuthorization.md#manual-verification)

## Week 44 — Reliability

**Task 01 — Identify failure points.** We documented nine ways the service could fail. Start with FP01 and FP02. If RabbitMQ is slow, a channel write can leave its caller waiting. If saving the channel succeeds and publishing the event fails, the caller can see an error even though the channel already exists. Those two failures need different responses. [Failure analysis and priorities](FAILURE_POINTS.md)

**Task 02 — Implement reliability measures.** We put a timeout and a limit on simultaneous publishes in the shared message client, so every publisher uses the same limits. We avoided automatic write retries because repeating a request after an uncertain publish could repeat work already done. We also added version checks so an older channel update cannot overwrite a newer one. The tests simulate a stalled broker, exhausted publish capacity and stale saves. [Bounded message client](../packages/MessageClient/src/BoundedMessageClient.cs) · [Design decisions and failure tests](Week44ReliabilityMeasures.md#tests-og-fejlsimulering)

## Week 45 — Security

**Task 01 — Service-to-service access.** Another service can ask whether a channel exists. Both the gateway and Channel Service check the caller’s token and require the `internal-service` role. We enforce this inside Channel Service because another container on the private network can reach it directly. The live checks call the same endpoint through both paths and show when it returns 401, 403 or 200. [Internal endpoint](../apps/channel-microservice/src/ChannelMicroservice.Presentation/Controllers/InternalChannelsController.cs) · [Service policy](../apps/channel-microservice/src/ChannelMicroservice.Presentation/Authentication/ServiceAuthenticationExtensions.cs) · [Design and live checks](Week45EastWestSecurity.md)

**Task 02 — Vault.** The gateway and Channel Service need the same JWT signing key, but only Channel Service needs the broker password. We therefore give each workload a separate read-only Vault token. In the guide, compare the two policies and try reading the broker credential with the gateway token. Vault should return 403. [Vault setup and policy checks](Week45VaultIntegration.md#manual-setup-and-verification) · [Channel Service secret loading](../apps/channel-microservice/src/ChannelMicroservice.Presentation/Infrastructure/Secrets)

## Week 46 — Data management

**Task 01 — Design the upload scenario.** A user posts an attachment and then exceeds their quota. By the time quota is checked, others may have seen the provisional message. Our workflow retracts that message, removes its contribution to unread counts and deletes the staged file. It preserves later channel activity. Channel Service is real in this demo. File storage, quota and messaging are simulated. [Workflow and compensation diagram](Week46AttachmentSaga.md#saga-design) · [Participant simulators](../apps/upload-workflow/SimulatedParticipants.cs)

**Task 02 — Implement the saga.** One coordinator records the current step in SQLite and sends commands through our existing EasyNetQ message client. We chose orchestration because the cleanup depends on how far the upload got. A quota refusal needs no refund, while an activation failure after a successful reservation does. The Moodle title mentions Rebus. The detailed task asks for hand-written coordination through the existing client, which is the version we implemented. The two-upload demo shows one completion and one rejection. [Coordinator transitions](../apps/upload-workflow/UploadCoordinator.cs) · [Run the demo](Week46AttachmentSaga.md#run-the-workflow) · [Recovery and compensation tests](Week46AttachmentSaga.md#verification)

## Week 47 — Maintainability and decoupling

**Task 01 — Logging sidecar.** Channel Service writes to stdout. Docker forwards those records to Fluentd, which prints them to its own console. We chose Docker’s logging driver so Fluentd can collect application logs without mounting the host’s container log directory. Delivery is buffered and best-effort, so a prolonged outage can lose records. Run `docker compose logs fluentd` and look for `bizcord.channel` to distinguish application records from Fluentd’s own startup messages. [Compose logging settings](../docker-compose.yml) · [Fluentd configuration](../fluentd/fluent.conf) · [Transport alternatives and verification](Week47LoggingSidecar.md)

**Task 02 — GraphQL presentation.** A client can create a channel through REST and query its ID and name through GraphQL. Both entry points call the same application service, so they share the creation rules and see the same channel data. We hosted GraphQL inside Channel Service because a separate frontend-facing service would need another network call to reach that state. Read and write permissions are checked on individual GraphQL fields because queries and mutations use the same HTTP route. [GraphQL queries and mutations](../apps/channel-microservice/src/ChannelMicroservice.Presentation/GraphQL) · [Runnable example](Week47GraphQLPresentation.md#run-the-example) · [Design alternatives](Week47GraphQLPresentation.md#ownership-and-alternatives)

## Run from a fresh checkout

Use Docker with Compose v2, the .NET 10 SDK, Bash, curl and Python 3. Run commands from the Bizcord root. The configured Ocelot gateway is included under [apps/api-gateway](../apps/api-gateway), based on the [course starter](https://github.com/patrickstolc/api-gateway).

1. Follow [Vault bootstrap, steps 1–5](Week45VaultIntegration.md#manual-setup-and-verification). Start RabbitMQ and Vault first, unseal Vault, store the two secrets, then set the two scoped tokens in the root `.env`.
2. Run `docker compose up -d --build` and `docker compose ps`. The gateway should publish `127.0.0.1:8080`. The Channel Service HTTP port should have no host mapping.
3. Once the gateway is ready, run the first command below. A **401** confirms that the protected route is listening. Use the [week 43 token examples](Week43ApiGatewayAndAuthorization.md#manual-verification) to get a **200** for the same request. Use the live checks in the weekly sections while the stack is running.
4. Run the two test commands below. The .NET suite uses a disposable RabbitMQ container. The gateway smoke suite starts an isolated gateway with test routes.
5. When finished, run `docker compose down`. This preserves Vault and RabbitMQ volumes. On a later run, start the infrastructure, unseal the existing Vault and issue fresh one-hour application tokens. Channel data is held in memory and resets when its process stops.

```bash
curl -i http://localhost:8080/channels
dotnet test Bizcord.slnx
python3 apps/api-gateway/tests/smoke.py
```

## Verification record

On 26 November 2026, all 69 .NET tests passed, including the RabbitMQ workflow tests. The gateway smoke suite also passed. An isolated run of the submitted Compose services used fresh Vault storage and separate workload tokens. The gateway received 403 when reading the broker credential. Live requests confirmed REST scope enforcement, forwarded identity, the internal service role, REST and GraphQL reading each other's writes, and Channel Service stdout reaching Fluentd.

The test restore reported NU1903 for the upload workflow's transitive `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 dependency.
