using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.Contracts;

public sealed record SchedulingProblemBuildResult(
    SchedulingProblem Problem,
    IReadOnlyCollection<SchedulingProblemBuildWarning> Warnings);
