namespace Aether.Domain.Optimization;

public sealed class SchedulingProblem
{
    public SchedulePeriod Period { get; }
    public IReadOnlyCollection<Resource> Resources { get; }
    public IReadOnlyCollection<Shift> Shifts { get; }
    public IReadOnlyCollection<AvailabilityWindow> AvailabilityWindows { get; }
    public IReadOnlyCollection<ResourcePreference> ResourcePreferences { get; }
    public IReadOnlyCollection<ResourceMonthlyNightShiftHistory> ResourceMonthlyNightShiftHistories { get; }
    public IReadOnlyCollection<ResourceWorkloadDemand> ResourceWorkloadDemands { get; }
    public int MinimumAssignedHoursPerResource { get; }
    public int MinimumMorningShiftsPerResourcePerFullWeek { get; }
    public int MinimumAfternoonShiftsPerResourcePerFullWeek { get; }
    public double? MaximumAssignedHoursDeviationFromAverageHours { get; }

    public SchedulingProblem(
        SchedulePeriod period,
        IReadOnlyCollection<Resource> resources,
        IReadOnlyCollection<Shift> shifts,
        IReadOnlyCollection<AvailabilityWindow> availabilityWindows,
        IReadOnlyCollection<ResourcePreference> resourcePreferences,
        int minimumAssignedHoursPerResource = 0,
        int minimumMorningShiftsPerResourcePerFullWeek = 0,
        int minimumAfternoonShiftsPerResourcePerFullWeek = 0,
        IReadOnlyCollection<ResourceMonthlyNightShiftHistory>? resourceMonthlyNightShiftHistories = null,
        double? maximumAssignedHoursDeviationFromAverageHours = null,
        IReadOnlyCollection<ResourceWorkloadDemand>? resourceWorkloadDemands = null)
    {
        ArgumentNullException.ThrowIfNull(period);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(shifts);
        ArgumentNullException.ThrowIfNull(availabilityWindows);
        ArgumentNullException.ThrowIfNull(resourcePreferences);

        if (minimumAssignedHoursPerResource < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumAssignedHoursPerResource),
                "Minimum assigned hours per resource cannot be negative.");
        }

        if (minimumMorningShiftsPerResourcePerFullWeek < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumMorningShiftsPerResourcePerFullWeek),
                "Minimum morning shifts per resource per full week cannot be negative.");
        }

        if (minimumAfternoonShiftsPerResourcePerFullWeek < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumAfternoonShiftsPerResourcePerFullWeek),
                "Minimum afternoon shifts per resource per full week cannot be negative.");
        }

        if (maximumAssignedHoursDeviationFromAverageHours is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumAssignedHoursDeviationFromAverageHours),
                "Maximum assigned hours deviation from average hours cannot be negative.");
        }

        if (resources.Count == 0)
        {
            throw new ArgumentException("At least one resource is required.", nameof(resources));
        }

        if (shifts.Count == 0)
        {
            throw new ArgumentException("At least one shift is required.", nameof(shifts));
        }

        var resourceIds = resources
            .Select(resource => resource.Id)
            .ToHashSet();

        var hasUnknownResourceAvailability = availabilityWindows
            .Any(window => !resourceIds.Contains(window.ResourceId));

        if (hasUnknownResourceAvailability)
        {
            throw new ArgumentException(
                "Availability windows must reference known resources.",
                nameof(availabilityWindows));
        }

        var hasUnknownResourcePreference = resourcePreferences
            .Any(preference => !resourceIds.Contains(preference.ResourceId));

        if (hasUnknownResourcePreference)
        {
            throw new ArgumentException(
                "Resource preferences must reference known resources.",
                nameof(resourcePreferences));
        }

        var monthlyNightShiftHistories =
            resourceMonthlyNightShiftHistories ?? Array.Empty<ResourceMonthlyNightShiftHistory>();

        var hasUnknownResourceMonthlyNightShiftHistory = monthlyNightShiftHistories
            .Any(history => !resourceIds.Contains(history.ResourceId));

        if (hasUnknownResourceMonthlyNightShiftHistory)
        {
            throw new ArgumentException(
                "Monthly night shift histories must reference known resources.",
                nameof(resourceMonthlyNightShiftHistories));
        }

        var workloadDemands =
            resourceWorkloadDemands ?? Array.Empty<ResourceWorkloadDemand>();

        var hasUnknownResourceWorkloadDemand = workloadDemands
            .Any(demand => !resourceIds.Contains(demand.ResourceId));

        if (hasUnknownResourceWorkloadDemand)
        {
            throw new ArgumentException(
                "Resource workload demands must reference known resources.",
                nameof(resourceWorkloadDemands));
        }

        var hasDuplicateResourceWorkloadDemand = workloadDemands
            .GroupBy(demand => demand.ResourceId)
            .Any(group => group.Count() > 1);

        if (hasDuplicateResourceWorkloadDemand)
        {
            throw new ArgumentException(
                "Only one resource workload demand is allowed per resource.",
                nameof(resourceWorkloadDemands));
        }

        Period = period;
        Resources = resources.ToArray();
        Shifts = shifts.ToArray();
        AvailabilityWindows = availabilityWindows.ToArray();
        ResourcePreferences = resourcePreferences.ToArray();
        ResourceMonthlyNightShiftHistories = monthlyNightShiftHistories.ToArray();
        ResourceWorkloadDemands = workloadDemands.ToArray();
        MinimumAssignedHoursPerResource = minimumAssignedHoursPerResource;
        MinimumMorningShiftsPerResourcePerFullWeek = minimumMorningShiftsPerResourcePerFullWeek;
        MinimumAfternoonShiftsPerResourcePerFullWeek = minimumAfternoonShiftsPerResourcePerFullWeek;
        MaximumAssignedHoursDeviationFromAverageHours = maximumAssignedHoursDeviationFromAverageHours;
    }
}
