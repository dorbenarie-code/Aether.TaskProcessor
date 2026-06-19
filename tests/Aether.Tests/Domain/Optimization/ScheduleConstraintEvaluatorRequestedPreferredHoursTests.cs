using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ScheduleConstraintEvaluatorRequestedPreferredHoursTests
{
    [Fact]
    public void Evaluate_reports_soft_violation_when_requested_preferred_shift_was_not_assigned_even_when_target_hours_are_satisfied()
    {
        var resource = CreateResource();

        var requestedShift = CreateShift(
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc),
            minResourceCount: 0,
            maxResourceCount: 1);

        var assignedShift = CreateShift(
            new DateTime(2026, 1, 2, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 2, 17, 0, 0, DateTimeKind.Utc),
            minResourceCount: 1,
            maxResourceCount: 1);

        var requestedPreference = CreatePreferPreference(resource, requestedShift);

        var problem = new SchedulingProblem(
            period: CreatePeriod(),
            resources: [resource],
            shifts: [requestedShift, assignedShift],
            availabilityWindows:
            [
                CreateAvailability(resource, requestedShift),
                CreateAvailability(resource, assignedShift)
            ],
            resourcePreferences: [requestedPreference],
            resourceWorkloadDemands:
            [
                new ResourceWorkloadDemand(
                    resource.Id,
                    requestedPreferredHours: 8,
                    minimumRequiredHours: 0)
            ]);

        var candidate = new ScheduleCandidate(
        [
            new Assignment(resource.Id, assignedShift.Id)
        ]);

        var evaluator = new ScheduleConstraintEvaluator();

        var violations = evaluator.Evaluate(problem, candidate);

        Assert.DoesNotContain(
            violations,
            violation => violation.Type == ConstraintViolationType.ResourceEffectiveTargetAssignedHoursBelowTarget);

        Assert.DoesNotContain(
            violations,
            violation => violation.Type == ConstraintViolationType.ResourceEffectiveTargetAssignedHoursAboveTarget);

        var violation = Assert.Single(
            violations,
            violation => violation.Type == ConstraintViolationType.ResourceRequestedPreferredHoursNotSatisfied);

        Assert.Equal(ConstraintViolationSeverity.Soft, violation.Severity);
        Assert.Equal(resource.Id, violation.ResourceId);
        Assert.Equal(requestedShift.Id, violation.ShiftId);
        Assert.Equal(8, violation.Magnitude);
    }

    [Fact]
    public void Evaluate_does_not_report_violation_when_requested_preferred_shift_was_assigned()
    {
        var resource = CreateResource();

        var requestedShift = CreateShift(
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc),
            minResourceCount: 1,
            maxResourceCount: 1);

        var requestedPreference = CreatePreferPreference(resource, requestedShift);

        var problem = new SchedulingProblem(
            period: CreatePeriod(),
            resources: [resource],
            shifts: [requestedShift],
            availabilityWindows: [CreateAvailability(resource, requestedShift)],
            resourcePreferences: [requestedPreference]);

        var candidate = new ScheduleCandidate(
        [
            new Assignment(resource.Id, requestedShift.Id)
        ]);

        var evaluator = new ScheduleConstraintEvaluator();

        var violations = evaluator.Evaluate(problem, candidate);

        Assert.DoesNotContain(
            violations,
            violation => violation.Type == ConstraintViolationType.ResourceRequestedPreferredHoursNotSatisfied);
    }

    [Fact]
    public void Evaluate_deduplicates_multiple_prefer_preferences_that_match_the_same_resource_and_shift()
    {
        var resource = CreateResource();

        var requestedShift = CreateShift(
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc),
            minResourceCount: 0,
            maxResourceCount: 1);

        var firstPreference = CreatePreferPreference(resource, requestedShift);

        var secondPreference = new ResourcePreference(
            resource.Id,
            new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            ResourcePreferenceType.Prefer,
            ResourcePreferencePriority.High);

        var problem = new SchedulingProblem(
            period: CreatePeriod(),
            resources: [resource],
            shifts: [requestedShift],
            availabilityWindows: [],
            resourcePreferences: [firstPreference, secondPreference]);

        var candidate = new ScheduleCandidate([]);

        var evaluator = new ScheduleConstraintEvaluator();

        var violations = evaluator.Evaluate(problem, candidate);

        var violation = Assert.Single(
            violations,
            violation => violation.Type == ConstraintViolationType.ResourceRequestedPreferredHoursNotSatisfied);

        Assert.Equal(resource.Id, violation.ResourceId);
        Assert.Equal(requestedShift.Id, violation.ShiftId);
        Assert.Equal(8, violation.Magnitude);
    }

    [Fact]
    public void Evaluate_ignores_avoid_preferences_for_requested_preferred_hours_fulfillment()
    {
        var resource = CreateResource();

        var shift = CreateShift(
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc),
            minResourceCount: 0,
            maxResourceCount: 1);

        var avoidPreference = new ResourcePreference(
            resource.Id,
            shift.StartUtc,
            shift.EndUtc,
            ResourcePreferenceType.Avoid,
            ResourcePreferencePriority.High);

        var problem = new SchedulingProblem(
            period: CreatePeriod(),
            resources: [resource],
            shifts: [shift],
            availabilityWindows: [],
            resourcePreferences: [avoidPreference]);

        var candidate = new ScheduleCandidate([]);

        var evaluator = new ScheduleConstraintEvaluator();

        var violations = evaluator.Evaluate(problem, candidate);

        Assert.DoesNotContain(
            violations,
            violation => violation.Type == ConstraintViolationType.ResourceRequestedPreferredHoursNotSatisfied);
    }

    private static SchedulePeriod CreatePeriod()
    {
        return new SchedulePeriod(
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc));
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
        DateTime endUtc,
        int minResourceCount,
        int maxResourceCount)
    {
        return new Shift(
            Guid.NewGuid(),
            startUtc,
            endUtc,
            kind: ShiftKind.Morning,
            minResourceCount,
            maxResourceCount);
    }

    private static AvailabilityWindow CreateAvailability(
        Resource resource,
        Shift shift)
    {
        return new AvailabilityWindow(
            resource.Id,
            shift.StartUtc.AddHours(-1),
            shift.EndUtc.AddHours(1));
    }

    private static ResourcePreference CreatePreferPreference(
        Resource resource,
        Shift shift)
    {
        return new ResourcePreference(
            resource.Id,
            shift.StartUtc,
            shift.EndUtc,
            ResourcePreferenceType.Prefer,
            ResourcePreferencePriority.High);
    }
}
