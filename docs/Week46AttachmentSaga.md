# Attachment upload saga

Bizcord can record a pending attachment in a busy channel while its author's quota is being checked. File activation requires a successful reservation. If quota runs out, the upload coordinator retracts the provisional activity and message and deletes the staged file. Only a completed reservation can trigger a refund.

| Role | Implementation |
| --- | --- |
| Upload coordinator | .NET worker with its own SQLite database. Each row holds the original request, current step and first business rejection. |
| Channel Service | Existing Bizcord service. It applies activity and unread counts to readable members other than the author. The channel repository is in memory. |
| File Sharing | Simulator. SQLite stores size and availability. File bytes and download URLs are outside this demo. |
| Storage / Quota | Simulator. A per-user limit of 1 MiB is enforced atomically across that user's uploads. |
| Message | Simulator. A row represents the pending channel message and attachment reference. There is no connected chat client. |

The three simulators share an executable in simulator mode and own separate SQLite files. Communication uses Bizcord's existing EasyNetQ-backed `IMessageClient` over RabbitMQ. The coordinator owns the sequence because a quota refusal and an activation failure require different compensation. Checking quota before posting would reduce visible reversals in a real product. The late check exercises the scenario where a message may already have been seen.

## Run the workflow

From the repository root, build with the .NET 10 SDK. Start Bizcord, its gateway and a channel using the [existing stack instructions](../README.md#start) and [authenticated channel creation example](Week45VaultIntegration.md). Keep the channel ID. Use the local broker address reachable from the host. Enter its connection string at the prompt (for example, `host=localhost;username=<user>;password=<password>`).

```bash
dotnet build Bizcord.slnx
read -rsp 'Local RabbitMQ connection string: ' Messaging__ConnectionString
export Messaging__ConnectionString
workflow() { dotnet apps/upload-workflow/bin/Debug/net10.0/UploadWorkflow.dll "$@"; }
worker=apps/upload-workflow/bin/Debug/net10.0/UploadWorkflow.dll
dotnet "$worker" coordinator &
coordinator_pid=$!
dotnet "$worker" simulators &
simulator_pid=$!
```

Wait for both ready messages before starting the first upload. Paste the ID of the existing channel. Use a fresh user so the first upload starts with a full quota.

```bash
read -rp 'Existing channel GUID: ' channel_id
user_id=$(cat /proc/sys/kernel/random/uuid)
first=$(cat /proc/sys/kernel/random/uuid)
second=$(cat /proc/sys/kernel/random/uuid)
workflow start "$first" "$user_id" "$channel_id" 800000
workflow status "$first"
```

Repeat the status command until it shows `Completed`. Then send another 300,000 bytes for the same user. The remaining 248,576 bytes are insufficient.

```bash
workflow start "$second" "$user_id" "$channel_id" 300000
workflow status "$second"
```

The second upload eventually shows `(Rejected, Storage quota exceeded)`. During processing, status reports the pending command. Preserve `upload-data/` to resume the coordinator. Override its path with `UPLOAD_DATA` when running isolated instances. Stop the demo workers with `kill "$coordinator_pid" "$simulator_pid"` when finished. Restarting Channel Service discards its channels and counters, even when the workflow databases survive.

## Verification

```bash
dotnet test Bizcord.slnx --filter 'FullyQualifiedName~UploadSagaTests'
dotnet test Bizcord.slnx --filter 'FullyQualifiedName~AttachmentUpload_ReachesExpectedOutcomeOverRabbitMq'
```

The first command exercises lost replies, coordinator recovery, duplicate deliveries, missing channels, and compensation in a channel with 200 recipients and intervening activity. The second runs both outcomes through a RabbitMQ Testcontainer and the registered Channel Service handler. Docker must be available for the latter.
