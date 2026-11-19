using System.Text.Json;
using MessageClient;
using Messages;
using static Messages.UploadStep;

namespace UploadWorkflow;

public sealed class UploadCoordinator(string path, IMessageClient bus)
{
    private readonly Database database = new(path, """
        CREATE TABLE IF NOT EXISTS saga(id TEXT PRIMARY KEY, request TEXT NOT NULL, step TEXT NOT NULL, reason TEXT);
        """);

    public Task Start(StartUpload upload, CancellationToken ct)
    {
        if (!upload.IsValid) throw new ArgumentException("Invalid upload");
        using var connection = database.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO saga VALUES ($id,$request,'StageFile',NULL) ON CONFLICT(id) DO NOTHING";
        cmd.Parameters.AddWithValue("$id", upload.SagaId.ToString());
        cmd.Parameters.AddWithValue("$request", JsonSerializer.Serialize(upload));
        cmd.ExecuteNonQuery();
        return Task.CompletedTask;
    }

    public Task Receive(UploadStepResult result, CancellationToken ct)
    {
        var next = Next(result);
        using var connection = database.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE saga SET step=$next, reason=COALESCE(reason,$reason)
            WHERE id=$id AND step=$expected
            """;
        cmd.Parameters.AddWithValue("$id", result.SagaId.ToString());
        cmd.Parameters.AddWithValue("$expected", result.Step.ToString());
        cmd.Parameters.AddWithValue("$next", next.ToString());
        cmd.Parameters.AddWithValue("$reason", (object?)result.Rejection ?? DBNull.Value);
        cmd.ExecuteNonQuery();
        return Task.CompletedTask;
    }

    private static UploadStep Next(UploadStepResult result) => (result.Step, result.Rejection is null) switch
    {
        (StageFile, true) => PostMessage,
        (PostMessage, true) => RecordActivity,
        (RecordActivity, true) => ReserveQuota,
        (ReserveQuota, true) => ActivateFile,
        (ActivateFile, true) => Completed,
        (StageFile, false) => Rejected,
        (PostMessage, false) => DeleteFile,
        (RecordActivity, false) => RetractMessage,
        (ReserveQuota, false) => RetractActivity,
        (ActivateFile, false) => ReleaseQuota,
        (ReleaseQuota, true) => RetractActivity,
        (RetractActivity, true) => RetractMessage,
        (RetractMessage, true) => DeleteFile,
        (DeleteFile, true) => Rejected,
        _ => throw new InvalidOperationException("Unexpected outcome for " + result.Step)
    };

    public (UploadStep Step, string? Reason)? Status(Guid id)
    {
        using var connection = database.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT step,reason FROM saga WHERE id=$id";
        cmd.Parameters.AddWithValue("$id", id.ToString());
        using var rows = cmd.ExecuteReader();
        return rows.Read() ? (Enum.Parse<UploadStep>(rows.GetString(0)), rows.IsDBNull(1) ? null : rows.GetString(1)) : null;
    }

    // The stored step is also the pending command. A lost command/reply is retried on the next pass.
    public async Task DispatchPending(CancellationToken ct)
    {
        var pending = new List<(StartUpload, UploadStep)>();
        using (var connection = database.Open())
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT request,step FROM saga WHERE step NOT IN ('Completed','Rejected')";
            using var rows = cmd.ExecuteReader();
            while (rows.Read())
                pending.Add((JsonSerializer.Deserialize<StartUpload>(rows.GetString(0))!, Enum.Parse<UploadStep>(rows.GetString(1))));
        }

        foreach (var (upload, step) in pending)
        {
            await (step switch
            {
                StageFile or ActivateFile or DeleteFile => bus.PublishAsync(new FileCommand(upload, step), ct),
                ReserveQuota or ReleaseQuota => bus.PublishAsync(new QuotaCommand(upload, step), ct),
                PostMessage or RetractMessage => bus.PublishAsync(new AttachmentMessageCommand(upload, step), ct),
                RecordActivity or RetractActivity => bus.PublishAsync(new AttachmentActivityCommand(upload, step), ct),
                _ => throw new InvalidOperationException("Unknown pending step")
            });
        }
    }
}
