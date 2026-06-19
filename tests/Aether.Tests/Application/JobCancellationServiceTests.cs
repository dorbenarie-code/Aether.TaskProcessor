using Aether.Application.Jobs;
using Aether.Domain.Jobs;
using Aether.Infrastructure.Repositories;

namespace Aether.Tests.Application;

public class JobCancellationServiceTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CancelAsync_ShouldCancelPendingJob()
    {
        var repository = new InMemoryJobRepository();
        var timeProvider = new FakeTimeProvider(Now.AddMinutes(1));
        var service = new JobCancellationService(repository, timeProvider);

        var job = Job.Create("TestJob", "{}", Now);

        await repository.AddAsync(job);

        var result = await service.CancelAsync(job.Id);

        Assert.NotNull(result);
        Assert.Equal(JobStatus.Cancelled, result.Status);
        Assert.Equal(Now.AddMinutes(1), result.CompletedAtUtc);

        var storedJob = await repository.FindByIdAsync(job.Id);

        Assert.NotNull(storedJob);
        Assert.Equal(JobStatus.Cancelled, storedJob.Status);
    }

    [Fact]
    public async Task CancelAsync_ShouldReturnNull_WhenJobDoesNotExist()
    {
        var repository = new InMemoryJobRepository();
        var timeProvider = new FakeTimeProvider(Now);
        var service = new JobCancellationService(repository, timeProvider);

        var result = await service.CancelAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task CancelAsync_ShouldThrow_WhenJobIsAlreadyCompleted()
    {
        var repository = new InMemoryJobRepository();
        var timeProvider = new FakeTimeProvider(Now.AddMinutes(3));
        var service = new JobCancellationService(repository, timeProvider);

        var job = Job.Create("TestJob", "{}", Now);
        job.StartProcessing(Now.AddMinutes(1));
        job.Complete(Now.AddMinutes(2));

        await repository.AddAsync(job);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CancelAsync(job.Id));
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