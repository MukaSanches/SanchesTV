using System.Diagnostics;
using System.Diagnostics.Metrics;
using Serilog;

namespace SanchesTV.Desktop.Infrastructure;

public static class AppTelemetry
{
    public static readonly ActivitySource ActivitySource = new("SanchesTV");
    public static readonly Meter Meter = new("SanchesTV", "7.0.0");

    private static readonly Counter<long> PlaybackStarts = Meter.CreateCounter<long>("sanchestv.playback.starts");
    private static readonly Counter<long> PlaybackFailures = Meter.CreateCounter<long>("sanchestv.playback.failures");
    private static readonly Counter<long> P2pRecoveries = Meter.CreateCounter<long>("sanchestv.p2p.recoveries");
    private static readonly Counter<long> ScheduledRecordings = Meter.CreateCounter<long>("sanchestv.recordings.scheduled");
    private static readonly Histogram<double> StartupMs = Meter.CreateHistogram<double>("sanchestv.startup.ms", "ms");
    private static readonly Histogram<double> StreamOpenMs = Meter.CreateHistogram<double>("sanchestv.stream.open.ms", "ms");

    public static string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SanchesTV", "Logs");

    public static void Initialize()
    {
        Directory.CreateDirectory(LogDirectory);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.WithProperty("Application", "SanchesTV")
            .WriteTo.File(
                Path.Combine(LogDirectory, "sanchestv-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true)
            .CreateLogger();

        Log.Information("SanchesTV telemetry initialized. OS={OS} Runtime={Runtime}",
            Environment.OSVersion.VersionString,
            System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
    }

    public static Activity? StartActivity(string name) => ActivitySource.StartActivity(name);

    public static void TrackStartup(TimeSpan duration)
    {
        StartupMs.Record(duration.TotalMilliseconds);
        Log.Information("Startup completed in {ElapsedMs} ms", duration.TotalMilliseconds);
    }

    public static void TrackPlaybackStart(string engine, string provider, TimeSpan duration)
    {
        PlaybackStarts.Add(1, new KeyValuePair<string, object?>("engine", engine));
        StreamOpenMs.Record(duration.TotalMilliseconds,
            new KeyValuePair<string, object?>("engine", engine));
        Log.Information("Playback started. Engine={Engine} Provider={Provider} ElapsedMs={ElapsedMs}",
            engine, provider, duration.TotalMilliseconds);
    }

    public static void TrackPlaybackFailure(string engine, string provider, Exception exception)
    {
        PlaybackFailures.Add(1, new KeyValuePair<string, object?>("engine", engine));
        Log.Warning(exception, "Playback failed. Engine={Engine} Provider={Provider}", engine, provider);
    }

    public static void TrackP2pRecovery(string reason)
    {
        P2pRecoveries.Add(1);
        Log.Information("P2P recovery: {Reason}", reason);
    }

    public static void TrackScheduledRecording(string title)
    {
        ScheduledRecordings.Add(1);
        Log.Information("Recording scheduled: {Title}", title);
    }

    public static void Error(Exception exception, string area) =>
        Log.Error(exception, "Unhandled error in {Area}", area);

    public static void Shutdown()
    {
        Log.Information("SanchesTV shutting down");
        Log.CloseAndFlush();
        Meter.Dispose();
        ActivitySource.Dispose();
    }
}
