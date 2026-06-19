using System.Collections.Concurrent;
using Aether.Application.Jobs;
using Aether.Domain.Jobs;
using Aether.Infrastructure.Queues;
using Aether.Infrastructure.Repositories;

namespace Aether.Tests.Application;

public class JobWorkerTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RunAsync_ShouldMarkJobAsFailed_WhenHandlerIsMissing()
    {
        var repository = new InMemoryJobRepository();
        var queue = new InMemoryJobQueue();
        var timeProvider = new FakeTimeProvider(Now);

        var job = Job.Create(
            jobType: "MissingHandler",
            payload: "{}",
            createdAtUtc: Now);

        await repository.AddAsync(job);
        await queue.EnqueueAsync(job.Id);

        var worker = new JobWorker(
            queue,
            repository,
            handlers: Array.Empty<IJobHandler>(),
            timeProvider);

        using var cancellationTokenSource = new CancellationTokenSource();

        var workerTask = Task.Run(() => worker.RunAsync(cancellationTokenSource.Token));

        await WaitUntilAsync(async () =>
        {
            var storedJob = await repository.FindByIdAsync(job.Id);

            return storedJob?.Status == JobStatus.Failed;
        });

        cancellationTokenSource.Cancel();

        await StopWorkerAsync(workerTask);

        var updatedJob = await repository.FindByIdAsync(job.Id);

        Assert.NotNull(updatedJob);
        Assert.Equal(JobStatus.Failed, updatedJob.Status);
        Assert.Equal(0, updatedJob.RetryCount);
        Assert.Equal(Now, updatedJob.StartedAtUtc);
        Assert.Equal(Now, updatedJob.CompletedAtUtc);
        Assert.Null(updatedJob.NextRetryAtUtc);
        Assert.Equal("No handler found for job type 'MissingHandler'", updatedJob.ErrorMessage);
    }

    [Fact]
    public async Task RunAsync_ShouldKeepJobPending_WhenHandlerFailsAndRetryIsScheduled()
    {
        var repository = new InMemoryJobRepository();
        var queue = new InMemoryJobQueue();
        var timeProvider = new FakeTimeProvider(Now);

        var job = Job.Create(
            jobType: "FailingJob",
            payload: "{}",
            createdAtUtc: Now,
            maxRetries: 3);

        await repository.AddAsync(job);
        await queue.EnqueueAsync(job.Id);

        var worker = new JobWorker(
            queue,
            repository,
            handlers: new IJobHandler[] { new FailingJobHandler() },
            timeProvider);

        using var cancellationTokenSource = new CancellationTokenSource();

        var workerTask = Task.Run(() => worker.RunAsync(cancellationTokenSource.Token));

        await WaitUntilAsync(async () =>
        {
            var storedJob = await repository.FindByIdAsync(job.Id);

            return storedJob?.Status == JobStatus.Pending &&
                   storedJob.RetryCount == 1;
        });

        cancellationTokenSource.Cancel();

        await StopWorkerAsync(workerTask);

        var updatedJob = await repository.FindByIdAsync(job.Id);

        Assert.NotNull(updatedJob);
        Assert.Equal(JobStatus.Pending, updatedJob.Status);
        Assert.Equal(1, updatedJob.RetryCount);
        Assert.Equal("handler failed", updatedJob.ErrorMessage);
        Assert.Equal(Now.AddSeconds(10), updatedJob.NextRetryAtUtc);
    }

    [Fact]
    public async Task RunAsync_ShouldProcessMultipleJobs_WhenMultipleWorkersAreRunning()
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

        var firstWorker = new JobWorker(
            queue,
            repository,
            handlers: new IJobHandler[] { handler },
            timeProvider);

        var secondWorker = new JobWorker(
            queue,
            repository,
            handlers: new IJobHandler[] { handler },
            timeProvider);

        using var cancellationTokenSource = new CancellationTokenSource();

        var firstWorkerTask = Task.Run(() => firstWorker.RunAsync(cancellationTokenSource.Token));
        var secondWorkerTask = Task.Run(() => secondWorker.RunAsync(cancellationTokenSource.Token));

        await WaitUntilAsync(() =>
            Task.FromResult(handler.ProcessedJobIds.Count == jobs.Length));

        cancellationTokenSource.Cancel();

        await StopWorkersAsync(firstWorkerTask, secondWorkerTask);

        foreach (var job in jobs)
        {
            var storedJob = await repository.FindByIdAsync(job.Id);

            Assert.NotNull(storedJob);
            Assert.Equal(JobStatus.Completed, storedJob.Status);
            Assert.Equal(0, storedJob.RetryCount);
            Assert.Contains(job.Id, handler.ProcessedJobIds);
        }

        Assert.Equal(jobs.Length, handler.ProcessedJobIds.Count);
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var timeoutAtUtc = DateTime.UtcNow.AddSeconds(3);

        while (DateTime.UtcNow < timeoutAtUtc)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        throw new TimeoutException("The expected condition was not met.");
    }

    private static async Task StopWorkerAsync(Task workerTask)
    {
        try
        {
            await workerTask;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static async Task StopWorkersAsync(params Task[] workerTasks)
    {
        try
        {
            await Task.WhenAll(workerTasks);
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

    private sealed class FailingJobHandler : IJobHandler
    {
        public string JobType => "FailingJob";

        public Task HandleAsync(Job job, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("handler failed");
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
}