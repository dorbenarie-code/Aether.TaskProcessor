using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.SubmissionForms;

public sealed record WorkerSubmissionAggregationWarning(
    WorkerSubmissionAggregationWarningType Type,
    string Message,
    Guid? ResourceId = null,
    string? ResourceName = null,
    DateOnly? Date = null,
    ShiftKind? ShiftKind = null);
