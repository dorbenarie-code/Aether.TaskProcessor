using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ScheduleConstraintEvaluatorResourceWorkloadDemandEffectiveTargetAssignedHoursTests
{
    [Fact]
    public void Evaluate_does_not_report_effective_target_gap_when_resource_has_no_workload_demand()
    {
        var resource = CreateResource();

        var shift = CreateShift(
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            hours: 8);

        var problem = CreateProblem(
            resource,
            new[] { shift },
            Array.Empty<ResourceWorkloadDemand>());

        var candidate = CreateCandidate(resource, new[] { shift });

        var evaluator = new ScheduleConstraintEvaluator();

        Assert.Empty(evaluator.Evaluate(problem, candidate));
    }

    [Fact]
    public void Evaluate_reports_soft_violation_when_assigned_hours_are_below_effective_target_but_above_minimum()
    {
        var resource = CreateResource();

        var shifts = CreateDailyShifts(
            startUtc: new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            count: 12,
            hoursPerShift: 8);

        var demand = new ResourceWorkloadDemand(
            resource.Id,
            requestedPreferredHours: 180,
            minimumRequiredHours: 90);

        var problem = CreateProblem(
            resource,
            shifts,
            new[] { demand });

        var candidate = CreateCandidate(resource, shifts);

        var evaluator = new ScheduleConstraintEvaluator();

        var violation = Assert.Single(evaluator.Evaluate(problem, candidate));

        Assert.Equal(
            ConstraintViolationType.ResourceEffectiveTargetAssignedHoursBelowTarget,
            violation.Type);

        Assert.Equal(ConstraintViolationSeverity.Soft, violation.Severity);
        Assert.Equal(resource.Id, violation.ResourceId);
        Assert.Null(violation.ShiftId);
        Assert.Equal(84, violation.Magnitude);
    }

    [Fact]
    public void Evaluate_does_not_report_effective_target_gap_when_assigned_hours_equal_effective_target()
    {
        var resource = CreateResource();

        var shift = CreateShift(
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            hours: 120);

        var demand = new ResourceWorkloadDemand(
            resource.Id,
            requestedPreferredHours: 120,
            minimumRequiredHours: 90);

        var problem = CreateProblem(
            resource,
            new[] { shift },
            new[] { demand });

        var candidate = CreateCandidate(resource, new[] { shift });

        var evaluator = new ScheduleConstraintEvaluator();

        Assert.Empty(evaluator.Evaluate(problem, candidate));
    }

    [Fact]
    public void Evaluate_reports_soft_violation_when_assigned_hours_are_above_effective_target()
    {
        var resource = CreateResource();

        var shift = CreateShift(
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            hours: 128);

        var demand = new ResourceWorkloadDemand(
            resource.Id,
            requestedPreferredHours: 120,
            minimumRequiredHours: 90);

        var problem = CreateProblem(
            resource,
            new[] { shift },
            new[] { demand });

        var candidate = CreateCandidate(resource, new[] { shift });

        var evaluator = new ScheduleConstraintEvaluator();

        var violation = Assert.Single(evaluator.Evaluate(problem, candidate));

        Assert.Equal(
            ConstraintViolationType.ResourceEffectiveTargetAssignedHoursAboveTarget,
            violation.Type);

        Assert.Equal(ConstraintViolationSeverity.Soft, violation.Severity);
        Assert.Equal(resource.Id, violation.ResourceId);
        Assert.Null(violation.ShiftId);
        Assert.Equal(8, violation.Magnitude);
    }

    [Fact]
    public void Evaluate_does_not_add_effective_target_gap_violation_when_resource_is_below_minimum()
    {
        var resource = CreateResource();

        var shift = CreateShift(
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            hours: 8);

        var demand = new ResourceWorkloadDemand(
            resource.Id,
            requestedPreferredHours: 180,
            minimumRequiredHours: 90);

        var problem = CreateProblem(
            resource,
            new[] { shift },
            new[] { demand });

        var candidate = CreateCandidate(resource, new[] { shift });

        var evaluator = new ScheduleConstraintEvaluator();

        var violation = Assert.Single(evaluator.Evaluate(problem, candidate));

        Assert.Equal(ConstraintViolationType.ResourceMinimumAssignedHoursNotMet, violation.Type);
        Assert.Equal(ConstraintViolationSeverity.Hard, violation.Severity);
        Assert.Null(violation.Magnitude);
    }

    private static SchedulingProblem CreateProblem(
        Resource resource,
        IReadOnlyCollection<Shift> shifts,
        IReadOnlyCollection<ResourceWorkloadDemand> resourceWorkloadDemands)
    {
        return new SchedulingProblem(
            period: CreateMonthlyPeriod(),
            resources: new[] { resource },
            shifts: shifts,
            availabilityWindows: new[] { CreateMonthlyAvailability(resource) },
            resourcePreferences: Array.Empty<ResourcePreference>(),
            resourceWorkloadDemands: resourceWorkloadDemands);
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

    private static Shift[] CreateDailyShifts(
        DateTime startUtc,
        int count,
        double hoursPerShift)
    {
        return Enumerable
            .Range(0, count)
            .Select(day => CreateShift(startUtc.AddDays(day), hoursPerShift))
            .ToArray();
    }

    private static Shift CreateShift(
        DateTime startUtc,
        double hours)
    {
        return new Shift(
            Guid.NewGuid(),
            startUtc,
            startUtc.AddMinutes(hours * 60),
            kind: ShiftKind.Morning,
            minResourceCount: 0,
            maxResourceCount: 1);
    }

    private static AvailabilityWindow CreateMonthlyAvailability(Resource resource)
    {
        return new AvailabilityWindow(
            resource.Id,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    private static ScheduleCandidate CreateCandidate(
        Resource resource,
        IReadOnlyCollection<Shift> shifts)
    {
        return new ScheduleCandidate(
            shifts
                .Select(shift => new Assignment(resource.Id, shift.Id))
                .ToArray());
    }
}
