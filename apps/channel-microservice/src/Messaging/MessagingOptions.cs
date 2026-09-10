namespace ChannelService.Messaging;

// Bindes til sektionen "Messaging" i appsettings.
public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    // Kan overskrives med miljøvariablen Messaging__ConnectionString.
    public string ConnectionString { get; init; } = string.Empty;
}
