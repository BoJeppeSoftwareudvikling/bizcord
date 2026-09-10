using EasyNetQ;

namespace WebAPI.Messaging;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRabbitMqMessageClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("RabbitMQ")
            ?? configuration["RabbitMQ:ConnectionString"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "RabbitMQ connection string is missing. Configure ConnectionStrings:RabbitMQ or RabbitMQ:ConnectionString.");
        }

        services.AddEasyNetQ(connectionString).UseSystemTextJson();
        services.AddSingleton<IMessageClient, EasyNetQMessageClient>();

        return services;
    }
}
