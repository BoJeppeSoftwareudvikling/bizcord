using ChannelService.Application.Channels;
using MessageClient;
using Microsoft.AspNetCore.Http;
using Moq;

namespace ChannelService.Tests.Presentation;

public sealed class ReliabilityExceptionHandlerTests
{
    [Theory]
    [InlineData(409)]
    [InlineData(503)]
    [InlineData(504)]
    public async Task KnownFailure_ReturnsProblemDetailsWithExpectedStatus(int status)
    {
        Exception exception = status switch
        {
            409 => new ChannelConcurrencyException(Guid.NewGuid()),
            503 => new MessagingCapacityExceededException(),
            _ => new PublishTimeoutException(new OperationCanceledException())
        };
        var context = new DefaultHttpContext();
        var writer = new Mock<IProblemDetailsService>();
        writer.Setup(service => service.TryWriteAsync(It.IsAny<ProblemDetailsContext>()))
            .ReturnsAsync(true);
        var handler = new ReliabilityExceptionHandler(writer.Object);

        Assert.True(await handler.TryHandleAsync(context, exception, CancellationToken.None));
        Assert.Equal(status, context.Response.StatusCode);
        writer.Verify(service => service.TryWriteAsync(It.Is<ProblemDetailsContext>(details =>
            details.ProblemDetails.Status == status && details.ProblemDetails.Detail != null)), Times.Once);
    }

    [Fact]
    public async Task UnknownFailure_IsNotReportedAsSuccess()
    {
        var handler = new ReliabilityExceptionHandler(Mock.Of<IProblemDetailsService>());
        Assert.False(await handler.TryHandleAsync(new DefaultHttpContext(),
            new InvalidOperationException(), CancellationToken.None));
    }
}
