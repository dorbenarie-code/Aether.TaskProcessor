using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class SchedulingProblemMonthlyNightShiftHistoryTests
{
    [Fact]
    public void Scheduling_problem_disables_monthly_night_shift_history_by_default()
    {
        var problem = CreateProblem();

        Assert.Empty(problem.ResourceMonthlyNightShiftHistories);
    }

    [Fact]
    public void Scheduling_problem_accepts_monthly_night_shift_history()
    {
        var resource = CreateResource();

        var history = new ResourceMonthlyNightShiftHistory(
            resource.Id,
            year: 2026,
            month: 1,
            NightShiftCategory.MotzeiShabbatNight,
            assignedCount: 1);

        var problem = CreateProblem(
            resources: [resource],
            resourceMonthlyNightShiftHistories: [history]);

        var storedHistory = Assert.Single(problem.ResourceMonthlyNightShiftHistories);

        Assert.Equal(history, storedHistory);
    }

    [Fact]
    public void Scheduling_problem_rejects_monthly_night_shift_history_for_unknown_resource()
    {
        var knownResource = CreateResource();

        var history = new ResourceMonthlyNightShiftHistory(
            Guid.NewGuid(),
            year: 2026,
            month: 1,
            NightShiftCategory.MotzeiShabbatNight,
            assignedCount: 1);

        var exception = Assert.Throws<ArgumentException>(() =>
            CreateProblem(
                resources: [knownResource],
                resourceMonthlyNightShiftHistories: [history]));

        Assert.Equal("resourceMonthlyNightShiftHistories", exception.ParamName);
    }

    private static SchedulingProblem CreateProblem(
        IReadOnlyCollection<Resource>? resources = null,
        IReadOnlyCollection<ResourceMonthlyNightShiftHistory>? resourceMonthlyNightShiftHistories = null)
    {
        resources ??= [CreateResource()];

        var shift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc),
            ShiftKind.Morning,
            minResourceCount: 1,
            maxResourceCount: 1);

        return new SchedulingProblem(
            period: new SchedulePeriod(
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc)),
            resources: resources,
            shifts: [shift],
            availabilityWindows: [],
            resourcePreferences: [],
            resourceMonthlyNightShiftHistories: resourceMonthlyNightShiftHistories);
    }

    private static Resource CreateResource()
    {
        return new Resource(
            Guid.NewGuid(),
            "Guard",
            hourlyCost: 100m);
    }
}
