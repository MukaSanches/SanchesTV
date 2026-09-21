using System.Net;
using System.Net.Http;
using Polly;
using Polly.Retry;
using SanchesTV.Desktop.Diagnostics;

namespace SanchesTV.Desktop.Infrastructure;

public sealed class ResilientHttpHandler : DelegatingHandler
{
    private static readonly ResiliencePipeline<HttpResponseMessage> Pipeline =
        new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
                    .HandleResult(response =>
                        response.StatusCode == HttpStatusCode.RequestTimeout ||
                        (int)response.StatusCode == 429 ||
                        (int)response.StatusCode >= 500),
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(450),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = args =>
                {
                    if (args.Outcome.Result is { } response)
                        response.Dispose();
                    AppTelemetry.Info("http.retry", new
                    {
                        attempt = args.AttemptNumber + 1,
                        reason = args.Outcome.Exception?.GetType().Name
                            ?? args.Outcome.Result?.StatusCode.ToString()
                            ?? "unknown"
                    });
                    return default;
                }
            })
            .AddTimeout(TimeSpan.FromSeconds(30))
            .Build();

    public ResilientHttpHandler()
        : base(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 16,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            UseCookies = false
        })
    {
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var template = await RequestTemplate.CreateAsync(request, cancellationToken);

        return await Pipeline.ExecuteAsync(
            async token =>
            {
                using var clone = template.Create();
                return await base.SendAsync(clone, token);
            },
            cancellationToken);
    }

    private sealed record RequestTemplate(
        HttpMethod Method,
        Uri? Uri,
        Version Version,
        HttpVersionPolicy VersionPolicy,
        IReadOnlyList<KeyValuePair<string, IEnumerable<string>>> Headers,
        byte[]? Content,
        IReadOnlyList<KeyValuePair<string, IEnumerable<string>>> ContentHeaders)
    {
        public static async Task<RequestTemplate> CreateAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            byte[]? content = null;
            var contentHeaders = Array.Empty<KeyValuePair<string, IEnumerable<string>>>();

            if (request.Content is not null)
            {
                content = await request.Content.ReadAsByteArrayAsync(cancellationToken);
                contentHeaders = request.Content.Headers
                    .Select(x => new KeyValuePair<string, IEnumerable<string>>(x.Key, x.Value.ToArray()))
                    .ToArray();
            }

            return new RequestTemplate(
                request.Method,
                request.RequestUri,
                request.Version,
                request.VersionPolicy,
                request.Headers
                    .Select(x => new KeyValuePair<string, IEnumerable<string>>(x.Key, x.Value.ToArray()))
                    .ToArray(),
                content,
                contentHeaders);
        }

        public HttpRequestMessage Create()
        {
            var request = new HttpRequestMessage(Method, Uri)
            {
                Version = Version,
                VersionPolicy = VersionPolicy
            };

            foreach (var header in Headers)
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);

            if (Content is not null)
            {
                request.Content = new ByteArrayContent(Content);
                foreach (var header in ContentHeaders)
                    request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return request;
        }
    }
}
