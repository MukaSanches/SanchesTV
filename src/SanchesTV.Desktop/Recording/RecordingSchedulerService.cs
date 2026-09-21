using System.Diagnostics;
using System.IO;
using SanchesTV.Core.Models;
using SanchesTV.Core.Storage;

namespace SanchesTV.Desktop.Recording;

public sealed class RecordingSchedulerService : IAsyncDisposable
{
    private readonly AppDatabase _db;
    private readonly string _ffmpegPath;
    private readonly CancellationTokenSource _cts = new();
    private readonly Dictionary<Guid, Process> _active = new();
    private Task? _loop;

    public event EventHandler<string>? StatusChanged;

    public RecordingSchedulerService(AppDatabase db)
    {
        _db = db;
        _ffmpegPath = Path.Combine(AppContext.BaseDirectory, "runtime", "ffmpeg", "ffmpeg.exe");
    }

    public void Start()
    {
        if (_loop is not null)
            return;
        _loop = Task.Run(() => RunLoopAsync(_cts.Token));
    }

    public async Task ScheduleAsync(
        Channel channel,
        EpgProgram program,
        string? seriesKey = null,
        CancellationToken cancellationToken = default)
    {
        if (program.End <= program.Start)
            throw new ArgumentException("Programa possui horário inválido.");

        await _db.AddScheduledRecordingAsync(new ScheduledRecording(
            Guid.NewGuid(),
            channel.Id,
            program.Title,
            program.Start,
            program.End,
            seriesKey,
            ScheduledRecordingStatus.Scheduled,
            null,
            null), cancellationToken);

        StatusChanged?.Invoke(this,
            $"Gravação agendada: {program.Title} • {program.Start.ToLocalTime():dd/MM HH:mm}");
    }

    public async Task<int> ScheduleSeriesAsync(
        Channel channel,
        IEnumerable<EpgProgram> programs,
        string title,
        CancellationToken cancellationToken = default)
    {
        var key = $"{channel.Id:N}:{title.Trim().ToLowerInvariant()}";
        var existing = await _db.GetScheduledRecordingsAsync(includeFinished: true, cancellationToken);
        var existingKeys = existing
            .Where(x => string.Equals(x.SeriesKey, key, StringComparison.Ordinal))
            .Select(x => x.Start.UtcDateTime)
            .ToHashSet();

        var count = 0;
        foreach (var p in programs
                     .Where(x => string.Equals(x.Title, title, StringComparison.CurrentCultureIgnoreCase))
                     .Where(x => x.End > DateTimeOffset.UtcNow))
        {
            if (existingKeys.Contains(p.Start.UtcDateTime))
                continue;

            await ScheduleAsync(channel, p, key, cancellationToken);
            count++;
        }

        return count;
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, "Agendador: " + ex.Message);
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(cancellationToken))
                    break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_ffmpegPath))
            return;

        var now = DateTimeOffset.UtcNow;

        foreach (var active in _active.ToArray())
        {
            if (!active.Value.HasExited)
                continue;

            var status = active.Value.ExitCode == 0
                ? ScheduledRecordingStatus.Completed
                : ScheduledRecordingStatus.Failed;

            await _db.UpdateScheduledRecordingAsync(
                active.Key,
                status,
                lastError: status == ScheduledRecordingStatus.Failed
                    ? $"FFmpeg encerrou com código {active.Value.ExitCode}"
                    : null,
                cancellationToken: cancellationToken);

            active.Value.Dispose();
            _active.Remove(active.Key);
        }

        var pending = await _db.GetScheduledRecordingsAsync(false, cancellationToken);
        foreach (var missed in pending.Where(x =>
                     x.Status == ScheduledRecordingStatus.Scheduled &&
                     x.End <= now))
        {
            await _db.UpdateScheduledRecordingAsync(
                missed.Id,
                ScheduledRecordingStatus.Failed,
                lastError: "Horário da gravação expirou antes de iniciar.",
                cancellationToken: cancellationToken);
        }

        var due = await _db.GetDueScheduledRecordingsAsync(now, cancellationToken);
        if (due.Count == 0)
            return;

        var channels = await _db.GetChannelsAsync(cancellationToken);
        var map = channels.ToDictionary(x => x.Id);

        foreach (var recording in due)
        {
            if (_active.ContainsKey(recording.Id))
                continue;

            if (!map.TryGetValue(recording.ChannelId, out var channel) ||
                channel.Sources.Count == 0)
            {
                await _db.UpdateScheduledRecordingAsync(
                    recording.Id,
                    ScheduledRecordingStatus.Failed,
                    lastError: "Canal ou fonte não encontrado.",
                    cancellationToken: cancellationToken);
                continue;
            }

            var source = channel.Sources
                .OrderBy(x => x.Status == StreamStatus.Online ? 0 : x.Status == StreamStatus.NotTested ? 1 : 2)
                .ThenBy(x => x.Priority)
                .First();

            try
            {
                var output = CreateOutputPath(recording, channel);
                var process = StartRecording(source, output, recording.End - now);
                _active[recording.Id] = process;

                await _db.UpdateScheduledRecordingAsync(
                    recording.Id,
                    ScheduledRecordingStatus.Recording,
                    outputPath: output,
                    cancellationToken: cancellationToken);

                StatusChanged?.Invoke(this, $"Gravando: {recording.Title}");
            }
            catch (Exception ex)
            {
                await _db.UpdateScheduledRecordingAsync(
                    recording.Id,
                    ScheduledRecordingStatus.Failed,
                    lastError: ex.Message,
                    cancellationToken: cancellationToken);
            }
        }
    }

    private Process StartRecording(ChannelSource source, string output, TimeSpan remaining)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            UseShellExecute = false,
            CreateNoWindow = true,
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
        psi.ArgumentList.Add("0");
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add("copy");
        psi.ArgumentList.Add("-t");
        psi.ArgumentList.Add(Math.Max(1, remaining.TotalSeconds).ToString("0", System.Globalization.CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add(output);

        return Process.Start(psi)
            ?? throw new InvalidOperationException("Não foi possível iniciar FFmpeg.");
    }

    private static string CreateOutputPath(ScheduledRecording recording, Channel channel)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            "SanchesTV", "Gravações");
        Directory.CreateDirectory(dir);

        static string Clean(string value)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');
            return value.Length > 90 ? value[..90] : value;
        }

        var name = $"{recording.Start.ToLocalTime():yyyyMMdd-HHmm} - {Clean(channel.Name)} - {Clean(recording.Title)}.mkv";
        return Path.Combine(dir, name);
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();

        if (_loop is not null)
        {
            try { await _loop.WaitAsync(TimeSpan.FromSeconds(2)); } catch { }
        }

        foreach (var process in _active.Values)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
            }
            process.Dispose();
        }
        _active.Clear();
        _cts.Dispose();
    }
}
