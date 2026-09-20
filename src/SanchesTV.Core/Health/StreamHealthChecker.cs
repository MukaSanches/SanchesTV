using System.Diagnostics;
using SanchesTV.Core.Models;

namespace SanchesTV.Core.Health;

public sealed class StreamHealthChecker(HttpClient httpClient)
{
    public async Task<ChannelSource> CheckAsync(ChannelSource source, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, source.Url);
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 2047);
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            sw.Stop();

            if (!response.IsSuccessStatusCode)
                return source with { Status = StreamStatus.Error, Latency = sw.Elapsed, LastError = $"HTTP {(int)response.StatusCode}" };

            var status = sw.Elapsed > TimeSpan.FromSeconds(4) ? StreamStatus.Slow : StreamStatus.Online;
            return source with { Status = status, Latency = sw.Elapsed, LastError = null };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return source with { Status = StreamStatus.Offline, LastError = "Timeout" };
        }
        catch (Exception ex)
        {
            return source with { Status = StreamStatus.Error, LastError = ex.Message };
        }
    }
}
