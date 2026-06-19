using Aether.Application.Scheduling.Policies;
using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.Profiles;

public sealed class LastDorLocalScheduleGenerationProfile
{
    private const int DaysInSchedule = 14;

    public double TotalEffectiveTargetHours => 736.0;

    public double MaximumAssignedHoursDeviationFromAverageHours => 5.0;

    public int Seed => 20260603;

    public SchedulePeriod CreateSchedulePeriod()
    {
        return new SchedulePeriod(
            new DateTime(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 28, 0, 0, 0, DateTimeKind.Utc));
    }

    public IReadOnlyList<Resource> CreateResources()
    {
        var names = new[]
        {
            "Worker01",
            "Worker02",
            "Worker03",
            "Worker04",
            "Worker05",
            "Worker06",
            "Worker07",
            "Worker08",
            "Worker09",
            "Worker10",
            "Worker11",
            "Worker12",
            "Worker13",
            "Worker14",
            "Worker15",
            "Worker16",
            "Worker17",
            "Worker18",
            "Worker19"
        };

        return names
            .Select((name, index) => new Resource(
                CreateResourceGuid(index + 1),
                name,
                hourlyCost: 100m))
            .ToArray();
    }

    public IReadOnlyList<Shift> CreateShifts()
    {
        var shifts = CreateBiWeeklySequencePressureShifts();

        return new LastDorBusinessPolicyProfile()
            .ApplyToShifts(shifts);
    }

    private static IReadOnlyList<Shift> CreateBiWeeklySequencePressureShifts(
        int weekdayMorningMinResourceCount = 3)
    {
        if (weekdayMorningMinResourceCount is < 0 or > 6)
        {
            throw new ArgumentOutOfRangeException(
                nameof(weekdayMorningMinResourceCount),
                "Weekday morning minimum resource count must be between 0 and 6.");
        }

        var shifts = new List<Shift>();
        var shiftIndex = 1;

        for (var dayOffset = 0; dayOffset < DaysInSchedule; dayOffset++)
        {
            var date = new DateOnly(2026, 6, 14).AddDays(dayOffset);

            shifts.Add(CreateShift(shiftIndex++, date, ShiftKind.Morning, weekdayMorningMinResourceCount));
            shifts.Add(CreateShift(shiftIndex++, date, ShiftKind.Afternoon, weekdayMorningMinResourceCount));
            shifts.Add(CreateShift(shiftIndex++, date, ShiftKind.Night, weekdayMorningMinResourceCount));
        }

        return shifts
            .OrderBy(shift => shift.StartUtc)
            .ToArray();
    }

    private static Shift CreateShift(
        int shiftIndex,
        DateOnly date,
        ShiftKind kind,
        int weekdayMorningMinResourceCount)
    {
        var capacity = GetCapacityRule(
            date,
            kind,
            weekdayMorningMinResourceCount);

        return new Shift(
            CreateShiftGuid(shiftIndex),
            GetStartUtc(date, kind),
            GetEndUtc(date, kind),
            kind,
            capacity.Min,
            capacity.Max,
            requiresPreferenceToAssign: capacity.RequiresPreference,
            requiresMinimumWhenPreferenceExists: false,
            nightShiftCategory: GetNightShiftCategory(date, kind));
    }

    private static CapacityRule GetCapacityRule(
        DateOnly date,
        ShiftKind kind,
        int weekdayMorningMinResourceCount)
    {
        if (date.DayOfWeek == DayOfWeek.Friday)
        {
            return kind switch
            {
                ShiftKind.Morning => new CapacityRule(0, 2, true),
                ShiftKind.Afternoon => new CapacityRule(0, 1, true),
                ShiftKind.Night => new CapacityRule(0, 1, true),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
        }

        if (date.DayOfWeek == DayOfWeek.Saturday)
        {
            return kind switch
            {
                ShiftKind.Morning => new CapacityRule(0, 1, true),
                ShiftKind.Afternoon => new CapacityRule(0, 1, true),
                ShiftKind.Night => new CapacityRule(3, 3, false),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
        }

        return kind switch
        {
            ShiftKind.Morning => new CapacityRule(weekdayMorningMinResourceCount, 6, false),
            ShiftKind.Afternoon => new CapacityRule(2, 4, true),
            ShiftKind.Night => new CapacityRule(0, 1, true),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private static DateTime GetStartUtc(
        DateOnly date,
        ShiftKind kind)
    {
        var time = kind switch
        {
            ShiftKind.Morning => new TimeOnly(6, 30),
            ShiftKind.Afternoon => new TimeOnly(14, 20),
            ShiftKind.Night => new TimeOnly(22, 40),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        return date.ToDateTime(time, DateTimeKind.Utc);
    }

    private static DateTime GetEndUtc(
        DateOnly date,
        ShiftKind kind)
    {
        var endDate = kind == ShiftKind.Night
            ? date.AddDays(1)
            : date;

        var time = kind switch
        {
            ShiftKind.Morning => new TimeOnly(14, 20),
            ShiftKind.Afternoon => new TimeOnly(22, 40),
            ShiftKind.Night => new TimeOnly(6, 30),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        return endDate.ToDateTime(time, DateTimeKind.Utc);
    }

    private static NightShiftCategory? GetNightShiftCategory(
        DateOnly date,
        ShiftKind kind)
    {
        if (kind != ShiftKind.Night)
        {
            return null;
        }

        return date.DayOfWeek switch
        {
            DayOfWeek.Friday => NightShiftCategory.FridayNight,
            DayOfWeek.Saturday => NightShiftCategory.MotzeiShabbatNight,
            _ => NightShiftCategory.Regular
        };
    }

    private static Guid CreateResourceGuid(int index)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{index:000000000000}");
    }

    private static Guid CreateShiftGuid(int index)
    {
        return Guid.Parse($"00000000-0000-0000-0001-{index:000000000000}");
    }

    private sealed record CapacityRule(
        int Min,
        int Max,
        bool RequiresPreference);
}
