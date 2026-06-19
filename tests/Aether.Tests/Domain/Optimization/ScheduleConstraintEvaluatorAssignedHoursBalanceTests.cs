using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ScheduleConstraintEvaluatorAssignedHoursBalanceTests
{
    [Fact]
    public void Evaluate_does_not_report_assigned_hours_balance_violation_when_policy_is_disabled()
    {
        var resources = CreateResources(count: 3);
        var shifts = CreateShifts(count: 3);

        var problem = CreateProblem(
            resources,
            shifts,
            maximumAssignedHoursDeviationFromAverageHours: null);

        var candidate = new ScheduleCandidate(
        [
            new Assignment(resources[0].Id, shifts[0].Id),
            new Assignment(resources[0].Id, shifts[1].Id),
            new Assignment(resources[0].Id, shifts[2].Id)
        ]);

        var evaluator = new ScheduleConstraintEvaluator();

        var violations = evaluator
            .Evaluate(problem, candidate)
            .Where(violation => violation.Type == ConstraintViolationType.ResourceAssignedHoursBalanceExceeded)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Evaluate_does_not_report_assigned_hours_balance_violation_when_resources_are_inside_tolerance()
    {
        var resources = CreateResources(count: 3);
        var shifts = CreateShifts(count: 5);

        var problem = CreateProblem(
            resources,
            shifts,
            maximumAssignedHoursDeviationFromAverageHours: 6.0);

        var candidate = new ScheduleCandidate(
        [
            new Assignment(resources[0].Id, shifts[0].Id),
            new Assignment(resources[0].Id, shifts[1].Id),
            new Assignment(resources[1].Id, shifts[2].Id),
            new Assignment(resources[1].Id, shifts[3].Id),
            new Assignment(resources[2].Id, shifts[4].Id)
        ]);

        var evaluator = new ScheduleConstraintEvaluator();

        var violations = evaluator
            .Evaluate(problem, candidate)
            .Where(violation => violation.Type == ConstraintViolationType.ResourceAssignedHoursBalanceExceeded)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Evaluate_reports_soft_violation_for_resource_above_assigned_hours_balance_tolerance()
    {
        var resources = CreateResources(count: 3);
        var shifts = CreateShifts(count: 4);

        var problem = CreateProblem(
            resources,
            shifts,
            maximumAssignedHoursDeviationFromAverageHours: 5.0);

        var candidate = new ScheduleCandidate(
        [
            new Assignment(resources[0].Id, shifts[0].Id),
            new Assignment(resources[0].Id, shifts[1].Id),
            new Assignment(resources[1].Id, shifts[2].Id),
            new Assignment(resources[2].Id, shifts[3].Id)
        ]);

        var evaluator = new ScheduleConstraintEvaluator();

        var violation = Assert.Single(evaluator
            .Evaluate(problem, candidate)
            .Where(violation => violation.Type == ConstraintViolationType.ResourceAssignedHoursBalanceExceeded));

        Assert.Equal(ConstraintViolationSeverity.Soft, violation.Severity);
        Assert.Equal(resources[0].Id, violation.ResourceId);
        Assert.Null(violation.ShiftId);
        Assert.NotNull(violation.Magnitude);
        Assert.Equal(1.0 / 3.0, violation.Magnitude.Value, precision: 6);
    }

    [Fact]
    public void Evaluate_reports_soft_violation_for_resource_below_assigned_hours_balance_tolerance()
    {
        var resources = CreateResources(count: 3);
        var shifts = CreateShifts(count: 5);

        var problem = CreateProblem(
            resources,
            shifts,
            maximumAssignedHoursDeviationFromAverageHours: 5.0);

        var candidate = new ScheduleCandidate(
        [
            new Assignment(resources[0].Id, shifts[0].Id),
            new Assignment(resources[0].Id, shifts[1].Id),
            new Assignment(resources[1].Id, shifts[2].Id),
            new Assignment(resources[1].Id, shifts[3].Id),
            new Assignment(resources[2].Id, shifts[4].Id)
        ]);

        var evaluator = new ScheduleConstraintEvaluator();

        var violation = Assert.Single(evaluator
            .Evaluate(problem, candidate)
            .Where(violation => violation.Type == ConstraintViolationType.ResourceAssignedHoursBalanceExceeded));

        Assert.Equal(ConstraintViolationSeverity.Soft, violation.Severity);
        Assert.Equal(resources[2].Id, violation.ResourceId);
        Assert.Null(violation.ShiftId);
        Assert.NotNull(violation.Magnitude);
        Assert.Equal(1.0 / 3.0, violation.Magnitude.Value, precision: 6);
    }

    [Fact]
    public void Evaluate_reports_one_assigned_hours_balance_violation_per_resource_outside_tolerance()
    {
        var resources = CreateResources(count: 3);
        var shifts = CreateShifts(count: 5);

        var problem = CreateProblem(
            resources,
            shifts,
            maximumAssignedHoursDeviationFromAverageHours: 5.0);

        var candidate = new ScheduleCandidate(
        [
            new Assignment(resources[0].Id, shifts[0].Id),
            new Assignment(resources[0].Id, shifts[1].Id),
            new Assignment(resources[0].Id, shifts[2].Id),
            new Assignment(resources[1].Id, shifts[3].Id),
            new Assignment(resources[2].Id, shifts[4].Id)
        ]);

        var evaluator = new ScheduleConstraintEvaluator();

        var violations = evaluator
            .Evaluate(problem, candidate)
            .Where(violation => violation.Type == ConstraintViolationType.ResourceAssignedHoursBalanceExceeded)
            .ToArray();

        Assert.Equal(3, violations.Length);
        Assert.All(violations, violation => Assert.Equal(ConstraintViolationSeverity.Soft, violation.Severity));
        Assert.All(violations, violation => Assert.Null(violation.ShiftId));

        var violationsByResourceId = violations.ToDictionary(
            violation => violation.ResourceId!.Value);

        Assert.Equal(17.0 / 3.0, violationsByResourceId[resources[0].Id].Magnitude!.Value, precision: 6);
        Assert.Equal(1.0 / 3.0, violationsByResourceId[resources[1].Id].Magnitude!.Value, precision: 6);
        Assert.Equal(1.0 / 3.0, violationsByResourceId[resources[2].Id].Magnitude!.Value, precision: 6);
    }

    private static Resource[] CreateResources(int count)
    {
        return Enumerable
            .Range(1, count)
            .Select(index => new Resource(
                Guid.NewGuid(),
                $"Guard {index}",
                hourlyCost: 100m))
            .ToArray();
    }

    private static Shift[] CreateShifts(int count)
    {
        return Enumerable
            .Range(0, count)
            .Select(index => new Shift(
                Guid.NewGuid(),
                new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc).AddDays(index),
                new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc).AddDays(index),
                kind: ShiftKind.Morning,
                minResourceCount: 0,
                maxResourceCount: 1))
            .ToArray();
    }

    private static SchedulingProblem CreateProblem(
        IReadOnlyCollection<Resource> resources,
        IReadOnlyCollection<Shift> shifts,
        double? maximumAssignedHoursDeviationFromAverageHours)
    {
        return new SchedulingProblem(
            period: new SchedulePeriod(
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc)),
            resources: resources,
            shifts: shifts,
            availabilityWindows: resources
                .Select(resource => new AvailabilityWindow(
                    resource.Id,
                    new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc)))
                .ToArray(),
            resourcePreferences: [],
            maximumAssignedHoursDeviationFromAverageHours: maximumAssignedHoursDeviationFromAverageHours);
    }
}
