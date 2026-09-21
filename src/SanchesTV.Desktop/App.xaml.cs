using System.IO;
using System.Windows;
using LibVLCSharp.Shared;
using SanchesTV.Core.Catalog;
using SanchesTV.Core.Parsing;
using SanchesTV.Core.Storage;
using SanchesTV.Desktop.Playback;
using SanchesTV.Desktop.Audio;
using SanchesTV.Desktop.P2P;
using MonoTorrent.Client;
using SanchesTV.Core.P2P;
using SanchesTV.Desktop.Diagnostics;

namespace SanchesTV.Desktop;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppTelemetry.Initialize();
        DispatcherUnhandledException += (_, args) =>
        {
            AppTelemetry.Error("app.unhandled", args.Exception);
            MessageBox.Show(
                "O SanchesTV encontrou um erro inesperado e registrou um diagnóstico local.\n\n" + args.Exception.Message,
                "SanchesTV 7",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        if (e.Args.Any(a => string.Equals(a, "--self-test", StringComparison.OrdinalIgnoreCase)))
        {
            var exit = await SelfTest.RunAsync();
            Shutdown(exit);
            return;
        }

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppTelemetry.Shutdown();
        base.OnExit(e);
    }
}

internal static class SelfTest
{
    public static async Task<int> RunAsync()
    {
        try
        {
            var temp = Path.Combine(Path.GetTempPath(), $"sanchestv-selftest-{Guid.NewGuid():N}.db");
            var db = new AppDatabase(temp);
            await db.InitializeAsync();

            const string m3u = "#EXTM3U\n#EXTINF:-1 tvg-id=\"test\" group-title=\"Teste\",Canal Teste\nhttps://example.org/live.m3u8";
            var channels = M3uParser.Parse(m3u);
            if (channels.Count != 1)
                return 11;

            await db.UpsertChannelsAsync(channels.Concat(BuiltInCatalog.Create()));
            if ((await db.GetChannelsAsync()).Count < 2)
                return 12;

            const string xml = "<tv><programme start=\"20260920200000 -0300\" stop=\"20260920210000 -0300\" channel=\"test\"><title>Teste</title></programme></tv>";
            var epg = XmlTvParser.Parse(xml);
            if (epg.Count != 1)
                return 13;

            LibVLCSharp.Shared.Core.Initialize();
            using var lib = new LibVLC("--no-video-title-show", "--quiet");
            using var player = new MediaPlayer(lib);
            _ = player.Volume;

            var mpvVersion = MpvNative.SelfTest();
            if (string.IsNullOrWhiteSpace(mpvVersion))
                return 21;

            using var recorder = new FfmpegRecorder();
            var ffmpegVersion = await recorder.GetVersionAsync();
            if (!ffmpegVersion.Contains("ffmpeg", StringComparison.OrdinalIgnoreCase))
                return 22;

            var audio = new WindowsAudioService();
            if (string.IsNullOrWhiteSpace(audio.NAudioVersion))
                return 23;

            var monoTorrentVersion = typeof(ClientEngine).Assembly.GetName().Version?.ToString();
            if (string.IsNullOrWhiteSpace(monoTorrentVersion) ||
                !P2pMediaPolicy.IsLikelyVideo("selftest.mkv"))
                return 24;

            var p2pDefaults = new P2pSettings();
            if (!p2pDefaults.AutoRecovery ||
                p2pDefaults.InitialBufferMb < 8 ||
                p2pDefaults.StallRecoverySeconds < 6 ||
                p2pDefaults.MetadataTimeoutSeconds < 15)
                return 25;

            try { File.Delete(temp); } catch { }
            return 0;
        }
        catch
        {
            return 99;
        }
    }
}
