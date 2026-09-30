using Microsoft.Data.Sqlite;
using SanchesTV.Core.Orchestration;
using Xunit;

namespace SanchesTV.Tests;

public sealed class SqliteWorkflowStateStoreTests
{
    [Fact]
    public async Task ReopenedDatabasePreservesDefinitionStateAndEventHistory()
    {
        using var database = new TemporaryDatabase();
        var store = new SqliteWorkflowStateStore(database.Path);
        await store.InitializeAsync();

        var definition = new WorkflowDefinition("startup", 3,
        [
            new WorkflowTaskDefinition("database", "initialize", RetryCount: 1),
            new WorkflowTaskDefinition("home", "render", ["database"], Optional: true)
        ]);
        var state = CreateState(definition, WorkflowExecutionStatus.Running, DateTimeOffset.UtcNow);
        state.CorrelationId = "startup-session";
        state.InputJson = "{\"theme\":\"cinema\"}";
        state.Tasks["database"].Status = WorkflowTaskStatus.Completed;
        state.Tasks["database"].Attempt = 2;
        state.Tasks["database"].OutputJson = "{\"channels\":42}";
        state.Tasks["home"].Status = WorkflowTaskStatus.InProgress;

        await store.SaveAsync(definition, state);
        await store.AppendEventAsync(state.Id, state.Name, "task.completed", "database", new { channels = 42 });
        await store.AppendEventAsync(state.Id, state.Name, "task.started", "home");
        database.ClosePools();

        var reopened = new SqliteWorkflowStateStore(database.Path);
        var stored = await reopened.LoadAsync(state.Id);
        Assert.NotNull(stored);
        Assert.Equal("startup", stored.Definition.Name);
        Assert.Equal(3, stored.Definition.Version);
        Assert.Equal(["database"], stored.Definition.Tasks[1].DependsOn);
        Assert.True(stored.Definition.Tasks[1].Optional);
        Assert.Equal(state.CorrelationId, stored.State.CorrelationId);
        Assert.Equal(state.InputJson, stored.State.InputJson);
        Assert.Equal(2, stored.State.Tasks["database"].Attempt);
        Assert.Equal(state.Tasks["database"].OutputJson, stored.State.Tasks["database"].OutputJson);
        Assert.Equal(WorkflowTaskStatus.InProgress, stored.State.Tasks["home"].Status);

        var recoverable = Assert.Single(await reopened.LoadRecoverableAsync());
        Assert.Equal(state.Id, recoverable.State.Id);
        var events = await reopened.GetEventsAsync(state.Id);
        Assert.Collection(events,
            completed =>
            {
                Assert.Equal("task.completed", completed.EventType);
                Assert.Equal("database", completed.TaskReference);
                Assert.Equal("{\"channels\":42}", completed.DetailJson);
            },
            started => Assert.Equal("task.started", started.EventType));
        Assert.True(events[0].Id < events[1].Id);
    }

    [Theory]
    [InlineData(WorkflowExecutionStatus.Completed)]
    [InlineData(WorkflowExecutionStatus.Failed)]
    [InlineData(WorkflowExecutionStatus.Terminated)]
    public async Task RetentionDeletesExpiredExecutionAndEventsButKeepsRecentAndOpenWorkflows(
        WorkflowExecutionStatus finishedStatus)
    {
        using var database = new TemporaryDatabase();
        var store = new SqliteWorkflowStateStore(database.Path);
        await store.InitializeAsync();

        var now = DateTimeOffset.UtcNow;
        var definition = new WorkflowDefinition("retention", 1, [new WorkflowTaskDefinition("work", "noop")]);
        var expired = CreateState(definition, finishedStatus, now.AddDays(-30));
        var recent = CreateState(definition, finishedStatus, now.AddDays(-1));
        var running = CreateState(definition, WorkflowExecutionStatus.Running, now.AddDays(-30));
        var paused = CreateState(definition, WorkflowExecutionStatus.Paused, now.AddDays(-30));

        foreach (var state in new[] { expired, recent, running, paused })
        {
            await store.SaveAsync(definition, state);
            await store.AppendEventAsync(state.Id, state.Name, "workflow.saved");
        }
        database.ClosePools();

        var reopened = new SqliteWorkflowStateStore(database.Path);
        await reopened.PurgeCompletedBeforeAsync(now.AddDays(-14));

        Assert.Null(await reopened.LoadAsync(expired.Id));
        Assert.Empty(await reopened.GetEventsAsync(expired.Id));
        foreach (var state in new[] { recent, running, paused })
        {
            Assert.NotNull(await reopened.LoadAsync(state.Id));
            Assert.Single(await reopened.GetEventsAsync(state.Id));
        }
        Assert.Equal(running.Id, Assert.Single(await reopened.LoadRecoverableAsync()).State.Id);
    }

    [Fact]
    public async Task ReopenedDatabaseRejectsEventsWithoutAnExecution()
    {
        using var database = new TemporaryDatabase();
        var store = new SqliteWorkflowStateStore(database.Path);
        await store.InitializeAsync();
        database.ClosePools();

        var reopened = new SqliteWorkflowStateStore(database.Path);
        var exception = await Assert.ThrowsAsync<SqliteException>(() =>
            reopened.AppendEventAsync(Guid.NewGuid(), "missing", "task.started"));

        Assert.Equal(19, exception.SqliteErrorCode);
        Assert.Equal(787, exception.SqliteExtendedErrorCode);
    }

    private static WorkflowExecutionState CreateState(
        WorkflowDefinition definition,
        WorkflowExecutionStatus status,
        DateTimeOffset updatedUtc) => new()
    {
        Id = Guid.NewGuid(),
        Name = definition.Name,
        Version = definition.Version,
        Status = status,
        CreatedUtc = updatedUtc.AddMinutes(-1),
        UpdatedUtc = updatedUtc,
        CompletedUtc = status is WorkflowExecutionStatus.Completed or WorkflowExecutionStatus.Failed or WorkflowExecutionStatus.Terminated
            ? updatedUtc
            : null,
        Tasks = definition.Tasks.ToDictionary(task => task.ReferenceName, task => new WorkflowTaskState
        {
            ReferenceName = task.ReferenceName,
            TaskType = task.TaskType
        }, StringComparer.Ordinal)
    };

    private sealed class TemporaryDatabase : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"sanchestv-workflow-{Guid.NewGuid():N}.db");

        public void ClosePools()
        {
            // Release only this test database; other tests may use SQLite concurrently.
            foreach (var foreignKeys in new bool?[] { null, true })
            {
                using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
                {
                    DataSource = Path,
                    Mode = SqliteOpenMode.ReadWriteCreate,
                    ForeignKeys = foreignKeys
                }.ToString());
                SqliteConnection.ClearPool(connection);
            }
        }

        public void Dispose()
        {
            ClosePools();
            foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
                File.Delete(Path + suffix);
        }
    }
}
