namespace ChannelService.Infrastructure.Secrets;

public sealed class VaultOptions
{
    public const string SectionName = "Vault";

    public string Address { get; init; } = string.Empty;

    public string MessagingSecretPath { get; init; } = string.Empty;

    public string MessagingSecretKey { get; init; } = string.Empty;

    public string JwtSecretPath { get; init; } = string.Empty;

    public string JwtSecretKey { get; init; } = string.Empty;

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);
}
