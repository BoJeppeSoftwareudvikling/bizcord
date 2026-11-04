using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ChannelService.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ChannelService.Tests.Presentation;

public sealed class ServiceAuthenticationTests
{
    private const string SigningKey =
        "test-signing-key-with-at-least-thirty-two-characters";

    [Fact]
    public async Task InternalServicePolicy_AcceptsExpectedServiceToken()
    {
        using var provider = CreateServices();
        var principal = ValidateToken(provider, CreateToken("internal-service"));
        var authorization = provider.GetRequiredService<IAuthorizationService>();

        var result = await authorization.AuthorizeAsync(
            principal,
            resource: null,
            ServiceAuthorizationPolicies.InternalService);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task InternalServicePolicy_RejectsTokenWithoutRequiredRole()
    {
        using var provider = CreateServices();
        var principal = ValidateToken(provider, CreateToken("member"));
        var authorization = provider.GetRequiredService<IAuthorizationService>();

        var result = await authorization.AuthorizeAsync(
            principal,
            resource: null,
            ServiceAuthorizationPolicies.InternalService);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void TokenValidation_RejectsWrongAudience()
    {
        using var provider = CreateServices();
        var token = CreateToken("internal-service", audience: "another-service");

        Assert.Throws<SecurityTokenInvalidAudienceException>(
            () => ValidateToken(provider, token));
    }

    private static ServiceProvider CreateServices()
    {
        var values = new Dictionary<string, string?>
        {
            ["Authentication:Issuer"] = "bizcord-local-gateway",
            ["Authentication:Audience"] = "channel-service",
            ["Authentication:SigningKey"] = SigningKey
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServiceAuthentication(configuration);

        return services.BuildServiceProvider();
    }

    private static ClaimsPrincipal ValidateToken(
        ServiceProvider provider,
        string token)
    {
        var options = provider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        var handler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = false
        };

        return handler.ValidateToken(
            token,
            options.TokenValidationParameters,
            out _);
    }

    private static string CreateToken(
        string role,
        string audience = "channel-service")
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "bizcord-local-gateway",
            audience: audience,
            claims:
            [
                new Claim("sub", "message-service"),
                new Claim("role", role)
            ],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
