using Aether.Application.Scheduling.Optimization;
using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.Contracts;

public sealed record SchedulingRunOptimizationResult(
    ScheduleCandidate Candidate,
    ScheduleEvaluationResult Evaluation,
    IReadOnlyCollection<ResourceLoadSummary> LoadByResource,
    IReadOnlyDictionary<ConstraintViolationType, int> ViolationsByType,
    IReadOnlyCollection<GeneticGenerationDiagnostic> GenerationDiagnostics);
