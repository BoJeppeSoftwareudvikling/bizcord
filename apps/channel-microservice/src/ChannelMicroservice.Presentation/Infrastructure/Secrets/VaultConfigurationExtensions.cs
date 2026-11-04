using Microsoft.Extensions.Configuration;

namespace ChannelService.Infrastructure.Secrets;

public static class VaultConfigurationExtensions
{
    public static async Task AddSecretsFromVaultAsync(
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
        var secretReader = new VaultSecretReader(httpClient);
        var connectionString = await secretReader.ReadRequiredSecretAsync(
            options.MessagingSecretPath,
            options.MessagingSecretKey,
            token!,
            cancellationToken);
        var jwtSigningKey = await secretReader.ReadRequiredSecretAsync(
            options.JwtSecretPath,
            options.JwtSecretKey,
            token!,
            cancellationToken);

        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Messaging:ConnectionString"] = connectionString,
            ["Authentication:SigningKey"] = jwtSigningKey
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

        if (string.IsNullOrWhiteSpace(options.MessagingSecretPath))
        {
            throw new InvalidOperationException(
                "Vault:MessagingSecretPath is required.");
        }

        if (string.IsNullOrWhiteSpace(options.MessagingSecretKey))
        {
            throw new InvalidOperationException(
                "Vault:MessagingSecretKey is required.");
        }

        if (string.IsNullOrWhiteSpace(options.JwtSecretPath))
        {
            throw new InvalidOperationException(
                "Vault:JwtSecretPath is required.");
        }

        if (string.IsNullOrWhiteSpace(options.JwtSecretKey))
        {
            throw new InvalidOperationException(
                "Vault:JwtSecretKey is required.");
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
