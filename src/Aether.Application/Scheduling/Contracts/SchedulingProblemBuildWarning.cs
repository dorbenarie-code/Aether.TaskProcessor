using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.Contracts;

public sealed record SchedulingProblemBuildWarning(
    SchedulingProblemBuildWarningType Type,
    string Message,
    string? ResourceName = null,
    DateOnly? Date = null,
    ShiftKind? ShiftKind = null);
