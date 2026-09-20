using System.IO;
using LibVLCSharp.Shared;
using SanchesTV.Core.Playback;

namespace SanchesTV.Desktop.Playback;

public sealed class VlcPlaybackEngine : IPlaybackEngine
{
    private readonly LibVLC _libVlc;
    private MediaPlayer? _recorder;
    private string? _recordingPath;

    public string Name => "LibVLC";
    public bool IsAvailable => MediaPlayer.NativeReference != IntPtr.Zero;
    public MediaPlayer MediaPlayer { get; }
    public Uri? CurrentSource { get; private set; }
    public string? RecordingPath => _recordingPath;

    public event EventHandler<string>? PlaybackError;

    public VlcPlaybackEngine()
    {
        LibVLCSharp.Shared.Core.Initialize();
        _libVlc = new LibVLC(
            "--no-video-title-show",
            "--quiet",
            "--avcodec-hw=any",
            "--network-caching=1500",
            "--live-caching=1500");
        MediaPlayer = new MediaPlayer(_libVlc)
        {
            EnableKeyInput = false,
            EnableMouseInput = false,
            Volume = 80
        };
        MediaPlayer.EncounteredError += (_, _) => PlaybackError?.Invoke(this, "Falha ao reproduzir a fonte atual.");
    }

    public Task OpenAsync(Uri source, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CurrentSource = source;
        using var media = new Media(
            _libVlc,
            source,
            ":network-caching=1500",
            ":live-caching=1500",
            ":http-reconnect=true");
        if (!MediaPlayer.Play(media))
            throw new InvalidOperationException("O mecanismo de vídeo recusou a fonte.");
        return Task.CompletedTask;
    }

    public async Task<Uri> OpenWithFallbackAsync(IEnumerable<Uri> sources, CancellationToken cancellationToken = default)
    {
        Exception? last = null;
        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var played = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<EventArgs>? onPlaying = null;
            EventHandler<EventArgs>? onError = null;

            onPlaying = (_, _) => played.TrySetResult(true);
            onError = (_, _) => played.TrySetResult(false);
            MediaPlayer.Playing += onPlaying;
            MediaPlayer.EncounteredError += onError;

            try
            {
                await OpenAsync(source, cancellationToken);
                var finished = await Task.WhenAny(
                    played.Task,
                    Task.Delay(TimeSpan.FromSeconds(8), cancellationToken));
                if (finished == played.Task && await played.Task)
                    return source;

                await StopAsync(cancellationToken);
                last = new InvalidOperationException($"Fonte não iniciou: {source.Host}");
            }
            catch (Exception ex)
            {
                last = ex;
                await StopAsync(cancellationToken);
            }
            finally
            {
                MediaPlayer.Playing -= onPlaying;
                MediaPlayer.EncounteredError -= onError;
            }
        }

        throw new InvalidOperationException("Nenhuma fonte disponível conseguiu iniciar.", last);
    }

    public Task PlayAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (MediaPlayer.State == VLCState.Paused)
            MediaPlayer.SetPause(false);
        else
            MediaPlayer.Play();
        return Task.CompletedTask;
    }

    public Task PauseAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        MediaPlayer.SetPause(true);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        MediaPlayer.Stop();
        return Task.CompletedTask;
    }

    public Task SetVolumeAsync(double volume, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        MediaPlayer.Volume = Math.Clamp((int)Math.Round(volume), 0, 100);
        return Task.CompletedTask;
    }

    public Task SetMuteAsync(bool muted, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        MediaPlayer.Mute = muted;
        return Task.CompletedTask;
    }

    public bool StartRecording(string directory)
    {
        if (CurrentSource is null || _recorder is not null)
            return false;

        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, $"SanchesTV-{DateTime.Now:yyyyMMdd-HHmmss}.ts");
        var escaped = file.Replace("\\", "/");

        using var media = new Media(
            _libVlc,
            CurrentSource,
            $":sout=#file{{dst={escaped}}}",
            ":sout-keep",
            ":network-caching=1500");

        _recorder = new MediaPlayer(_libVlc);
        if (!_recorder.Play(media))
        {
            _recorder.Dispose();
            _recorder = null;
            return false;
        }

        _recordingPath = file;
        return true;
    }

    public void StopRecording()
    {
        if (_recorder is null)
            return;
        _recorder.Stop();
        _recorder.Dispose();
        _recorder = null;
    }

    public async ValueTask DisposeAsync()
    {
        StopRecording();
        try { MediaPlayer.Stop(); } catch { }
        MediaPlayer.Dispose();
        _libVlc.Dispose();
        await ValueTask.CompletedTask;
    }
}
