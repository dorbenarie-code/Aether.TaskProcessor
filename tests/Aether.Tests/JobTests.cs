using Aether.Domain.Jobs;

namespace Aether.Tests;

public class JobTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_ShouldInitializeJobCorrectly()
    {
        var job = Job.Create("TestJob", "{}", Now);

        Assert.NotEqual(Guid.Empty, job.Id);
        Assert.Equal("TestJob", job.JobType);
        Assert.Equal("{}", job.Payload);
        Assert.Equal(JobStatus.Pending, job.Status);
        Assert.Equal(0, job.RetryCount);
        Assert.Equal(3, job.MaxRetries);
        Assert.Equal(Now, job.CreatedAtUtc);
        Assert.Null(job.StartedAtUtc);
        Assert.Null(job.CompletedAtUtc);
        Assert.Null(job.NextRetryAtUtc);
    }

    [Fact]
    public void Create_ShouldThrow_WhenJobTypeIsEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            Job.Create("", "{}", Now));
    }

    [Fact]
    public void Create_ShouldThrow_WhenPayloadIsEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            Job.Create("Test", "", Now));
    }

    [Fact]
    public void StartProcessing_ShouldMoveToProcessing()
    {
        var job = Job.Create("Test", "{}", Now);

        var startedAt = Now.AddMinutes(1);

        job.StartProcessing(startedAt);

        Assert.Equal(JobStatus.Processing, job.Status);
        Assert.Equal(startedAt, job.StartedAtUtc);
        Assert.Null(job.NextRetryAtUtc);
    }

    [Fact]
    public void StartProcessing_ShouldThrow_WhenNotPending()
    {
        var job = Job.Create("Test", "{}", Now);

        job.StartProcessing(Now.AddMinutes(1));

        Assert.Throws<InvalidOperationException>(() =>
            job.StartProcessing(Now.AddMinutes(2)));
    }

    [Fact]
    public void Complete_ShouldMoveToCompleted()
    {
        var job = Job.Create("Test", "{}", Now);

        job.StartProcessing(Now.AddMinutes(1));

        var completedAt = Now.AddMinutes(2);

        job.Complete(completedAt);

        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Equal(completedAt, job.CompletedAtUtc);
        Assert.Null(job.ErrorMessage);
        Assert.Null(job.NextRetryAtUtc);
    }

    [Fact]
    public void Complete_ShouldThrow_WhenNotProcessing()
    {
        var job = Job.Create("Test", "{}", Now);

        Assert.Throws<InvalidOperationException>(() =>
            job.Complete(Now.AddMinutes(1)));
    }

    [Fact]
    public void Fail_ShouldRetry_WhenRetriesRemaining()
    {
        var job = Job.Create("Test", "{}", Now, maxRetries: 3);

        job.StartProcessing(Now.AddMinutes(1));

        var failedAt = Now.AddMinutes(2);

        job.Fail("error", failedAt);

        Assert.Equal(JobStatus.Pending, job.Status);
        Assert.Equal(1, job.RetryCount);
        Assert.Equal("error", job.ErrorMessage);
        Assert.Null(job.StartedAtUtc);
        Assert.Null(job.CompletedAtUtc);
        Assert.Equal(failedAt.AddSeconds(10), job.NextRetryAtUtc);
    }

    [Fact]
    public void Fail_ShouldUseExponentialRetryDelay()
    {
        var job = Job.Create("Test", "{}", Now, maxRetries: 3);

        job.StartProcessing(Now.AddMinutes(1));

        var firstFailedAt = Now.AddMinutes(2);

        job.Fail("first error", firstFailedAt);

        Assert.Equal(firstFailedAt.AddSeconds(10), job.NextRetryAtUtc);

        job.StartProcessing(Now.AddMinutes(13));

        var secondFailedAt = Now.AddMinutes(14);

        job.Fail("second error", secondFailedAt);

        Assert.Equal(JobStatus.Pending, job.Status);
        Assert.Equal(2, job.RetryCount);
        Assert.Equal("second error", job.ErrorMessage);
        Assert.Equal(secondFailedAt.AddSeconds(20), job.NextRetryAtUtc);
    }

    [Fact]
    public void Fail_ShouldMoveToFailed_WhenMaxRetriesReached()
    {
        var job = Job.Create("Test", "{}", Now, maxRetries: 1);

        job.StartProcessing(Now.AddMinutes(1));

        var failedAt = Now.AddMinutes(2);

        job.Fail("error", failedAt);

        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Equal(1, job.RetryCount);
        Assert.Equal("error", job.ErrorMessage);
        Assert.Equal(failedAt, job.CompletedAtUtc);
        Assert.Null(job.NextRetryAtUtc);
    }

    [Fact]
    public void Fail_ShouldThrow_WhenNotProcessing()
    {
        var job = Job.Create("Test", "{}", Now);

        Assert.Throws<InvalidOperationException>(() =>
            job.Fail("error", Now.AddMinutes(1)));
    }

    [Fact]
    public void MarkAsPermanentlyFailed_ShouldMoveToFailedWithoutIncreasingRetryCount()
    {
        var job = Job.Create("Test", "{}", Now, maxRetries: 3);

        job.StartProcessing(Now.AddMinutes(1));

        var failedAt = Now.AddMinutes(2);

        job.MarkAsPermanentlyFailed("permanent error", failedAt);

        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Equal(0, job.RetryCount);
        Assert.Equal("permanent error", job.ErrorMessage);
        Assert.Equal(failedAt, job.CompletedAtUtc);
        Assert.Null(job.NextRetryAtUtc);
    }
    [Fact]
public void Cancel_ShouldMovePendingJobToCancelled()
{
    var job = Job.Create("Test", "{}", Now);

    var cancelledAt = Now.AddMinutes(1);

    job.Cancel(cancelledAt);

    Assert.Equal(JobStatus.Cancelled, job.Status);
    Assert.Equal(cancelledAt, job.CompletedAtUtc);
    Assert.Null(job.ErrorMessage);
    Assert.Null(job.NextRetryAtUtc);
}

[Fact]
public void Cancel_ShouldThrow_WhenJobIsProcessing()
{
    var job = Job.Create("Test", "{}", Now);

    job.StartProcessing(Now.AddMinutes(1));

    Assert.Throws<InvalidOperationException>(() =>
        job.Cancel(Now.AddMinutes(2)));
}

[Fact]
public void Cancel_ShouldThrow_WhenTimeIsNotUtc()
{
    var job = Job.Create("Test", "{}", Now);

    Assert.Throws<ArgumentException>(() =>
        job.Cancel(DateTime.Now));
}

[Fact]
public void Cancel_ShouldThrow_WhenCancellationTimeIsEarlierThanCreationTime()
{
    var job = Job.Create("Test", "{}", Now);

    Assert.Throws<InvalidOperationException>(() =>
        job.Cancel(Now.AddMinutes(-1)));
}
[Fact]
public void Restore_ShouldRebuildExistingJob()
{
    var id = Guid.NewGuid();

    var job = Job.Restore(
        id,
        jobType: "RestoredJob",
        payload: "{}",
        status: JobStatus.Completed,
        retryCount: 1,
        maxRetries: 3,
        errorMessage: null,
        createdAtUtc: Now,
        startedAtUtc: Now.AddMinutes(1),
        completedAtUtc: Now.AddMinutes(2),
        nextRetryAtUtc: null);

    Assert.Equal(id, job.Id);
    Assert.Equal("RestoredJob", job.JobType);
    Assert.Equal("{}", job.Payload);
    Assert.Equal(JobStatus.Completed, job.Status);
    Assert.Equal(1, job.RetryCount);
    Assert.Equal(3, job.MaxRetries);
    Assert.Equal(Now, job.CreatedAtUtc);
    Assert.Equal(Now.AddMinutes(1), job.StartedAtUtc);
    Assert.Equal(Now.AddMinutes(2), job.CompletedAtUtc);
    Assert.Null(job.NextRetryAtUtc);
}

    [Fact]
    public void MarkAsPermanentlyFailed_ShouldThrow_WhenNotProcessing()
    {
        var job = Job.Create("Test", "{}", Now);

        Assert.Throws<InvalidOperationException>(() =>
            job.MarkAsPermanentlyFailed("permanent error", Now.AddMinutes(1)));
    }

    [Fact]
    public void ShouldThrow_WhenTimeIsNotUtc()
    {
        var localTime = DateTime.Now;

        Assert.Throws<ArgumentException>(() =>
            Job.Create("Test", "{}", localTime));
    }
}