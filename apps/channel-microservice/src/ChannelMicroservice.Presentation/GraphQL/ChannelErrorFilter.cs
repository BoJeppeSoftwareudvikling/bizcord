using ChannelService.Application.Channels;
using HotChocolate.Execution;
using MessageClient;

namespace ChannelService.GraphQL;

public sealed class ChannelErrorFilter : IErrorFilter
{
    public IError OnError(IError error) => error.Exception switch
    {
        ChannelValidationException exception => error
            .WithMessage(exception.Message)
            .WithCode("CHANNEL_VALIDATION"),
        ChannelConcurrencyException => error
            .WithMessage("Reload the channel before deciding whether to submit the change again.")
            .WithCode("CHANNEL_CONFLICT"),
        PublishTimeoutException => error
            .WithMessage("The channel may already be changed and event delivery is uncertain. Do not blindly repeat the write.")
            .WithCode("PUBLISH_TIMEOUT"),
        MessagingCapacityExceededException => error
            .WithMessage("The event was not sent by this attempt, but the channel may already be changed. Check its state before retrying.")
            .WithCode("PUBLISH_CAPACITY_EXCEEDED"),
        _ => error
    };
}
