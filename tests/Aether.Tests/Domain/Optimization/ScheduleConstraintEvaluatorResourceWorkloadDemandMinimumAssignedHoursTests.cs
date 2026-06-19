using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ScheduleConstraintEvaluatorResourceWorkloadDemandMinimumAssignedHoursTests
{
    [Fact]
    public void Evaluate_uses_workload_demand_minimum_when_global_minimum_is_disabled()
    {
        var resource = CreateResource();

        var shift = CreateShift(
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            hours: 8);

        var demand = new ResourceWorkloadDemand(
            resource.Id,
            requestedPreferredHours: 120,
            minimumRequiredHours: 90);

        var problem = CreateProblem(
            resource,
            shifts: [shift],
            resourceWorkloadDemands: [demand]);

        var candidate = CreateCandidate(resource, [shift]);

        var evaluator = new ScheduleConstraintEvaluator();

        var violation = Assert.Single(evaluator.Evaluate(problem, candidate));

        Assert.Equal(ConstraintViolationType.ResourceMinimumAssignedHoursNotMet, violation.Type);
        Assert.Equal(ConstraintViolationSeverity.Hard, violation.Severity);
        Assert.Equal(resource.Id, violation.ResourceId);
        Assert.Null(violation.ShiftId);
    }

    [Fact]
    public void Evaluate_uses_workload_demand_minimum_instead_of_global_minimum_when_demand_exists()
    {
        var resource = CreateResource();

        var shift = CreateShift(
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            hours: 48);

        var demand = new ResourceWorkloadDemand(
            resource.Id,
            requestedPreferredHours: 48,
            minimumRequiredHours: 40);

        var problem = CreateProblem(
            resource,
            shifts: [shift],
            minimumAssignedHoursPerResource: 90,
            resourceWorkloadDemands: [demand]);

        var candidate = CreateCandidate(resource, [shift]);

        var evaluator = new ScheduleConstraintEvaluator();

        Assert.Empty(evaluator.Evaluate(problem, candidate));
    }

    [Fact]
    public void Evaluate_reports_above_target_when_assigned_above_requested_hours_but_below_legacy_fractional_minimum()
    {
        var resource = CreateResource();

        var shift = CreateShift(
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            hours: 66);

        var demand = new ResourceWorkloadDemand(
            resource.Id,
            requestedPreferredHours: 40,
            minimumRequiredHours: 66.5);

        var problem = CreateProblem(
            resource,
            shifts: [shift],
            resourceWorkloadDemands: [demand]);

        var candidate = CreateCandidate(resource, [shift]);

        var evaluator = new ScheduleConstraintEvaluator();

        var violation = Assert.Single(evaluator.Evaluate(problem, candidate));

        Assert.Equal(ConstraintViolationType.ResourceEffectiveTargetAssignedHoursAboveTarget, violation.Type);
        Assert.Equal(ConstraintViolationSeverity.Soft, violation.Severity);
        Assert.Equal(resource.Id, violation.ResourceId);
    }

    [Fact]
    public void Evaluate_reports_above_target_when_assigned_legacy_fractional_minimum_above_requested_hours()
    {
        var resource = CreateResource();

        var shift = CreateShift(
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            hours: 66.5);

        var demand = new ResourceWorkloadDemand(
            resource.Id,
            requestedPreferredHours: 40,
            minimumRequiredHours: 66.5);

        var problem = CreateProblem(
            resource,
            shifts: [shift],
            resourceWorkloadDemands: [demand]);

        var candidate = CreateCandidate(resource, [shift]);

        var evaluator = new ScheduleConstraintEvaluator();

        var violation = Assert.Single(evaluator.Evaluate(problem, candidate));

        Assert.Equal(ConstraintViolationType.ResourceEffectiveTargetAssignedHoursAboveTarget, violation.Type);
        Assert.Equal(ConstraintViolationSeverity.Soft, violation.Severity);
        Assert.Equal(resource.Id, violation.ResourceId);
        Assert.Null(violation.ShiftId);
        Assert.Equal(26.5, violation.Magnitude);
    }

    [Fact]
    public void Evaluate_uses_global_minimum_when_workload_demand_is_missing_for_resource()
    {
        var resourceWithDemand = CreateResource();
        var resourceWithoutDemand = CreateResource();

        var shiftsForResourceWithDemand = Enumerable
            .Range(0, 12)
            .Select(day => CreateShift(
                new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc).AddDays(day),
                hours: 8))
            .ToArray();

        var shiftForResourceWithoutDemand = CreateShift(
            new DateTime(2026, 1, 20, 9, 0, 0, DateTimeKind.Utc),
            hours: 8);

        var demand = new ResourceWorkloadDemand(
            resourceWithDemand.Id,
            requestedPreferredHours: 96,
            minimumRequiredHours: 90);

        var problem = new SchedulingProblem(
            period: CreateMonthlyPeriod(),
            resources: [resourceWithDemand, resourceWithoutDemand],
            shifts: shiftsForResourceWithDemand
                .Append(shiftForResourceWithoutDemand)
                .ToArray(),
            availabilityWindows:
            [
                CreateMonthlyAvailability(resourceWithDemand),
                CreateMonthlyAvailability(resourceWithoutDemand)
            ],
            resourcePreferences: [],
            minimumAssignedHoursPerResource: 90,
            resourceWorkloadDemands: [demand]);

        var candidate = new ScheduleCandidate(
            shiftsForResourceWithDemand
                .Select(shift => new Assignment(resourceWithDemand.Id, shift.Id))
                .Append(new Assignment(resourceWithoutDemand.Id, shiftForResourceWithoutDemand.Id))
                .ToArray());

        var evaluator = new ScheduleConstraintEvaluator();

        var violation = Assert.Single(evaluator.Evaluate(problem, candidate));

        Assert.Equal(ConstraintViolationType.ResourceMinimumAssignedHoursNotMet, violation.Type);
        Assert.Equal(ConstraintViolationSeverity.Hard, violation.Severity);
        Assert.Equal(resourceWithoutDemand.Id, violation.ResourceId);
        Assert.Null(violation.ShiftId);
    }

    private static SchedulingProblem CreateProblem(
        Resource resource,
        IReadOnlyCollection<Shift> shifts,
        int minimumAssignedHoursPerResource = 0,
        IReadOnlyCollection<ResourceWorkloadDemand>? resourceWorkloadDemands = null)
    {
        return new SchedulingProblem(
            period: CreateMonthlyPeriod(),
            resources: [resource],
            shifts: shifts,
            availabilityWindows: [CreateMonthlyAvailability(resource)],
            resourcePreferences: [],
            minimumAssignedHoursPerResource: minimumAssignedHoursPerResource,
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
