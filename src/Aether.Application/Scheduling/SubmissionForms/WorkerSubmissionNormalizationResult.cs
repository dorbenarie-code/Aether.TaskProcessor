using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.SubmissionForms;

public sealed record WorkerSubmissionNormalizationResult(
    IReadOnlyCollection<AvailabilityWindow> AvailabilityWindows,
    IReadOnlyCollection<ResourcePreference> ResourcePreferences,
    IReadOnlyCollection<WorkerSubmissionNormalizationWarning> Warnings);
