using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.SubmissionForms;

public sealed class WorkerSubmissionAggregator
{
    public WorkerSubmissionAggregationResult Aggregate(
        SchedulePeriod period,
        IReadOnlyCollection<Resource> resources,
        IReadOnlyCollection<Shift> shifts,
        IReadOnlyCollection<WorkerSubmission> submissions)
    {
        ArgumentNullException.ThrowIfNull(period);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(shifts);
        ArgumentNullException.ThrowIfNull(submissions);

        var resourcesById = resources.ToDictionary(resource => resource.Id);

        var handledResourceIds = new HashSet<Guid>();
        var availabilityWindows = new List<AvailabilityWindow>();
        var resourcePreferences = new List<ResourcePreference>();
        var warnings = new List<WorkerSubmissionAggregationWarning>();

        var normalizer = new WorkerSubmissionNormalizer();

        foreach (var submission in submissions)
        {
            if (!resourcesById.TryGetValue(submission.ResourceId, out var resource))
            {
                warnings.Add(new WorkerSubmissionAggregationWarning(
                    WorkerSubmissionAggregationWarningType.UnknownResource,
                    $"Submission resource id '{submission.ResourceId}' does not match any known resource.",
                    submission.ResourceId));

                continue;
            }

            if (!handledResourceIds.Add(submission.ResourceId))
            {
                warnings.Add(new WorkerSubmissionAggregationWarning(
                    WorkerSubmissionAggregationWarningType.DuplicateWorkerSubmission,
                    $"Resource '{resource.Name}' submitted more than one worker submission. Duplicate submission was skipped.",
                    resource.Id,
                    resource.Name));

                continue;
            }

            var normalizationResult = normalizer.Normalize(
                period,
                resource,
                shifts,
                submission);

            availabilityWindows.AddRange(normalizationResult.AvailabilityWindows);
            resourcePreferences.AddRange(normalizationResult.ResourcePreferences);

            foreach (var normalizationWarning in normalizationResult.Warnings)
            {
                warnings.Add(ToAggregationWarning(
                    resource,
                    normalizationWarning));
            }
        }

        return new WorkerSubmissionAggregationResult(
            availabilityWindows,
            resourcePreferences,
            warnings);
    }

    private static WorkerSubmissionAggregationWarning ToAggregationWarning(
        Resource resource,
        WorkerSubmissionNormalizationWarning warning)
    {
        return new WorkerSubmissionAggregationWarning(
            ToAggregationWarningType(warning.Type),
            warning.Message,
            resource.Id,
            warning.ResourceName ?? resource.Name,
            warning.Date,
            warning.ShiftKind);
    }

    private static WorkerSubmissionAggregationWarningType ToAggregationWarningType(
        WorkerSubmissionNormalizationWarningType type)
    {
        return type switch
        {
            WorkerSubmissionNormalizationWarningType.DuplicateShiftSubmission =>
                WorkerSubmissionAggregationWarningType.DuplicateShiftSubmission,

            WorkerSubmissionNormalizationWarningType.DateOutsidePeriod =>
                WorkerSubmissionAggregationWarningType.DateOutsidePeriod,

            WorkerSubmissionNormalizationWarningType.NoMatchingShift =>
                WorkerSubmissionAggregationWarningType.NoMatchingShift,

            _ => throw new ArgumentOutOfRangeException(
                nameof(type),
                "Worker submission normalization warning type is not supported.")
        };
    }
}
