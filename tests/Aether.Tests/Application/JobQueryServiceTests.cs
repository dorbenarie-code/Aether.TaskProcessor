using Aether.Application.Jobs;
using Aether.Domain.Jobs;
using Aether.Infrastructure.Repositories;

namespace Aether.Tests.Application;

public class JobQueryServiceTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetByIdAsync_ShouldReturnJob_WhenJobExists()
    {
        var repository = new InMemoryJobRepository();
        var service = new JobQueryService(repository);

        var job = Job.Create("TestJob", "{}", Now);

        await repository.AddAsync(job);

        var result = await service.GetByIdAsync(job.Id);

        Assert.NotNull(result);
        Assert.Equal(job.Id, result.Id);
        Assert.Equal("TestJob", result.JobType);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenJobDoesNotExist()
    {
        var repository = new InMemoryJobRepository();
        var service = new JobQueryService(repository);

        var result = await service.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }
    [Fact]
public async Task GetAllAsync_WithQuery_ShouldFilterByStatus()
{
    var repository = new InMemoryJobRepository();
    var service = new JobQueryService(repository);

    var completedJob = Job.Create("CompletedJob", "{}", Now);
    completedJob.StartProcessing(Now.AddMinutes(1));
    completedJob.Complete(Now.AddMinutes(2));

    var failedJob = Job.Create("FailedJob", "{}", Now, maxRetries: 1);
    failedJob.StartProcessing(Now.AddMinutes(1));
    failedJob.Fail("error", Now.AddMinutes(2));

    await repository.AddAsync(completedJob);
    await repository.AddAsync(failedJob);

    var query = new GetJobsQuery(status: JobStatus.Failed);

    var result = await service.GetAllAsync(query);

    Assert.Single(result);
    Assert.Contains(result, job => job.Id == failedJob.Id);
    Assert.DoesNotContain(result, job => job.Id == completedJob.Id);
}

[Fact]
public async Task GetAllAsync_WithQuery_ShouldApplyPagination()
{
    var repository = new InMemoryJobRepository();
    var service = new JobQueryService(repository);

    var firstJob = Job.Create("FirstJob", "{}", Now);
    var secondJob = Job.Create("SecondJob", "{}", Now.AddMinutes(1));
    var thirdJob = Job.Create("ThirdJob", "{}", Now.AddMinutes(2));

    await repository.AddAsync(firstJob);
    await repository.AddAsync(secondJob);
    await repository.AddAsync(thirdJob);

    var query = new GetJobsQuery(page: 2, pageSize: 1);

    var result = await service.GetAllAsync(query);

    Assert.Single(result);
    Assert.Contains(result, job => job.Id == secondJob.Id);
}

[Fact]
public async Task GetAllAsync_WithQuery_ShouldThrow_WhenQueryIsNull()
{
    var repository = new InMemoryJobRepository();
    var service = new JobQueryService(repository);

    await Assert.ThrowsAsync<ArgumentNullException>(() =>
        service.GetAllAsync(query: null!));
}

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllJobs()
    {
        var repository = new InMemoryJobRepository();
        var service = new JobQueryService(repository);

        var firstJob = Job.Create("FirstJob", "{}", Now);
        var secondJob = Job.Create("SecondJob", "{}", Now.AddMinutes(1));

        await repository.AddAsync(firstJob);
        await repository.AddAsync(secondJob);

        var result = await service.GetAllAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, job => job.Id == firstJob.Id);
        Assert.Contains(result, job => job.Id == secondJob.Id);
    }

    [Fact]
    public async Task GetFailedJobsAsync_ShouldReturnOnlyFailedJobs()
    {
        var repository = new InMemoryJobRepository();
        var service = new JobQueryService(repository);

        var failedJob = Job.Create("FailedJob", "{}", Now, maxRetries: 1);
        failedJob.StartProcessing(Now.AddMinutes(1));
        failedJob.Fail("error", Now.AddMinutes(2));

        var pendingJob = Job.Create("PendingJob", "{}", Now);

        await repository.AddAsync(failedJob);
        await repository.AddAsync(pendingJob);

        var result = await service.GetFailedJobsAsync();

        Assert.Single(result);
        Assert.Contains(result, job => job.Id == failedJob.Id);
        Assert.DoesNotContain(result, job => job.Id == pendingJob.Id);
    }
}