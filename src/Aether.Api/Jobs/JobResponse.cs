using Aether.Domain.Jobs;

namespace Aether.Api.Jobs;

public sealed record JobResponse(
    Guid Id,
    string JobType,
    JobStatus Status,
    int RetryCount,
    int MaxRetries,
    string? ErrorMessage,
    DateTime CreatedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? NextRetryAtUtc);