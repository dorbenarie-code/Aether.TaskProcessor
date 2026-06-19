using System.Collections.Concurrent;
using Aether.Application.Jobs;
using Aether.Domain.Jobs;
using Aether.Infrastructure.Queues;
using Aether.Infrastructure.Repositories;

namespace Aether.Tests.Application;

public class JobWorkerPoolTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RunAsync_ShouldThrow_WhenWorkerCountIsZero()
    {
        var pool = new JobWorkerPool(() => throw new InvalidOperationException());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            pool.RunAsync(workerCount: 0, CancellationToken.None));
    }
    [Fact]
public async Task RunAsync_ShouldStopCleanly_WhenCancellationIsRequested()
{
    var repository = new InMemoryJobRepository();
    var queue = new InMemoryJobQueue();
    var timeProvider = new FakeTimeProvider(Now);
    var handler = new TrackingJobHandler();

    var pool = new JobWorkerPool(() =>
        new JobWorker(
            queue,
            repository,
            handlers: new IJobHandler[] { handler },
            timeProvider));

    using var cancellationTokenSource = new CancellationTokenSource();

    var poolTask = Task.Run(() =>
        pool.RunAsync(workerCount: 2, cancellationTokenSource.Token));

    await Task.Delay(100);

    cancellationTokenSource.Cancel();

    await StopPoolAsync(poolTask);

    Assert.True(poolTask.IsCompleted);
}
[Fact]
public async Task RunAsync_ShouldStopCleanly_WhenCancellationIsRequestedDuringJobProcessing()
{
    var repository = new InMemoryJobRepository();
    var queue = new InMemoryJobQueue();
    var timeProvider = new FakeTimeProvider(Now);
    var handler = new CancellableJobHandler();

    var job = Job.Create(
        jobType: "CancellableJob",
        payload: "{}",
        createdAtUtc: Now);

    await repository.AddAsync(job);
    await queue.EnqueueAsync(job.Id);

    var pool = new JobWorkerPool(() =>
        new JobWorker(
            queue,
            repository,
            handlers: new IJobHandler[] { handler },
            timeProvider));

    using var cancellationTokenSource = new CancellationTokenSource();

    var poolTask = Task.Run(() =>
        pool.RunAsync(workerCount: 1, cancellationTokenSource.Token));

    await handler.WaitUntilStartedAsync();

    cancellationTokenSource.Cancel();

    await StopPoolAsync(poolTask);

    Assert.True(poolTask.IsCompleted);
}

    [Fact]
    public async Task RunAsync_ShouldProcessMultipleJobs_WhenWorkerCountIsGreaterThanOne()
    {
        var repository = new InMemoryJobRepository();
        var queue = new InMemoryJobQueue();
        var timeProvider = new FakeTimeProvider(Now);
        var handler = new TrackingJobHandler();

        var jobs = Enumerable.Range(1, 5)
            .Select(index => Job.Create(
                jobType: "TrackingJob",
                payload: $"{{\"index\":{index}}}",
                createdAtUtc: Now))
            .ToArray();

        foreach (var job in jobs)
        {
            await repository.AddAsync(job);
            await queue.EnqueueAsync(job.Id);
        }

        var pool = new JobWorkerPool(() =>
            new JobWorker(
                queue,
                repository,
                handlers: new IJobHandler[] { handler },
                timeProvider));

        using var cancellationTokenSource = new CancellationTokenSource();

        var poolTask = Task.Run(() =>
            pool.RunAsync(workerCount: 2, cancellationTokenSource.Token));

        await Task.Delay(300);

        cancellationTokenSource.Cancel();

        await StopPoolAsync(poolTask);

        foreach (var job in jobs)
        {
            var storedJob = await repository.FindByIdAsync(job.Id);

            Assert.NotNull(storedJob);
            Assert.Equal(JobStatus.Completed, storedJob.Status);
            Assert.Contains(job.Id, handler.ProcessedJobIds);
        }

        Assert.Equal(jobs.Length, handler.ProcessedJobIds.Count);
    }
    [Fact]
public async Task RunAsync_ShouldReturnJobToPending_WhenCancelledDuringProcessing()
{
    var repository = new InMemoryJobRepository();
    var queue = new InMemoryJobQueue();
    var timeProvider = new FakeTimeProvider(Now);
    var handler = new CancellableJobHandler();

    var job = Job.Create(
        jobType: "CancellableJob",
        payload: "{}",
        createdAtUtc: Now);

    await repository.AddAsync(job);
    await queue.EnqueueAsync(job.Id);

    var pool = new JobWorkerPool(() =>
        new JobWorker(
            queue,
            repository,
            handlers: new IJobHandler[] { handler },
            timeProvider));

    using var cancellationTokenSource = new CancellationTokenSource();

    var poolTask = Task.Run(() =>
        pool.RunAsync(workerCount: 1, cancellationTokenSource.Token));

    await handler.WaitUntilStartedAsync();

    cancellationTokenSource.Cancel();

    await StopPoolAsync(poolTask);

    var storedJob = await repository.FindByIdAsync(job.Id);

    Assert.NotNull(storedJob);
    Assert.Equal(JobStatus.Pending, storedJob.Status);
    Assert.Equal(0, storedJob.RetryCount);
}

    private static async Task StopPoolAsync(Task poolTask)
    {
        try
        {
            await poolTask;
        }
        catch (OperationCanceledException)
        {
            
        }
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTime utcNow)
        {
            _utcNow = new DateTimeOffset(utcNow, TimeSpan.Zero);
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }

    private sealed class TrackingJobHandler : IJobHandler
    {
        private readonly ConcurrentBag<Guid> _processedJobIds = new();

        public string JobType => "TrackingJob";

        public IReadOnlyCollection<Guid> ProcessedJobIds => _processedJobIds.ToArray();

        public Task HandleAsync(Job job, CancellationToken cancellationToken = default)
        {
            _processedJobIds.Add(job.Id);

            return Task.CompletedTask;
        }
    }
    private sealed class CancellableJobHandler : IJobHandler
{
    private readonly TaskCompletionSource _started = new(
        TaskCreationOptions.RunContinuationsAsynchronously);

    public string JobType => "CancellableJob";

    public Task WaitUntilStartedAsync()
    {
        return _started.Task;
    }

    public async Task HandleAsync(Job job, CancellationToken cancellationToken = default)
    {
        _started.SetResult();

        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }
}
}