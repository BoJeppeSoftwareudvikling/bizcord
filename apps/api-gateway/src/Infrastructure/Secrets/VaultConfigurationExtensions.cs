using Microsoft.Extensions.Configuration;

namespace Bizcord.ApiGateway.Secrets;

public static class VaultConfigurationExtensions
{
    public static async Task AddJwtSigningKeyFromVaultAsync(
        this ConfigurationManager configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var options = configuration
            .GetRequiredSection(VaultOptions.SectionName)
            .Get<VaultOptions>()
            ?? throw new InvalidOperationException(
                $"Configuration section '{VaultOptions.SectionName}' is required.");
        var address = configuration["VAULT_ADDR"];
        if (string.IsNullOrWhiteSpace(address))
        {
            address = options.Address;
        }

        var token = configuration["VAULT_TOKEN"];
        Validate(options, address, token);

        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri(address.TrimEnd('/') + "/"),
            Timeout = options.Timeout
        };
        var signingKey = await new VaultSecretReader(httpClient)
            .ReadRequiredSecretAsync(
                options.SecretPath,
                options.SecretKey,
                token!,
                cancellationToken);

        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AuthenticationProviderKey"] = signingKey
        });
    }

    private static void Validate(
        VaultOptions options,
        string? address,
        string? token)
    {
        if (string.IsNullOrWhiteSpace(address) ||
            !Uri.TryCreate(address, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException(
                "VAULT_ADDR or Vault:Address must be an absolute URI.");
        }

        if (string.IsNullOrWhiteSpace(options.SecretPath))
        {
            throw new InvalidOperationException("Vault:SecretPath is required.");
        }

        if (string.IsNullOrWhiteSpace(options.SecretKey))
        {
            throw new InvalidOperationException("Vault:SecretKey is required.");
        }

        if (options.Timeout <= TimeSpan.Zero ||
            options.Timeout > TimeSpan.FromMinutes(1))
        {
            throw new InvalidOperationException(
                "Vault:Timeout must be positive and at most one minute.");
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                "VAULT_TOKEN must contain a scoped application token.");
        }
    }
}
