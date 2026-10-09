using System.Net;
using SanchesTV.Core.Health;
using SanchesTV.Core.Models;
using Xunit;

namespace SanchesTV.Tests;

public sealed class StreamHealthCheckerTests
{
    [Fact]
    public async Task Sends_Source_Headers_Required_By_Providers()
    {
        string? agent = null;
        string? referer = null;
        using var client = new HttpClient(new Handler(request =>
        {
            agent = request.Headers.GetValues("User-Agent").Single();
            referer = request.Headers.GetValues("Referer").Single();
            return new HttpResponseMessage(HttpStatusCode.PartialContent);
        }));
        var source = Source("https://example.org/live") with
        {
            UserAgent = "SanchesTV/8",
            Referrer = "https://example.org/"
        };
        var result = await new StreamHealthChecker(client).CheckAsync(source);
        Assert.Equal(StreamStatus.Online, result.Status);
        Assert.Equal("SanchesTV/8", agent);
        Assert.Equal("https://example.org/", referer);
    }

    [Fact]
    public async Task Non_Http_Source_Is_Unchanged()
    {
        using var client = new HttpClient(new Handler(_ => throw new Exception("Unexpected request")));
        var source = Source("rtsp://example.org/live");
        Assert.Equal(source, await new StreamHealthChecker(client).CheckAsync(source));
    }

    [Fact]
    public async Task User_Cancellation_Propagates_Instead_Of_Penalizing_Source()
    {
        using var client = new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new StreamHealthChecker(client).CheckAsync(Source("https://example.org/live"), cts.Token));
    }

    private static ChannelSource Source(string url) =>
        new(Guid.NewGuid(), "Test", new Uri(url), 0);

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(response(request));
        }
    }
}
