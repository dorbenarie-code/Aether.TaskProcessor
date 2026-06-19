using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ScheduleConstraintEvaluatorMinimumAssignedHoursTests
{
    [Fact]
    public void Evaluate_allows_optional_night_shift_to_remain_unassigned()
    {
        var resource = CreateResource();

        var nightShift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 1, 1, 22, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 2, 6, 30, 0, DateTimeKind.Utc),
            kind: ShiftKind.Night,
            minResourceCount: 0,
            maxResourceCount: 1,
            requiresPreferenceToAssign: true);

        var problem = new SchedulingProblem(
            period: CreateMonthlyPeriod(),
            resources: [resource],
            shifts: [nightShift],
            availabilityWindows: [],
            resourcePreferences: []);

        var candidate = new ScheduleCandidate([]);

        var evaluator = new ScheduleConstraintEvaluator();

        Assert.Empty(evaluator.Evaluate(problem, candidate));
    }

    [Fact]
    public void Evaluate_does_not_report_minimum_hours_violation_when_policy_is_disabled()
    {
        var resource = CreateResource();
        var shift = CreateShift(
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc));

        var problem = new SchedulingProblem(
            period: CreateMonthlyPeriod(),
            resources: [resource],
            shifts: [shift],
            availabilityWindows: [CreateMonthlyAvailability(resource)],
            resourcePreferences: []);

        var candidate = new ScheduleCandidate(
        [
            new Assignment(resource.Id, shift.Id)
        ]);

        var evaluator = new ScheduleConstraintEvaluator();

        Assert.Empty(evaluator.Evaluate(problem, candidate));
    }

    [Fact]
    public void Evaluate_detects_resource_below_minimum_assigned_hours()
    {
        var resource = CreateResource();
        var shift = CreateShift(
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc));

        var problem = new SchedulingProblem(
            period: CreateMonthlyPeriod(),
            resources: [resource],
            shifts: [shift],
            availabilityWindows: [CreateMonthlyAvailability(resource)],
            resourcePreferences: [],
            minimumAssignedHoursPerResource: 90);

        var candidate = new ScheduleCandidate(
        [
            new Assignment(resource.Id, shift.Id)
        ]);

        var evaluator = new ScheduleConstraintEvaluator();

        var violation = Assert.Single(evaluator.Evaluate(problem, candidate));

        Assert.Equal(ConstraintViolationType.ResourceMinimumAssignedHoursNotMet, violation.Type);
        Assert.Equal(ConstraintViolationSeverity.Hard, violation.Severity);
        Assert.Equal(resource.Id, violation.ResourceId);
        Assert.Null(violation.ShiftId);
    }

    [Fact]
    public void Evaluate_does_not_report_minimum_hours_violation_when_resource_meets_minimum()
    {
        var resource = CreateResource();

        var shifts = Enumerable
            .Range(0, 12)
            .Select(day => CreateShift(
                new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc).AddDays(day),
                new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc).AddDays(day)))
            .ToArray();

        var problem = new SchedulingProblem(
            period: CreateMonthlyPeriod(),
            resources: [resource],
            shifts: shifts,
            availabilityWindows: [CreateMonthlyAvailability(resource)],
            resourcePreferences: [],
            minimumAssignedHoursPerResource: 90);

        var candidate = new ScheduleCandidate(
            shifts
                .Select(shift => new Assignment(resource.Id, shift.Id))
                .ToArray());

        var evaluator = new ScheduleConstraintEvaluator();

        Assert.Empty(evaluator.Evaluate(problem, candidate));
    }

    private static SchedulePeriod CreateMonthlyPeriod()
    {
        return new SchedulePeriod(
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    private static Resource CreateResource()
    {
        return new Resource(
            Guid.NewGuid(),
            "Dana",
            hourlyCost: 100m);
    }

    private static Shift CreateShift(
        DateTime startUtc,
        DateTime endUtc)
    {
        return new Shift(
            Guid.NewGuid(),
            startUtc,
            endUtc,
            kind: ShiftKind.Morning,
            minResourceCount: 1,
            maxResourceCount: 1);
    }

    private static AvailabilityWindow CreateMonthlyAvailability(Resource resource)
    {
        return new AvailabilityWindow(
            resource.Id,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
    }
}
