using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.SubmissionForms;

public sealed record WorkerSubmissionNormalizationWarning(
    WorkerSubmissionNormalizationWarningType Type,
    string Message,
    string? ResourceName = null,
    DateOnly? Date = null,
    ShiftKind? ShiftKind = null);
