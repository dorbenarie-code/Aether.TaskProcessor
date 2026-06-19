using Aether.Domain.Jobs;
using Aether.Infrastructure.Data.SqlServer;
using Aether.Infrastructure.Repositories;
using Aether.SqlServer.Tests.TestSupport;
using Microsoft.Extensions.Options;

namespace Aether.SqlServer.Tests.Infrastructure;

public sealed class SqlServerJobRepositoryTests : IAsyncLifetime
{
    private static readonly DateTime Now =
        new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _jobTypePrefix;
    private readonly SqlServerTestDatabase _database;
    private readonly SqlServerJobRepository _repository;

    public SqlServerJobRepositoryTests()
    {
        var connectionString = SqlServerTestConfiguration.GetConnectionString();

        _jobTypePrefix = $"AetherSqlTest_{Guid.NewGuid():N}_";
        _database = new SqlServerTestDatabase(connectionString);

        var options = Options.Create(new SqlServerOptions
        {
            ConnectionString = connectionString
        });

        var connectionFactory = new SqlServerConnectionFactory(options);

        _repository = new SqlServerJobRepository(connectionFactory);
    }

    public async Task InitializeAsync()
    {
        await _database.EnsureJobsTableExistsAsync();
        await _database.DeleteJobsByJobTypePrefixAsync(_jobTypePrefix);
    }

    public async Task DisposeAsync()
    {
        await _database.DeleteJobsByJobTypePrefixAsync(_jobTypePrefix);
    }

    [Fact]
    public async Task AddAsync_ShouldInsertJob()
    {
        var job = CreateJob("Add", Now);

        await _repository.AddAsync(job);

        var storedJob = await _repository.FindByIdAsync(job.Id);

        Assert.NotNull(storedJob);
        Assert.Equal(job.Id, storedJob.Id);
        Assert.Equal(job.JobType, storedJob.JobType);
        Assert.Equal(job.Payload, storedJob.Payload);
        Assert.Equal(JobStatus.Pending, storedJob.Status);
        Assert.Equal(0, storedJob.RetryCount);
        Assert.Equal(3, storedJob.MaxRetries);
        Assert.Equal(Now, storedJob.CreatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, storedJob.CreatedAtUtc.Kind);
    }

    [Fact]
    public async Task FindByIdAsync_ShouldReturnJob_WhenJobExists()
    {
        var job = CreateJob("FindExisting", Now);

        await _repository.AddAsync(job);

        var result = await _repository.FindByIdAsync(job.Id);

        Assert.NotNull(result);
        Assert.Equal(job.Id, result.Id);
        Assert.Equal(job.JobType, result.JobType);
    }

    [Fact]
    public async Task FindByIdAsync_ShouldReturnNull_WhenJobDoesNotExist()
    {
        var result = await _repository.FindByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateJobStatus()
    {
        var job = CreateJob("Update", Now);

        await _repository.AddAsync(job);

        var startedAtUtc = Now.AddMinutes(1);
        var completedAtUtc = Now.AddMinutes(2);

        job.StartProcessing(startedAtUtc);
        job.Complete(completedAtUtc);

        await _repository.UpdateAsync(job);

        var storedJob = await _repository.FindByIdAsync(job.Id);

        Assert.NotNull(storedJob);
        Assert.Equal(JobStatus.Completed, storedJob.Status);
        Assert.Equal(startedAtUtc, storedJob.StartedAtUtc);
        Assert.Equal(completedAtUtc, storedJob.CompletedAtUtc);
        Assert.Null(storedJob.ErrorMessage);
        Assert.Null(storedJob.NextRetryAtUtc);

        Assert.Equal(DateTimeKind.Utc, storedJob.CreatedAtUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, storedJob.StartedAtUtc?.Kind);
        Assert.Equal(DateTimeKind.Utc, storedJob.CompletedAtUtc?.Kind);
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrow_WhenJobDoesNotExist()
    {
        var job = CreateJob("MissingUpdate", Now);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repository.UpdateAsync(job));
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnJobsOrderedByCreatedAt()
    {
        var firstJob = CreateJob("First", Now);
        var secondJob = CreateJob("Second", Now.AddMinutes(1));

        await _repository.AddAsync(secondJob);
        await _repository.AddAsync(firstJob);

        var result = await _repository.GetAllAsync();

        var testJobs = result
            .Where(job => job.Id == firstJob.Id || job.Id == secondJob.Id)
            .ToArray();

        Assert.Equal(2, testJobs.Length);
        Assert.Equal(firstJob.Id, testJobs[0].Id);
        Assert.Equal(secondJob.Id, testJobs[1].Id);
    }
    [Fact]
public async Task UpdateAsync_ShouldPersistRetryState_WhenJobFailsAndRetryIsScheduled()
{
    var job = CreateJob("RetryScheduled", Now, maxRetries: 3);

    await _repository.AddAsync(job);

    var startedAtUtc = Now.AddMinutes(1);
    var failedAtUtc = Now.AddMinutes(2);

    job.StartProcessing(startedAtUtc);
    job.Fail("temporary sql failure", failedAtUtc);

    await _repository.UpdateAsync(job);

    var storedJob = await _repository.FindByIdAsync(job.Id);

    Assert.NotNull(storedJob);
    Assert.Equal(JobStatus.Pending, storedJob.Status);
    Assert.Equal(1, storedJob.RetryCount);
    Assert.Equal("temporary sql failure", storedJob.ErrorMessage);
    Assert.Null(storedJob.StartedAtUtc);
    Assert.Null(storedJob.CompletedAtUtc);
    Assert.Equal(failedAtUtc.AddSeconds(10), storedJob.NextRetryAtUtc);

    Assert.Equal(DateTimeKind.Utc, storedJob.CreatedAtUtc.Kind);
    Assert.Equal(DateTimeKind.Utc, storedJob.NextRetryAtUtc?.Kind);
}

[Fact]
public async Task UpdateAsync_ShouldPersistCancelledJob()
{
    var job = CreateJob("Cancelled", Now);

    await _repository.AddAsync(job);

    var cancelledAtUtc = Now.AddMinutes(1);

    job.Cancel(cancelledAtUtc);

    await _repository.UpdateAsync(job);

    var storedJob = await _repository.FindByIdAsync(job.Id);

    Assert.NotNull(storedJob);
    Assert.Equal(JobStatus.Cancelled, storedJob.Status);
    Assert.Equal(0, storedJob.RetryCount);
    Assert.Null(storedJob.ErrorMessage);
    Assert.Null(storedJob.StartedAtUtc);
    Assert.Equal(cancelledAtUtc, storedJob.CompletedAtUtc);
    Assert.Null(storedJob.NextRetryAtUtc);

    Assert.Equal(DateTimeKind.Utc, storedJob.CreatedAtUtc.Kind);
    Assert.Equal(DateTimeKind.Utc, storedJob.CompletedAtUtc?.Kind);
}

[Fact]
public async Task UpdateAsync_ShouldPersistPermanentlyFailedJob()
{
    var job = CreateJob("PermanentlyFailed", Now, maxRetries: 3);

    await _repository.AddAsync(job);

    var startedAtUtc = Now.AddMinutes(1);
    var failedAtUtc = Now.AddMinutes(2);

    job.StartProcessing(startedAtUtc);
    job.MarkAsPermanentlyFailed("permanent sql failure", failedAtUtc);

    await _repository.UpdateAsync(job);

    var storedJob = await _repository.FindByIdAsync(job.Id);

    Assert.NotNull(storedJob);
    Assert.Equal(JobStatus.Failed, storedJob.Status);
    Assert.Equal(0, storedJob.RetryCount);
    Assert.Equal("permanent sql failure", storedJob.ErrorMessage);
    Assert.Equal(startedAtUtc, storedJob.StartedAtUtc);
    Assert.Equal(failedAtUtc, storedJob.CompletedAtUtc);
    Assert.Null(storedJob.NextRetryAtUtc);

    Assert.Equal(DateTimeKind.Utc, storedJob.CreatedAtUtc.Kind);
    Assert.Equal(DateTimeKind.Utc, storedJob.StartedAtUtc?.Kind);
    Assert.Equal(DateTimeKind.Utc, storedJob.CompletedAtUtc?.Kind);
}

    [Fact]
    public async Task GetFailedJobsAsync_ShouldReturnOnlyFailedJobs()
    {
        var failedJob = CreateJob("Failed", Now, maxRetries: 1);
        failedJob.StartProcessing(Now.AddMinutes(1));
        failedJob.Fail("sql failure", Now.AddMinutes(2));

        var pendingJob = CreateJob("Pending", Now.AddMinutes(3));

        await _repository.AddAsync(failedJob);
        await _repository.AddAsync(pendingJob);

        var result = await _repository.GetFailedJobsAsync();

        Assert.Contains(result, job => job.Id == failedJob.Id);
        Assert.DoesNotContain(result, job => job.Id == pendingJob.Id);
    }

    private Job CreateJob(string name, DateTime createdAtUtc, int maxRetries = 3)
    {
        return Job.Create(
            jobType: $"{_jobTypePrefix}{name}",
            payload: "{\"message\":\"sql integration test\"}",
            createdAtUtc: createdAtUtc,
            maxRetries: maxRetries);
    }
}