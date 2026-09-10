using ChannelService.Messaging;
using EasyNetQ;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ChannelService.Tests.Messaging;

public sealed class MessagingServiceCollectionExtensionsTests
{
    [Fact]
    public void AddMessageClient_RegistersSingletonsAndBindsConfiguration()
    {
        const string connectionString = "host=rabbitmq";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{MessagingOptions.SectionName}:{nameof(MessagingOptions.ConnectionString)}"] =
                    connectionString
            })
            .Build();
        var services = new ServiceCollection();

        var result = services.AddMessageClient(configuration);

        Assert.Same(services, result);
        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IBus) &&
                descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IMessageClient) &&
                descriptor.ImplementationType == typeof(EasyNetQMessageClient) &&
                descriptor.Lifetime == ServiceLifetime.Singleton);

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<MessagingOptions>>();
        Assert.Equal(connectionString, options.Value.ConnectionString);
    }

    [Fact]
    public void AddMessageClient_RejectsMissingConnectionString()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddMessageClient(configuration));

        Assert.Contains("Messaging:ConnectionString", exception.Message);
    }
}
