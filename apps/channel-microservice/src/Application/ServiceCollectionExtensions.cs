using ChannelService.Application.Channels;
using Microsoft.Extensions.DependencyInjection;

namespace ChannelService.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddChannelApplication(this IServiceCollection services)
    {
        services.AddScoped<ChannelManagementService>();

        return services;
    }
}
