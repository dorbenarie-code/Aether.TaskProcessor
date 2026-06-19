using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.ManagerConstraints;

public sealed class ManagerConstraintApplicator
{
    public IReadOnlyList<Shift> ApplyToShifts(
        IReadOnlyCollection<Shift> shifts,
        ManagerConstraintSet constraintSet)
    {
        ArgumentNullException.ThrowIfNull(shifts);
        ArgumentNullException.ThrowIfNull(constraintSet);

        if (constraintSet.ShiftCapacityOverrides.Count == 0)
        {
            return shifts.ToArray();
        }

        var overridesByShiftId = constraintSet.ShiftCapacityOverrides
            .GroupBy(capacityOverride => capacityOverride.ShiftId)
            .ToDictionary(
                group => group.Key,
                group => group.Last());

        return shifts
            .Select(shift => ApplyToShift(
                shift,
                overridesByShiftId))
            .ToArray();
    }

    private static Shift ApplyToShift(
        Shift shift,
        IReadOnlyDictionary<Guid, ManagerShiftCapacityOverride> overridesByShiftId)
    {
        if (!overridesByShiftId.TryGetValue(
                shift.Id,
                out var capacityOverride))
        {
            return shift;
        }

        return new Shift(
            shift.Id,
            shift.StartUtc,
            shift.EndUtc,
            shift.Kind,
            capacityOverride.MinResourceCount,
            capacityOverride.MaxResourceCount,
            shift.RequiresPreferenceToAssign,
            shift.RequiresMinimumWhenPreferenceExists,
            shift.NightShiftCategory);
    }

    public ManagerConstraintApplicationResult Apply(
        IReadOnlyCollection<Resource> resources,
        IReadOnlyCollection<Shift> shifts,
        IReadOnlyCollection<AvailabilityWindow> availabilityWindows,
        IReadOnlyCollection<ResourcePreference> resourcePreferences,
        ManagerConstraintSet constraintSet)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(shifts);
        ArgumentNullException.ThrowIfNull(availabilityWindows);
        ArgumentNullException.ThrowIfNull(resourcePreferences);
        ArgumentNullException.ThrowIfNull(constraintSet);

        var forbiddenIntervals = CreateForbiddenIntervals(
            shifts,
            constraintSet);

        var avoidIntervals = CreateAvoidIntervals(
            shifts,
            constraintSet);

        var updatedAvailabilityWindows = ApplyForbiddenIntervalsToAvailability(
            availabilityWindows,
            forbiddenIntervals);

        var resourcePreferencesAfterForbid = ApplyForbiddenIntervalsToPreferences(
            resourcePreferences,
            forbiddenIntervals);

        var updatedResourcePreferences = ApplyAvoidIntervalsToPreferences(
            resourcePreferencesAfterForbid,
            avoidIntervals,
            forbiddenIntervals);

        return new ManagerConstraintApplicationResult(
            updatedAvailabilityWindows,
            updatedResourcePreferences);
    }

    private static IReadOnlyCollection<ForbiddenInterval> CreateForbiddenIntervals(
        IReadOnlyCollection<Shift> shifts,
        ManagerConstraintSet constraintSet)
    {
        var shiftsById = shifts.ToDictionary(shift => shift.Id);
        var intervals = new List<ForbiddenInterval>();

        foreach (var forbiddenAssignment in constraintSet.ForbiddenAssignments)
        {
            if (!shiftsById.TryGetValue(
                    forbiddenAssignment.ShiftId,
                    out var shift))
            {
                continue;
            }

            intervals.Add(new ForbiddenInterval(
                forbiddenAssignment.ResourceId,
                shift.StartUtc,
                shift.EndUtc));
        }

        return intervals;
    }

    private static IReadOnlyCollection<AvoidInterval> CreateAvoidIntervals(
        IReadOnlyCollection<Shift> shifts,
        ManagerConstraintSet constraintSet)
    {
        var shiftsById = shifts.ToDictionary(shift => shift.Id);
        var intervals = new List<AvoidInterval>();

        foreach (var avoidAssignment in constraintSet.AvoidAssignments)
        {
            if (!shiftsById.TryGetValue(
                    avoidAssignment.ShiftId,
                    out var shift))
            {
                continue;
            }

            intervals.Add(new AvoidInterval(
                avoidAssignment.ResourceId,
                shift.StartUtc,
                shift.EndUtc));
        }

        return intervals;
    }

    private static IReadOnlyCollection<AvailabilityWindow> ApplyForbiddenIntervalsToAvailability(
        IReadOnlyCollection<AvailabilityWindow> availabilityWindows,
        IReadOnlyCollection<ForbiddenInterval> forbiddenIntervals)
    {
        var result = new List<AvailabilityWindow>();

        foreach (var window in availabilityWindows)
        {
            var remainingIntervals = SubtractForbiddenIntervals(
                window.ResourceId,
                window.StartUtc,
                window.EndUtc,
                forbiddenIntervals);

            foreach (var interval in remainingIntervals)
            {
                result.Add(new AvailabilityWindow(
                    window.ResourceId,
                    interval.StartUtc,
                    interval.EndUtc));
            }
        }

        return result;
    }

    private static IReadOnlyCollection<ResourcePreference> ApplyForbiddenIntervalsToPreferences(
        IReadOnlyCollection<ResourcePreference> resourcePreferences,
        IReadOnlyCollection<ForbiddenInterval> forbiddenIntervals)
    {
        var result = new List<ResourcePreference>();

        foreach (var preference in resourcePreferences)
        {
            var remainingIntervals = SubtractForbiddenIntervals(
                preference.ResourceId,
                preference.StartUtc,
                preference.EndUtc,
                forbiddenIntervals);

            foreach (var interval in remainingIntervals)
            {
                result.Add(new ResourcePreference(
                    preference.ResourceId,
                    interval.StartUtc,
                    interval.EndUtc,
                    preference.Type,
                    preference.Priority));
            }
        }

        return result;
    }

    private static IReadOnlyCollection<ResourcePreference> ApplyAvoidIntervalsToPreferences(
        IReadOnlyCollection<ResourcePreference> resourcePreferences,
        IReadOnlyCollection<AvoidInterval> avoidIntervals,
        IReadOnlyCollection<ForbiddenInterval> forbiddenIntervals)
    {
        if (avoidIntervals.Count == 0)
        {
            return resourcePreferences;
        }

        var result = new List<ResourcePreference>();

        foreach (var preference in resourcePreferences)
        {
            var remainingIntervals = preference.Type == ResourcePreferenceType.Prefer
                ? SubtractAvoidIntervals(
                    preference.ResourceId,
                    preference.StartUtc,
                    preference.EndUtc,
                    avoidIntervals)
                : [new TimeInterval(preference.StartUtc, preference.EndUtc)];

            foreach (var interval in remainingIntervals)
            {
                result.Add(new ResourcePreference(
                    preference.ResourceId,
                    interval.StartUtc,
                    interval.EndUtc,
                    preference.Type,
                    preference.Priority));
            }
        }

        foreach (var avoidInterval in avoidIntervals)
        {
            var remainingIntervals = SubtractForbiddenIntervals(
                avoidInterval.ResourceId,
                avoidInterval.StartUtc,
                avoidInterval.EndUtc,
                forbiddenIntervals);

            foreach (var interval in remainingIntervals)
            {
                if (HasAvoidPreferenceCovering(
                        result,
                        avoidInterval.ResourceId,
                        interval))
                {
                    continue;
                }

                result.Add(new ResourcePreference(
                    avoidInterval.ResourceId,
                    interval.StartUtc,
                    interval.EndUtc,
                    ResourcePreferenceType.Avoid,
                    ResourcePreferencePriority.High));
            }
        }

        return result;
    }

    private static bool HasAvoidPreferenceCovering(
        IReadOnlyCollection<ResourcePreference> preferences,
        Guid resourceId,
        TimeInterval interval)
    {
        return preferences
            .Where(preference => preference.ResourceId == resourceId)
            .Where(preference => preference.Type == ResourcePreferenceType.Avoid)
            .Any(preference =>
                preference.StartUtc <= interval.StartUtc &&
                preference.EndUtc >= interval.EndUtc);
    }

    private static IReadOnlyCollection<TimeInterval> SubtractForbiddenIntervals(
        Guid resourceId,
        DateTime startUtc,
        DateTime endUtc,
        IReadOnlyCollection<ForbiddenInterval> forbiddenIntervals)
    {
        var remainingIntervals = new List<TimeInterval>
        {
            new(startUtc, endUtc)
        };

        foreach (var forbiddenInterval in forbiddenIntervals
                     .Where(interval => interval.ResourceId == resourceId))
        {
            remainingIntervals = remainingIntervals
                .SelectMany(interval => SubtractForbiddenInterval(
                    interval,
                    forbiddenInterval))
                .ToList();
        }

        return remainingIntervals;
    }

    private static IReadOnlyCollection<TimeInterval> SubtractForbiddenInterval(
        TimeInterval interval,
        ForbiddenInterval forbiddenInterval)
    {
        if (!Overlaps(
                interval.StartUtc,
                interval.EndUtc,
                forbiddenInterval.StartUtc,
                forbiddenInterval.EndUtc))
        {
            return [interval];
        }

        var remainingIntervals = new List<TimeInterval>();

        if (interval.StartUtc < forbiddenInterval.StartUtc)
        {
            remainingIntervals.Add(new TimeInterval(
                interval.StartUtc,
                forbiddenInterval.StartUtc));
        }

        if (forbiddenInterval.EndUtc < interval.EndUtc)
        {
            remainingIntervals.Add(new TimeInterval(
                forbiddenInterval.EndUtc,
                interval.EndUtc));
        }

        return remainingIntervals;
    }

    private static IReadOnlyCollection<TimeInterval> SubtractAvoidIntervals(
        Guid resourceId,
        DateTime startUtc,
        DateTime endUtc,
        IReadOnlyCollection<AvoidInterval> avoidIntervals)
    {
        var remainingIntervals = new List<TimeInterval>
        {
            new(startUtc, endUtc)
        };

        foreach (var avoidInterval in avoidIntervals
                     .Where(interval => interval.ResourceId == resourceId))
        {
            remainingIntervals = remainingIntervals
                .SelectMany(interval => SubtractAvoidInterval(
                    interval,
                    avoidInterval))
                .ToList();
        }

        return remainingIntervals;
    }

    private static IReadOnlyCollection<TimeInterval> SubtractAvoidInterval(
        TimeInterval interval,
        AvoidInterval avoidInterval)
    {
        if (!Overlaps(
                interval.StartUtc,
                interval.EndUtc,
                avoidInterval.StartUtc,
                avoidInterval.EndUtc))
        {
            return [interval];
        }

        var remainingIntervals = new List<TimeInterval>();

        if (interval.StartUtc < avoidInterval.StartUtc)
        {
            remainingIntervals.Add(new TimeInterval(
                interval.StartUtc,
                avoidInterval.StartUtc));
        }

        if (avoidInterval.EndUtc < interval.EndUtc)
        {
            remainingIntervals.Add(new TimeInterval(
                avoidInterval.EndUtc,
                interval.EndUtc));
        }

        return remainingIntervals;
    }

    private static bool Overlaps(
        DateTime firstStartUtc,
        DateTime firstEndUtc,
        DateTime secondStartUtc,
        DateTime secondEndUtc)
    {
        return firstStartUtc < secondEndUtc &&
               secondStartUtc < firstEndUtc;
    }

    private sealed record AvoidInterval(
        Guid ResourceId,
        DateTime StartUtc,
        DateTime EndUtc);

    private sealed record ForbiddenInterval(
        Guid ResourceId,
        DateTime StartUtc,
        DateTime EndUtc);

    private sealed record TimeInterval(
        DateTime StartUtc,
        DateTime EndUtc);
}
