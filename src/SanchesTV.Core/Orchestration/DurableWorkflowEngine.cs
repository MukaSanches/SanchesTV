using System.Collections.Concurrent;

namespace SanchesTV.Core.Orchestration;

public sealed class DurableWorkflowEngine
{
    private readonly IWorkflowStateStore _store;
    private readonly ConcurrentDictionary<string, Func<WorkflowTaskContext, CancellationToken, Task<WorkflowTaskResult>>> _handlers =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _taskTypeGates =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, ExecutionControl> _controls = new();

    public DurableWorkflowEngine(IWorkflowStateStore store)
    {
        _store = store;
    }

    public void RegisterHandler(
        string taskType,
        Func<WorkflowTaskContext, CancellationToken, Task<WorkflowTaskResult>> handler)
    {
        if (string.IsNullOrWhiteSpace(taskType))
            throw new ArgumentException("Task type is required.", nameof(taskType));

        _handlers[taskType] = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        _store.InitializeAsync(cancellationToken);

    public async Task<WorkflowExecutionState> StartAsync(
        WorkflowDefinition definition,
        object? input = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        definition.Validate();

        var now = DateTimeOffset.UtcNow;
        var state = new WorkflowExecutionState
        {
            Id = Guid.NewGuid(),
            Name = definition.Name,
            Version = definition.Version,
            CorrelationId = correlationId,
            Status = WorkflowExecutionStatus.Running,
            InputJson = WorkflowJson.SerializeObject(input),
            CreatedUtc = now,
            UpdatedUtc = now,
            Tasks = definition.Tasks.ToDictionary(
                task => task.ReferenceName,
                task => new WorkflowTaskState
                {
                    ReferenceName = task.ReferenceName,
                    TaskType = task.TaskType,
                    Status = WorkflowTaskStatus.Scheduled
                },
                StringComparer.Ordinal)
        };

        await _store.SaveAsync(definition, state, cancellationToken);
        await _store.AppendEventAsync(
            state.Id, state.Name, "workflow.started", detail: new { state.Version, state.CorrelationId },
            cancellationToken: cancellationToken);

        return await RunAsync(definition, state, cancellationToken);
    }

    public async Task<WorkflowExecutionState?> GetAsync(
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        var stored = await _store.LoadAsync(executionId, cancellationToken);
        return stored?.State;
    }

    public Task<IReadOnlyList<WorkflowEvent>> GetEventsAsync(
        Guid executionId,
        CancellationToken cancellationToken = default) =>
        _store.GetEventsAsync(executionId, cancellationToken);

    public async Task<WorkflowExecutionState> PauseAsync(
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        if (_controls.TryGetValue(executionId, out var control))
        {
            control.PauseRequested = true;
            await _store.AppendEventAsync(
                executionId, control.WorkflowName, "workflow.pause_requested",
                cancellationToken: cancellationToken);
        }

        var stored = await _store.LoadAsync(executionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Workflow execution '{executionId}' was not found.");

        if (stored.State.Status == WorkflowExecutionStatus.Running && !_controls.ContainsKey(executionId))
        {
            stored.State.Status = WorkflowExecutionStatus.Paused;
            Touch(stored.State);
            await _store.SaveAsync(stored.Definition, stored.State, cancellationToken);
            await _store.AppendEventAsync(
                executionId, stored.State.Name, "workflow.paused",
                cancellationToken: cancellationToken);
        }

        return stored.State;
    }

    public async Task<WorkflowExecutionState> ResumeAsync(
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        var stored = await _store.LoadAsync(executionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Workflow execution '{executionId}' was not found.");

        if (stored.State.Status != WorkflowExecutionStatus.Paused)
            return stored.State;

        stored.State.Status = WorkflowExecutionStatus.Running;
        stored.State.CompletedUtc = null;
        stored.State.LastError = null;
        Touch(stored.State);
        await _store.SaveAsync(stored.Definition, stored.State, cancellationToken);
        await _store.AppendEventAsync(
            executionId, stored.State.Name, "workflow.resumed",
            cancellationToken: cancellationToken);

        return await RunAsync(stored.Definition, stored.State, cancellationToken);
    }

    public async Task<WorkflowExecutionState> TerminateAsync(
        Guid executionId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        if (_controls.TryGetValue(executionId, out var control))
        {
            control.TerminateReason = reason ?? "Terminated by request.";
            control.TerminateRequested = true;
            control.CancelActiveTasks();
        }

        var stored = await _store.LoadAsync(executionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Workflow execution '{executionId}' was not found.");

        if (!_controls.ContainsKey(executionId) &&
            stored.State.Status is WorkflowExecutionStatus.Running or WorkflowExecutionStatus.Paused)
        {
            MarkTerminated(stored.State, reason);
            await _store.SaveAsync(stored.Definition, stored.State, cancellationToken);
            await _store.AppendEventAsync(
                executionId, stored.State.Name, "workflow.terminated",
                detail: new { reason = stored.State.LastError }, cancellationToken: cancellationToken);
        }

        return stored.State;
    }

    public async Task<WorkflowExecutionState> RestartAsync(
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        var stored = await _store.LoadAsync(executionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Workflow execution '{executionId}' was not found.");

        await _store.AppendEventAsync(
            executionId, stored.State.Name, "workflow.restart_requested",
            cancellationToken: cancellationToken);

        return await StartAsync(
            stored.Definition,
            ParseOpaqueInput(stored.State.InputJson),
            stored.State.CorrelationId,
            cancellationToken);
    }

    public async Task<WorkflowExecutionState> RetryFailedAsync(
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        var stored = await _store.LoadAsync(executionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Workflow execution '{executionId}' was not found.");

        if (stored.State.Status is not WorkflowExecutionStatus.Failed
            and not WorkflowExecutionStatus.Terminated)
            return stored.State;

        foreach (var task in stored.State.Tasks.Values)
        {
            if (task.Status is WorkflowTaskStatus.Failed
                or WorkflowTaskStatus.TimedOut
                or WorkflowTaskStatus.Canceled
                or WorkflowTaskStatus.InProgress)
            {
                ResetTask(task);
            }
        }

        stored.State.Status = WorkflowExecutionStatus.Running;
        stored.State.CompletedUtc = null;
        stored.State.LastError = null;
        Touch(stored.State);
        await _store.SaveAsync(stored.Definition, stored.State, cancellationToken);
        await _store.AppendEventAsync(
            executionId, stored.State.Name, "workflow.retry_requested",
            cancellationToken: cancellationToken);

        return await RunAsync(stored.Definition, stored.State, cancellationToken);
    }

    public async Task<WorkflowExecutionState> RerunFromAsync(
        Guid executionId,
        string taskReference,
        CancellationToken cancellationToken = default)
    {
        var stored = await _store.LoadAsync(executionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Workflow execution '{executionId}' was not found.");

        if (!stored.State.Tasks.ContainsKey(taskReference))
            throw new KeyNotFoundException($"Task '{taskReference}' does not exist in workflow '{stored.State.Name}'.");

        var affected = FindTaskAndDescendants(stored.Definition, taskReference);
        foreach (var reference in affected)
            ResetTask(stored.State.Tasks[reference]);

        stored.State.Status = WorkflowExecutionStatus.Running;
        stored.State.CompletedUtc = null;
        stored.State.LastError = null;
        Touch(stored.State);
        await _store.SaveAsync(stored.Definition, stored.State, cancellationToken);
        await _store.AppendEventAsync(
            executionId, stored.State.Name, "workflow.rerun_requested",
            taskReference, new { affected = affected.Count }, cancellationToken);

        return await RunAsync(stored.Definition, stored.State, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkflowExecutionState>> RecoverIncompleteAsync(
        CancellationToken cancellationToken = default)
    {
        var storedExecutions = await _store.LoadRecoverableAsync(cancellationToken);
        var result = new List<WorkflowExecutionState>();

        foreach (var stored in storedExecutions)
        {
            foreach (var task in stored.State.Tasks.Values.Where(x => x.Status == WorkflowTaskStatus.InProgress))
            {
                task.Status = WorkflowTaskStatus.Scheduled;
                task.NextAttemptUtc = null;
                task.LastError = "Recovered after process restart; task may be redelivered.";
            }

            Touch(stored.State);
            await _store.SaveAsync(stored.Definition, stored.State, cancellationToken);
            await _store.AppendEventAsync(
                stored.State.Id, stored.State.Name, "workflow.recovered",
                detail: new { note = "In-progress tasks were safely redelivered." },
                cancellationToken: cancellationToken);

            result.Add(await RunAsync(stored.Definition, stored.State, cancellationToken));
        }

        return result;
    }

    public Task PurgeHistoryAsync(
        TimeSpan retention,
        CancellationToken cancellationToken = default)
    {
        if (retention <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(retention));

        return _store.PurgeCompletedBeforeAsync(DateTimeOffset.UtcNow - retention, cancellationToken);
    }

    private async Task<WorkflowExecutionState> RunAsync(
        WorkflowDefinition definition,
        WorkflowExecutionState state,
        CancellationToken cancellationToken)
    {
        definition.Validate();

        var control = _controls.GetOrAdd(state.Id, _ => new ExecutionControl(state.Name));
        var stateGate = new SemaphoreSlim(1, 1);

        try
        {
            while (state.Status == WorkflowExecutionStatus.Running)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (control.TerminateRequested)
                {
                    MarkTerminated(state, control.TerminateReason);
                    await PersistAndEventAsync(
                        definition, state, stateGate, "workflow.terminated",
                        detail: new { reason = state.LastError }, cancellationToken: cancellationToken);
                    break;
                }

                if (control.PauseRequested)
                {
                    state.Status = WorkflowExecutionStatus.Paused;
                    Touch(state);
                    await PersistAndEventAsync(
                        definition, state, stateGate, "workflow.paused",
                        cancellationToken: cancellationToken);
                    break;
                }

                if (TryFinalize(definition, state, out var finalEvent))
                {
                    await PersistAndEventAsync(
                        definition, state, stateGate, finalEvent,
                        detail: state.LastError is null ? null : new { error = state.LastError },
                        cancellationToken: cancellationToken);
                    break;
                }

                var now = DateTimeOffset.UtcNow;
                var runnable = definition.Tasks
                    .Where(task => IsRunnable(task, state, now))
                    .Take(definition.MaxParallelTasks)
                    .ToArray();

                if (runnable.Length == 0)
                {
                    var nextAttempt = state.Tasks.Values
                        .Where(x => x.Status == WorkflowTaskStatus.Scheduled && x.NextAttemptUtc > now)
                        .Select(x => x.NextAttemptUtc!.Value)
                        .OrderBy(x => x)
                        .FirstOrDefault();

                    if (nextAttempt != default)
                    {
                        var delay = nextAttempt - DateTimeOffset.UtcNow;
                        if (delay > TimeSpan.Zero)
                            await Task.Delay(delay > TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : delay, cancellationToken);
                        continue;
                    }

                    state.Status = WorkflowExecutionStatus.Failed;
                    state.LastError = "Workflow reached a deadlock: no runnable task and no retry scheduled.";
                    state.CompletedUtc = DateTimeOffset.UtcNow;
                    CancelPendingTasks(state);
                    Touch(state);
                    await PersistAndEventAsync(
                        definition, state, stateGate, "workflow.failed",
                        detail: new { error = state.LastError }, cancellationToken: cancellationToken);
                    break;
                }

                using var linkedBatch = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, control.ActiveTasks.Token);

                await Task.WhenAll(runnable.Select(task =>
                    ExecuteTaskAsync(definition, state, task, stateGate, linkedBatch.Token)));
            }

            return state;
        }
        catch (OperationCanceledException) when (control.TerminateRequested)
        {
            MarkTerminated(state, control.TerminateReason);
            await PersistAndEventAsync(
                definition, state, stateGate, "workflow.terminated",
                detail: new { reason = state.LastError }, cancellationToken: CancellationToken.None);
            return state;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            state.Status = WorkflowExecutionStatus.Failed;
            state.LastError = SafeError(ex.Message);
            state.CompletedUtc = DateTimeOffset.UtcNow;
            CancelPendingTasks(state);
            Touch(state);
            await PersistAndEventAsync(
                definition, state, stateGate, "workflow.failed",
                detail: new { error = state.LastError }, cancellationToken: CancellationToken.None);
            return state;
        }
        finally
        {
            stateGate.Dispose();
            _controls.TryRemove(state.Id, out _);
            control.Dispose();
        }
    }

    private async Task ExecuteTaskAsync(
        WorkflowDefinition definition,
        WorkflowExecutionState state,
        WorkflowTaskDefinition taskDefinition,
        SemaphoreSlim stateGate,
        CancellationToken cancellationToken)
    {
        if (!_handlers.TryGetValue(taskDefinition.TaskType, out var handler))
        {
            await FailWithoutHandlerAsync(definition, state, taskDefinition, stateGate, cancellationToken);
            return;
        }

        WorkflowTaskState taskState;
        IReadOnlyDictionary<string, string?> outputs;

        await stateGate.WaitAsync(cancellationToken);
        try
        {
            taskState = state.Tasks[taskDefinition.ReferenceName];
            if (taskState.Status != WorkflowTaskStatus.Scheduled)
                return;

            taskState.Status = WorkflowTaskStatus.InProgress;
            taskState.Attempt++;
            taskState.StartedUtc = DateTimeOffset.UtcNow;
            taskState.CompletedUtc = null;
            taskState.NextAttemptUtc = null;
            Touch(state);
            await _store.SaveAsync(definition, state, cancellationToken);
            await _store.AppendEventAsync(
                state.Id, state.Name, "task.started", taskDefinition.ReferenceName,
                new { taskState.Attempt, taskDefinition.TaskType }, cancellationToken);

            outputs = state.Tasks
                .Where(x => x.Value.Status is WorkflowTaskStatus.Completed or WorkflowTaskStatus.CompletedWithErrors)
                .ToDictionary(x => x.Key, x => x.Value.OutputJson, StringComparer.Ordinal);
        }
        finally
        {
            stateGate.Release();
        }

        SemaphoreSlim? typeGate = null;
        if (taskDefinition.ConcurrencyLimit is { } concurrencyLimit)
        {
            typeGate = _taskTypeGates.GetOrAdd(
                $"{taskDefinition.TaskType}:{concurrencyLimit}",
                _ => new SemaphoreSlim(concurrencyLimit, concurrencyLimit));
            await typeGate.WaitAsync(cancellationToken);
        }

        WorkflowTaskResult result;
        var timedOut = false;

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (taskDefinition.Timeout is { } taskTimeout)
                timeout.CancelAfter(taskTimeout);

            var context = new WorkflowTaskContext(
                state.Id,
                state.Name,
                state.Version,
                state.CorrelationId,
                state.InputJson,
                taskDefinition,
                taskState.Attempt,
                outputs);

            try
            {
                result = await handler(context, timeout.Token)
                    ?? WorkflowTaskResult.Failed("Task handler returned no result.");
            }
            catch (OperationCanceledException) when (
                taskDefinition.Timeout is not null &&
                !cancellationToken.IsCancellationRequested)
            {
                timedOut = true;
                result = WorkflowTaskResult.Failed(
                    $"Task exceeded timeout of {taskDefinition.Timeout.Value}.",
                    terminal: false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                result = WorkflowTaskResult.Failed(SafeError(ex.Message));
            }
        }
        finally
        {
            typeGate?.Release();
        }

        await stateGate.WaitAsync(CancellationToken.None);
        try
        {
            taskState = state.Tasks[taskDefinition.ReferenceName];

            if (result.Success)
            {
                taskState.Status = WorkflowTaskStatus.Completed;
                taskState.OutputJson = WorkflowJson.SerializeObject(result.Output);
                taskState.LastError = null;
                taskState.CompletedUtc = DateTimeOffset.UtcNow;
                taskState.NextAttemptUtc = null;
                Touch(state);
                await _store.SaveAsync(definition, state, CancellationToken.None);
                await _store.AppendEventAsync(
                    state.Id, state.Name, "task.completed", taskDefinition.ReferenceName,
                    new { taskState.Attempt }, cancellationToken: CancellationToken.None);
                return;
            }

            var canRetry = !result.Terminal && taskState.Attempt <= taskDefinition.RetryCount;
            if (canRetry)
            {
                taskState.Status = WorkflowTaskStatus.Scheduled;
                taskState.LastError = SafeError(result.Error);
                taskState.CompletedUtc = null;
                taskState.NextAttemptUtc = DateTimeOffset.UtcNow + ComputeRetryDelay(taskDefinition, taskState.Attempt);
                Touch(state);
                await _store.SaveAsync(definition, state, CancellationToken.None);
                await _store.AppendEventAsync(
                    state.Id, state.Name, "task.retry_scheduled", taskDefinition.ReferenceName,
                    new
                    {
                        taskState.Attempt,
                        nextAttemptUtc = taskState.NextAttemptUtc,
                        reason = taskState.LastError,
                        timedOut
                    },
                    cancellationToken: CancellationToken.None);
                return;
            }

            taskState.LastError = SafeError(result.Error);
            taskState.CompletedUtc = DateTimeOffset.UtcNow;
            taskState.NextAttemptUtc = null;
            taskState.OutputJson = WorkflowJson.SerializeObject(result.Output);

            if (taskDefinition.Optional)
            {
                taskState.Status = WorkflowTaskStatus.CompletedWithErrors;
                Touch(state);
                await _store.SaveAsync(definition, state, CancellationToken.None);
                await _store.AppendEventAsync(
                    state.Id, state.Name, "task.completed_with_errors", taskDefinition.ReferenceName,
                    new { taskState.Attempt, error = taskState.LastError, timedOut },
                    cancellationToken: CancellationToken.None);
            }
            else
            {
                taskState.Status = timedOut ? WorkflowTaskStatus.TimedOut : WorkflowTaskStatus.Failed;
                Touch(state);
                await _store.SaveAsync(definition, state, CancellationToken.None);
                await _store.AppendEventAsync(
                    state.Id, state.Name,
                    result.Terminal ? "task.terminal_failed" : timedOut ? "task.timed_out" : "task.failed",
                    taskDefinition.ReferenceName,
                    new { taskState.Attempt, error = taskState.LastError },
                    cancellationToken: CancellationToken.None);
            }
        }
        finally
        {
            stateGate.Release();
        }
    }

    private async Task FailWithoutHandlerAsync(
        WorkflowDefinition definition,
        WorkflowExecutionState state,
        WorkflowTaskDefinition taskDefinition,
        SemaphoreSlim stateGate,
        CancellationToken cancellationToken)
    {
        await stateGate.WaitAsync(cancellationToken);
        try
        {
            var task = state.Tasks[taskDefinition.ReferenceName];
            task.Status = taskDefinition.Optional
                ? WorkflowTaskStatus.CompletedWithErrors
                : WorkflowTaskStatus.Failed;
            task.Attempt++;
            task.StartedUtc = DateTimeOffset.UtcNow;
            task.CompletedUtc = DateTimeOffset.UtcNow;
            task.LastError = $"No handler registered for task type '{taskDefinition.TaskType}'.";
            Touch(state);
            await _store.SaveAsync(definition, state, cancellationToken);
            await _store.AppendEventAsync(
                state.Id, state.Name, "task.handler_missing", taskDefinition.ReferenceName,
                new { taskDefinition.TaskType, taskDefinition.Optional }, cancellationToken);
        }
        finally
        {
            stateGate.Release();
        }
    }

    private static bool IsRunnable(
        WorkflowTaskDefinition definition,
        WorkflowExecutionState state,
        DateTimeOffset now)
    {
        var task = state.Tasks[definition.ReferenceName];
        if (task.Status != WorkflowTaskStatus.Scheduled)
            return false;
        if (task.NextAttemptUtc is { } next && next > now)
            return false;

        foreach (var dependency in definition.DependsOn ?? Array.Empty<string>())
        {
            var dependencyState = state.Tasks[dependency].Status;
            if (dependencyState is not WorkflowTaskStatus.Completed
                and not WorkflowTaskStatus.CompletedWithErrors)
                return false;
        }

        return true;
    }

    private static bool TryFinalize(
        WorkflowDefinition definition,
        WorkflowExecutionState state,
        out string eventName)
    {
        var failedTask = definition.Tasks
            .FirstOrDefault(task =>
            {
                var status = state.Tasks[task.ReferenceName].Status;
                return !task.Optional &&
                       status is WorkflowTaskStatus.Failed
                           or WorkflowTaskStatus.TimedOut
                           or WorkflowTaskStatus.Canceled;
            });

        if (failedTask is not null)
        {
            state.Status = WorkflowExecutionStatus.Failed;
            state.LastError =
                $"Task '{failedTask.ReferenceName}' failed: {state.Tasks[failedTask.ReferenceName].LastError}";
            state.CompletedUtc = DateTimeOffset.UtcNow;
            CancelPendingTasks(state);
            Touch(state);
            eventName = "workflow.failed";
            return true;
        }

        if (state.Tasks.Values.All(task =>
                task.Status is WorkflowTaskStatus.Completed or WorkflowTaskStatus.CompletedWithErrors))
        {
            state.Status = WorkflowExecutionStatus.Completed;
            state.LastError = null;
            state.CompletedUtc = DateTimeOffset.UtcNow;
            Touch(state);
            eventName = "workflow.completed";
            return true;
        }

        eventName = string.Empty;
        return false;
    }

    private async Task PersistAndEventAsync(
        WorkflowDefinition definition,
        WorkflowExecutionState state,
        SemaphoreSlim stateGate,
        string eventName,
        string? taskReference = null,
        object? detail = null,
        CancellationToken cancellationToken = default)
    {
        await stateGate.WaitAsync(cancellationToken);
        try
        {
            await _store.SaveAsync(definition, state, cancellationToken);
            await _store.AppendEventAsync(
                state.Id, state.Name, eventName, taskReference, detail, cancellationToken);
        }
        finally
        {
            stateGate.Release();
        }
    }

    private static TimeSpan ComputeRetryDelay(WorkflowTaskDefinition definition, int attempt)
    {
        var baseDelay = definition.RetryDelay ?? TimeSpan.FromMilliseconds(500);
        var multiplier = definition.RetryLogic switch
        {
            WorkflowRetryLogic.Fixed => 1d,
            WorkflowRetryLogic.LinearBackoff => Math.Max(1, attempt),
            WorkflowRetryLogic.ExponentialBackoff => Math.Pow(2, Math.Max(0, attempt - 1)),
            _ => 1d
        };

        var milliseconds = Math.Min(
            TimeSpan.FromMinutes(5).TotalMilliseconds,
            Math.Max(0, baseDelay.TotalMilliseconds * multiplier));

        if (definition.UseJitter && milliseconds > 0)
            milliseconds *= 0.8 + (Random.Shared.NextDouble() * 0.4);

        return TimeSpan.FromMilliseconds(milliseconds);
    }

    private static HashSet<string> FindTaskAndDescendants(
        WorkflowDefinition definition,
        string taskReference)
    {
        var affected = new HashSet<string>(StringComparer.Ordinal) { taskReference };
        var changed = true;

        while (changed)
        {
            changed = false;
            foreach (var task in definition.Tasks)
            {
                if (affected.Contains(task.ReferenceName))
                    continue;

                if ((task.DependsOn ?? Array.Empty<string>()).Any(affected.Contains))
                {
                    affected.Add(task.ReferenceName);
                    changed = true;
                }
            }
        }

        return affected;
    }

    private static void ResetTask(WorkflowTaskState task)
    {
        task.Status = WorkflowTaskStatus.Scheduled;
        task.Attempt = 0;
        task.OutputJson = null;
        task.LastError = null;
        task.StartedUtc = null;
        task.CompletedUtc = null;
        task.NextAttemptUtc = null;
    }

    private static void CancelPendingTasks(WorkflowExecutionState state)
    {
        foreach (var task in state.Tasks.Values)
        {
            if (task.Status is WorkflowTaskStatus.Scheduled or WorkflowTaskStatus.InProgress)
            {
                task.Status = WorkflowTaskStatus.Canceled;
                task.CompletedUtc = DateTimeOffset.UtcNow;
                task.NextAttemptUtc = null;
            }
        }
    }

    private static void MarkTerminated(WorkflowExecutionState state, string? reason)
    {
        state.Status = WorkflowExecutionStatus.Terminated;
        state.LastError = SafeError(reason ?? "Workflow terminated.");
        state.CompletedUtc = DateTimeOffset.UtcNow;
        CancelPendingTasks(state);
        Touch(state);
    }

    private static void Touch(WorkflowExecutionState state) =>
        state.UpdatedUtc = DateTimeOffset.UtcNow;

    private static string SafeError(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Task failed.";
        return value.Length > 800 ? value[..800] : value;
    }

    private static object? ParseOpaqueInput(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        using var document = System.Text.Json.JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private sealed class ExecutionControl : IDisposable
    {
        public ExecutionControl(string workflowName)
        {
            WorkflowName = workflowName;
        }

        public string WorkflowName { get; }
        public volatile bool PauseRequested;
        public volatile bool TerminateRequested;
        public string? TerminateReason;
        public CancellationTokenSource ActiveTasks { get; private set; } = new();

        public void CancelActiveTasks()
        {
            try { ActiveTasks.Cancel(); } catch { }
        }

        public void Dispose() => ActiveTasks.Dispose();
    }
}
