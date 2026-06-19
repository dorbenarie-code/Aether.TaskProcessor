namespace Aether.Domain.Jobs;

public sealed class Job
{
    private static readonly TimeSpan BaseRetryDelay = TimeSpan.FromSeconds(10);

    public Guid Id { get; }
    public string JobType { get; }
    public string Payload { get; }

    public JobStatus Status { get; private set; }
    public int RetryCount { get; private set; }
    public int MaxRetries { get; }
    public string? ErrorMessage { get; private set; }

    public DateTime CreatedAtUtc { get; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public DateTime? NextRetryAtUtc { get; private set; }

    private Job(
        Guid id,
        string jobType,
        string payload,
        int maxRetries,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Job id cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(jobType))
        {
            throw new ArgumentException("Job type is required.", nameof(jobType));
        }

        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new ArgumentException("Payload is required.", nameof(payload));
        }

        if (maxRetries < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRetries), "Max retries cannot be negative.");
        }

        EnsureUtc(createdAtUtc, nameof(createdAtUtc));

        Id = id;
        JobType = jobType.Trim();
        Payload = payload;
        MaxRetries = maxRetries;
        CreatedAtUtc = createdAtUtc;

        Status = JobStatus.Pending;
        RetryCount = 0;
        ErrorMessage = null;
        StartedAtUtc = null;
        CompletedAtUtc = null;
        NextRetryAtUtc = null;
    }

    public static Job Create(string jobType, string payload, DateTime createdAtUtc, int maxRetries = 3)
{
    return new Job(
        Guid.NewGuid(),
        jobType,
        payload,
        maxRetries,
        createdAtUtc);
}

public static Job Restore(
    Guid id,
    string jobType,
    string payload,
    JobStatus status,
    int retryCount,
    int maxRetries,
    string? errorMessage,
    DateTime createdAtUtc,
    DateTime? startedAtUtc,
    DateTime? completedAtUtc,
    DateTime? nextRetryAtUtc)
{
    var job = new Job(
        id,
        jobType,
        payload,
        maxRetries,
        createdAtUtc);

    if (retryCount < 0)
    {
        throw new ArgumentOutOfRangeException(nameof(retryCount), "Retry count cannot be negative.");
    }

    if (startedAtUtc is not null)
    {
        EnsureUtc(startedAtUtc.Value, nameof(startedAtUtc));
    }

    if (completedAtUtc is not null)
    {
        EnsureUtc(completedAtUtc.Value, nameof(completedAtUtc));
    }

    if (nextRetryAtUtc is not null)
    {
        EnsureUtc(nextRetryAtUtc.Value, nameof(nextRetryAtUtc));
    }

    job.Status = status;
    job.RetryCount = retryCount;
    job.ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? null : errorMessage;
    job.StartedAtUtc = startedAtUtc;
    job.CompletedAtUtc = completedAtUtc;
    job.NextRetryAtUtc = nextRetryAtUtc;

    return job;
}

    public void StartProcessing(DateTime startedAtUtc)
    {
        if (Status != JobStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot start processing a job in '{Status}' state.");
        }

        EnsureUtc(startedAtUtc, nameof(startedAtUtc));

        if (startedAtUtc < CreatedAtUtc)
        {
            throw new InvalidOperationException("Processing start time cannot be earlier than creation time.");
        }

        Status = JobStatus.Processing;
        StartedAtUtc = startedAtUtc;
        CompletedAtUtc = null;
        ErrorMessage = null;
        NextRetryAtUtc = null;
    }

    public void Complete(DateTime completedAtUtc)
    {
        if (Status != JobStatus.Processing)
        {
            throw new InvalidOperationException("Only processing jobs can be completed.");
        }

        EnsureUtc(completedAtUtc, nameof(completedAtUtc));

        if (StartedAtUtc is null)
        {
            throw new InvalidOperationException("Processing start time is missing.");
        }

        if (completedAtUtc < StartedAtUtc.Value)
        {
            throw new InvalidOperationException("Completion time cannot be earlier than processing start time.");
        }

        Status = JobStatus.Completed;
        CompletedAtUtc = completedAtUtc;
        ErrorMessage = null;
        NextRetryAtUtc = null;
    }
   public void Cancel(DateTime cancelledAtUtc)
{
    if (Status != JobStatus.Pending)
    {
        throw new InvalidOperationException($"Cannot cancel a job in '{Status}' state.");
    }

    EnsureUtc(cancelledAtUtc, nameof(cancelledAtUtc));

    if (cancelledAtUtc < CreatedAtUtc)
    {
        throw new InvalidOperationException("Cancellation time cannot be earlier than creation time.");
    }

    Status = JobStatus.Cancelled;
    CompletedAtUtc = cancelledAtUtc;
    ErrorMessage = null;
    NextRetryAtUtc = null;
}

    public void Fail(string errorMessage, DateTime failedAtUtc)
    {
        EnsureCanFail(errorMessage, failedAtUtc);

        RetryCount++;
        ErrorMessage = errorMessage.Trim();

        if (RetryCount >= MaxRetries)
        {
            Status = JobStatus.Failed;
            CompletedAtUtc = failedAtUtc;
            NextRetryAtUtc = null;
            return;
        }

        Status = JobStatus.Pending;
        StartedAtUtc = null;
        CompletedAtUtc = null;
        NextRetryAtUtc = failedAtUtc.Add(CalculateRetryDelay());
    }

    public void MarkAsPermanentlyFailed(string errorMessage, DateTime failedAtUtc)
    {
        EnsureCanFail(errorMessage, failedAtUtc);

        Status = JobStatus.Failed;
        ErrorMessage = errorMessage.Trim();
        CompletedAtUtc = failedAtUtc;
        NextRetryAtUtc = null;
    }

    public void ReturnToPendingAfterCancellation()
    {
        if (Status != JobStatus.Processing)
        {
            throw new InvalidOperationException("Only processing jobs can be returned to pending.");
        }

        Status = JobStatus.Pending;
        StartedAtUtc = null;
        CompletedAtUtc = null;
        NextRetryAtUtc = null;
    }

    private TimeSpan CalculateRetryDelay()
    {
        var multiplier = Math.Pow(2, RetryCount - 1);

        return TimeSpan.FromTicks(BaseRetryDelay.Ticks * (long)multiplier);
    }

    private void EnsureCanFail(string errorMessage, DateTime failedAtUtc)
    {
        if (Status != JobStatus.Processing)
        {
            throw new InvalidOperationException("Only processing jobs can fail.");
        }

        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("Error message is required.", nameof(errorMessage));
        }

        EnsureUtc(failedAtUtc, nameof(failedAtUtc));

        if (StartedAtUtc is null)
        {
            throw new InvalidOperationException("Processing start time is missing.");
        }

        if (failedAtUtc < StartedAtUtc.Value)
        {
            throw new InvalidOperationException("Failure time cannot be earlier than processing start time.");
        }
    }

    private static void EnsureUtc(DateTime value, string paramName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("DateTime value must be in UTC.", paramName);
        }
    }
}