using Aether.Application.Scheduling.Contracts;
using Aether.Application.Scheduling.Interfaces;
using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.Builders;

public sealed class SchedulingProblemBuilder : ISchedulingProblemBuilder
{
    public SchedulingProblemBuildResult Build(SchedulingProblemBuildRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        ThrowIfDuplicateResourceNames(request.Resources);

        var resourcesByName = request.Resources.ToDictionary(
            resource => resource.Name.Trim(),
            StringComparer.Ordinal);

        var availabilityWindows = new List<AvailabilityWindow>();
        var resourcePreferences = new List<ResourcePreference>();
        var warnings = new List<SchedulingProblemBuildWarning>();

        foreach (var submission in request.ResourceSubmissions)
        {
            var resourceName = submission.ResourceName.Trim();

            if (!resourcesByName.TryGetValue(resourceName, out var resource))
            {
                warnings.Add(new SchedulingProblemBuildWarning(
                    SchedulingProblemBuildWarningType.UnknownResourceName,
                    $"Submission resource name '{submission.ResourceName}' does not match any known resource.",
                    submission.ResourceName));

                continue;
            }

            if (!string.IsNullOrWhiteSpace(submission.RawSpecialRequestNote))
            {
                warnings.Add(new SchedulingProblemBuildWarning(
                    SchedulingProblemBuildWarningType.RawSpecialRequestNote,
                    $"Resource '{resource.Name}' submitted a raw special request note that was not parsed.",
                    resource.Name));
            }

            foreach (var selection in submission.ShiftSelections)
            {
                if (!selection.IsSelected)
                {
                    continue;
                }

                var matchingShift = FindMatchingShift(
                    request.Shifts,
                    selection.Date,
                    selection.ShiftKind);

                if (matchingShift is null)
                {
                    warnings.Add(new SchedulingProblemBuildWarning(
                        SchedulingProblemBuildWarningType.NoMatchingShift,
                        $"No matching shift was found for resource '{resource.Name}' on {selection.Date} with kind {selection.ShiftKind}.",
                        resource.Name,
                        selection.Date,
                        selection.ShiftKind));

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
                    ResourcePreferencePriority.High));
            }
        }

        var problem = new SchedulingProblem(
            period: request.Period,
            resources: request.Resources,
            shifts: request.Shifts,
            availabilityWindows: availabilityWindows,
            resourcePreferences: resourcePreferences,
            minimumAssignedHoursPerResource: request.MinimumAssignedHoursPerResource,
            minimumMorningShiftsPerResourcePerFullWeek: request.MinimumMorningShiftsPerResourcePerFullWeek,
            minimumAfternoonShiftsPerResourcePerFullWeek: request.MinimumAfternoonShiftsPerResourcePerFullWeek);

        return new SchedulingProblemBuildResult(problem, warnings);
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

    private static void ThrowIfDuplicateResourceNames(
        IReadOnlyCollection<Resource> resources)
    {
        var duplicateResourceName = resources
            .Select(resource => resource.Name.Trim())
            .GroupBy(name => name, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateResourceName is not null)
        {
            throw new ArgumentException(
                $"Duplicate resource name '{duplicateResourceName.Key}' was found.",
                nameof(resources));
        }
    }
}
