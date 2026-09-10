using EasyNetQ;
using Microsoft.Extensions.Options;

namespace ChannelService.Messaging;

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

        services.Configure<MessagingOptions>(section);

        // Singleton sikrer, at hele applikationen genbruger samme bus/forbindelse.
        services.AddSingleton<IBus>(serviceProvider =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<MessagingOptions>>()
                .Value;

            return RabbitHutch.CreateBus(options.ConnectionString);
        });

        // Applikationskode efterspørger interfacet og får EasyNetQ-adapteren.
        services.AddSingleton<IMessageClient, EasyNetQMessageClient>();

        return services;
    }
}
