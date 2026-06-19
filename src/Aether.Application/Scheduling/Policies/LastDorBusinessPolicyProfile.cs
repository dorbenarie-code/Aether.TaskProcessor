using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.Policies;

public sealed class LastDorBusinessPolicyProfile
{
    private const int WeekdayMorningMinResourceCount = 4;
    private const int WeekdayMorningMaxResourceCount = 6;

    public IReadOnlyList<Shift> ApplyToShifts(IReadOnlyCollection<Shift> shifts)
    {
        ArgumentNullException.ThrowIfNull(shifts);

        return shifts
            .Select(ApplyToShift)
            .ToArray();
    }

    private static Shift ApplyToShift(Shift shift)
    {
        if (!IsWeekdayMorning(shift))
        {
            return shift;
        }

        return new Shift(
            shift.Id,
            shift.StartUtc,
            shift.EndUtc,
            shift.Kind,
            WeekdayMorningMinResourceCount,
            WeekdayMorningMaxResourceCount,
            shift.RequiresPreferenceToAssign,
            shift.RequiresMinimumWhenPreferenceExists,
            shift.NightShiftCategory);
    }

    private static bool IsWeekdayMorning(Shift shift)
    {
        return shift.Kind == ShiftKind.Morning &&
               IsIsraeliBusinessWeekday(shift.StartUtc.DayOfWeek);
    }

    private static bool IsIsraeliBusinessWeekday(DayOfWeek dayOfWeek)
    {
        return dayOfWeek is DayOfWeek.Sunday
            or DayOfWeek.Monday
            or DayOfWeek.Tuesday
            or DayOfWeek.Wednesday
            or DayOfWeek.Thursday;
    }
}
