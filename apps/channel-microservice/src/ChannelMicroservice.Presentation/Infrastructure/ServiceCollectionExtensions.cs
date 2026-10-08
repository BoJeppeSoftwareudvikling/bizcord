using ChannelService.Application.Abstractions;
using ChannelService.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace ChannelService.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddChannelInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IChannelRepository, InMemoryChannelRepository>();
        services.AddScoped<MessagePostedHandler>();
        services.AddHostedService<MessagePostedSubscription>();

        return services;
    }
}
