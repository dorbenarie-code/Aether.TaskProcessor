using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class SchedulingProblemAssignedHoursBalanceTests
{
    [Fact]
    public void Scheduling_problem_disables_assigned_hours_balance_by_default()
    {
        var problem = CreateProblem();

        Assert.Null(problem.MaximumAssignedHoursDeviationFromAverageHours);
    }

    [Fact]
    public void Scheduling_problem_stores_assigned_hours_balance_tolerance()
    {
        var problem = CreateProblem(
            maximumAssignedHoursDeviationFromAverageHours: 5.0);

        Assert.Equal(5.0, problem.MaximumAssignedHoursDeviationFromAverageHours);
    }

    [Fact]
    public void Scheduling_problem_accepts_zero_assigned_hours_balance_tolerance()
    {
        var problem = CreateProblem(
            maximumAssignedHoursDeviationFromAverageHours: 0.0);

        Assert.Equal(0.0, problem.MaximumAssignedHoursDeviationFromAverageHours);
    }

    [Fact]
    public void Scheduling_problem_rejects_negative_assigned_hours_balance_tolerance()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateProblem(maximumAssignedHoursDeviationFromAverageHours: -0.1));

        Assert.Equal("maximumAssignedHoursDeviationFromAverageHours", exception.ParamName);
    }

    private static SchedulingProblem CreateProblem(
        double? maximumAssignedHoursDeviationFromAverageHours = null)
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
            maximumAssignedHoursDeviationFromAverageHours: maximumAssignedHoursDeviationFromAverageHours);
    }
}
