# Week 47, task 01: Fluentd logging sidecar

## Result and log flow

Channel Service produces console logs using the console provider registered by
`WebApplication.CreateBuilder` in `Program.cs`. Docker delivers those records to
the Fluentd companion, which prints them to its own stdout:

```text
Channel Service stdout/stderr
    → Docker fluentd logging driver (tag: bizcord.channel)
    → Fluentd forward input on TCP 24224
    → Fluentd stdout output
```

Before this change, application output stayed with Docker's default logger. The
new flow moves collection and delivery into deployment configuration. Application
code, log levels, business behavior, authentication and Vault retrieval remain
unchanged. No Fluentd SDK or additional .NET package is required.

`docker-compose.yml` owns the transport, container lifecycle and logging limits.
`fluentd/fluent.conf` owns the input and destination. Only Channel Service opts
into forwarding, and Fluentd matches only `bizcord.channel`. Fluentd itself uses
the `local` driver, so its output cannot feed back into the collector. RabbitMQ
and Vault are not forwarded.

## Transport decision

The assignment's initial example tails Docker's private `json-file` files.
Its linked Fluentd article instead uses the Docker `fluentd` driver and a
`forward` input. This implementation follows the article's transport and keeps
only the required console destination. Elasticsearch and Kibana are optional and
are not part of this implementation.

| Choice | Benefit | Cost or limitation |
| --- | --- | --- |
| `json-file` plus `tail` | Closely follows the initial task example; position files track read offsets. | Requires Docker log-directory access, permissions, rotation handling and strict input selection; Docker advises against external access to these files. |
| `fluentd` driver plus `forward` (selected) | Collects stdout without host log mounts, Docker socket access or application changes. | Requires a daemon-reachable collector endpoint and an explicit delivery policy during outages. |

This fulfills the stdout → Fluentd → console behavior using the transport in the
linked article. It deliberately differs from the initial `tail` example. If the
assessment requires that exact input plugin, confirm this distinction with the
instructor.

## Deployment and failure behavior

- The image is versioned as `fluent/fluentd:v1.20.1-debian-1.1` instead of `latest`.
- TCP 24224 is published only on `127.0.0.1`. The Docker daemon connects to that
  address; `fluentd:24224` is a Compose DNS name and is not the daemon's endpoint.
  No UDP publication is needed for this TCP logging driver.
- Fluentd joins the default network. Channel Service retains its internal backend
  network and has no published HTTP port.
- Compose starts Fluentd before Channel Service. The driver connects
  asynchronously, so collector readiness or an outage does not prevent the
  application container from starting. Vault and RabbitMQ startup requirements
  still apply.
- Docker uses non-blocking delivery with a 1 MiB queue, and the Fluentd driver has
  an additional memory buffer limited to 8,192 events and a one-second write
  timeout. These are separate buffers. Full buffers can drop logs; restarting a
  container or daemon can lose pending records. This is best-effort delivery,
  without exactly-once or durable replay guarantees.
- Fluentd's console output rotates through at most three 10 MiB local log files.
  Rotation limits console retention; no `tail` position file or input log rotation
  is involved. Docker's local cache for the application is not a replay queue.
- Use `docker compose up` / `down` to manage the pair. Compose does not automatically
  stop Fluentd when only Channel Service is stopped. This is a companion for the
  current single-instance development setup, not automatic one-sidecar-per-replica
  orchestration. Scaling the service would share this collector; a strict per-instance
  deployment needs instance-specific pairing and endpoints.
- Another local stack cannot bind the same collector port. A remote Docker daemon
  must be able to access the bind-mounted configuration on its own host.

## Run and verify

From the repository root, prepare Vault and the scoped application token using
[the week 45 guide](Week45VaultIntegration.md), then run:

```bash
docker compose config --quiet
docker compose run --rm --no-deps fluentd fluentd --dry-run -c /fluentd/etc/fluent.conf
docker compose up -d --build
docker compose logs -f fluentd
```

Look for `bizcord.channel` records containing the Channel Service container ID,
`source: stdout` and lifecycle messages such as `Now listening on:`. Fluentd also
prints its own startup messages; those are not collected application records.

To generate a fresh lifecycle log and compare both outputs:

```bash
docker compose restart channel-microservice
docker compose logs --since 1m channel-microservice
docker compose logs --since 1m fluentd
```

Restarting Channel Service clears its existing in-memory domain data, as it did
before this task. Use a disposable development session for restart checks.

To check a collector outage in that session:

```bash
docker compose stop fluentd
docker run --rm --network bizcord-backend curlimages/curl:8.10.1 \
  -fsS --max-time 5 http://channel-microservice:8080/health
docker compose start fluentd
```

The health response should remain healthy. Generate fresh application output and
check Fluentd after reconnecting. A successful short-outage test is not proof of
lossless delivery under buffer exhaustion or a Docker daemon restart.

## Verification evidence

Executed against an isolated Compose project derived from the repository config.
The test used a separate backend network, collector port `32424`, disposable
RabbitMQ storage and a real Vault in development mode with test-only credentials.
HTTP framework logging was temporarily raised to Information in that test project
so UUID query markers appeared in request logs. The repository's default log
levels remain unchanged.

Passed checks:

- `docker compose config --quiet` on the repository configuration.
- Fluentd `--dry-run -c /fluentd/etc/fluent.conf` using the pinned image.
- A fresh Docker build of the actual Channel Service.
- Default application lifecycle logs and a newly generated UUID request marker
  appeared in Fluentd output with `bizcord.channel`, the original container ID
  and `source: stdout`.
- Creating a channel and reading it back worked with real RabbitMQ.
- Health and an existing channel read worked while Fluentd was stopped.
- Channel Service restarted successfully while Fluentd remained stopped.
- Fresh logs reached Fluentd after recovery and again after a collector restart.
- All forwarded container IDs belonged to Channel Service; neither Fluentd nor
  the infrastructure services appeared as collected records.

One request marker generated during the outage was **not** found after restarting
Channel Service before recovering Fluentd. This observation is consistent with
non-durable delivery; it is not evidence of lossless buffering or replay.

The temporary project was removed after verification. Existing application
containers and data volumes were not used or changed. The .NET test suite,
local-log rotation, buffer saturation, daemon restart and sustained-load delivery
were not tested as part of this configuration change.

## Reflection

The application owns what it logs; Docker and Fluentd own collection and delivery.
Changing the destination can now be done in Fluentd configuration. Console output
proves the transport but provides neither a central searchable store nor durable
retention. A future central sink should define buffering, access control and the
acceptable loss policy before this is used beyond the local exercise.

## Resources

- [Assignment-linked Fluentd Docker Compose guide](https://docs.fluentd.org/container-deployment/docker-compose)
- [Docker Fluentd logging driver](https://docs.docker.com/engine/logging/drivers/fluentd/)
- [Docker JSON File driver and direct-file-access warning](https://docs.docker.com/engine/logging/drivers/json-file/)
- [Fluentd image versions](https://github.com/fluent/fluentd-docker-image)
