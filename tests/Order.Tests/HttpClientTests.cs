using System.Net;
using Order.Application.Exceptions;
using Order.Infrastructure.Http;

namespace Order.Tests;

public sealed class HttpClientTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealHttpTimeoutBecomesUpstreamTimeout(bool product)
    {
        using var http = new HttpClient(new WaitingHandler())
        {
            BaseAddress = new Uri("http://upstream.test/"), Timeout = TimeSpan.FromMilliseconds(50)
        };
        var exception = await Assert.ThrowsAsync<UpstreamServiceException>(async () =>
        {
            if (product) await new ProductClient(http).GetByIdAsync(Guid.NewGuid(), default);
            else await new CustomerClient(http).GetByIdAsync(Guid.NewGuid(), default);
        });
        Assert.True(exception.IsTimeout);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallerCancellationPropagatesInsteadOfBecomingGatewayTimeout(bool product)
    {
        using var http = new HttpClient(new WaitingHandler()) { BaseAddress = new Uri("http://upstream.test/") };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            if (product) await new ProductClient(http).GetByIdAsync(Guid.NewGuid(), cancellation.Token);
            else await new CustomerClient(http).GetByIdAsync(Guid.NewGuid(), cancellation.Token);
        });
    }

    private sealed class WaitingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
