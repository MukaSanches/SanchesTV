using System.Text.Json;
using System.Text.Json.Serialization;

namespace SanchesTV.Core.Orchestration;

public enum WorkflowExecutionStatus
{
    Running,
    Paused,
    Completed,
    Failed,
    Terminated
}

public enum WorkflowTaskStatus
{
    Scheduled,
    InProgress,
    Completed,
    CompletedWithErrors,
    Failed,
    TimedOut,
    Canceled
}

public enum WorkflowRetryLogic
{
    Fixed,
    LinearBackoff,
    ExponentialBackoff
}

public sealed record WorkflowTaskDefinition(
    string ReferenceName,
    string TaskType,
    IReadOnlyList<string>? DependsOn = null,
    int RetryCount = 2,
    TimeSpan? RetryDelay = null,
    WorkflowRetryLogic RetryLogic = WorkflowRetryLogic.ExponentialBackoff,
    TimeSpan? Timeout = null,
    bool Optional = false,
    string? InputJson = null,
    int? ConcurrencyLimit = null,
    bool UseJitter = true);

public sealed record WorkflowDefinition(
    string Name,
    int Version,
    IReadOnlyList<WorkflowTaskDefinition> Tasks,
    int MaxParallelTasks = 4)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
            throw new InvalidOperationException("Workflow name is required.");
        if (Version < 1)
            throw new InvalidOperationException("Workflow version must be at least 1.");
        if (MaxParallelTasks is < 1 or > 64)
            throw new InvalidOperationException("MaxParallelTasks must be between 1 and 64.");
        if (Tasks.Count == 0)
            throw new InvalidOperationException("A workflow must contain at least one task.");

        var map = new Dictionary<string, WorkflowTaskDefinition>(StringComparer.Ordinal);
        foreach (var task in Tasks)
        {
            if (string.IsNullOrWhiteSpace(task.ReferenceName))
                throw new InvalidOperationException("Every task requires a reference name.");
            if (string.IsNullOrWhiteSpace(task.TaskType))
                throw new InvalidOperationException($"Task '{task.ReferenceName}' requires a task type.");
            if (!map.TryAdd(task.ReferenceName, task))
                throw new InvalidOperationException($"Duplicate task reference '{task.ReferenceName}'.");
            if (task.RetryCount is < 0 or > 20)
                throw new InvalidOperationException($"Task '{task.ReferenceName}' RetryCount must be between 0 and 20.");
            if (task.Timeout is { } timeout && timeout <= TimeSpan.Zero)
                throw new InvalidOperationException($"Task '{task.ReferenceName}' timeout must be positive.");
            if (task.RetryDelay is { } retryDelay && retryDelay < TimeSpan.Zero)
                throw new InvalidOperationException($"Task '{task.ReferenceName}' retry delay cannot be negative.");
            if (task.ConcurrencyLimit is <= 0)
                throw new InvalidOperationException($"Task '{task.ReferenceName}' concurrency limit must be positive.");
        }

        foreach (var task in Tasks)
        {
            foreach (var dependency in task.DependsOn ?? Array.Empty<string>())
            {
                if (!map.ContainsKey(dependency))
                    throw new InvalidOperationException(
                        $"Task '{task.ReferenceName}' depends on missing task '{dependency}'.");
                if (string.Equals(task.ReferenceName, dependency, StringComparison.Ordinal))
                    throw new InvalidOperationException($"Task '{task.ReferenceName}' cannot depend on itself.");
            }
        }

        var colors = new Dictionary<string, byte>(StringComparer.Ordinal);

        void Visit(string reference)
        {
            colors.TryGetValue(reference, out var color);
            if (color == 1)
                throw new InvalidOperationException($"Workflow '{Name}' contains a dependency cycle at '{reference}'.");
            if (color == 2)
                return;

            colors[reference] = 1;
            foreach (var dependency in map[reference].DependsOn ?? Array.Empty<string>())
                Visit(dependency);
            colors[reference] = 2;
        }

        foreach (var task in Tasks)
            Visit(task.ReferenceName);
    }

    public IReadOnlyList<IReadOnlyList<string>> BuildExecutionLayers()
    {
        Validate();

        var remaining = Tasks.ToDictionary(
            task => task.ReferenceName,
            task => new HashSet<string>(task.DependsOn ?? Array.Empty<string>(), StringComparer.Ordinal),
            StringComparer.Ordinal);

        var layers = new List<IReadOnlyList<string>>();
        while (remaining.Count > 0)
        {
            var ready = Tasks
                .Where(task => remaining.TryGetValue(task.ReferenceName, out var dependencies) && dependencies.Count == 0)
                .Select(task => task.ReferenceName)
                .ToArray();

            if (ready.Length == 0)
                throw new InvalidOperationException($"Workflow '{Name}' cannot be topologically ordered.");

            layers.Add(ready);

            foreach (var reference in ready)
                remaining.Remove(reference);

            foreach (var dependencies in remaining.Values)
                dependencies.ExceptWith(ready);
        }

        return layers;
    }
}

public sealed record WorkflowTaskResult(
    bool Success,
    object? Output = null,
    string? Error = null,
    bool Terminal = false)
{
    public static WorkflowTaskResult Completed(object? output = null) =>
        new(true, output);

    public static WorkflowTaskResult Failed(string error, bool terminal = false, object? output = null) =>
        new(false, output, error, terminal);
}

public sealed class WorkflowTaskState
{
    public string ReferenceName { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;
    public WorkflowTaskStatus Status { get; set; } = WorkflowTaskStatus.Scheduled;
    public int Attempt { get; set; }
    public string? OutputJson { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? StartedUtc { get; set; }
    public DateTimeOffset? CompletedUtc { get; set; }
    public DateTimeOffset? NextAttemptUtc { get; set; }
}

public sealed class WorkflowExecutionState
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
    public string? CorrelationId { get; set; }
    public WorkflowExecutionStatus Status { get; set; } = WorkflowExecutionStatus.Running;
    public string? InputJson { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public DateTimeOffset? CompletedUtc { get; set; }
    public Dictionary<string, WorkflowTaskState> Tasks { get; set; } = new(StringComparer.Ordinal);
}

public sealed record WorkflowStoredExecution(
    WorkflowDefinition Definition,
    WorkflowExecutionState State);

public sealed record WorkflowEvent(
    long Id,
    Guid ExecutionId,
    string WorkflowName,
    string EventType,
    string? TaskReference,
    DateTimeOffset CreatedUtc,
    string? DetailJson);

public sealed class WorkflowTaskContext
{
    private readonly IReadOnlyDictionary<string, string?> _outputs;

    internal WorkflowTaskContext(
        Guid executionId,
        string workflowName,
        int workflowVersion,
        string? correlationId,
        string? workflowInputJson,
        WorkflowTaskDefinition task,
        int attempt,
        IReadOnlyDictionary<string, string?> outputs)
    {
        ExecutionId = executionId;
        WorkflowName = workflowName;
        WorkflowVersion = workflowVersion;
        CorrelationId = correlationId;
        WorkflowInputJson = workflowInputJson;
        Task = task;
        Attempt = attempt;
        _outputs = outputs;
    }

    public Guid ExecutionId { get; }
    public string WorkflowName { get; }
    public int WorkflowVersion { get; }
    public string? CorrelationId { get; }
    public string? WorkflowInputJson { get; }
    public WorkflowTaskDefinition Task { get; }
    public int Attempt { get; }

    public T? GetWorkflowInput<T>() => Deserialize<T>(WorkflowInputJson);

    public T? GetTaskInput<T>() => Deserialize<T>(Task.InputJson);

    public T? GetTaskOutput<T>(string taskReference)
    {
        return _outputs.TryGetValue(taskReference, out var json)
            ? Deserialize<T>(json)
            : default;
    }

    private static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return default;
        return JsonSerializer.Deserialize<T>(json, WorkflowJson.Options);
    }
}

internal static class WorkflowJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static string? SerializeObject(object? value) =>
        value is null ? null : JsonSerializer.Serialize(value, Options);

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
