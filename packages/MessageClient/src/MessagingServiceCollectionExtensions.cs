using EasyNetQ;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MessageClient;

// Samler al messaging-registrering i ét kald fra Program.cs.
public static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddMessageClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(MessagingOptions.SectionName);
        var connectionString = section[nameof(MessagingOptions.ConnectionString)];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Configuration value '{MessagingOptions.SectionName}:{nameof(MessagingOptions.ConnectionString)}' is required.");
        }

        services.AddOptions<MessagingOptions>()
            .Bind(section)
            .Validate(options => options.PublishTimeout > TimeSpan.Zero &&
                options.PublishTimeout <= TimeSpan.FromMinutes(5),
                "Messaging:PublishTimeout must be positive and at most five minutes.")
            .Validate(options => options.MaxConcurrentPublishes is >= 1 and <= 1024,
                "Messaging:MaxConcurrentPublishes must be between 1 and 1024.")
            .ValidateOnStart();

        // Singleton sikrer, at hele applikationen genbruger samme bus/forbindelse.
        services.AddSingleton<IBus>(serviceProvider =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<MessagingOptions>>()
                .Value;

            return RabbitHutch.CreateBus(
                options.ConnectionString,
                serviceRegister => serviceRegister.EnableSystemTextJson());
        });

        // Applikationskode efterspørger interfacet og får EasyNetQ-adapteren.
        services.AddSingleton<EasyNetQMessageClient>();
        services.AddSingleton<IMessageClient>(provider => new BoundedMessageClient(
            provider.GetRequiredService<EasyNetQMessageClient>(),
            provider.GetRequiredService<IOptions<MessagingOptions>>()));

        return services;
    }
}
