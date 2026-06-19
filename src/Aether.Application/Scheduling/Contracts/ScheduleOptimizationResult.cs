using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.Contracts;

public sealed record ScheduleOptimizationResult(
    ScheduleCandidate Candidate,
    ScheduleEvaluationResult Evaluation);
