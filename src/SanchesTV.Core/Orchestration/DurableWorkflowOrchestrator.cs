using System.Text.Json;

namespace SanchesTV.Core.Orchestration;

public enum WorkflowRunStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}

public enum WorkflowTaskStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    TimedOut,
    Cancelled
}

public sealed record WorkflowTaskDefinition(
    string Name,
    Func<CancellationToken, Task> Handler,
    IReadOnlyList<string>? DependsOn = null,
    int MaxAttempts = 3,
    TimeSpan? Timeout = null,
    TimeSpan? RetryDelay = null,
    TimeSpan? MaxRetryDelay = null,
    int RetryJitterMilliseconds = 250);

public sealed record WorkflowDefinition(
    string Name,
    int Version,
    IReadOnlyList<WorkflowTaskDefinition> Tasks,
    int MaxConcurrency = 4);

public sealed class WorkflowTaskSnapshot
{
    public string Name { get; set; } = string.Empty;
    public WorkflowTaskStatus Status { get; set; } = WorkflowTaskStatus.Pending;
    public int Attempts { get; set; }
    public DateTimeOffset? StartedUtc { get; set; }
    public DateTimeOffset? CompletedUtc { get; set; }
    public string? LastError { get; set; }
}

public sealed class WorkflowRunSnapshot
{
    public string RunId { get; set; } = string.Empty;
    public string WorkflowName { get; set; } = string.Empty;
    public int WorkflowVersion { get; set; }
    public WorkflowRunStatus Status { get; set; } = WorkflowRunStatus.Pending;
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? LastError { get; set; }
    public Dictionary<string, WorkflowTaskSnapshot> Tasks { get; set; } = new(StringComparer.Ordinal);
}

public sealed record WorkflowEvent(
    string RunId,
    string WorkflowName,
    string? TaskName,
    WorkflowRunStatus WorkflowStatus,
    WorkflowTaskStatus? TaskStatus,
    DateTimeOffset TimestampUtc,
    string? Message = null);

public sealed class DurableWorkflowOrchestrator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _stateDirectory;
    private readonly SemaphoreSlim _fileGate = new(1, 1);
    private readonly object _snapshotGate = new();

    public event EventHandler<WorkflowEvent>? EventPublished;

    public DurableWorkflowOrchestrator(string? stateDirectory = null)
    {
        _stateDirectory = stateDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SanchesTV",
            "workflows");
        Directory.CreateDirectory(_stateDirectory);
    }

    public async Task<WorkflowRunSnapshot> RunAsync(
        WorkflowDefinition definition,
        string? runId = null,
        CancellationToken cancellationToken = default)
    {
        Validate(definition);
        runId ??= $"{Sanitize(definition.Name)}-{Guid.NewGuid():N}";

        var snapshot = await LoadAsync(runId, cancellationToken)
            ?? CreateSnapshot(definition, runId);

        if (!string.Equals(snapshot.WorkflowName, definition.Name, StringComparison.Ordinal) ||
            snapshot.WorkflowVersion != definition.Version)
            throw new InvalidOperationException("A definição do workflow não corresponde ao estado persistido.");

        EnsureTaskSnapshots(definition, snapshot);

        if (snapshot.Status == WorkflowRunStatus.Completed)
            return snapshot;

        lock (_snapshotGate)
        {
            snapshot.Status = WorkflowRunStatus.Running;
            snapshot.LastError = null;
            snapshot.UpdatedUtc = DateTimeOffset.UtcNow;

            foreach (var task in snapshot.Tasks.Values)
            {
                if (task.Status is WorkflowTaskStatus.Running or WorkflowTaskStatus.Failed or WorkflowTaskStatus.TimedOut or WorkflowTaskStatus.Cancelled)
                {
                    task.Status = WorkflowTaskStatus.Pending;
                    task.CompletedUtc = null;
                }
            }
        }

        await PersistAsync(snapshot, cancellationToken);
        Publish(snapshot, null, null, "workflow.started");

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                List<WorkflowTaskDefinition> ready;
                lock (_snapshotGate)
                {
                    if (snapshot.Tasks.Values.All(x => x.Status == WorkflowTaskStatus.Completed))
                    {
                        snapshot.Status = WorkflowRunStatus.Completed;
                        snapshot.UpdatedUtc = DateTimeOffset.UtcNow;
                        ready = [];
                    }
                    else
                    {
                        ready = definition.Tasks
                            .Where(task =>
                            {
                                var state = snapshot.Tasks[task.Name];
                                if (state.Status != WorkflowTaskStatus.Pending)
                                    return false;

                                var deps = task.DependsOn ?? Array.Empty<string>();
                                return deps.All(dep => snapshot.Tasks[dep].Status == WorkflowTaskStatus.Completed);
                            })
                            .ToList();
                    }
                }

                if (snapshot.Status == WorkflowRunStatus.Completed)
                {
                    await PersistAsync(snapshot, cancellationToken);
                    Publish(snapshot, null, null, "workflow.completed");
                    return snapshot;
                }

                if (ready.Count == 0)
                {
                    var blocked = snapshot.Tasks.Values
                        .Where(x => x.Status != WorkflowTaskStatus.Completed)
                        .Select(x => x.Name);
                    throw new InvalidOperationException(
                        "Workflow bloqueado por dependência inválida ou ciclo: " + string.Join(", ", blocked));
                }

                foreach (var batch in ready.Chunk(Math.Max(1, definition.MaxConcurrency)))
                    await Task.WhenAll(batch.Select(task => ExecuteTaskAsync(definition, task, snapshot, cancellationToken)));
            }
        }
        catch (OperationCanceledException)
        {
            lock (_snapshotGate)
            {
                snapshot.Status = WorkflowRunStatus.Cancelled;
                snapshot.UpdatedUtc = DateTimeOffset.UtcNow;
                snapshot.LastError = "Execução cancelada.";
            }
            await PersistAsync(snapshot, CancellationToken.None);
            Publish(snapshot, null, null, "workflow.cancelled");
            throw;
        }
        catch (Exception ex)
        {
            lock (_snapshotGate)
            {
                snapshot.Status = WorkflowRunStatus.Failed;
                snapshot.UpdatedUtc = DateTimeOffset.UtcNow;
                snapshot.LastError = ex.Message;
            }
            await PersistAsync(snapshot, CancellationToken.None);
            Publish(snapshot, null, null, ex.Message);
            throw;
        }
    }

    private async Task ExecuteTaskAsync(
        WorkflowDefinition definition,
        WorkflowTaskDefinition task,
        WorkflowRunSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var state = snapshot.Tasks[task.Name];
        var maxAttempts = Math.Max(1, task.MaxAttempts);
        var timeout = task.Timeout ?? TimeSpan.FromSeconds(60);
        var baseDelay = task.RetryDelay ?? TimeSpan.FromSeconds(1);
        var maxDelay = task.MaxRetryDelay ?? TimeSpan.FromSeconds(30);

        for (var attempt = state.Attempts + 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_snapshotGate)
            {
                state.Status = WorkflowTaskStatus.Running;
                state.Attempts = attempt;
                state.StartedUtc ??= DateTimeOffset.UtcNow;
                state.CompletedUtc = null;
                state.LastError = null;
                snapshot.UpdatedUtc = DateTimeOffset.UtcNow;
            }
            await PersistAsync(snapshot, cancellationToken);
            Publish(snapshot, task.Name, WorkflowTaskStatus.Running, $"task.attempt.{attempt}");

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);

            try
            {
                await task.Handler(timeoutCts.Token);
                lock (_snapshotGate)
                {
                    state.Status = WorkflowTaskStatus.Completed;
                    state.CompletedUtc = DateTimeOffset.UtcNow;
                    state.LastError = null;
                    snapshot.UpdatedUtc = DateTimeOffset.UtcNow;
                }
                await PersistAsync(snapshot, cancellationToken);
                Publish(snapshot, task.Name, WorkflowTaskStatus.Completed, "task.completed");
                return;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
            {
                lock (_snapshotGate)
                {
                    state.Status = WorkflowTaskStatus.TimedOut;
                    state.LastError = $"Tempo limite de {timeout.TotalSeconds:N0}s excedido.";
                    snapshot.UpdatedUtc = DateTimeOffset.UtcNow;
                }
                await PersistAsync(snapshot, CancellationToken.None);
                Publish(snapshot, task.Name, WorkflowTaskStatus.TimedOut, state.LastError);
            }
            catch (OperationCanceledException)
            {
                lock (_snapshotGate)
                {
                    state.Status = WorkflowTaskStatus.Cancelled;
                    state.LastError = "Tarefa cancelada.";
                    snapshot.UpdatedUtc = DateTimeOffset.UtcNow;
                }
                await PersistAsync(snapshot, CancellationToken.None);
                Publish(snapshot, task.Name, WorkflowTaskStatus.Cancelled, state.LastError);
                throw;
            }
            catch (Exception ex)
            {
                lock (_snapshotGate)
                {
                    state.Status = WorkflowTaskStatus.Failed;
                    state.LastError = ex.Message;
                    snapshot.UpdatedUtc = DateTimeOffset.UtcNow;
                }
                await PersistAsync(snapshot, CancellationToken.None);
                Publish(snapshot, task.Name, WorkflowTaskStatus.Failed, ex.Message);
            }

            if (attempt >= maxAttempts)
                throw new InvalidOperationException(
                    $"A tarefa '{task.Name}' falhou após {attempt} tentativa(s): {state.LastError}");

            lock (_snapshotGate)
                state.Status = WorkflowTaskStatus.Pending;

            var exponent = Math.Max(0, attempt - 1);
            var delayMs = Math.Min(
                maxDelay.TotalMilliseconds,
                baseDelay.TotalMilliseconds * Math.Pow(2, exponent));
            delayMs += Random.Shared.Next(0, Math.Max(1, task.RetryJitterMilliseconds + 1));

            Publish(snapshot, task.Name, WorkflowTaskStatus.Pending, $"task.retry_in_ms.{delayMs:N0}");
            await Task.Delay(TimeSpan.FromMilliseconds(delayMs), cancellationToken);
        }
    }

    private WorkflowRunSnapshot CreateSnapshot(WorkflowDefinition definition, string runId)
    {
        var now = DateTimeOffset.UtcNow;
        return new WorkflowRunSnapshot
        {
            RunId = runId,
            WorkflowName = definition.Name,
            WorkflowVersion = definition.Version,
            Status = WorkflowRunStatus.Pending,
            CreatedUtc = now,
            UpdatedUtc = now,
            Tasks = definition.Tasks.ToDictionary(
                x => x.Name,
                x => new WorkflowTaskSnapshot { Name = x.Name },
                StringComparer.Ordinal)
        };
    }

    private static void EnsureTaskSnapshots(WorkflowDefinition definition, WorkflowRunSnapshot snapshot)
    {
        foreach (var task in definition.Tasks)
            snapshot.Tasks.TryAdd(task.Name, new WorkflowTaskSnapshot { Name = task.Name });
    }

    private async Task<WorkflowRunSnapshot?> LoadAsync(string runId, CancellationToken cancellationToken)
    {
        var path = StatePath(runId);
        if (!File.Exists(path))
            return null;

        await _fileGate.WaitAsync(cancellationToken);
        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<WorkflowRunSnapshot>(stream, JsonOptions, cancellationToken);
        }
        catch
        {
            return null;
        }
        finally
        {
            _fileGate.Release();
        }
    }

    private async Task PersistAsync(WorkflowRunSnapshot snapshot, CancellationToken cancellationToken)
    {
        string json;
        lock (_snapshotGate)
        {
            snapshot.UpdatedUtc = DateTimeOffset.UtcNow;
            json = JsonSerializer.Serialize(snapshot, JsonOptions);
        }

        await _fileGate.WaitAsync(cancellationToken);
        try
        {
            var path = StatePath(snapshot.RunId);
            var temp = path + ".tmp";
            await File.WriteAllTextAsync(temp, json, cancellationToken);
            File.Move(temp, path, true);
        }
        finally
        {
            _fileGate.Release();
        }
    }

    private void Publish(
        WorkflowRunSnapshot snapshot,
        string? taskName,
        WorkflowTaskStatus? taskStatus,
        string? message)
    {
        EventPublished?.Invoke(this, new WorkflowEvent(
            snapshot.RunId,
            snapshot.WorkflowName,
            taskName,
            snapshot.Status,
            taskStatus,
            DateTimeOffset.UtcNow,
            message));
    }

    private string StatePath(string runId) => Path.Combine(_stateDirectory, Sanitize(runId) + ".json");

    private static string Sanitize(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '-');
        return value;
    }

    private static void Validate(WorkflowDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Name))
            throw new ArgumentException("Workflow precisa de nome.", nameof(definition));
        if (definition.Tasks.Count == 0)
            throw new ArgumentException("Workflow precisa de pelo menos uma tarefa.", nameof(definition));

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var task in definition.Tasks)
        {
            if (string.IsNullOrWhiteSpace(task.Name) || !names.Add(task.Name))
                throw new ArgumentException($"Nome de tarefa inválido ou duplicado: '{task.Name}'.", nameof(definition));
        }

        foreach (var task in definition.Tasks)
        {
            foreach (var dependency in task.DependsOn ?? Array.Empty<string>())
            {
                if (!names.Contains(dependency))
                    throw new ArgumentException(
                        $"A tarefa '{task.Name}' depende de '{dependency}', que não existe.",
                        nameof(definition));
                if (string.Equals(task.Name, dependency, StringComparison.Ordinal))
                    throw new ArgumentException($"A tarefa '{task.Name}' não pode depender de si mesma.", nameof(definition));
            }
        }
    }
}
