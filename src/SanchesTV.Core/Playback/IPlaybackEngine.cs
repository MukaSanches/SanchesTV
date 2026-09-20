namespace SanchesTV.Core.Playback;

public interface IPlaybackEngine : IAsyncDisposable
{
    string Name { get; }
    bool IsAvailable { get; }
    Task OpenAsync(Uri source, CancellationToken cancellationToken = default);
    Task PlayAsync(CancellationToken cancellationToken = default);
    Task PauseAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task SetVolumeAsync(double volume, CancellationToken cancellationToken = default);
    Task SetMuteAsync(bool muted, CancellationToken cancellationToken = default);
}
