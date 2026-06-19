using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.SubmissionForms;

public sealed class MandatoryShiftAvailabilityPolicy
{
    public MandatoryShiftAvailabilityPolicyResult Apply(
        IReadOnlyCollection<Resource> resources,
        IReadOnlyCollection<Shift> shifts,
        IReadOnlyCollection<AvailabilityWindow> availabilityWindows,
        IReadOnlyCollection<ResourcePreference> resourcePreferences)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(shifts);
        ArgumentNullException.ThrowIfNull(availabilityWindows);
        ArgumentNullException.ThrowIfNull(resourcePreferences);

        var enrichedAvailabilityWindows = availabilityWindows.ToList();
        var enrichedResourcePreferences = resourcePreferences.ToList();

        foreach (var shift in shifts.Where(IsMandatoryPolicyShift))
        {
            foreach (var resource in resources)
            {
                EnsureAvailability(
                    enrichedAvailabilityWindows,
                    resource,
                    shift);

                EnsureAvoidWhenNoPreferExists(
                    enrichedResourcePreferences,
                    resource,
                    shift);
            }
        }

        return new MandatoryShiftAvailabilityPolicyResult(
            enrichedAvailabilityWindows,
            enrichedResourcePreferences);
    }

    private static bool IsMandatoryPolicyShift(Shift shift)
    {
        return IsWeekdayMorning(shift) ||
               shift.NightShiftCategory == NightShiftCategory.MotzeiShabbatNight;
    }

    private static bool IsWeekdayMorning(Shift shift)
    {
        var date = DateOnly.FromDateTime(shift.StartUtc);

        return shift.Kind == ShiftKind.Morning &&
               date.DayOfWeek is not DayOfWeek.Friday and not DayOfWeek.Saturday;
    }

    private static void EnsureAvailability(
        ICollection<AvailabilityWindow> availabilityWindows,
        Resource resource,
        Shift shift)
    {
        var alreadyAvailable = availabilityWindows.Any(window =>
            window.ResourceId == resource.Id &&
            window.Covers(shift));

        if (alreadyAvailable)
        {
            return;
        }

        availabilityWindows.Add(new AvailabilityWindow(
            resource.Id,
            shift.StartUtc,
            shift.EndUtc));
    }

    private static void EnsureAvoidWhenNoPreferExists(
        ICollection<ResourcePreference> resourcePreferences,
        Resource resource,
        Shift shift)
    {
        if (HasPreference(
                resourcePreferences,
                resource,
                shift,
                ResourcePreferenceType.Prefer))
        {
            return;
        }

        if (HasPreference(
                resourcePreferences,
                resource,
                shift,
                ResourcePreferenceType.Avoid))
        {
            return;
        }

        resourcePreferences.Add(new ResourcePreference(
            resource.Id,
            shift.StartUtc,
            shift.EndUtc,
            ResourcePreferenceType.Avoid,
            ResourcePreferencePriority.High));
    }

    private static bool HasPreference(
        IEnumerable<ResourcePreference> resourcePreferences,
        Resource resource,
        Shift shift,
        ResourcePreferenceType type)
    {
        return resourcePreferences.Any(preference =>
            preference.ResourceId == resource.Id &&
            preference.Type == type &&
            Overlaps(
                preference.StartUtc,
                preference.EndUtc,
                shift.StartUtc,
                shift.EndUtc));
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
}
