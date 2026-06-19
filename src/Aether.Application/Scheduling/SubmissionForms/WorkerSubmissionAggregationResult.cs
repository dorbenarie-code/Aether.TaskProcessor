using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.SubmissionForms;

public sealed record WorkerSubmissionAggregationResult(
    IReadOnlyCollection<AvailabilityWindow> AvailabilityWindows,
    IReadOnlyCollection<ResourcePreference> ResourcePreferences,
    IReadOnlyCollection<WorkerSubmissionAggregationWarning> Warnings);
