using MessageClient;
using Messages;
using Microsoft.Data.Sqlite;

namespace UploadWorkflow;

// Each simulated service has its own SQLite file. A receipt and its resource
// change commit together so a duplicate command returns the original outcome.
public sealed class SimulatedParticipants
{
    public const long QuotaBytes = 1_048_576;
    private const string Receipts = """
        CREATE TABLE IF NOT EXISTS receipt(id TEXT, step TEXT, rejection TEXT, PRIMARY KEY(id,step));
        """;
    private readonly Database files;
    private readonly Database quota;
    private readonly Database messages;
    private readonly IMessageClient bus;

    public SimulatedParticipants(string directory, IMessageClient bus)
    {
        this.bus = bus;
        files = new(Path.Combine(directory, "files.db"), Receipts +
            "CREATE TABLE IF NOT EXISTS file(id TEXT PRIMARY KEY, bytes INTEGER, available INTEGER);");
        quota = new(Path.Combine(directory, "quota.db"), Receipts +
            "CREATE TABLE IF NOT EXISTS reservation(id TEXT PRIMARY KEY, owner TEXT, bytes INTEGER);");
        messages = new(Path.Combine(directory, "messages.db"), Receipts +
            "CREATE TABLE IF NOT EXISTS message(id TEXT PRIMARY KEY, channel TEXT, author TEXT, attachment TEXT);");
    }

    public Task Handle(FileCommand command, CancellationToken ct) =>
        Execute(files, command.Upload, command.Step, sql => command.Step switch
        {
            UploadStep.StageFile => Write(sql, "INSERT INTO file VALUES ($id,$bytes,0)"),
            UploadStep.ActivateFile => Write(sql, "UPDATE file SET available=1 WHERE id=$id", "Temporary file missing"),
            UploadStep.DeleteFile => Write(sql, "DELETE FROM file WHERE id=$id"),
            _ => throw new ArgumentException("Invalid file command")
        }, ct);

    public Task Handle(QuotaCommand command, CancellationToken ct) =>
        Execute(quota, command.Upload, command.Step, sql => command.Step switch
        {
            UploadStep.ReserveQuota => Write(sql, """
                INSERT INTO reservation
                SELECT $id,$user,$bytes WHERE $bytes <= $limit -
                    (SELECT COALESCE(SUM(bytes),0) FROM reservation WHERE owner=$user)
                """, "Storage quota exceeded"),
            UploadStep.ReleaseQuota => Write(sql, "DELETE FROM reservation WHERE id=$id"),
            _ => throw new ArgumentException("Invalid quota command")
        }, ct);

    public Task Handle(AttachmentMessageCommand command, CancellationToken ct) =>
        Execute(messages, command.Upload, command.Step, sql => command.Step switch
        {
            UploadStep.PostMessage => Write(sql, "INSERT INTO message VALUES ($id,$channel,$user,'attachment:' || $id)"),
            UploadStep.RetractMessage => Write(sql, "DELETE FROM message WHERE id=$id"),
            _ => throw new ArgumentException("Invalid message command")
        }, ct);

    private static string? Write(SqliteCommand command, string statement, string? noChange = null)
    {
        command.CommandText = statement;
        return command.ExecuteNonQuery() == 0 ? noChange : null;
    }

    private async Task Execute(Database db, StartUpload upload, UploadStep step,
        Func<SqliteCommand,string?> apply, CancellationToken ct)
    {
        if (!upload.IsValid) throw new ArgumentException("Invalid upload");
        string? rejection;
        using (var connection = db.Open())
        using (var tx = connection.BeginTransaction(deferred: false))
        using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.Parameters.AddWithValue("$id", upload.SagaId.ToString());
            cmd.Parameters.AddWithValue("$step", step.ToString());
            cmd.CommandText = "SELECT COALESCE(rejection,'') FROM receipt WHERE id=$id AND step=$step";
            if (cmd.ExecuteScalar() is string previous)
            {
                rejection = previous.Length == 0 ? null : previous;
            }
            else
            {
                cmd.Parameters.AddWithValue("$bytes", upload.Bytes);
                cmd.Parameters.AddWithValue("$limit", QuotaBytes);
                cmd.Parameters.AddWithValue("$user", upload.UserId.ToString());
                cmd.Parameters.AddWithValue("$channel", upload.ChannelId.ToString());
                rejection = apply(cmd);
                cmd.CommandText = "INSERT INTO receipt VALUES ($id,$step,$reason)";
                cmd.Parameters.AddWithValue("$reason", (object?)rejection ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
        }

        await bus.PublishAsync(new UploadStepResult(upload.SagaId, step, rejection), ct);
    }
}
