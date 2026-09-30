using Microsoft.Data.Sqlite;
using SanchesTV.Core.Models;
using SanchesTV.Core.Storage;
using Xunit;

namespace SanchesTV.Tests;

public sealed class DiscoveryDatabaseTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"sanchestv-discovery-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task ReadsCurrentAndNextPerChannelWithUtcBoundariesAndOverlappingPrograms()
    {
        var db = new AppDatabase(_path);
        await db.InitializeAsync();
        var now = new DateTimeOffset(2026, 9, 30, 18, 0, 0, TimeSpan.FromHours(-3));
        await db.ReplaceEpgAsync([
            Program("a", now.AddHours(-2), now.AddMinutes(30), "Sobreposição anterior"),
            Program("a", now.AddMinutes(-15), now.AddMinutes(30), "Agora"),
            Program("a", now.AddMinutes(30), now.AddMinutes(60), "Próximo"),
            Program("a", now.AddMinutes(60), now.AddMinutes(90), "Mais tarde"),
            Program("b", now.AddMinutes(-30), now, "Terminou"),
            Program("b", now, now.AddMinutes(30), "Começou agora"),
            Program("b", now.AddMinutes(30), now.AddHours(1), "A seguir B"),
            Program("c", now.AddDays(1), now.AddDays(1).AddHours(1), "Fora da janela"),
            Program("d", now.AddMinutes(20), now.AddMinutes(10), "Inválido")
        ]);

        var programs = await db.GetDiscoveryProgramsAsync(now.ToUniversalTime());
        Assert.Equal(new[] { "Agora", "Próximo", "Começou agora", "A seguir B" }, programs.Select(p => p.Title));
        Assert.All(programs, p => Assert.True(p.End > now));
    }

    [Fact]
    public async Task EpgTieBreaksMatchDiscoveryRegardlessOfImportOrder()
    {
        var db = new AppDatabase(_path);
        await db.InitializeAsync();
        var now = new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
        var programs = new[]
        {
            Program("a", now.AddMinutes(-30), now.AddHours(2), "Longo"),
            Program("a", now.AddMinutes(-30), now.AddHours(1), "Z"),
            Program("a", now.AddMinutes(-30), now.AddHours(1), "A"),
            Program("a", now.AddHours(1), now.AddHours(3), "Próximo longo"),
            Program("a", now.AddHours(1), now.AddHours(2), "Próximo curto")
        };
        foreach (var input in new[] { programs, programs.Reverse().ToArray() })
        {
            await db.ReplaceEpgAsync(input);
            var selection = await db.GetDiscoveryProgramsAsync(now);
            Assert.Equal(new[] { "A", "Próximo curto" }, selection.Select(p => p.Title));
        }
    }

    [Fact]
    public async Task InitializationAndDiscoveryPreserveFavoritesMyTvHistoryAndSettings()
    {
        var db = new AppDatabase(_path);
        await db.InitializeAsync();
        var channel = new Channel(Guid.NewGuid(), "Canal Preservado", "CANAL PRESERVADO", "BR", "pt",
            null, null, "Cultura", null, "preservado", null, []);
        await db.UpsertChannelsAsync([channel]);
        await db.SetFavoriteAsync(channel.Id, true);
        await db.SetMyTvPositionAsync(channel.Id, 3);
        await db.RecordPlayedAsync(channel.Id);
        await db.SetSettingAsync("existing.preference", "preservada");

        var reopened = new AppDatabase(_path);
        await reopened.InitializeAsync();
        Assert.Empty(await reopened.GetDiscoveryProgramsAsync(DateTimeOffset.UtcNow));
        var stored = Assert.Single(await reopened.GetChannelsAsync());
        Assert.True(stored.IsFavorite);
        Assert.Equal(3, stored.MyTvPosition);
        Assert.Equal(channel.Id, Assert.Single(await reopened.GetRecentChannelIdsAsync()));
        Assert.Equal("preservada", await reopened.GetSettingAsync("existing.preference"));
    }

    [Fact]
    public async Task EnforcesChannelReferencesOnNewConnections()
    {
        var db = new AppDatabase(_path);
        await db.InitializeAsync();
        await Assert.ThrowsAsync<SqliteException>(() => db.RecordPlayedAsync(Guid.NewGuid()));
    }

    private static EpgProgram Program(string id, DateTimeOffset start, DateTimeOffset end, string title) =>
        new(id, start, end, title, null, null);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
            File.Delete(_path + suffix);
    }
}
