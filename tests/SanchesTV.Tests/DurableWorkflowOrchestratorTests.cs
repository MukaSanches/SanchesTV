using SanchesTV.Core.Orchestration;

namespace SanchesTV.Tests;

public sealed class DurableWorkflowOrchestratorTests
{
    [Fact]
    public async Task Runs_dependencies_parallel_branches_and_retry()
    {
        var root = Path.Combine(Path.GetTempPath(), "sanchestv-workflow-tests", Guid.NewGuid().ToString("N"));
        var orchestrator = new DurableWorkflowOrchestrator(root);
        var flakyAttempts = 0;
        var parallelHits = 0;

        var definition = new WorkflowDefinition(
            "catalog-maintenance",
            1,
            [
                new WorkflowTaskDefinition(
                    "seed",
                    _ => Task.CompletedTask,
                    MaxAttempts: 1),
                new WorkflowTaskDefinition(
                    "flaky",
                    _ =>
                    {
                        if (Interlocked.Increment(ref flakyAttempts) == 1)
                            throw new InvalidOperationException("transient");
                        return Task.CompletedTask;
                    },
                    ["seed"],
                    MaxAttempts: 2,
                    RetryDelay: TimeSpan.FromMilliseconds(1),
                    MaxRetryDelay: TimeSpan.FromMilliseconds(2),
                    RetryJitterMilliseconds: 0),
                new WorkflowTaskDefinition(
                    "parallel",
                    _ =>
                    {
                        Interlocked.Increment(ref parallelHits);
                        return Task.CompletedTask;
                    },
                    ["seed"],
                    MaxAttempts: 1),
                new WorkflowTaskDefinition(
                    "join",
                    _ => Task.CompletedTask,
                    ["flaky", "parallel"],
                    MaxAttempts: 1)
            ],
            MaxConcurrency: 4);

        var result = await orchestrator.RunAsync(definition, "run-1");

        Assert.Equal(WorkflowRunStatus.Completed, result.Status);
        Assert.Equal(2, result.Tasks["flaky"].Attempts);
        Assert.Equal(1, parallelHits);
        Assert.All(result.Tasks.Values, task => Assert.Equal(WorkflowTaskStatus.Completed, task.Status));

        var resumed = await orchestrator.RunAsync(definition, "run-1");
        Assert.Equal(WorkflowRunStatus.Completed, resumed.Status);
        Assert.Equal(2, flakyAttempts);

        try { Directory.Delete(root, true); } catch { }
    }

    [Fact]
    public async Task Rejects_unknown_dependency()
    {
        var root = Path.Combine(Path.GetTempPath(), "sanchestv-workflow-tests", Guid.NewGuid().ToString("N"));
        var orchestrator = new DurableWorkflowOrchestrator(root);
        var definition = new WorkflowDefinition(
            "invalid",
            1,
            [new WorkflowTaskDefinition("a", _ => Task.CompletedTask, ["missing"])]);

        await Assert.ThrowsAsync<ArgumentException>(() => orchestrator.RunAsync(definition, "run-invalid"));
        try { Directory.Delete(root, true); } catch { }
    }
}
