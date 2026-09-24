using SanchesTV.Core.Orchestration;

namespace SanchesTV.Tests;

public sealed class WorkflowEngineTests
{
    [Fact]
    public void DefinitionRejectsDependencyCycle()
    {
        var definition = new WorkflowDefinition(
            "cycle",
            1,
            [
                new WorkflowTaskDefinition("a", "noop", ["b"]),
                new WorkflowTaskDefinition("b", "noop", ["a"])
            ]);

        Assert.Throws<InvalidOperationException>(() => definition.Validate());
    }

    [Fact]
    public void DefinitionBuildsParallelExecutionLayers()
    {
        var definition = new WorkflowDefinition(
            "layers",
            1,
            [
                new WorkflowTaskDefinition("database", "noop"),
                new WorkflowTaskDefinition("catalog", "noop", ["database"]),
                new WorkflowTaskDefinition("epg", "noop", ["database"]),
                new WorkflowTaskDefinition("home", "noop", ["catalog", "epg"])
            ]);

        var layers = definition.BuildExecutionLayers();

        Assert.Equal(3, layers.Count);
        Assert.Equal(["database"], layers[0]);
        Assert.Equal(2, layers[1].Count);
        Assert.Contains("catalog", layers[1]);
        Assert.Contains("epg", layers[1]);
        Assert.Equal(["home"], layers[2]);
    }

    [Fact]
    public async Task EngineRetriesTransientFailureAndPersistsHistory()
    {
        var store = new InMemoryWorkflowStateStore();
        var engine = new DurableWorkflowEngine(store);
        await engine.InitializeAsync();

        var attempts = 0;
        engine.RegisterHandler("unstable", (_, _) =>
        {
            attempts++;
            return Task.FromResult(
                attempts == 1
                    ? WorkflowTaskResult.Failed("temporary")
                    : WorkflowTaskResult.Completed(new { ok = true }));
        });

        var result = await engine.StartAsync(
            new WorkflowDefinition(
                "retry",
                1,
                [
                    new WorkflowTaskDefinition(
                        "work",
                        "unstable",
                        RetryCount: 1,
                        RetryDelay: TimeSpan.FromMilliseconds(1),
                        UseJitter: false)
                ]));

        Assert.Equal(WorkflowExecutionStatus.Completed, result.Status);
        Assert.Equal(2, attempts);
        Assert.Equal(2, result.Tasks["work"].Attempt);
        Assert.Equal(WorkflowTaskStatus.Completed, result.Tasks["work"].Status);

        var events = await engine.GetEventsAsync(result.Id);
        Assert.Contains(events, x => x.EventType == "task.retry_scheduled");
        Assert.Contains(events, x => x.EventType == "workflow.completed");
    }

    [Fact]
    public async Task EngineRunsIndependentTasksInParallel()
    {
        var store = new InMemoryWorkflowStateStore();
        var engine = new DurableWorkflowEngine(store);
        await engine.InitializeAsync();

        var running = 0;
        var maxRunning = 0;

        engine.RegisterHandler("parallel", async (_, cancellationToken) =>
        {
            var current = Interlocked.Increment(ref running);
            InterlockedExtensions.Max(ref maxRunning, current);
            try
            {
                await Task.Delay(60, cancellationToken);
                return WorkflowTaskResult.Completed();
            }
            finally
            {
                Interlocked.Decrement(ref running);
            }
        });

        var result = await engine.StartAsync(
            new WorkflowDefinition(
                "parallel",
                1,
                [
                    new WorkflowTaskDefinition("a", "parallel"),
                    new WorkflowTaskDefinition("b", "parallel"),
                    new WorkflowTaskDefinition("join", "parallel", ["a", "b"])
                ],
                MaxParallelTasks: 2));

        Assert.Equal(WorkflowExecutionStatus.Completed, result.Status);
        Assert.True(maxRunning >= 2);
        Assert.Equal(WorkflowTaskStatus.Completed, result.Tasks["join"].Status);
    }

    [Fact]
    public async Task OptionalTimeoutDoesNotFailWorkflow()
    {
        var store = new InMemoryWorkflowStateStore();
        var engine = new DurableWorkflowEngine(store);
        await engine.InitializeAsync();

        engine.RegisterHandler("slow", async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return WorkflowTaskResult.Completed();
        });

        engine.RegisterHandler("final", (_, _) =>
            Task.FromResult(WorkflowTaskResult.Completed()));

        var result = await engine.StartAsync(
            new WorkflowDefinition(
                "optional-timeout",
                1,
                [
                    new WorkflowTaskDefinition(
                        "optional",
                        "slow",
                        RetryCount: 0,
                        Timeout: TimeSpan.FromMilliseconds(20),
                        Optional: true),
                    new WorkflowTaskDefinition("final", "final", ["optional"])
                ]));

        Assert.Equal(WorkflowExecutionStatus.Completed, result.Status);
        Assert.Equal(WorkflowTaskStatus.CompletedWithErrors, result.Tasks["optional"].Status);
        Assert.Equal(WorkflowTaskStatus.Completed, result.Tasks["final"].Status);
    }

    [Fact]
    public async Task RerunFromKeepsEarlierOutputsAndReexecutesDescendants()
    {
        var store = new InMemoryWorkflowStateStore();
        var engine = new DurableWorkflowEngine(store);
        await engine.InitializeAsync();

        var firstRuns = 0;
        var secondRuns = 0;

        engine.RegisterHandler("first", (_, _) =>
        {
            firstRuns++;
            return Task.FromResult(WorkflowTaskResult.Completed(new { value = 42 }));
        });

        engine.RegisterHandler("second", (context, _) =>
        {
            secondRuns++;
            var first = context.GetTaskOutput<Dictionary<string, int>>("first");
            return Task.FromResult(
                first is not null && first.TryGetValue("value", out var value) && value == 42
                    ? WorkflowTaskResult.Completed()
                    : WorkflowTaskResult.Failed("missing previous output", terminal: true));
        });

        var initial = await engine.StartAsync(
            new WorkflowDefinition(
                "rerun",
                1,
                [
                    new WorkflowTaskDefinition("first", "first"),
                    new WorkflowTaskDefinition("second", "second", ["first"])
                ]));

        Assert.Equal(WorkflowExecutionStatus.Completed, initial.Status);

        var rerun = await engine.RerunFromAsync(initial.Id, "second");

        Assert.Equal(WorkflowExecutionStatus.Completed, rerun.Status);
        Assert.Equal(1, firstRuns);
        Assert.Equal(2, secondRuns);
    }

    private static class InterlockedExtensions
    {
        public static void Max(ref int target, int value)
        {
            while (true)
            {
                var current = Volatile.Read(ref target);
                if (value <= current)
                    return;

                if (Interlocked.CompareExchange(ref target, value, current) == current)
                    return;
            }
        }
    }
}
