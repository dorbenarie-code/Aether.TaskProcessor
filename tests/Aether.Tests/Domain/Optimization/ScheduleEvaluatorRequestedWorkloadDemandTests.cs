using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ScheduleEvaluatorRequestedWorkloadDemandTests
{
    [Fact]
    public void Evaluate_does_not_force_requested_workload_demand_to_legacy_90_hour_minimum()
    {
        const double requestedPreferredHours = 32;
        const double legacyMinimumHours = 90;

        var resource = CreateResource("Resource A");

        var shifts = new[]
        {
            CreateShift(
                new DateTime(2026, 1, 1, 6, 30, 0, DateTimeKind.Utc),
                new DateTime(2026, 1, 1, 14, 30, 0, DateTimeKind.Utc)),
            CreateShift(
                new DateTime(2026, 1, 2, 6, 30, 0, DateTimeKind.Utc),
                new DateTime(2026, 1, 2, 14, 30, 0, DateTimeKind.Utc)),
            CreateShift(
                new DateTime(2026, 1, 3, 6, 30, 0, DateTimeKind.Utc),
                new DateTime(2026, 1, 3, 14, 30, 0, DateTimeKind.Utc)),
            CreateShift(
                new DateTime(2026, 1, 4, 6, 30, 0, DateTimeKind.Utc),
                new DateTime(2026, 1, 4, 14, 30, 0, DateTimeKind.Utc))
        };

        var availability = new AvailabilityWindow(
            resource.Id,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc));

        var workloadDemand = new ResourceWorkloadDemand(
            resource.Id,
            requestedPreferredHours: requestedPreferredHours,
            minimumRequiredHours: legacyMinimumHours);

        var problem = new SchedulingProblem(
            period: new SchedulePeriod(
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc)),
            resources: [resource],
            shifts: shifts,
            availabilityWindows: [availability],
            resourcePreferences: [],
            resourceWorkloadDemands: [workloadDemand]);

        var candidate = new ScheduleCandidate(
            shifts
                .Select(shift => new Assignment(resource.Id, shift.Id))
                .ToArray());

        var evaluator = new ScheduleEvaluator();

        var result = evaluator.Evaluate(problem, candidate);

        Assert.Empty(result.Violations);
        Assert.True(result.IsFeasible);
        Assert.Equal(0, result.Score.TotalPenalty);
    }

    private static Resource CreateResource(string name)
    {
        return new Resource(
            Guid.NewGuid(),
            name,
            hourlyCost: 0);
    }

    private static Shift CreateShift(
        DateTime startUtc,
        DateTime endUtc)
    {
        return new Shift(
            Guid.NewGuid(),
            startUtc,
            endUtc,
            ShiftKind.Morning,
            minResourceCount: 0,
            maxResourceCount: 1);
    }
}
