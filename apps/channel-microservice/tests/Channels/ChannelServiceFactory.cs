using MessageClient;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ChannelService.Tests.Channels;

public sealed class ChannelServiceFactory : WebApplicationFactory<Program>
{
    public IMessageClient MessageClient => Services.GetRequiredService<IMessageClient>();
}
