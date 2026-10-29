using MessageClient;
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
                descriptor.ImplementationFactory is not null &&
                descriptor.Lifetime == ServiceLifetime.Singleton);

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<MessagingOptions>>();
        Assert.Equal(connectionString, options.Value.ConnectionString);
        Assert.Equal(TimeSpan.FromSeconds(5), options.Value.PublishTimeout);
        Assert.Equal(16, options.Value.MaxConcurrentPublishes);
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
    [Theory]
    [InlineData("Messaging:PublishTimeout", "00:00:00")]
    [InlineData("Messaging:PublishTimeout", "00:06:00")]
    [InlineData("Messaging:MaxConcurrentPublishes", "0")]
    [InlineData("Messaging:MaxConcurrentPublishes", "1025")]
    public void AddMessageClient_RejectsInvalidLimits(string key, string value)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Messaging:ConnectionString"] = "host=localhost",
                [key] = value
            }).Build();
        using var provider = new ServiceCollection().AddMessageClient(configuration).BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<MessagingOptions>>().Value);
    }
}
