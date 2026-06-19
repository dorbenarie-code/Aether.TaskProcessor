using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class SchedulingProblemWeeklyMinimumShiftMixTests
{
    [Fact]
    public void Scheduling_problem_disables_weekly_minimum_shift_mix_by_default()
    {
        var problem = CreateProblem();

        Assert.Equal(0, problem.MinimumMorningShiftsPerResourcePerFullWeek);
        Assert.Equal(0, problem.MinimumAfternoonShiftsPerResourcePerFullWeek);
    }

    [Fact]
    public void Scheduling_problem_stores_weekly_minimum_shift_mix_policy()
    {
        var problem = CreateProblem(
            minimumMorningShiftsPerResourcePerFullWeek: 2,
            minimumAfternoonShiftsPerResourcePerFullWeek: 1);

        Assert.Equal(2, problem.MinimumMorningShiftsPerResourcePerFullWeek);
        Assert.Equal(1, problem.MinimumAfternoonShiftsPerResourcePerFullWeek);
    }

    [Fact]
    public void Scheduling_problem_rejects_negative_minimum_morning_shifts_per_full_week()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateProblem(minimumMorningShiftsPerResourcePerFullWeek: -1));

        Assert.Equal("minimumMorningShiftsPerResourcePerFullWeek", exception.ParamName);
    }

    [Fact]
    public void Scheduling_problem_rejects_negative_minimum_afternoon_shifts_per_full_week()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateProblem(minimumAfternoonShiftsPerResourcePerFullWeek: -1));

        Assert.Equal("minimumAfternoonShiftsPerResourcePerFullWeek", exception.ParamName);
    }

    private static SchedulingProblem CreateProblem(
        int minimumMorningShiftsPerResourcePerFullWeek = 0,
        int minimumAfternoonShiftsPerResourcePerFullWeek = 0)
    {
        var resource = new Resource(
            Guid.NewGuid(),
            "Dana",
            hourlyCost: 100m);

        var shift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc),
            kind: ShiftKind.Morning,
            minResourceCount: 1,
            maxResourceCount: 1);

        return new SchedulingProblem(
            period: new SchedulePeriod(
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc)),
            resources: [resource],
            shifts: [shift],
            availabilityWindows: [],
            resourcePreferences: [],
            minimumMorningShiftsPerResourcePerFullWeek: minimumMorningShiftsPerResourcePerFullWeek,
            minimumAfternoonShiftsPerResourcePerFullWeek: minimumAfternoonShiftsPerResourcePerFullWeek);
    }
}
