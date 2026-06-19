using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.Builders;

public sealed class ProportionalWorkloadDemandBuilder
{
    public IReadOnlyList<ResourceWorkloadDemand> Build(
        IReadOnlyCollection<Resource> resources,
        IReadOnlyCollection<Shift> shifts,
        IReadOnlyCollection<ResourcePreference> preferences,
        double totalEffectiveTargetHours)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(shifts);
        ArgumentNullException.ThrowIfNull(preferences);

        if (!double.IsFinite(totalEffectiveTargetHours))
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalEffectiveTargetHours),
                "Total effective target hours must be a finite number.");
        }

        if (totalEffectiveTargetHours <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalEffectiveTargetHours),
                "Total effective target hours must be greater than zero.");
        }

        if (resources.Count == 0)
        {
            throw new ArgumentException(
                "At least one resource is required.",
                nameof(resources));
        }

        if (shifts.Count == 0)
        {
            throw new ArgumentException(
                "At least one shift is required.",
                nameof(shifts));
        }

        var submittedPreferredHoursByResourceId = resources.ToDictionary(
            resource => resource.Id,
            resource => GetSubmittedPreferredHours(
                resource,
                shifts,
                preferences));

        var totalSubmittedPreferredHours = submittedPreferredHoursByResourceId
            .Values
            .Sum();

        if (totalSubmittedPreferredHours <= 0)
        {
            throw new InvalidOperationException(
                "Cannot create workload demands without submitted preferred hours.");
        }

        return resources
            .Select(resource =>
            {
                var submittedPreferredHours = submittedPreferredHoursByResourceId[resource.Id];

                var requestedPreferredHours =
                    totalEffectiveTargetHours *
                    submittedPreferredHours /
                    totalSubmittedPreferredHours;

                return new ResourceWorkloadDemand(
                    resource.Id,
                    requestedPreferredHours: requestedPreferredHours,
                    minimumRequiredHours: 0);
            })
            .ToArray();
    }

    private static double GetSubmittedPreferredHours(
        Resource resource,
        IReadOnlyCollection<Shift> shifts,
        IReadOnlyCollection<ResourcePreference> preferences)
    {
        return preferences
            .Where(preference => preference.ResourceId == resource.Id)
            .Where(preference => preference.Type == ResourcePreferenceType.Prefer)
            .Sum(preference =>
            {
                var shift = FindSingleMatchingShift(shifts, preference);

                return GetShiftHours(shift);
            });
    }

    private static Shift FindSingleMatchingShift(
        IReadOnlyCollection<Shift> shifts,
        ResourcePreference preference)
    {
        var matchingShifts = shifts
            .Where(shift => shift.StartUtc == preference.StartUtc)
            .Where(shift => shift.EndUtc == preference.EndUtc)
            .ToArray();

        if (matchingShifts.Length == 0)
        {
            throw new InvalidOperationException(
                "Cannot create workload demands because a preference does not have a matching shift.");
        }

        if (matchingShifts.Length > 1)
        {
            throw new InvalidOperationException(
                "Cannot create workload demands because a preference matches more than one shift.");
        }

        return matchingShifts[0];
    }

    private static double GetShiftHours(Shift shift)
    {
        return (shift.EndUtc - shift.StartUtc).TotalHours;
    }
}
