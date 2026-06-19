using Aether.Domain.Jobs;

namespace Aether.Api.Jobs;

public sealed record SubmitJobResponse(
    Guid Id,
    string JobType,
    JobStatus Status,
    int RetryCount,
    int MaxRetries,
    DateTime CreatedAtUtc);