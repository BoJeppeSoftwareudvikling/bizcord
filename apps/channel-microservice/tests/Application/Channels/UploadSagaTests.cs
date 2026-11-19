using ChannelService.Application.Channels;
using ChannelService.Domain;
using ChannelService.Infrastructure;
using ChannelService.Infrastructure.Messaging;
using MessageClient;
using Messages;
using Microsoft.Data.Sqlite;
using Moq;
using UploadWorkflow;
using static Messages.UploadStep;

namespace ChannelService.Tests.Application.Channels;

public sealed class UploadSagaTests : IDisposable
{
    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("upload-saga");
    private readonly Mock<IMessageClient> bus = new();
    private readonly InMemoryChannelRepository channels = new();
    private readonly Channel channel = new() { Id = Guid.NewGuid() };
    private readonly Guid author = Guid.NewGuid();
    private readonly SimulatedParticipants participants;
    private readonly AttachmentActivitySubscription activity;
    private UploadCoordinator coordinator;
    private bool dropNextReply;

    public UploadSagaTests()
    {
        coordinator = OpenCoordinator();
        participants = new(directory.FullName, bus.Object);
        activity = new(bus.Object, channels);
        Route<FileCommand>(participants.Handle);
        Route<AttachmentMessageCommand>(participants.Handle);
        Route<QuotaCommand>(participants.Handle);
        Route<AttachmentActivityCommand>(activity.Handle);
        Route<UploadStepResult>((result, ct) =>
        {
            if (!dropNextReply) return coordinator.Receive(result, ct);
            dropNextReply = false;
            return Task.CompletedTask;
        });
    }

    private void Route<T>(Func<T, CancellationToken, Task> handler) where T : class =>
        bus.Setup(b => b.PublishAsync(It.IsAny<T>(), It.IsAny<CancellationToken>())).Returns(handler);

    private UploadCoordinator OpenCoordinator() => new(Path.Combine(directory.FullName, "sagas.db"), bus.Object);
    private StartUpload Upload(long bytes) => new(Guid.NewGuid(), author, channel.Id, bytes, DateTimeOffset.UtcNow);

    private async Task Advance(StartUpload upload, UploadStep target)
    {
        for (var attempt = 0; attempt < 15 && coordinator.Status(upload.SagaId)?.Step != target; attempt++)
            await coordinator.DispatchPending(default);
        Assert.Equal(target, coordinator.Status(upload.SagaId)?.Step);
    }

    private long Query(string file, string sql)
    {
        using var db = new SqliteConnection(new SqliteConnectionStringBuilder {
            DataSource = Path.Combine(directory.FullName, file + ".db"), Pooling = false }.ToString());
        db.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    [Fact]
    public async Task LateRejection_RetractsOnlyThisUpload_ForTwoHundredRecipients()
    {
        for (var i = 0; i < 200; i++) channel.Members.Add(new() { UserId = Guid.NewGuid() });
        channel.Members.Add(new() { UserId = author });
        await channels.AddAsync(channel);
        var accepted = Upload(800_000);
        await coordinator.Start(accepted, default);
        await Advance(accepted, Completed);
        Assert.Equal(1, Query("files", "SELECT available FROM file"));
        var rejected = Upload(300_000);
        await coordinator.Start(rejected, default);
        await Advance(rejected, ReserveQuota);
        var visible = await channels.GetAsync(channel.Id);
        Assert.Equal(rejected.SagaId, visible!.LastMessageId);
        Assert.All(visible.Members.Where(m => m.UserId != author), m => Assert.Equal(2, m.UnreadAttachments));
        var ordinary = Guid.NewGuid();
        await new ChannelManagementService(channels, bus.Object)
            .RecordMessageActivityAsync(channel.Id, ordinary, rejected.PostedAt.AddSeconds(1));
        await Advance(rejected, Rejected);
        await activity.Handle(new(rejected, RecordActivity), default);
        await activity.Handle(new(rejected, RetractActivity), default);
        await participants.Handle(new AttachmentMessageCommand(rejected, PostMessage), default);
        var stored = await channels.GetAsync(channel.Id);
        Assert.Equal(ordinary, stored!.LastMessageId);
        Assert.All(stored.Members.Where(m => m.UserId != author), m => Assert.Equal(1, m.UnreadAttachments));
        Assert.Equal(0, stored.Members.Single(m => m.UserId == author).UnreadAttachments);
        Assert.Equal("Storage quota exceeded", coordinator.Status(rejected.SagaId)?.Reason);
        Assert.Equal(800_000, Query("quota", "SELECT SUM(bytes) FROM reservation"));
        Assert.Equal(1, Query("messages", "SELECT COUNT(*) FROM message"));
        Assert.Equal(800_000, Query("files", "SELECT SUM(bytes) FROM file"));
    }

    [Fact]
    public async Task LostQuotaReply_RestartRetriesReservation_ActivationFailureRefundsExactlyOnce()
    {
        await channels.AddAsync(channel);
        var upload = Upload(500);
        await coordinator.Start(upload, default);
        await Advance(upload, ReserveQuota);
        dropNextReply = true;
        await coordinator.DispatchPending(default);
        Assert.Equal(500, Query("quota", "SELECT SUM(bytes) FROM reservation"));
        coordinator = OpenCoordinator();
        await coordinator.Start(upload, default);
        await Advance(upload, ActivateFile);
        Query("files", "DELETE FROM file");
        await Advance(upload, Rejected);
        await participants.Handle(new QuotaCommand(upload, ReserveQuota), default);
        Assert.Equal("Temporary file missing", coordinator.Status(upload.SagaId)?.Reason);
        Assert.Equal(0, Query("quota", "SELECT COUNT(*) FROM reservation"));
        Assert.Equal(0, Query("messages", "SELECT COUNT(*) FROM message"));
        Assert.Null((await channels.GetAsync(channel.Id))!.LastMessageId);
    }

    [Fact]
    public async Task MissingChannel_CleansStagedFileAndMessage()
    {
        var upload = Upload(100);
        await coordinator.Start(upload, default);
        await Advance(upload, Rejected);
        Assert.Equal("Channel does not exist", coordinator.Status(upload.SagaId)?.Reason);
        Assert.Equal(0, Query("files", "SELECT COUNT(*) FROM file"));
        Assert.Equal(0, Query("messages", "SELECT COUNT(*) FROM message"));
        Assert.Equal(0, Query("quota", "SELECT COUNT(*) FROM reservation"));
    }

    public void Dispose()
    {
        activity.Dispose();
        directory.Delete(true);
    }
}
