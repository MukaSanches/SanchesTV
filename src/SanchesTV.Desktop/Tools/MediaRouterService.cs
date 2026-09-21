using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using SanchesTV.Core.Models;

namespace SanchesTV.Desktop.Tools;

public sealed record MediaRouterSession(
    string PathName,
    Uri RtspUrl,
    Uri HlsUrl,
    Uri WebRtcUrl,
    Uri RtmpUrl,
    Uri SrtUrl,
    bool LanEnabled);

public sealed class MediaRouterService : IAsyncDisposable
{
    private readonly MediaToolsService _tools;
    private Process? _server;
    private Process? _publisher;
    private string? _workDir;

    public bool IsRunning => _server is { HasExited: false } && _publisher is { HasExited: false };
    public MediaRouterSession? Current { get; private set; }

    public MediaRouterService(MediaToolsService tools) => _tools = tools;

    public async Task<MediaRouterSession> StartAsync(
        ChannelSource source,
        bool enableLan,
        CancellationToken cancellationToken = default)
    {
        await StopAsync();

        if (!File.Exists(_tools.MediaMtxPath))
            throw new FileNotFoundException("MediaMTX não está instalado no runtime.", _tools.MediaMtxPath);
        if (!File.Exists(_tools.FfmpegPath))
            throw new FileNotFoundException("FFmpeg não está instalado no runtime.", _tools.FfmpegPath);

        var token = Guid.NewGuid().ToString("N")[..12];
        var pathName = "sanchestv-" + token;
        _workDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SanchesTV", "Router");
        Directory.CreateDirectory(_workDir);

        var bind = enableLan ? "" : "127.0.0.1";
        var configPath = Path.Combine(_workDir, "mediamtx-v7.yml");
        var config = $"""
logLevel: warn
logDestinations: [stdout]
api: false
metrics: false
pprof: false
playback: false

authMethod: internal
authInternalUsers:
  - user: any
    pass:
    ips: ["127.0.0.1", "::1"]
    permissions:
      - action: publish
        path:
      - action: read
        path:
      - action: playback
        path:
  - user: any
    pass:
    ips: []
    permissions:
      - action: read
        path:
      - action: playback
        path:

rtsp: true
rtspTransports: [tcp]
rtspAddress: {bind}:8554
hls: true
hlsAddress: {bind}:8888
hlsAllowOrigins: ["*"]
webrtc: true
webrtcAddress: {bind}:8889
webrtcAllowOrigins: ["*"]
rtmp: true
rtmpAddress: {bind}:1935
srt: true
srtAddress: {bind}:8890

paths:
  all_others:
""";
        await File.WriteAllTextAsync(configPath, config, cancellationToken);

        _server = Process.Start(new ProcessStartInfo
        {
            FileName = _tools.MediaMtxPath,
            WorkingDirectory = _workDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            ArgumentList = { configPath }
        }) ?? throw new InvalidOperationException("Não foi possível iniciar MediaMTX.");

        await WaitForPortAsync(8554, TimeSpan.FromSeconds(12), cancellationToken);

        _publisher = StartPublisher(source, pathName, copyCodecs: true);
        await Task.Delay(2500, cancellationToken);

        if (_publisher.HasExited)
        {
            _publisher.Dispose();
            var encoder = await PickH264EncoderAsync(cancellationToken);
            _publisher = StartPublisher(source, pathName, copyCodecs: false, encoder);
            await Task.Delay(3000, cancellationToken);
            if (_publisher.HasExited)
                throw new InvalidOperationException(
                    "FFmpeg não conseguiu publicar a fonte no roteador, nem por remux nem por transcodificação.");
        }

        var host = enableLan ? GetLanAddress() : "127.0.0.1";
        Current = new MediaRouterSession(
            pathName,
            new Uri($"rtsp://{host}:8554/{pathName}"),
            new Uri($"http://{host}:8888/{pathName}/index.m3u8"),
            new Uri($"http://{host}:8889/{pathName}"),
            new Uri($"rtmp://{host}:1935/{pathName}"),
            new Uri($"srt://{host}:8890?streamid=read:{pathName}"),
            enableLan);
        return Current;
    }

    private Process StartPublisher(
        ChannelSource source,
        string pathName,
        bool copyCodecs,
        string? h264Encoder = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _tools.FfmpegPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        psi.ArgumentList.Add("-hide_banner");
        psi.ArgumentList.Add("-loglevel");
        psi.ArgumentList.Add("warning");

        if (!string.IsNullOrWhiteSpace(source.UserAgent))
        {
            psi.ArgumentList.Add("-user_agent");
            psi.ArgumentList.Add(source.UserAgent);
        }

        var headers = new List<string>();
        if (!string.IsNullOrWhiteSpace(source.Referrer))
            headers.Add($"Referer: {source.Referrer}");
        if (!string.IsNullOrWhiteSpace(source.Origin))
            headers.Add($"Origin: {source.Origin}");
        if (headers.Count > 0)
        {
            psi.ArgumentList.Add("-headers");
            psi.ArgumentList.Add(string.Join("\r\n", headers) + "\r\n");
        }

        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(source.Url.ToString());
        psi.ArgumentList.Add("-map");
        psi.ArgumentList.Add("0:v:0?");
        psi.ArgumentList.Add("-map");
        psi.ArgumentList.Add("0:a:0?");

        if (copyCodecs)
        {
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add("copy");
        }
        else
        {
            psi.ArgumentList.Add("-c:v");
            psi.ArgumentList.Add(h264Encoder ?? "h264_mf");
            if (string.Equals(h264Encoder, "libx264", StringComparison.OrdinalIgnoreCase))
            {
                psi.ArgumentList.Add("-preset");
                psi.ArgumentList.Add("veryfast");
            }
            psi.ArgumentList.Add("-b:v");
            psi.ArgumentList.Add("5500k");
            psi.ArgumentList.Add("-c:a");
            psi.ArgumentList.Add("aac");
            psi.ArgumentList.Add("-b:a");
            psi.ArgumentList.Add("160k");
            psi.ArgumentList.Add("-ar");
            psi.ArgumentList.Add("48000");
        }

        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add("rtsp");
        psi.ArgumentList.Add("-rtsp_transport");
        psi.ArgumentList.Add("tcp");
        psi.ArgumentList.Add($"rtsp://127.0.0.1:8554/{pathName}");

        return Process.Start(psi)
            ?? throw new InvalidOperationException("Não foi possível iniciar o publicador FFmpeg.");
    }

    private async Task<string> PickH264EncoderAsync(CancellationToken cancellationToken)
    {
        var text = await MediaToolsService.RunAsync(
            _tools.FfmpegPath,
            ["-hide_banner", "-encoders"],
            TimeSpan.FromSeconds(15),
            cancellationToken);

        foreach (var candidate in new[] { "h264_nvenc", "h264_qsv", "h264_amf", "h264_mf", "libx264" })
            if (text.Contains(candidate, StringComparison.OrdinalIgnoreCase))
                return candidate;

        throw new InvalidOperationException("Nenhum encoder H.264 compatível foi encontrado no FFmpeg.");
    }

    public async Task StopAsync()
    {
        await StopProcessAsync(_publisher);
        _publisher?.Dispose();
        _publisher = null;

        await StopProcessAsync(_server);
        _server?.Dispose();
        _server = null;

        Current = null;
    }

    private static async Task StopProcessAsync(Process? process)
    {
        if (process is null)
            return;
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
        catch
        {
        }
    }

    private static async Task WaitForPortAsync(
        int port,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var until = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < until)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var client = new TcpClient();
            try
            {
                await client.ConnectAsync(IPAddress.Loopback, port, cancellationToken);
                return;
            }
            catch
            {
                await Task.Delay(250, cancellationToken);
            }
        }
        throw new TimeoutException("MediaMTX não abriu a porta RTSP dentro do prazo.");
    }

    private static string GetLanAddress()
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up ||
                nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            foreach (var uni in nic.GetIPProperties().UnicastAddresses)
            {
                if (uni.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(uni.Address))
                    return uni.Address.ToString();
            }
        }
        return "127.0.0.1";
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
