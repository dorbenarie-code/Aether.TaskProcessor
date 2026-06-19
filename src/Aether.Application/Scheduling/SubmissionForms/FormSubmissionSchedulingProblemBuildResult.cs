using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.SubmissionForms;

public sealed record FormSubmissionSchedulingProblemBuildResult(
    SchedulingProblem Problem,
    IReadOnlyCollection<WorkerSubmissionAggregationWarning> Warnings);
