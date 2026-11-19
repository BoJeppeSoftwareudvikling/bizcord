namespace ChannelService.Domain;

public sealed class Channel
{
    public Guid Id { get; set; }

    public long Version { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Guid? LastMessageId { get; set; }

    public DateTimeOffset? LastActivityAt { get; set; }

    // Ordinary activity can arrive while a provisional attachment is the newest message.
    public Guid? LastOrdinaryMessageId { get; set; }
    public DateTimeOffset? LastOrdinaryActivityAt { get; set; }
    public Dictionary<Guid, AttachmentActivity> Attachments { get; set; } = new();

    public void RefreshLastActivity()
    {
        var latest = Attachments.Where(entry => !entry.Value.Retracted)
            .OrderByDescending(entry => entry.Value.PostedAt).ThenBy(entry => entry.Key)
            .FirstOrDefault();
        if (latest.Value is not null && (LastOrdinaryActivityAt is null || latest.Value.PostedAt > LastOrdinaryActivityAt))
        {
            LastMessageId = latest.Key;
            LastActivityAt = latest.Value.PostedAt;
            return;
        }

        LastMessageId = LastOrdinaryMessageId;
        LastActivityAt = LastOrdinaryActivityAt;
    }

    public List<ChannelMember> Members { get; set; } = new();
}

public sealed record AttachmentActivity(DateTimeOffset PostedAt, Guid[] ReadersCharged, bool Retracted);
