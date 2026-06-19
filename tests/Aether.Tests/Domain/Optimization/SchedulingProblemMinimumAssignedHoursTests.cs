using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class SchedulingProblemMinimumAssignedHoursTests
{
    [Fact]
    public void Scheduling_problem_disables_minimum_assigned_hours_by_default()
    {
        var problem = CreateProblem();

        Assert.Equal(0, problem.MinimumAssignedHoursPerResource);
    }

    [Fact]
    public void Scheduling_problem_stores_minimum_assigned_hours_per_resource()
    {
        var problem = CreateProblem(minimumAssignedHoursPerResource: 90);

        Assert.Equal(90, problem.MinimumAssignedHoursPerResource);
    }

    [Fact]
    public void Scheduling_problem_rejects_negative_minimum_assigned_hours()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateProblem(minimumAssignedHoursPerResource: -1));

        Assert.Equal("minimumAssignedHoursPerResource", exception.ParamName);
    }

    private static SchedulingProblem CreateProblem(
        int minimumAssignedHoursPerResource = 0)
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
                new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)),
            resources: [resource],
            shifts: [shift],
            availabilityWindows: [],
            resourcePreferences: [],
            minimumAssignedHoursPerResource: minimumAssignedHoursPerResource);
    }
}
