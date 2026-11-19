using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ChannelService.Application;
using ChannelService.Application.Abstractions;
using ChannelService.Authentication;
using ChannelService.Controllers;
using ChannelService.GraphQL;
using ChannelService.Infrastructure;
using MessageClient;
using Messages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace ChannelService.Tests.Presentation.GraphQL;

public sealed class ChannelGraphQLTests
{
    private const string SigningKey = "graphql-test-signing-key-at-least-thirty-two-characters";
    private const string CreateMutation = "mutation($input: CreateChannelInput!) { createChannel(input: $input) { id name } }";

    [Fact]
    public async Task CreateThenQuery_SharesStateWithRestAndSelectsOnlyRequestedFields()
    {
        var messages = new Mock<IMessageClient>();
        await using var app = await StartAsync(messages);
        using var client = Client(app, "channel.read channel.write");

        var created = await GraphQLAsync(client, CreateMutation,
            new { input = new { name = "  Engineering  ", description = "  Team chat  " } });
        Assert.False(created.TryGetProperty("errors", out _), created.ToString());
        var channel = created.GetProperty("data").GetProperty("createChannel");
        Assert.Equal("Engineering", channel.GetProperty("name").GetString());
        Assert.Equal(2, channel.EnumerateObject().Count());
        var id = channel.GetProperty("id").GetGuid();

        var rest = await client.GetFromJsonAsync<JsonElement>($"/channels/{id}");
        Assert.Equal("Team chat", rest.GetProperty("description").GetString());
        Assert.True(rest.TryGetProperty("createdAt", out _));
        messages.Verify(m => m.PublishAsync(It.Is<ChannelCreatedEventDto>(e =>
            e.ChannelId == id && e.Name == "Engineering"), It.IsAny<CancellationToken>()), Times.Once);

        var query = await GraphQLAsync(client,
            "query($id: UUID!, $missing: UUID!) { channels { id name } selected: channel(id: $id) { name } missing: channel(id: $missing) { id } }",
            new { id, missing = Guid.NewGuid() });
        Assert.False(query.TryGetProperty("errors", out _), query.ToString());
        var data = query.GetProperty("data");
        Assert.Equal("Engineering", data.GetProperty("selected").GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("missing").ValueKind);
        Assert.Single(data.GetProperty("channels").EnumerateArray());
    }

    [Fact]
    public async Task RestCreatedChannel_IsVisibleThroughGraphQL()
    {
        await using var app = await StartAsync();
        using var client = Client(app, "channel.read channel.write");
        var response = await client.PostAsJsonAsync("/channels", new { name = "From REST" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var query = await GraphQLAsync(client, "{ channels { name } }");
        Assert.Equal("From REST", query.GetProperty("data").GetProperty("channels")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task InvalidName_UsesSameValidationAsRestWithoutPublishingOrSaving()
    {
        var messages = new Mock<IMessageClient>();
        await using var app = await StartAsync(messages);
        using var client = Client(app, "channel.write");
        var graph = await GraphQLAsync(client, CreateMutation, new { input = new { name = "   " } });
        var error = graph.GetProperty("errors")[0];
        Assert.Equal("CHANNEL_VALIDATION", error.GetProperty("extensions").GetProperty("code").GetString());
        var rest = await client.PostAsJsonAsync("/channels", new { name = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, rest.StatusCode);
        var restError = await rest.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(restError.GetProperty("error").GetString(), error.GetProperty("message").GetString());
        Assert.Empty(await app.Services.GetRequiredService<IChannelRepository>().ListAsync());
        messages.Verify(m => m.PublishAsync(It.IsAny<ChannelCreatedEventDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("channel.read", true, false)]
    [InlineData("channel.write", false, true)]
    [InlineData("channel.read channel.write", true, true)]
    [InlineData("unrelated", false, false)]
    [InlineData("channel.reader", false, false)]
    public async Task FieldPolicies_EnforceExactReadAndWriteScopes(string scope, bool canRead, bool canWrite)
    {
        var messages = new Mock<IMessageClient>();
        await using var app = await StartAsync(messages);
        using var client = Client(app, scope);
        var query = await GraphQLAsync(client, "{ channels { id } }");
        Assert.Equal(!canRead, query.TryGetProperty("errors", out _));
        var mutation = await GraphQLAsync(client, CreateMutation, new { input = new { name = "Permissions" } });
        Assert.Equal(!canWrite, mutation.TryGetProperty("errors", out _));
        if (!canWrite)
        {
            Assert.Equal("AUTH_NOT_AUTHORIZED", mutation.GetProperty("errors")[0].GetProperty("extensions").GetProperty("code").GetString());
            Assert.Empty(await app.Services.GetRequiredService<IChannelRepository>().ListAsync());
            messages.Verify(m => m.PublishAsync(It.IsAny<ChannelCreatedEventDto>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid-token")]
    [InlineData("wrong-audience")]
    public async Task Endpoint_RejectsMissingOrInvalidCredentials(string? credential)
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        if (credential is not null)
        {
            var token = credential == "wrong-audience" ? Token("channel.read", "another-service") : credential;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        var response = await client.PostAsJsonAsync("/graphql", new { query = "{ channels { id } }" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InvalidId_ReturnsValidationErrorAlongsideSuccessfulSiblingField()
    {
        await using var app = await StartAsync();
        using var client = Client(app, "channel.read");
        var result = await GraphQLAsync(client,
            "query($id: UUID!) { channels { id } channel(id: $id) { id } }", new { id = Guid.Empty });
        Assert.Equal("CHANNEL_VALIDATION", result.GetProperty("errors")[0].GetProperty("extensions").GetProperty("code").GetString());
        Assert.Equal("channel", result.GetProperty("errors")[0].GetProperty("path")[0].GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("data").GetProperty("channel").ValueKind);
        Assert.Empty(result.GetProperty("data").GetProperty("channels").EnumerateArray());
    }

    [Theory]
    [InlineData("timeout", "PUBLISH_TIMEOUT")]
    [InlineData("capacity", "PUBLISH_CAPACITY_EXCEEDED")]
    [InlineData("unexpected", "")]
    public async Task PublicationFailure_DoesNotHideExistingWriteOrLeakExceptionDetails(string failure, string code)
    {
        Exception exception = failure switch
        {
            "timeout" => new PublishTimeoutException(new OperationCanceledException("private-details")),
            "capacity" => new MessagingCapacityExceededException(),
            _ => new InvalidOperationException("private-details")
        };
        var messages = new Mock<IMessageClient>();
        messages.Setup(m => m.PublishAsync(It.IsAny<ChannelCreatedEventDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);
        await using var app = await StartAsync(messages);
        using var client = Client(app, "channel.write");
        var result = await GraphQLAsync(client, CreateMutation, new { input = new { name = "Stored before publish" } });
        var error = result.GetProperty("errors")[0];
        if (code.Length > 0)
        {
            Assert.Equal(code, error.GetProperty("extensions").GetProperty("code").GetString());
            Assert.Contains("may already be changed", error.GetProperty("message").GetString());
        }
        Assert.DoesNotContain("private-details", result.ToString());
        Assert.DoesNotContain("stackTrace", result.ToString());
        Assert.Single(await app.Services.GetRequiredService<IChannelRepository>().ListAsync());
    }

    [Fact]
    public async Task Schema_DoesNotExposeDomainInternals()
    {
        await using var app = await StartAsync();
        using var client = Client(app, "channel.read");
        var result = await GraphQLAsync(client, "{ channels { version members attachments } }");
        Assert.True(result.TryGetProperty("errors", out _));
        Assert.False(result.TryGetProperty("data", out _));
    }

    private static async Task<WebApplication> StartAsync(Mock<IMessageClient>? messages = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Issuer"] = "bizcord-local-gateway",
            ["Authentication:Audience"] = "channel-service",
            ["Authentication:SigningKey"] = SigningKey
        });
        builder.Services.AddServiceAuthentication(builder.Configuration);
        builder.Services.AddChannelApplication();
        builder.Services.AddSingleton<IChannelRepository, InMemoryChannelRepository>();
        builder.Services.AddSingleton((messages ?? new Mock<IMessageClient>()).Object);
        builder.Services.AddControllers().AddApplicationPart(typeof(ChannelsController).Assembly);
        builder.Services.AddChannelGraphQL();
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapGraphQL("/graphql").RequireAuthorization();
        await app.StartAsync();
        return app;
    }

    private static HttpClient Client(WebApplication app, string scope)
    {
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(scope));
        return client;
    }

    private static string Token(string scope, string audience = "channel-service") =>
        new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: "bizcord-local-gateway",
            audience: audience,
            claims: [new Claim("sub", "student"), new Claim("scope", scope)],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256)));

    private static async Task<JsonElement> GraphQLAsync(HttpClient client, string query, object? variables = null)
    {
        var response = await client.PostAsJsonAsync("/graphql", new { query, variables });
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
