using System.Diagnostics;
using SanchesTV.Core.Models;

namespace SanchesTV.Desktop.Playback;

public enum PlaybackEngineKind { Mpv, Vlc }

public sealed record PlaybackOpenResult(ChannelSource Source, PlaybackEngineKind Engine, bool FellBackFromMpv, TimeSpan StartupLatency);

public sealed class PlaybackCoordinator
{
    private readonly MpvPlaybackEngine _mpv;
    private readonly VlcPlaybackEngine _vlc;
    private readonly Func<Task<bool>> _ensureMpvReady;
    private readonly SourceHealthLedger _health;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public PlaybackCoordinator(MpvPlaybackEngine mpv, VlcPlaybackEngine vlc, Func<Task<bool>> ensureMpvReady, SourceHealthLedger health)
    {
        _mpv = mpv;
        _vlc = vlc;
        _ensureMpvReady = ensureMpvReady;
        _health = health;
    }

    public async Task<PlaybackOpenResult> OpenAsync(IEnumerable<ChannelSource> sources, CancellationToken cancellationToken = default)
    {
        var ordered = _health.Order(sources);
        if (ordered.Count == 0)
            throw new InvalidOperationException("Nenhuma fonte disponível.");

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var mpvAttempted = false;
            if (await SafeMpvReadyAsync())
            {
                mpvAttempted = true;
                try
                {
                    await _vlc.StopAsync(cancellationToken);
                    var watch = Stopwatch.StartNew();
                    var source = await _mpv.OpenWithFallbackAsync(ordered, cancellationToken);
                    watch.Stop();
                    _health.RecordSuccess(source, watch.Elapsed);
                    return new(source, PlaybackEngineKind.Mpv, false, watch.Elapsed);
                }
                catch (OperationCanceledException) { throw; }
                catch
                {
                    foreach (var source in ordered) _health.RecordFailure(source);
                    try { await _mpv.StopAsync(cancellationToken); } catch { }
                }
            }

            var fallback = Stopwatch.StartNew();
            try
            {
                var source = await _vlc.OpenWithFallbackAsync(ordered, cancellationToken);
                fallback.Stop();
                _health.RecordSuccess(source, fallback.Elapsed);
                return new(source, PlaybackEngineKind.Vlc, mpvAttempted, fallback.Elapsed);
            }
            catch
            {
                foreach (var source in ordered) _health.RecordFailure(source);
                throw;
            }
        }
        finally { _gate.Release(); }
    }

    private async Task<bool> SafeMpvReadyAsync()
    {
        try { return await _ensureMpvReady(); }
        catch { return false; }
    }
}
