using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.SubmissionForms;

public sealed class WorkerSubmissionNormalizer
{
    public WorkerSubmissionNormalizationResult Normalize(
        SchedulePeriod period,
        Resource resource,
        IReadOnlyCollection<Shift> shifts,
        WorkerSubmission submission)
    {
        ArgumentNullException.ThrowIfNull(period);
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(shifts);
        ArgumentNullException.ThrowIfNull(submission);

        if (submission.ResourceId != resource.Id)
        {
            throw new ArgumentException(
                "Worker submission resource id must match the provided resource.",
                nameof(submission));
        }

        var availabilityWindows = new List<AvailabilityWindow>();
        var resourcePreferences = new List<ResourcePreference>();
        var warnings = new List<WorkerSubmissionNormalizationWarning>();

        var handledSubmissionKeys = new HashSet<string>();

        foreach (var shiftSubmission in submission.ShiftSubmissions)
        {
            var submissionKey = CreateSubmissionKey(
                shiftSubmission.Date,
                shiftSubmission.ShiftKind);

            if (!handledSubmissionKeys.Add(submissionKey))
            {
                warnings.Add(new WorkerSubmissionNormalizationWarning(
                    WorkerSubmissionNormalizationWarningType.DuplicateShiftSubmission,
                    $"Resource '{resource.Name}' submitted duplicate selection for {shiftSubmission.Date} {shiftSubmission.ShiftKind}.",
                    resource.Name,
                    shiftSubmission.Date,
                    shiftSubmission.ShiftKind));

                continue;
            }

            if (!IsDateInsidePeriod(period, shiftSubmission.Date))
            {
                warnings.Add(new WorkerSubmissionNormalizationWarning(
                    WorkerSubmissionNormalizationWarningType.DateOutsidePeriod,
                    $"Resource '{resource.Name}' submitted selection outside the schedule period for {shiftSubmission.Date} {shiftSubmission.ShiftKind}.",
                    resource.Name,
                    shiftSubmission.Date,
                    shiftSubmission.ShiftKind));

                continue;
            }

            if (shiftSubmission.Choice == ShiftSubmissionChoice.Unavailable)
            {
                continue;
            }

            var matchingShift = FindMatchingShift(
                shifts,
                shiftSubmission.Date,
                shiftSubmission.ShiftKind);

            if (matchingShift is null)
            {
                warnings.Add(new WorkerSubmissionNormalizationWarning(
                    WorkerSubmissionNormalizationWarningType.NoMatchingShift,
                    $"No matching shift was found for resource '{resource.Name}' on {shiftSubmission.Date} with kind {shiftSubmission.ShiftKind}.",
                    resource.Name,
                    shiftSubmission.Date,
                    shiftSubmission.ShiftKind));

                continue;
            }

            availabilityWindows.Add(new AvailabilityWindow(
                resource.Id,
                matchingShift.StartUtc,
                matchingShift.EndUtc));

            resourcePreferences.Add(new ResourcePreference(
                resource.Id,
                matchingShift.StartUtc,
                matchingShift.EndUtc,
                ResourcePreferenceType.Prefer,
                ToPreferencePriority(shiftSubmission.Choice)));
        }

        return new WorkerSubmissionNormalizationResult(
            availabilityWindows,
            resourcePreferences,
            warnings);
    }

    private static bool IsDateInsidePeriod(
        SchedulePeriod period,
        DateOnly date)
    {
        var startDate = DateOnly.FromDateTime(period.StartUtc);
        var exclusiveEndDate = DateOnly.FromDateTime(period.EndUtc);

        return date >= startDate && date < exclusiveEndDate;
    }

    private static Shift? FindMatchingShift(
        IReadOnlyCollection<Shift> shifts,
        DateOnly date,
        ShiftKind shiftKind)
    {
        return shifts.FirstOrDefault(shift =>
            shift.Kind == shiftKind &&
            DateOnly.FromDateTime(shift.StartUtc) == date);
    }

    private static ResourcePreferencePriority ToPreferencePriority(
        ShiftSubmissionChoice choice)
    {
        return choice switch
        {
            ShiftSubmissionChoice.StrongAvailable => ResourcePreferencePriority.High,
            ShiftSubmissionChoice.Available => ResourcePreferencePriority.Medium,
            _ => throw new ArgumentOutOfRangeException(
                nameof(choice),
                "Shift submission choice does not map to a preference priority.")
        };
    }

    private static string CreateSubmissionKey(
        DateOnly date,
        ShiftKind shiftKind)
    {
        return $"{date:yyyy-MM-dd}:{shiftKind}";
    }
}
