using Aether.Application.Jobs;
using Aether.Domain.Jobs;
using Aether.Infrastructure.Queues;
using Aether.Infrastructure.Repositories;

namespace Aether.Tests.Application;

public class JobSubmissionServiceTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SubmitAsync_ShouldCreateStoreAndEnqueueJob()
    {
        var repository = new InMemoryJobRepository();
        var queue = new InMemoryJobQueue();
        var timeProvider = new FakeTimeProvider(Now);

        var service = new JobSubmissionService(repository, queue, timeProvider);

        var job = await service.SubmitAsync(
            jobType: "PrintMessage",
            payload: "{\"message\":\"hello\"}",
            maxRetries: 5);

        var storedJob = await repository.FindByIdAsync(job.Id);
        var dequeuedJobId = await queue.DequeueAsync();

        Assert.NotNull(storedJob);
        Assert.Equal(job.Id, storedJob.Id);
        Assert.Equal("PrintMessage", storedJob.JobType);
        Assert.Equal("{\"message\":\"hello\"}", storedJob.Payload);
        Assert.Equal(JobStatus.Pending, storedJob.Status);
        Assert.Equal(5, storedJob.MaxRetries);
        Assert.Equal(Now, storedJob.CreatedAtUtc);
        Assert.Equal(job.Id, dequeuedJobId);
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
}