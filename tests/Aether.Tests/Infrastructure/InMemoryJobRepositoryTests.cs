using Aether.Domain.Jobs;
using Aether.Infrastructure.Repositories;

namespace Aether.Tests.Infrastructure;

public class InMemoryJobRepositoryTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AddAsync_ShouldStoreJob()
    {
        var repository = new InMemoryJobRepository();
        var job = Job.Create("TestJob", "{}", Now);

        await repository.AddAsync(job);

        var storedJob = await repository.FindByIdAsync(job.Id);

        Assert.NotNull(storedJob);
        Assert.Equal(job.Id, storedJob.Id);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllJobsOrderedByCreatedAtUtc()
    {
        var repository = new InMemoryJobRepository();

        var secondJob = Job.Create("SecondJob", "{}", Now.AddMinutes(2));
        var firstJob = Job.Create("FirstJob", "{}", Now.AddMinutes(1));
        var thirdJob = Job.Create("ThirdJob", "{}", Now.AddMinutes(3));

        await repository.AddAsync(secondJob);
        await repository.AddAsync(firstJob);
        await repository.AddAsync(thirdJob);

        var jobs = await repository.GetAllAsync();

        Assert.Equal(3, jobs.Count);
        Assert.Collection(
            jobs,
            job => Assert.Equal(firstJob.Id, job.Id),
            job => Assert.Equal(secondJob.Id, job.Id),
            job => Assert.Equal(thirdJob.Id, job.Id));
    }

    [Fact]
    public async Task GetFailedJobsAsync_ShouldReturnOnlyFailedJobs()
    {
        var repository = new InMemoryJobRepository();

        var completedJob = Job.Create("CompletedJob", "{}", Now);
        completedJob.StartProcessing(Now.AddMinutes(1));
        completedJob.Complete(Now.AddMinutes(2));

        var failedJob = Job.Create("FailedJob", "{}", Now, maxRetries: 1);
        failedJob.StartProcessing(Now.AddMinutes(1));
        failedJob.Fail("error", Now.AddMinutes(2));

        var pendingJob = Job.Create("PendingJob", "{}", Now);

        await repository.AddAsync(completedJob);
        await repository.AddAsync(failedJob);
        await repository.AddAsync(pendingJob);

        var failedJobs = await repository.GetFailedJobsAsync();

        Assert.Single(failedJobs);
        Assert.Contains(failedJobs, job => job.Id == failedJob.Id);
        Assert.DoesNotContain(failedJobs, job => job.Id == completedJob.Id);
        Assert.DoesNotContain(failedJobs, job => job.Id == pendingJob.Id);
    }

    [Fact]
    public async Task AddAsync_ShouldThrow_WhenJobWithSameIdAlreadyExists()
    {
        var repository = new InMemoryJobRepository();
        var job = Job.Create("TestJob", "{}", Now);

        await repository.AddAsync(job);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.AddAsync(job));

        Assert.Contains(job.Id.ToString(), exception.Message);
    }

    [Fact]
    public async Task FindByIdAsync_ShouldReturnNull_WhenJobDoesNotExist()
    {
        var repository = new InMemoryJobRepository();

        var job = await repository.FindByIdAsync(Guid.NewGuid());

        Assert.Null(job);
    }

    [Fact]
    public async Task UpdateAsync_ShouldPersistUpdatedJobState()
    {
        var repository = new InMemoryJobRepository();
        var job = Job.Create("TestJob", "{}", Now);

        await repository.AddAsync(job);

        job.StartProcessing(Now.AddMinutes(1));
        job.Complete(Now.AddMinutes(2));

        await repository.UpdateAsync(job);

        var updatedJob = await repository.FindByIdAsync(job.Id);

        Assert.NotNull(updatedJob);
        Assert.Equal(JobStatus.Completed, updatedJob.Status);
        Assert.Equal(Now.AddMinutes(2), updatedJob.CompletedAtUtc);
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrow_WhenJobDoesNotExist()
    {
        var repository = new InMemoryJobRepository();
        var job = Job.Create("TestJob", "{}", Now);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.UpdateAsync(job));
    }
}