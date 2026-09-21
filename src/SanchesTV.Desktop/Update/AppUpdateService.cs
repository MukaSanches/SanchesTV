using Velopack;
using Velopack.Sources;

namespace SanchesTV.Desktop.Update;

public sealed class AppUpdateService
{
    private readonly UpdateManager _manager;

    public AppUpdateService()
    {
        var source = new GithubSource(
            "https://github.com/MukaSanches/SanchesTV",
            accessToken: null,
            prerelease: false);
        _manager = new UpdateManager(source);
    }

    public bool IsVelopackInstalled => _manager.IsInstalled;
    public string CurrentVersion => _manager.CurrentVersion?.ToString()
        ?? typeof(AppUpdateService).Assembly.GetName().Version?.ToString()
        ?? "?";

    public async Task<UpdateInfo?> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (!_manager.IsInstalled)
            return null;

        cancellationToken.ThrowIfCancellationRequested();
        return await _manager.CheckForUpdatesAsync();
    }

    public async Task DownloadAsync(
        UpdateInfo update,
        Action<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await _manager.DownloadUpdatesAsync(update, progress, cancellationToken);
    }

    public void ApplyAndRestart(UpdateInfo update) =>
        _manager.ApplyUpdatesAndRestart(update.TargetFullRelease);
}
