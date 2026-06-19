using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.Contracts;

public sealed record SchedulingRunResult(
    SchedulingProblem Problem,
    IReadOnlyCollection<SchedulingProblemBuildWarning> Warnings,
    SchedulingRunOptimizationResult DeterministicResult,
    SchedulingRunOptimizationResult GeneticResult,
    SchedulingRunComparison Comparison);
