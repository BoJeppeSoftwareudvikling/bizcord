using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace ChannelService.Authentication;

public static class ServiceAuthorizationPolicies
{
    public const string ChannelRead = "ChannelRead";
    public const string ChannelWrite = "ChannelWrite";
    public const string InternalService = "InternalService";
}

public sealed class ServiceAuthenticationOptions
{
    public const string SectionName = "Authentication";

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public string SigningKey { get; init; } = string.Empty;
}

public static class ServiceAuthenticationExtensions
{
    public static IServiceCollection AddServiceAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var settings = configuration
            .GetRequiredSection(ServiceAuthenticationOptions.SectionName)
            .Get<ServiceAuthenticationOptions>()
            ?? throw new InvalidOperationException(
                $"Configuration section '{ServiceAuthenticationOptions.SectionName}' is required.");

        Validate(settings);

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = settings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = settings.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(settings.SigningKey)),
                    NameClaimType = "sub",
                    RoleClaimType = "role",
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(ServiceAuthorizationPolicies.ChannelRead, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context => HasScope(context.User, "channel.read")));
            options.AddPolicy(ServiceAuthorizationPolicies.ChannelWrite, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context => HasScope(context.User, "channel.write")));
            options.AddPolicy(
                ServiceAuthorizationPolicies.InternalService,
                policy => policy
                    .RequireAuthenticatedUser()
                    .RequireClaim("role", "internal-service"));
        });

        return services;
    }

    private static bool HasScope(System.Security.Claims.ClaimsPrincipal user, string requiredScope) =>
        user.FindAll("scope")
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(requiredScope, StringComparer.Ordinal);

    private static void Validate(ServiceAuthenticationOptions settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Issuer))
        {
            throw new InvalidOperationException("Authentication:Issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(settings.Audience))
        {
            throw new InvalidOperationException("Authentication:Audience is required.");
        }

        if (settings.SigningKey.Length < 32)
        {
            throw new InvalidOperationException(
                "Authentication:SigningKey must contain at least 32 characters.");
        }
    }
}
