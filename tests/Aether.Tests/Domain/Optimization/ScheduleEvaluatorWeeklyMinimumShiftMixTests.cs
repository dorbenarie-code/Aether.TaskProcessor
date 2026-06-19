using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ScheduleEvaluatorWeeklyMinimumShiftMixTests
{
    [Fact]
    public void Evaluate_keeps_result_feasible_when_weekly_minimum_shift_mix_is_not_met()
    {
        var resource = new Resource(
            Guid.NewGuid(),
            "Dana",
            hourlyCost: 100m);

        var morningShift = CreateShift(
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            ShiftKind.Morning);

        var afternoonShift = CreateShift(
            new DateTime(2026, 1, 2, 14, 0, 0, DateTimeKind.Utc),
            ShiftKind.Afternoon);

        var problem = new SchedulingProblem(
            period: new SchedulePeriod(
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 1, 8, 0, 0, 0, DateTimeKind.Utc)),
            resources: [resource],
            shifts: [morningShift, afternoonShift],
            availabilityWindows: [CreateAvailability(resource)],
            resourcePreferences: [],
            minimumMorningShiftsPerResourcePerFullWeek: 2,
            minimumAfternoonShiftsPerResourcePerFullWeek: 1);

        var candidate = new ScheduleCandidate(
        [
            new Assignment(resource.Id, morningShift.Id),
            new Assignment(resource.Id, afternoonShift.Id)
        ]);

        var evaluator = new ScheduleEvaluator();

        var result = evaluator.Evaluate(problem, candidate);

        Assert.True(result.IsFeasible);
        Assert.Equal(0, result.Score.HardViolationCount);
        Assert.Equal(1, result.Score.SoftViolationCount);
        Assert.Equal(1000, result.Score.TotalPenalty);
    }

    private static Shift CreateShift(
        DateTime startUtc,
        ShiftKind kind)
    {
        return new Shift(
            Guid.NewGuid(),
            startUtc,
            startUtc.AddHours(8),
            kind,
            minResourceCount: 0,
            maxResourceCount: 1);
    }

    private static AvailabilityWindow CreateAvailability(Resource resource)
    {
        return new AvailabilityWindow(
            resource.Id,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 8, 0, 0, 0, DateTimeKind.Utc));
    }
}
