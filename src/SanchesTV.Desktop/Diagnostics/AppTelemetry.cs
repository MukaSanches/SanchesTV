using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using SanchesTV.Core.Diagnostics;

namespace SanchesTV.Desktop.Diagnostics;

public static class AppTelemetry
{
    private static readonly ActivitySource Activities = new("SanchesTV");
    private static readonly Meter Meter = new("SanchesTV");
    private static readonly Counter<long> PlaybackStarts = Meter.CreateCounter<long>("sanchestv.playback.starts");
    private static readonly Counter<long> PlaybackFailures = Meter.CreateCounter<long>("sanchestv.playback.failures");
    private static readonly Counter<long> P2pRecoveries = Meter.CreateCounter<long>("sanchestv.p2p.recoveries");

    private static TracerProvider? _traces;
    private static MeterProvider? _metrics;
    private static bool _initialized;

    public static string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SanchesTV", "Logs");

    public static void Initialize()
    {
        if (_initialized)
            return;

        Directory.CreateDirectory(LogDirectory);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                new Serilog.Formatting.Json.JsonFormatter(),
                Path.Combine(LogDirectory, "sanchestv-.jsonl"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true)
            .CreateLogger();

        _traces = Sdk.CreateTracerProviderBuilder()
            .AddSource("SanchesTV")
            .Build();

        _metrics = Sdk.CreateMeterProviderBuilder()
            .AddMeter("SanchesTV")
            .Build();

        _initialized = true;
        Info("app.start", new { version = "8.3.0", os = Environment.OSVersion.VersionString });
    }

    public static IDisposable? StartActivity(string name)
    {
        Initialize();
        return Activities.StartActivity(name, ActivityKind.Internal);
    }

    public static void PlaybackStarted(string engine, string provider, string? host)
    {
        Initialize();
        PlaybackStarts.Add(1,
            new KeyValuePair<string, object?>("engine", engine),
            new KeyValuePair<string, object?>("provider", provider));
        Info("playback.started", new { engine, provider, host });
    }

    public static void PlaybackFailed(string engine, string message)
    {
        Initialize();
        PlaybackFailures.Add(1, new KeyValuePair<string, object?>("engine", engine));
        Info("playback.failed", new { engine, message = Safe(message) });
    }

    public static void P2pRecovery(string reason)
    {
        Initialize();
        P2pRecoveries.Add(1);
        Info("p2p.recovery", new { reason = Safe(reason) });
    }

    public static void Info(string eventName, object? payload = null)
    {
        Initialize();
        Log.Information("{EventName} {@Payload}", Safe(eventName), SafePayload(payload));
    }

    public static void Error(string eventName, Exception exception, object? payload = null)
    {
        Initialize();
        Log.Error("{EventName} {@Error} {@Payload}",
            Safe(eventName), DescribeException(exception), SafePayload(payload));
    }

    private static string Safe(string? value) => DiagnosticRedactor.Redact(value);

    // Existing structured metrics remain intact. Arbitrary object payloads must be
    // curated by callers; the text redactor cannot guarantee their confidentiality.
    private static object? SafePayload(object? payload) => payload switch
    {
        string text => Safe(text),
        Uri address => Safe(address.OriginalString),
        Exception exception => DescribeException(exception),
        _ => payload
    };

    private static object DescribeException(Exception exception, int depth = 0) => new
    {
        type = exception.GetType().FullName,
        message = Safe(exception.Message),
        stackTrace = DiagnosticRedactor.Redact(exception.StackTrace, 2000),
        inner = depth < 3 && exception.InnerException is { } inner
            ? DescribeException(inner, depth + 1)
            : null
    };

    public static void Shutdown()
    {
        if (!_initialized)
            return;

        try { Info("app.stop"); } catch { }
        _traces?.Dispose();
        _metrics?.Dispose();
        Log.CloseAndFlush();
        _initialized = false;
    }
}
