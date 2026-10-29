namespace MessageClient;

// Bindes til sektionen "Messaging" i appsettings.
public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    // Kan overskrives med miljøvariablen Messaging__ConnectionString.
    public string ConnectionString { get; init; } = string.Empty;

    public TimeSpan PublishTimeout { get; init; } = TimeSpan.FromSeconds(5);

    public int MaxConcurrentPublishes { get; init; } = 16;
}
