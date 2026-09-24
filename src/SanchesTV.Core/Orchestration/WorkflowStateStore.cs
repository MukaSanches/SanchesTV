using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace SanchesTV.Core.Orchestration;

public interface IWorkflowStateStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(WorkflowDefinition definition, WorkflowExecutionState state, CancellationToken cancellationToken = default);
    Task<WorkflowStoredExecution?> LoadAsync(Guid executionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowStoredExecution>> LoadRecoverableAsync(CancellationToken cancellationToken = default);
    Task AppendEventAsync(
        Guid executionId,
        string workflowName,
        string eventType,
        string? taskReference = null,
        object? detail = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowEvent>> GetEventsAsync(Guid executionId, CancellationToken cancellationToken = default);
    Task PurgeCompletedBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default);
}

public sealed class SqliteWorkflowStateStore : IWorkflowStateStore
{
    private readonly string _connectionString;

    public SqliteWorkflowStateStore(string? databasePath = null)
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SanchesTV");
        Directory.CreateDirectory(root);

        DatabasePath = databasePath ?? Path.Combine(root, "orchestration.db");
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    public string DatabasePath { get; }

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
        PRAGMA busy_timeout=5000;

        CREATE TABLE IF NOT EXISTS workflow_executions (
            id TEXT PRIMARY KEY,
            name TEXT NOT NULL,
            version INTEGER NOT NULL,
            correlation_id TEXT,
            status INTEGER NOT NULL,
            definition_json TEXT NOT NULL,
            state_json TEXT NOT NULL,
            created_utc TEXT NOT NULL,
            updated_utc TEXT NOT NULL,
            completed_utc TEXT,
            last_error TEXT
        );

        CREATE INDEX IF NOT EXISTS ix_workflow_executions_status_updated
            ON workflow_executions(status, updated_utc);

        CREATE TABLE IF NOT EXISTS workflow_events (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            execution_id TEXT NOT NULL,
            workflow_name TEXT NOT NULL,
            event_type TEXT NOT NULL,
            task_reference TEXT,
            created_utc TEXT NOT NULL,
            detail_json TEXT,
            FOREIGN KEY(execution_id) REFERENCES workflow_executions(id) ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS ix_workflow_events_execution_time
            ON workflow_events(execution_id, created_utc, id);
        """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SaveAsync(
        WorkflowDefinition definition,
        WorkflowExecutionState state,
        CancellationToken cancellationToken = default)
    {
        await using var connection = Open();
        var command = connection.CreateCommand();
        command.CommandText = """
        INSERT INTO workflow_executions
            (id, name, version, correlation_id, status, definition_json, state_json,
             created_utc, updated_utc, completed_utc, last_error)
        VALUES
            ($id, $name, $version, $correlation, $status, $definition, $state,
             $created, $updated, $completed, $error)
        ON CONFLICT(id) DO UPDATE SET
            name=excluded.name,
            version=excluded.version,
            correlation_id=excluded.correlation_id,
            status=excluded.status,
            definition_json=excluded.definition_json,
            state_json=excluded.state_json,
            updated_utc=excluded.updated_utc,
            completed_utc=excluded.completed_utc,
            last_error=excluded.last_error;
        """;
        command.Parameters.AddWithValue("$id", state.Id.ToString());
        command.Parameters.AddWithValue("$name", state.Name);
        command.Parameters.AddWithValue("$version", state.Version);
        command.Parameters.AddWithValue("$correlation", (object?)state.CorrelationId ?? DBNull.Value);
        command.Parameters.AddWithValue("$status", (int)state.Status);
        command.Parameters.AddWithValue("$definition", JsonSerializer.Serialize(definition, WorkflowJson.Options));
        command.Parameters.AddWithValue("$state", JsonSerializer.Serialize(state, WorkflowJson.Options));
        command.Parameters.AddWithValue("$created", state.CreatedUtc.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$updated", state.UpdatedUtc.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$completed",
            state.CompletedUtc is null ? DBNull.Value : state.CompletedUtc.Value.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$error", (object?)state.LastError ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<WorkflowStoredExecution?> LoadAsync(
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = Open();
        var command = connection.CreateCommand();
        command.CommandText = """
        SELECT definition_json, state_json
        FROM workflow_executions
        WHERE id=$id
        LIMIT 1;
        """;
        command.Parameters.AddWithValue("$id", executionId.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return DeserializeStored(reader.GetString(0), reader.GetString(1));
    }

    public async Task<IReadOnlyList<WorkflowStoredExecution>> LoadRecoverableAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = Open();
        var result = new List<WorkflowStoredExecution>();
        var command = connection.CreateCommand();
        command.CommandText = """
        SELECT definition_json, state_json
        FROM workflow_executions
        WHERE status=$running
        ORDER BY updated_utc;
        """;
        command.Parameters.AddWithValue("$running", (int)WorkflowExecutionStatus.Running);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var stored = DeserializeStored(reader.GetString(0), reader.GetString(1));
            if (stored is not null)
                result.Add(stored);
        }

        return result;
    }

    public async Task AppendEventAsync(
        Guid executionId,
        string workflowName,
        string eventType,
        string? taskReference = null,
        object? detail = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = Open();
        var command = connection.CreateCommand();
        command.CommandText = """
        INSERT INTO workflow_events
            (execution_id, workflow_name, event_type, task_reference, created_utc, detail_json)
        VALUES
            ($execution, $workflow, $event, $task, $created, $detail);
        """;
        command.Parameters.AddWithValue("$execution", executionId.ToString());
        command.Parameters.AddWithValue("$workflow", workflowName);
        command.Parameters.AddWithValue("$event", eventType);
        command.Parameters.AddWithValue("$task", (object?)taskReference ?? DBNull.Value);
        command.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$detail", (object?)WorkflowJson.SerializeObject(detail) ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkflowEvent>> GetEventsAsync(
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = Open();
        var result = new List<WorkflowEvent>();
        var command = connection.CreateCommand();
        command.CommandText = """
        SELECT id, execution_id, workflow_name, event_type, task_reference, created_utc, detail_json
        FROM workflow_events
        WHERE execution_id=$execution
        ORDER BY id;
        """;
        command.Parameters.AddWithValue("$execution", executionId.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new WorkflowEvent(
                reader.GetInt64(0),
                Guid.Parse(reader.GetString(1)),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                DateTimeOffset.Parse(reader.GetString(5)),
                reader.IsDBNull(6) ? null : reader.GetString(6)));
        }

        return result;
    }

    public async Task PurgeCompletedBeforeAsync(
        DateTimeOffset cutoff,
        CancellationToken cancellationToken = default)
    {
        await using var connection = Open();
        var command = connection.CreateCommand();
        command.CommandText = """
        DELETE FROM workflow_executions
        WHERE status IN ($completed, $failed, $terminated)
          AND completed_utc IS NOT NULL
          AND completed_utc < $cutoff;
        """;
        command.Parameters.AddWithValue("$completed", (int)WorkflowExecutionStatus.Completed);
        command.Parameters.AddWithValue("$failed", (int)WorkflowExecutionStatus.Failed);
        command.Parameters.AddWithValue("$terminated", (int)WorkflowExecutionStatus.Terminated);
        command.Parameters.AddWithValue("$cutoff", cutoff.UtcDateTime.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static WorkflowStoredExecution? DeserializeStored(string definitionJson, string stateJson)
    {
        var definition = JsonSerializer.Deserialize<WorkflowDefinition>(definitionJson, WorkflowJson.Options);
        var state = JsonSerializer.Deserialize<WorkflowExecutionState>(stateJson, WorkflowJson.Options);
        if (definition is null || state is null)
            return null;

        state.Tasks = new Dictionary<string, WorkflowTaskState>(state.Tasks, StringComparer.Ordinal);
        return new WorkflowStoredExecution(definition, state);
    }
}

public sealed class InMemoryWorkflowStateStore : IWorkflowStateStore
{
    private readonly ConcurrentDictionary<Guid, WorkflowStoredExecution> _executions = new();
    private readonly ConcurrentDictionary<Guid, List<WorkflowEvent>> _events = new();
    private long _nextEventId;

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SaveAsync(
        WorkflowDefinition definition,
        WorkflowExecutionState state,
        CancellationToken cancellationToken = default)
    {
        _executions[state.Id] = Clone(new WorkflowStoredExecution(definition, state));
        return Task.CompletedTask;
    }

    public Task<WorkflowStoredExecution?> LoadAsync(
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            _executions.TryGetValue(executionId, out var stored)
                ? Clone(stored)
                : null);
    }

    public Task<IReadOnlyList<WorkflowStoredExecution>> LoadRecoverableAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<WorkflowStoredExecution> result = _executions.Values
            .Where(x => x.State.Status == WorkflowExecutionStatus.Running)
            .OrderBy(x => x.State.UpdatedUtc)
            .Select(Clone)
            .ToArray();
        return Task.FromResult(result);
    }

    public Task AppendEventAsync(
        Guid executionId,
        string workflowName,
        string eventType,
        string? taskReference = null,
        object? detail = null,
        CancellationToken cancellationToken = default)
    {
        var list = _events.GetOrAdd(executionId, _ => []);
        lock (list)
        {
            list.Add(new WorkflowEvent(
                Interlocked.Increment(ref _nextEventId),
                executionId,
                workflowName,
                eventType,
                taskReference,
                DateTimeOffset.UtcNow,
                WorkflowJson.SerializeObject(detail)));
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<WorkflowEvent>> GetEventsAsync(
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        if (!_events.TryGetValue(executionId, out var list))
            return Task.FromResult<IReadOnlyList<WorkflowEvent>>(Array.Empty<WorkflowEvent>());

        lock (list)
        {
            return Task.FromResult<IReadOnlyList<WorkflowEvent>>(list.ToArray());
        }
    }

    public Task PurgeCompletedBeforeAsync(
        DateTimeOffset cutoff,
        CancellationToken cancellationToken = default)
    {
        foreach (var pair in _executions)
        {
            var state = pair.Value.State;
            if (state.Status is WorkflowExecutionStatus.Completed
                or WorkflowExecutionStatus.Failed
                or WorkflowExecutionStatus.Terminated &&
                state.CompletedUtc is { } completed &&
                completed < cutoff)
            {
                _executions.TryRemove(pair.Key, out _);
                _events.TryRemove(pair.Key, out _);
            }
        }

        return Task.CompletedTask;
    }

    private static WorkflowStoredExecution Clone(WorkflowStoredExecution stored)
    {
        var json = JsonSerializer.Serialize(stored, WorkflowJson.Options);
        return JsonSerializer.Deserialize<WorkflowStoredExecution>(json, WorkflowJson.Options)
            ?? throw new InvalidOperationException("Could not clone workflow state.");
    }
}
