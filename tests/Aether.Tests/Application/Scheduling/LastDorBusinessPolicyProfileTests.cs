using Aether.Application.Scheduling.Policies;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class LastDorBusinessPolicyProfileTests
{
    [Fact]
    public void ApplyToShifts_ShouldSetWeekdayMorningCapacity_AndLeaveOtherShiftsUnchanged()
    {
        var sundayMorning = CreateShift(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            new DateOnly(2026, 6, 14),
            ShiftKind.Morning,
            minResourceCount: 3,
            maxResourceCount: 6,
            requiresPreferenceToAssign: true,
            requiresMinimumWhenPreferenceExists: true);

        var thursdayMorning = CreateShift(
            "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
            new DateOnly(2026, 6, 18),
            ShiftKind.Morning,
            minResourceCount: 3,
            maxResourceCount: 5);

        var fridayMorning = CreateShift(
            "cccccccc-cccc-cccc-cccc-cccccccccccc",
            new DateOnly(2026, 6, 19),
            ShiftKind.Morning,
            minResourceCount: 0,
            maxResourceCount: 2,
            requiresPreferenceToAssign: true);

        var saturdayMorning = CreateShift(
            "dddddddd-dddd-dddd-dddd-dddddddddddd",
            new DateOnly(2026, 6, 20),
            ShiftKind.Morning,
            minResourceCount: 0,
            maxResourceCount: 1);

        var mondayAfternoon = CreateShift(
            "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee",
            new DateOnly(2026, 6, 15),
            ShiftKind.Afternoon,
            minResourceCount: 2,
            maxResourceCount: 4,
            requiresMinimumWhenPreferenceExists: true);

        var mondayNight = CreateShift(
            "ffffffff-ffff-ffff-ffff-ffffffffffff",
            new DateOnly(2026, 6, 15),
            ShiftKind.Night,
            minResourceCount: 0,
            maxResourceCount: 1,
            nightShiftCategory: NightShiftCategory.Regular);

        var shifts = new[]
        {
            sundayMorning,
            thursdayMorning,
            fridayMorning,
            saturdayMorning,
            mondayAfternoon,
            mondayNight
        };

        var profile = new LastDorBusinessPolicyProfile();

        var result = profile.ApplyToShifts(shifts);

        Assert.Equal(shifts.Length, result.Count);

        AssertCapacity(result, sundayMorning.Id, minResourceCount: 4, maxResourceCount: 6);
        AssertSameIdentityAndMetadata(sundayMorning, GetShift(result, sundayMorning.Id));

        AssertCapacity(result, thursdayMorning.Id, minResourceCount: 4, maxResourceCount: 6);
        AssertSameIdentityAndMetadata(thursdayMorning, GetShift(result, thursdayMorning.Id));

        AssertUnchangedShift(result, fridayMorning);
        AssertUnchangedShift(result, saturdayMorning);
        AssertUnchangedShift(result, mondayAfternoon);
        AssertUnchangedShift(result, mondayNight);
    }

    private static void AssertCapacity(
        IReadOnlyCollection<Shift> shifts,
        Guid shiftId,
        int minResourceCount,
        int maxResourceCount)
    {
        var shift = GetShift(shifts, shiftId);

        Assert.Equal(minResourceCount, shift.MinResourceCount);
        Assert.Equal(maxResourceCount, shift.MaxResourceCount);
    }

    private static void AssertUnchangedShift(
        IReadOnlyCollection<Shift> shifts,
        Shift expected)
    {
        var actual = GetShift(shifts, expected.Id);

        AssertSameIdentityAndMetadata(expected, actual);
        Assert.Equal(expected.MinResourceCount, actual.MinResourceCount);
        Assert.Equal(expected.MaxResourceCount, actual.MaxResourceCount);
    }

    private static void AssertSameIdentityAndMetadata(
        Shift expected,
        Shift actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.StartUtc, actual.StartUtc);
        Assert.Equal(expected.EndUtc, actual.EndUtc);
        Assert.Equal(expected.Kind, actual.Kind);
        Assert.Equal(expected.NightShiftCategory, actual.NightShiftCategory);
        Assert.Equal(expected.RequiresPreferenceToAssign, actual.RequiresPreferenceToAssign);
        Assert.Equal(expected.RequiresMinimumWhenPreferenceExists, actual.RequiresMinimumWhenPreferenceExists);
    }

    private static Shift GetShift(
        IReadOnlyCollection<Shift> shifts,
        Guid shiftId)
    {
        return shifts.Single(shift => shift.Id == shiftId);
    }

    private static Shift CreateShift(
        string id,
        DateOnly date,
        ShiftKind kind,
        int minResourceCount,
        int maxResourceCount,
        bool requiresPreferenceToAssign = false,
        bool requiresMinimumWhenPreferenceExists = false,
        NightShiftCategory? nightShiftCategory = null)
    {
        return new Shift(
            Guid.Parse(id),
            GetStartUtc(date, kind),
            GetEndUtc(date, kind),
            kind,
            minResourceCount,
            maxResourceCount,
            requiresPreferenceToAssign,
            requiresMinimumWhenPreferenceExists,
            nightShiftCategory);
    }

    private static DateTime GetStartUtc(DateOnly date, ShiftKind kind)
    {
        return kind switch
        {
            ShiftKind.Morning => date.ToDateTime(new TimeOnly(6, 30), DateTimeKind.Utc),
            ShiftKind.Afternoon => date.ToDateTime(new TimeOnly(14, 20), DateTimeKind.Utc),
            ShiftKind.Night => date.ToDateTime(new TimeOnly(22, 40), DateTimeKind.Utc),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), "Shift kind is not supported.")
        };
    }

    private static DateTime GetEndUtc(DateOnly date, ShiftKind kind)
    {
        return kind switch
        {
            ShiftKind.Morning => date.ToDateTime(new TimeOnly(14, 20), DateTimeKind.Utc),
            ShiftKind.Afternoon => date.ToDateTime(new TimeOnly(22, 40), DateTimeKind.Utc),
            ShiftKind.Night => date.AddDays(1).ToDateTime(new TimeOnly(6, 30), DateTimeKind.Utc),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), "Shift kind is not supported.")
        };
    }
}
