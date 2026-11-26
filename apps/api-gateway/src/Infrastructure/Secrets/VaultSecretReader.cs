using System.Net;
using System.Text.Json;

namespace Bizcord.ApiGateway.Secrets;

public sealed class VaultSecretReader(HttpClient httpClient)
{
    private readonly HttpClient _httpClient =
        httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    public async Task<string> ReadRequiredSecretAsync(
        string secretPath,
        string secretKey,
        string token,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            secretPath.TrimStart('/'));
        request.Headers.Add("X-Vault-Token", token);

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new InvalidOperationException(
                "Vault denied access. Check the application token, TTL and policy.");
        }

        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            throw new InvalidOperationException(
                "Vault is unavailable or sealed. Unseal Vault before starting the gateway.");
        }

        response.EnsureSuccessStatusCode();

        await using var content = await response.Content.ReadAsStreamAsync(
            cancellationToken);

        try
        {
            using var document = await JsonDocument.ParseAsync(
                content,
                cancellationToken: cancellationToken);

            if (!document.RootElement.TryGetProperty("data", out var envelope) ||
                !envelope.TryGetProperty("data", out var values) ||
                !values.TryGetProperty(secretKey, out var secretElement) ||
                secretElement.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException(
                    $"Vault secret key '{secretKey}' is missing or is not a string.");
            }

            var secret = secretElement.GetString();

            return string.IsNullOrWhiteSpace(secret)
                ? throw new InvalidOperationException(
                    $"Vault secret key '{secretKey}' is empty.")
                : secret;
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "Vault returned an invalid KV v2 response.",
                exception);
        }
    }
}
