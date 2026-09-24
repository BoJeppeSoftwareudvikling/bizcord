using ChannelService.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace ChannelService.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddChannelInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IChannelRepository, InMemoryChannelRepository>();

        return services;
    }
}
