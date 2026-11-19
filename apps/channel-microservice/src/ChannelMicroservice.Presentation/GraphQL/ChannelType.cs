using ChannelService.Dtos.Channels;

namespace ChannelService.GraphQL;

public sealed class ChannelType : ObjectType<ChannelDto>
{
    protected override void Configure(IObjectTypeDescriptor<ChannelDto> descriptor)
    {
        descriptor.Name("Channel");
        descriptor.BindFieldsExplicitly();
        descriptor.Field(channel => channel.Id);
        descriptor.Field(channel => channel.Name);
        descriptor.Field(channel => channel.Description);
        descriptor.Field(channel => channel.CreatedAt);
        descriptor.Field(channel => channel.UpdatedAt);
    }
}
