using System.Net;
using System.Text;
using ChannelService.Infrastructure.Secrets;

namespace ChannelService.Tests.Infrastructure.Secrets;

public sealed class VaultSecretReaderTests
{
    [Fact]
    public async Task ReadRequiredSecretAsync_ReturnsKvV2Secret()
    {
        var handler = new RecordingHandler(_ => JsonResponse(
            HttpStatusCode.OK,
            """
            {
              "data": {
                "data": {
                  "ConnectionString": "host=rabbitmq;username=app;password=secret"
                }
              }
            }
            """));
        using var client = CreateClient(handler);
        var reader = new VaultSecretReader(client);

        var secret = await reader.ReadRequiredSecretAsync(
            "v1/secret/data/channel-service/messaging",
            "ConnectionString",
            "scoped-token");

        Assert.Equal("host=rabbitmq;username=app;password=secret", secret);
        Assert.Equal(
            "http://vault:8200/v1/secret/data/channel-service/messaging",
            handler.RequestUri?.ToString());
        Assert.Equal("scoped-token", handler.VaultToken);
    }

    [Fact]
    public async Task ReadRequiredSecretAsync_RejectsForbiddenResponseWithoutLeakingBody()
    {
        const string sensitiveBody = "do-not-leak-this-value";
        var handler = new RecordingHandler(_ => new HttpResponseMessage(
            HttpStatusCode.Forbidden)
        {
            Content = new StringContent(sensitiveBody)
        });
        using var client = CreateClient(handler);
        var reader = new VaultSecretReader(client);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            reader.ReadRequiredSecretAsync(
                "v1/secret/data/channel-service/messaging",
                "ConnectionString",
                "invalid-token"));

        Assert.DoesNotContain(sensitiveBody, exception.Message);
        Assert.Contains("denied access", exception.Message);
    }

    [Fact]
    public async Task ReadRequiredSecretAsync_ReportsSealedVault()
    {
        var handler = new RecordingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var client = CreateClient(handler);
        var reader = new VaultSecretReader(client);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            reader.ReadRequiredSecretAsync(
                "v1/secret/data/channel-service/messaging",
                "ConnectionString",
                "scoped-token"));

        Assert.Contains("sealed", exception.Message);
    }

    [Fact]
    public async Task ReadRequiredSecretAsync_RejectsMissingKey()
    {
        var handler = new RecordingHandler(_ => JsonResponse(
            HttpStatusCode.OK,
            """{"data":{"data":{"AnotherKey":"value"}}}"""));
        using var client = CreateClient(handler);
        var reader = new VaultSecretReader(client);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            reader.ReadRequiredSecretAsync(
                "v1/secret/data/channel-service/messaging",
                "ConnectionString",
                "scoped-token"));

        Assert.Contains("ConnectionString", exception.Message);
    }

    private static HttpClient CreateClient(HttpMessageHandler handler) => new(handler)
    {
        BaseAddress = new Uri("http://vault:8200/")
    };

    private static HttpResponseMessage JsonResponse(
        HttpStatusCode statusCode,
        string json) => new(statusCode)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public string? VaultToken { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            VaultToken = request.Headers.TryGetValues(
                "X-Vault-Token",
                out var values)
                ? values.Single()
                : null;

            return Task.FromResult(responseFactory(request));
        }
    }
}
