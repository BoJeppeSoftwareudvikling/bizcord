namespace Bizcord.ApiGateway.Secrets;

public sealed class VaultOptions
{
    public const string SectionName = "Vault";

    public string Address { get; init; } = string.Empty;

    public string SecretPath { get; init; } = string.Empty;

    public string SecretKey { get; init; } = string.Empty;

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);
}
