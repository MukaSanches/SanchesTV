using Microsoft.Data.Sqlite;
using SanchesTV.Core.Models;

namespace SanchesTV.Core.Storage;

public sealed class AppDatabase
{
    private readonly string _connectionString;

    public string DatabasePath { get; }

    public AppDatabase(string? databasePath = null)
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SanchesTV");
        Directory.CreateDirectory(root);

        DatabasePath = databasePath ?? Path.Combine(root, "sanchestv.db");
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = Open();
        var command = connection.CreateCommand();
        command.CommandText = """
        PRAGMA journal_mode=WAL;
        PRAGMA foreign_keys=ON;

        CREATE TABLE IF NOT EXISTS channels (
            id TEXT PRIMARY KEY,
            name TEXT NOT NULL,
            normalized_name TEXT NOT NULL,
            country TEXT,
            language TEXT,
            state TEXT,
            region TEXT,
            category TEXT,
            logo TEXT,
            epg_id TEXT,
            virtual_number INTEGER,
            favorite INTEGER NOT NULL DEFAULT 0,
            mytv_position INTEGER
        );

        CREATE INDEX IF NOT EXISTS ix_channels_normalized_name ON channels(normalized_name);
        CREATE INDEX IF NOT EXISTS ix_channels_epg_id ON channels(epg_id);

        CREATE TABLE IF NOT EXISTS channel_sources (
            id TEXT PRIMARY KEY,
            channel_id TEXT NOT NULL,
            provider TEXT NOT NULL,
            url TEXT NOT NULL,
            priority INTEGER NOT NULL DEFAULT 0,
            status INTEGER NOT NULL DEFAULT 4,
            latency_ms INTEGER,
            resolution TEXT,
            video_codec TEXT,
            audio_codec TEXT,
            bitrate INTEGER,
            last_error TEXT,
            user_agent TEXT,
            referrer TEXT,
            origin TEXT,
            UNIQUE(channel_id, url),
            FOREIGN KEY(channel_id) REFERENCES channels(id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS history (
            channel_id TEXT PRIMARY KEY,
            last_played TEXT NOT NULL,
            play_seconds INTEGER NOT NULL DEFAULT 0,
            FOREIGN KEY(channel_id) REFERENCES channels(id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS epg_programs (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            channel_epg_id TEXT NOT NULL,
            start_utc TEXT NOT NULL,
            end_utc TEXT NOT NULL,
            title TEXT NOT NULL,
            description TEXT,
            category TEXT
        );

        CREATE INDEX IF NOT EXISTS ix_epg_channel_time
            ON epg_programs(channel_epg_id, start_utc, end_utc);

        CREATE TABLE IF NOT EXISTS settings (
            key TEXT PRIMARY KEY,
            value TEXT
        );
        """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        await EnsureColumnAsync(connection, "channel_sources", "user_agent", "TEXT", cancellationToken);
        await EnsureColumnAsync(connection, "channel_sources", "referrer", "TEXT", cancellationToken);
        await EnsureColumnAsync(connection, "channel_sources", "origin", "TEXT", cancellationToken);
    }

    private static async Task EnsureColumnAsync(SqliteConnection connection, string table, string column, string sqlType, CancellationToken cancellationToken)
    {
        var check = connection.CreateCommand();
        check.CommandText = $"PRAGMA table_info({table});";
        await using var reader = await check.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return;
        }
        await reader.DisposeAsync();

        var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {sqlType};";
        await alter.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> UpsertChannelsAsync(IEnumerable<Channel> channels, CancellationToken cancellationToken = default)
    {
        await using var connection = Open();
        using var transaction = connection.BeginTransaction();
        var count = 0;

        foreach (var incoming in channels)
        {
            var existingId = await FindChannelIdAsync(connection, transaction, incoming, cancellationToken);
            var channelId = existingId ?? incoming.Id;

            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
            INSERT INTO channels
                (id, name, normalized_name, country, language, state, region, category, logo, epg_id, virtual_number)
            VALUES
                ($id, $name, $normalized, $country, $language, $state, $region, $category, $logo, $epg, $number)
            ON CONFLICT(id) DO UPDATE SET
                name=excluded.name,
                normalized_name=excluded.normalized_name,
                country=COALESCE(excluded.country, channels.country),
                language=COALESCE(excluded.language, channels.language),
                state=COALESCE(excluded.state, channels.state),
                region=COALESCE(excluded.region, channels.region),
                category=COALESCE(excluded.category, channels.category),
                logo=COALESCE(excluded.logo, channels.logo),
                epg_id=COALESCE(excluded.epg_id, channels.epg_id),
                virtual_number=COALESCE(excluded.virtual_number, channels.virtual_number);
            """;
            command.Parameters.AddWithValue("$id", channelId.ToString());
            command.Parameters.AddWithValue("$name", incoming.Name);
            command.Parameters.AddWithValue("$normalized", incoming.NormalizedName);
            command.Parameters.AddWithValue("$country", (object?)incoming.Country ?? DBNull.Value);
            command.Parameters.AddWithValue("$language", (object?)incoming.Language ?? DBNull.Value);
            command.Parameters.AddWithValue("$state", (object?)incoming.State ?? DBNull.Value);
            command.Parameters.AddWithValue("$region", (object?)incoming.Region ?? DBNull.Value);
            command.Parameters.AddWithValue("$category", (object?)incoming.Category ?? DBNull.Value);
            command.Parameters.AddWithValue("$logo", (object?)incoming.Logo ?? DBNull.Value);
            command.Parameters.AddWithValue("$epg", (object?)incoming.EpgId ?? DBNull.Value);
            command.Parameters.AddWithValue("$number", (object?)incoming.VirtualNumber ?? DBNull.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);

            foreach (var source in incoming.Sources)
            {
                var sourceCommand = connection.CreateCommand();
                sourceCommand.Transaction = transaction;
                sourceCommand.CommandText = """
                INSERT INTO channel_sources
                    (id, channel_id, provider, url, priority, status, latency_ms, resolution, video_codec, audio_codec, bitrate, last_error, user_agent, referrer, origin)
                VALUES
                    ($id, $channel, $provider, $url, $priority, $status, $latency, $resolution, $video, $audio, $bitrate, $error, $userAgent, $referrer, $origin)
                ON CONFLICT(channel_id, url) DO UPDATE SET
                    provider=excluded.provider,
                    priority=MIN(channel_sources.priority, excluded.priority),
                    user_agent=COALESCE(excluded.user_agent, channel_sources.user_agent),
                    referrer=COALESCE(excluded.referrer, channel_sources.referrer),
                    origin=COALESCE(excluded.origin, channel_sources.origin);
                """;
                sourceCommand.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
                sourceCommand.Parameters.AddWithValue("$channel", channelId.ToString());
                sourceCommand.Parameters.AddWithValue("$provider", source.Provider);
                sourceCommand.Parameters.AddWithValue("$url", source.Url.ToString());
                sourceCommand.Parameters.AddWithValue("$priority", source.Priority);
                sourceCommand.Parameters.AddWithValue("$status", (int)source.Status);
                sourceCommand.Parameters.AddWithValue("$latency", source.Latency is null ? DBNull.Value : (object)(long)source.Latency.Value.TotalMilliseconds);
                sourceCommand.Parameters.AddWithValue("$resolution", (object?)source.Resolution ?? DBNull.Value);
                sourceCommand.Parameters.AddWithValue("$video", (object?)source.VideoCodec ?? DBNull.Value);
                sourceCommand.Parameters.AddWithValue("$audio", (object?)source.AudioCodec ?? DBNull.Value);
                sourceCommand.Parameters.AddWithValue("$bitrate", (object?)source.Bitrate ?? DBNull.Value);
                sourceCommand.Parameters.AddWithValue("$error", (object?)source.LastError ?? DBNull.Value);
                sourceCommand.Parameters.AddWithValue("$userAgent", (object?)source.UserAgent ?? DBNull.Value);
                sourceCommand.Parameters.AddWithValue("$referrer", (object?)source.Referrer ?? DBNull.Value);
                sourceCommand.Parameters.AddWithValue("$origin", (object?)source.Origin ?? DBNull.Value);
                await sourceCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            count++;
        }

        transaction.Commit();
        return count;
    }

    private static async Task<Guid?> FindChannelIdAsync(SqliteConnection connection, SqliteTransaction transaction, Channel channel, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = string.IsNullOrWhiteSpace(channel.EpgId)
            ? "SELECT id FROM channels WHERE normalized_name=$name LIMIT 1;"
            : "SELECT id FROM channels WHERE epg_id=$epg OR normalized_name=$name LIMIT 1;";
        command.Parameters.AddWithValue("$name", channel.NormalizedName);
        if (!string.IsNullOrWhiteSpace(channel.EpgId))
            command.Parameters.AddWithValue("$epg", channel.EpgId);

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is string s && Guid.TryParse(s, out var id) ? id : null;
    }

    public async Task<IReadOnlyList<Channel>> GetChannelsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = Open();
        var result = new List<Channel>();

        var command = connection.CreateCommand();
        command.CommandText = """
        SELECT id, name, normalized_name, country, language, state, region, category, logo, epg_id,
               virtual_number, favorite, mytv_position
        FROM channels
        ORDER BY COALESCE(virtual_number, 999999), name COLLATE NOCASE;
        """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var channelId = Guid.Parse(reader.GetString(0));
            var sources = await GetSourcesAsync(connection, channelId, cancellationToken);
            result.Add(new Channel(
                channelId,
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetInt32(10),
                sources,
                reader.GetInt32(11) != 0,
                reader.IsDBNull(12) ? null : reader.GetInt32(12)));
        }

        return result;
    }

    private static async Task<IReadOnlyList<ChannelSource>> GetSourcesAsync(SqliteConnection connection, Guid channelId, CancellationToken cancellationToken)
    {
        var result = new List<ChannelSource>();
        var command = connection.CreateCommand();
        command.CommandText = """
        SELECT id, provider, url, priority, status, latency_ms, resolution, video_codec, audio_codec, bitrate, last_error,
               user_agent, referrer, origin
        FROM channel_sources WHERE channel_id=$channel ORDER BY priority, provider;
        """;
        command.Parameters.AddWithValue("$channel", channelId.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!Uri.TryCreate(reader.GetString(2), UriKind.Absolute, out var uri))
                continue;

            result.Add(new ChannelSource(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                uri,
                reader.GetInt32(3),
                (StreamStatus)reader.GetInt32(4),
                reader.IsDBNull(5) ? null : TimeSpan.FromMilliseconds(reader.GetInt64(5)),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetInt64(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetString(11),
                reader.IsDBNull(12) ? null : reader.GetString(12),
                reader.IsDBNull(13) ? null : reader.GetString(13)));
        }

        return result;
    }

    public async Task SetFavoriteAsync(Guid channelId, bool favorite, CancellationToken cancellationToken = default)
    {
        await using var connection = Open();
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE channels SET favorite=$favorite WHERE id=$id;";
        command.Parameters.AddWithValue("$favorite", favorite ? 1 : 0);
        command.Parameters.AddWithValue("$id", channelId.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SetMyTvPositionAsync(Guid channelId, int? position, CancellationToken cancellationToken = default)
    {
        await using var connection = Open();
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE channels SET mytv_position=$position WHERE id=$id;";
        command.Parameters.AddWithValue("$position", (object?)position ?? DBNull.Value);
        command.Parameters.AddWithValue("$id", channelId.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RecordPlayedAsync(Guid channelId, CancellationToken cancellationToken = default)
    {
        await using var connection = Open();
        var command = connection.CreateCommand();
        command.CommandText = """
        INSERT INTO history(channel_id, last_played, play_seconds)
        VALUES($id, $now, 0)
        ON CONFLICT(channel_id) DO UPDATE SET last_played=excluded.last_played;
        """;
        command.Parameters.AddWithValue("$id", channelId.ToString());
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetRecentChannelIdsAsync(int limit = 20, CancellationToken cancellationToken = default)
    {
        await using var connection = Open();
        var result = new List<Guid>();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT channel_id FROM history ORDER BY last_played DESC LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            if (Guid.TryParse(reader.GetString(0), out var id))
                result.Add(id);
        return result;
    }

    public async Task ReplaceEpgAsync(IEnumerable<EpgProgram> programs, CancellationToken cancellationToken = default)
    {
        await using var connection = Open();
        using var transaction = connection.BeginTransaction();

        var clear = connection.CreateCommand();
        clear.Transaction = transaction;
        clear.CommandText = "DELETE FROM epg_programs;";
        await clear.ExecuteNonQueryAsync(cancellationToken);

        foreach (var p in programs)
        {
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
            INSERT INTO epg_programs(channel_epg_id, start_utc, end_utc, title, description, category)
            VALUES($channel, $start, $end, $title, $description, $category);
            """;
            command.Parameters.AddWithValue("$channel", p.ChannelEpgId);
            command.Parameters.AddWithValue("$start", p.Start.UtcDateTime.ToString("O"));
            command.Parameters.AddWithValue("$end", p.End.UtcDateTime.ToString("O"));
            command.Parameters.AddWithValue("$title", p.Title);
            command.Parameters.AddWithValue("$description", (object?)p.Description ?? DBNull.Value);
            command.Parameters.AddWithValue("$category", (object?)p.Category ?? DBNull.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        transaction.Commit();
    }

    public async Task<(EpgProgram? Now, EpgProgram? Next)> GetNowNextAsync(string? epgId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(epgId))
            return (null, null);

        var now = DateTimeOffset.UtcNow;
        await using var connection = Open();

        EpgProgram? current = null;
        var currentCommand = connection.CreateCommand();
        currentCommand.CommandText = """
        SELECT channel_epg_id, start_utc, end_utc, title, description, category
        FROM epg_programs
        WHERE channel_epg_id=$id AND start_utc <= $now AND end_utc > $now
        ORDER BY start_utc DESC LIMIT 1;
        """;
        currentCommand.Parameters.AddWithValue("$id", epgId);
        currentCommand.Parameters.AddWithValue("$now", now.UtcDateTime.ToString("O"));
        await using (var reader = await currentCommand.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
                current = ReadProgram(reader);
        }

        EpgProgram? next = null;
        var nextCommand = connection.CreateCommand();
        nextCommand.CommandText = """
        SELECT channel_epg_id, start_utc, end_utc, title, description, category
        FROM epg_programs
        WHERE channel_epg_id=$id AND start_utc > $now
        ORDER BY start_utc LIMIT 1;
        """;
        nextCommand.Parameters.AddWithValue("$id", epgId);
        nextCommand.Parameters.AddWithValue("$now", now.UtcDateTime.ToString("O"));
        await using (var reader = await nextCommand.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
                next = ReadProgram(reader);
        }

        return (current, next);
    }

    public async Task<string?> GetSettingAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var connection = Open();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM settings WHERE key=$key LIMIT 1;";
        command.Parameters.AddWithValue("$key", key);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value as string;
    }

    public async Task SetSettingAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        await using var connection = Open();
        var command = connection.CreateCommand();
        command.CommandText = """
        INSERT INTO settings(key, value) VALUES($key, $value)
        ON CONFLICT(key) DO UPDATE SET value=excluded.value;
        """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", (object?)value ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static EpgProgram ReadProgram(SqliteDataReader reader)
    {
        return new EpgProgram(
            reader.GetString(0),
            DateTimeOffset.Parse(reader.GetString(1)),
            DateTimeOffset.Parse(reader.GetString(2)),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5));
    }
}
