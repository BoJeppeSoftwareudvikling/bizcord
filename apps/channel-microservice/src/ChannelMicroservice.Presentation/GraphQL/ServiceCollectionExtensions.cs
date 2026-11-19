namespace ChannelService.GraphQL;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddChannelGraphQL(this IServiceCollection services)
    {
        services.AddGraphQLServer()
            .AddAuthorization()
            .AddQueryType<ChannelQuery>()
            .AddMutationType<ChannelMutation>()
            .AddType<ChannelType>()
            .AddErrorFilter<ChannelErrorFilter>()
            .ModifyRequestOptions(options => options.IncludeExceptionDetails = false);

        return services;
    }
}
