namespace Aether.Api.Scheduling;

public sealed record ClosedFormOptimizationJobResponse(
    Guid Id,
    string JobType,
    string Status,
    int RetryCount,
    int MaxRetries,
    DateTime CreatedAtUtc);
