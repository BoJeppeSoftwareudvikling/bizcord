using ChannelService.Application.Abstractions;
using ChannelService.Domain;
using MessageClient;
using Messages;

namespace ChannelService.Infrastructure.Messaging;

public sealed class AttachmentActivitySubscription(IMessageClient bus, IChannelRepository channels) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var subscription = await bus.SubscribeAsync<AttachmentActivityCommand>(
            "channel-attachment-activity", Handle, stoppingToken);
        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    public async Task Handle(AttachmentActivityCommand command, CancellationToken ct)
    {
        var (upload, step) = command;
        if (!upload.IsValid || step is not (UploadStep.RecordActivity or UploadStep.RetractActivity))
            throw new ArgumentException("Invalid attachment activity command.");

        var channel = await channels.GetAsync(upload.ChannelId, ct);
        string? rejection = null;
        if (channel is null)
        {
            if (step == UploadStep.RecordActivity) rejection = "Channel does not exist";
        }
        else if (Apply(channel, upload, step))
        {
            await channels.SaveAsync(channel, ct);
        }

        await bus.PublishAsync(new UploadStepResult(upload.SagaId, step, rejection), ct);
    }

    private static bool Apply(Channel channel, StartUpload upload, UploadStep step)
    {
        channel.Attachments.TryGetValue(upload.SagaId, out var activity);
        if (step == UploadStep.RecordActivity)
        {
            if (activity is not null) return false;
            if (channel.Attachments.Count == 0)
            {
                channel.LastOrdinaryActivityAt = channel.LastActivityAt;
                channel.LastOrdinaryMessageId = channel.LastMessageId;
            }

            var recipients = channel.Members.Where(m => m.UserId != upload.UserId && m.Permissions.CanReadMessages).ToArray();
            foreach (var member in recipients) member.UnreadAttachments++;
            channel.Attachments[upload.SagaId] = new(upload.PostedAt, recipients.Select(m => m.UserId).ToArray(), false);
        }
        else
        {
            if (activity is null || activity.Retracted) return false;
            foreach (var member in channel.Members.Where(m => activity.ReadersCharged.Contains(m.UserId)))
                member.UnreadAttachments = Math.Max(0, member.UnreadAttachments - 1);
            channel.Attachments[upload.SagaId] = activity with { Retracted = true };
        }

        channel.RefreshLastActivity();
        return true;
    }
}
