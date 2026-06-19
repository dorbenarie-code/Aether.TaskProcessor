using Aether.Application.Scheduling.Contracts;
using Aether.Application.Scheduling.Optimization;
using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.SubmissionForms;

public sealed record ClosedFormSubmissionOptimizationResult(
    SchedulingProblem Problem,
    IReadOnlyCollection<WorkerSubmissionAggregationWarning> Warnings,
    SchedulingRunOptimizationResult GeneticResult,
    PostRunLocalAddImprovementResult? PostRunLocalAddImprovementResult = null);
