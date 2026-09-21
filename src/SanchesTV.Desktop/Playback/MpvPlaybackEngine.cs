using System.Globalization;
using System.Runtime.InteropServices;
using SanchesTV.Core.Models;

namespace SanchesTV.Desktop.Playback;

public enum PlaybackQualityProfile
{
    Balanced,
    MaximumQuality,
    LowLatency,
    LowPower
}

public sealed class MpvPlaybackEngine : IAsyncDisposable
{
    private readonly object _gate = new();
    private CancellationTokenSource? _eventLoopCts;
    private Task? _eventLoop;
    private TaskCompletionSource<bool>? _loadSignal;
    private bool _loadStarted;
    private bool _disposed;
    private IntPtr _context;
    private double _volume = 80;
    private bool _muted;

    public string Name => "libmpv + gpu-next/libplacebo";
    public bool IsAvailable => _context != IntPtr.Zero;
    public bool IsPlaying { get; private set; }
    public bool IsPaused => string.Equals(GetProperty("pause"), "yes", StringComparison.OrdinalIgnoreCase);
    public bool IsSeekable => string.Equals(GetProperty("seekable"), "yes", StringComparison.OrdinalIgnoreCase);
    public ChannelSource? CurrentChannelSource { get; private set; }
    public PlaybackQualityProfile Profile { get; private set; } = PlaybackQualityProfile.Balanced;
    public bool ExclusiveAudio { get; private set; }

    public event EventHandler<string>? PlaybackError;

    public void Initialize(IntPtr hwnd)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MpvPlaybackEngine));
        if (_context != IntPtr.Zero)
            return;
        if (hwnd == IntPtr.Zero)
            throw new ArgumentException("HWND inválido.", nameof(hwnd));

        var context = MpvNative.mpv_create();
        if (context == IntPtr.Zero)
            throw new InvalidOperationException("Não foi possível criar o contexto libmpv.");

        MpvNative.TrySetOption(context, "terminal", "no");
        MpvNative.TrySetOption(context, "msg-level", "all=warn");
        MpvNative.TrySetOption(context, "input-default-bindings", "no");
        MpvNative.TrySetOption(context, "input-vo-keyboard", "no");
        MpvNative.TrySetOption(context, "osc", "no");
        MpvNative.TrySetOption(context, "wid", hwnd.ToInt64().ToString(CultureInfo.InvariantCulture));

        // gpu-next usa libplacebo. D3D11 é o caminho nativo preferido no Windows.
        MpvNative.TrySetOption(context, "vo", "gpu-next");
        MpvNative.TrySetOption(context, "gpu-api", "d3d11");
        MpvNative.TrySetOption(context, "hwdec", "auto-safe");
        MpvNative.TrySetOption(context, "ao", "wasapi");
        MpvNative.TrySetOption(context, "audio-client-name", "SanchesTV");
        MpvNative.TrySetOption(context, "sub-auto", "fuzzy");
        MpvNative.TrySetOption(context, "sub-ass-override", "no");
        MpvNative.TrySetOption(context, "cache", "yes");
        MpvNative.TrySetOption(context, "demuxer-readahead-secs", "2");
        MpvNative.TrySetOption(context, "network-timeout", "10");
        MpvNative.TrySetOption(context, "keep-open", "no");

        var init = MpvNative.mpv_initialize(context);
        if (init < 0)
        {
            MpvNative.mpv_terminate_destroy(context);
            throw new InvalidOperationException($"Falha ao inicializar libmpv ({init}).");
        }

        _context = context;
        ApplyProfile(Profile);
        SetVolumeAsync(_volume).GetAwaiter().GetResult();

        _eventLoopCts = new CancellationTokenSource();
        _eventLoop = Task.Run(() => EventLoop(_eventLoopCts.Token));
    }

    public async Task<ChannelSource> OpenWithFallbackAsync(
        IEnumerable<ChannelSource> sources,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        Exception? last = null;

        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var ok = await OpenAndWaitAsync(source, cancellationToken);
                if (ok)
                    return source;

                last = new InvalidOperationException($"libmpv não carregou a fonte: {source.Url.Host}");
            }
            catch (Exception ex)
            {
                last = ex;
            }

            await StopAsync(cancellationToken);
        }

        throw new InvalidOperationException("Nenhuma fonte iniciou no libmpv.", last);
    }

    private async Task<bool> OpenAndWaitAsync(ChannelSource source, CancellationToken cancellationToken)
    {
        EnsureInitialized();

        MpvNative.TryCommand(_context, "stop");
        ConfigureHttpHeaders(source);

        TaskCompletionSource<bool> signal;
        lock (_gate)
        {
            _loadStarted = false;
            signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _loadSignal = signal;
        }

        var escaped = MpvNative.EscapeCommandArgument(source.Url.ToString());
        if (!MpvNative.TryCommand(_context, $"loadfile \"{escaped}\" replace"))
            return false;

        var completed = await Task.WhenAny(
            signal.Task,
            Task.Delay(TimeSpan.FromSeconds(9), cancellationToken));

        if (completed != signal.Task)
            return false;

        if (!await signal.Task)
            return false;

        CurrentChannelSource = source;
        IsPlaying = true;
        return true;
    }

    private void ConfigureHttpHeaders(ChannelSource source)
    {
        MpvNative.TrySetProperty(_context, "user-agent",
            string.IsNullOrWhiteSpace(source.UserAgent) ? "SanchesTV/6.0" : source.UserAgent);

        MpvNative.TrySetProperty(_context, "referrer",
            string.IsNullOrWhiteSpace(source.Referrer) ? string.Empty : source.Referrer);

        var headers = new List<string>();
        if (!string.IsNullOrWhiteSpace(source.Origin))
            headers.Add($"Origin: {source.Origin}");
        if (!string.IsNullOrWhiteSpace(source.Referrer))
            headers.Add($"Referer: {source.Referrer}");

        MpvNative.TrySetProperty(_context, "http-header-fields", string.Join(",", headers));
    }

    public Task PlayAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureInitialized();
        MpvNative.TrySetProperty(_context, "pause", "no");
        IsPlaying = true;
        return Task.CompletedTask;
    }

    public Task PauseAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureInitialized();
        MpvNative.TrySetProperty(_context, "pause", "yes");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_context != IntPtr.Zero)
            MpvNative.TryCommand(_context, "stop");
        IsPlaying = false;
        return Task.CompletedTask;
    }

    public Task SetVolumeAsync(double volume, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _volume = Math.Clamp(volume, 0, 100);
        if (_context != IntPtr.Zero)
            MpvNative.TrySetProperty(_context, "volume", _volume.ToString("0.##", CultureInfo.InvariantCulture));
        return Task.CompletedTask;
    }

    public Task SetMuteAsync(bool muted, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _muted = muted;
        if (_context != IntPtr.Zero)
            MpvNative.TrySetProperty(_context, "mute", muted ? "yes" : "no");
        return Task.CompletedTask;
    }

    public Task SeekRelativeAsync(double seconds, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureInitialized();
        MpvNative.TryCommand(_context,
            $"seek {seconds.ToString("0.###", CultureInfo.InvariantCulture)} relative exact");
        return Task.CompletedTask;
    }

    public Task GoLiveAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureInitialized();
        MpvNative.TryCommand(_context, "seek 100 absolute-percent");
        MpvNative.TrySetProperty(_context, "pause", "no");
        return Task.CompletedTask;
    }

    public Task SetProfileAsync(PlaybackQualityProfile profile, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Profile = profile;
        if (_context != IntPtr.Zero)
            ApplyProfile(profile);
        return Task.CompletedTask;
    }

    public Task SetExclusiveAudioAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ExclusiveAudio = enabled;
        if (_context != IntPtr.Zero)
            MpvNative.TrySetProperty(_context, "audio-exclusive", enabled ? "yes" : "no");
        return Task.CompletedTask;
    }

    private void ApplyProfile(PlaybackQualityProfile profile)
    {
        switch (profile)
        {
            case PlaybackQualityProfile.MaximumQuality:
                Set("hwdec", "auto-safe");
                Set("video-sync", "display-resample");
                Set("scale", "ewa_lanczossharp");
                Set("cscale", "ewa_lanczossharp");
                Set("dscale", "mitchell");
                Set("deband", "yes");
                Set("tone-mapping", "auto");
                Set("icc-profile-auto", "yes");
                Set("cache", "yes");
                Set("demuxer-readahead-secs", "3");
                break;

            case PlaybackQualityProfile.LowLatency:
                Set("hwdec", "auto-safe");
                Set("video-sync", "audio");
                Set("scale", "bilinear");
                Set("cscale", "bilinear");
                Set("deband", "no");
                Set("cache", "yes");
                Set("demuxer-readahead-secs", "0.3");
                Set("cache-pause", "no");
                break;

            case PlaybackQualityProfile.LowPower:
                Set("hwdec", "auto");
                Set("video-sync", "audio");
                Set("scale", "bilinear");
                Set("cscale", "bilinear");
                Set("dscale", "bilinear");
                Set("deband", "no");
                Set("cache", "yes");
                Set("demuxer-readahead-secs", "1");
                break;

            default:
                Set("hwdec", "auto-safe");
                Set("video-sync", "audio");
                Set("scale", "spline36");
                Set("cscale", "spline36");
                Set("dscale", "mitchell");
                Set("deband", "yes");
                Set("tone-mapping", "auto");
                Set("cache", "yes");
                Set("demuxer-readahead-secs", "2");
                break;
        }

        Set("audio-exclusive", ExclusiveAudio ? "yes" : "no");
    }

    private void Set(string name, string value) =>
        MpvNative.TrySetProperty(_context, name, value);

    public string? GetProperty(string name) =>
        _context == IntPtr.Zero ? null : MpvNative.GetPropertyString(_context, name);

    public string GetDiagnostics()
    {
        if (_context == IntPtr.Zero)
            return "libmpv indisponível";

        return string.Join(Environment.NewLine,
            $"mpv: {GetProperty("mpv-version") ?? "?"}",
            $"FFmpeg: {GetProperty("ffmpeg-version") ?? "integrado"}",
            $"VO: {GetProperty("current-vo") ?? "gpu-next"}",
            $"HW decode: {GetProperty("hwdec-current") ?? "software/auto"}",
            $"Vídeo: {GetProperty("video-codec") ?? "?"}",
            $"Áudio: {GetProperty("audio-codec-name") ?? "?"}",
            $"Formato: {GetProperty("video-format") ?? "?"}",
            $"FPS: {GetProperty("estimated-vf-fps") ?? GetProperty("container-fps") ?? "?"}",
            $"HDR primaries: {GetProperty("video-params/primaries") ?? "n/a"}",
            $"Transfer: {GetProperty("video-params/gamma") ?? "n/a"}",
            $"Perfil: {Profile}",
            $"WASAPI exclusivo: {(ExclusiveAudio ? "sim" : "não")}");
    }

    private void EventLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _context != IntPtr.Zero)
        {
            try
            {
                var ptr = MpvNative.mpv_wait_event(_context, 0.1);
                if (ptr == IntPtr.Zero)
                    continue;

                var ev = Marshal.PtrToStructure<MpvEvent>(ptr);
                if (ev.EventId == MpvEventId.None)
                    continue;

                switch (ev.EventId)
                {
                    case MpvEventId.StartFile:
                        lock (_gate)
                            _loadStarted = true;
                        break;

                    case MpvEventId.FileLoaded:
                        lock (_gate)
                            _loadSignal?.TrySetResult(true);
                        IsPlaying = true;
                        break;

                    case MpvEventId.PlaybackRestart:
                        IsPlaying = true;
                        break;

                    case MpvEventId.EndFile:
                        lock (_gate)
                        {
                            if (_loadStarted)
                                _loadSignal?.TrySetResult(false);
                        }
                        IsPlaying = false;
                        break;

                    case MpvEventId.Shutdown:
                        return;
                }
            }
            catch (Exception ex)
            {
                PlaybackError?.Invoke(this, $"libmpv: {ex.Message}");
                Thread.Sleep(100);
            }
        }
    }

    private void EnsureInitialized()
    {
        if (_context == IntPtr.Zero)
            throw new InvalidOperationException("libmpv ainda não foi inicializado.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        _eventLoopCts?.Cancel();
        if (_eventLoop is not null)
        {
            try { await _eventLoop.WaitAsync(TimeSpan.FromSeconds(2)); } catch { }
        }

        if (_context != IntPtr.Zero)
        {
            var context = _context;
            _context = IntPtr.Zero;
            MpvNative.mpv_terminate_destroy(context);
        }

        _eventLoopCts?.Dispose();
    }
}
