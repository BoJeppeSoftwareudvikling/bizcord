using ChannelService.Application.Channels;
using MessageClient;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ChannelService;

public sealed class ReliabilityExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception,
        CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            ChannelConcurrencyException => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Concurrent channel change",
                Detail = "Reload the channel before deciding whether to submit the change again."
            },
            PublishTimeoutException => new ProblemDetails
            {
                Status = StatusCodes.Status504GatewayTimeout,
                Title = "Event publication timed out",
                Detail = "The channel may already be changed and event delivery is uncertain. Do not blindly repeat the write."
            },
            MessagingCapacityExceededException => new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Event publishing capacity exhausted",
                Detail = "The event was not sent by this attempt, but the channel may already be changed. Check its state before retrying."
            },
            _ => null
        };

        if (problem is null) return false;
        context.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem
        });
    }
}
