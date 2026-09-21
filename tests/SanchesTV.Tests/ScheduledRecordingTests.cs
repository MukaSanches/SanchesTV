using Xunit;
using SanchesTV.Core.Models;
using SanchesTV.Core.Parsing;
using SanchesTV.Core.Storage;

namespace SanchesTV.Tests;

public sealed class ScheduledRecordingTests
{
    [Fact]
    public async Task PersistsAndFindsDueRecording()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sanchestv-test-{Guid.NewGuid():N}.db");
        try
        {
            var db = new AppDatabase(path);
            await db.InitializeAsync();

            var channel = new Channel(
                Guid.NewGuid(),
                "Canal Teste",
                TextNormalizer.Normalize("Canal Teste"),
                "BR",
                "Portuguese",
                null,
                null,
                "TV",
                null,
                "teste.br",
                null,
                Array.Empty<ChannelSource>(),
                false,
                null);

            await db.UpsertChannelsAsync([channel]);

            var now = DateTimeOffset.UtcNow;
            var recording = new ScheduledRecording(
                Guid.NewGuid(),
                channel.Id,
                "Programa Teste",
                now.AddMinutes(-1),
                now.AddMinutes(30),
                null,
                ScheduledRecordingStatus.Scheduled,
                null,
                null);

            await db.AddScheduledRecordingAsync(recording);

            var due = await db.GetDueScheduledRecordingsAsync(now);
            var found = Assert.Single(due);
            Assert.Equal(recording.Id, found.Id);
            Assert.Equal("Programa Teste", found.Title);

            await db.UpdateScheduledRecordingAsync(
                recording.Id,
                ScheduledRecordingStatus.Completed,
                outputPath: "teste.mkv");

            var active = await db.GetScheduledRecordingsAsync(includeFinished: false);
            Assert.Empty(active);

            var all = await db.GetScheduledRecordingsAsync(includeFinished: true);
            var completed = Assert.Single(all);
            Assert.Equal(ScheduledRecordingStatus.Completed, completed.Status);
            Assert.Equal("teste.mkv", completed.OutputPath);
        }
        finally
        {
            try { File.Delete(path); } catch { }
            try { File.Delete(path + "-wal"); } catch { }
            try { File.Delete(path + "-shm"); } catch { }
        }
    }
}
