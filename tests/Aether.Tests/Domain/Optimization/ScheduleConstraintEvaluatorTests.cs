using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ScheduleConstraintEvaluatorTests
{
    [Fact]
    public void Evaluate_returns_no_violations_when_candidate_satisfies_basic_constraints()
    {
        var resource = CreateResource();
        var shift = CreateShift(
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc));

        var availability = new AvailabilityWindow(
            resource.Id,
            new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 18, 0, 0, DateTimeKind.Utc));

        var problem = new SchedulingProblem(
            period: CreatePeriod(),
            resources: [resource],
            shifts: [shift],
            availabilityWindows: [availability],
            resourcePreferences: []);

        var candidate = new ScheduleCandidate(
        [
            new Assignment(resource.Id, shift.Id)
        ]);

        var evaluator = new ScheduleConstraintEvaluator();

        var violations = evaluator.Evaluate(problem, candidate);

        Assert.Empty(violations);
    }

    [Fact]
    public void Evaluate_detects_resource_unavailable_for_assigned_shift()
    {
        var resource = CreateResource();
        var shift = CreateShift(
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc));

        var availability = new AvailabilityWindow(
            resource.Id,
            new DateTime(2026, 1, 1, 18, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 22, 0, 0, DateTimeKind.Utc));

        var problem = new SchedulingProblem(
            period: CreatePeriod(),
            resources: [resource],
            shifts: [shift],
            availabilityWindows: [availability],
            resourcePreferences: []);

        var candidate = new ScheduleCandidate(
        [
            new Assignment(resource.Id, shift.Id)
        ]);

        var evaluator = new ScheduleConstraintEvaluator();

        var violation = Assert.Single(evaluator.Evaluate(problem, candidate));

        Assert.Equal(ConstraintViolationType.ResourceUnavailable, violation.Type);
        Assert.Equal(ConstraintViolationSeverity.Hard, violation.Severity);
        Assert.Equal(resource.Id, violation.ResourceId);
        Assert.Equal(shift.Id, violation.ShiftId);
    }

    [Fact]
    public void Evaluate_detects_resource_assigned_to_overlapping_shifts()
    {
        var resource = CreateResource();

        var firstShift = CreateShift(
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 13, 0, 0, DateTimeKind.Utc));

        var secondShift = CreateShift(
            new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 16, 0, 0, DateTimeKind.Utc));

        var availability = new AvailabilityWindow(
            resource.Id,
            new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 18, 0, 0, DateTimeKind.Utc));

        var problem = new SchedulingProblem(
            period: CreatePeriod(),
            resources: [resource],
            shifts: [firstShift, secondShift],
            availabilityWindows: [availability],
            resourcePreferences: []);

        var candidate = new ScheduleCandidate(
        [
            new Assignment(resource.Id, firstShift.Id),
            new Assignment(resource.Id, secondShift.Id)
        ]);

        var evaluator = new ScheduleConstraintEvaluator();

        var violation = Assert.Single(evaluator.Evaluate(problem, candidate));

        Assert.Equal(ConstraintViolationType.ResourceAssignedToOverlappingShifts, violation.Type);
        Assert.Equal(ConstraintViolationSeverity.Hard, violation.Severity);
        Assert.Equal(resource.Id, violation.ResourceId);
        Assert.Null(violation.ShiftId);
    }

    [Fact]
    public void Evaluate_does_not_report_understaffed_violation_when_shift_min_resource_count_is_zero()
    {
        var resource = CreateResource();

        var shift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc),
            kind: ShiftKind.Morning,
            minResourceCount: 0,
            maxResourceCount: 2);

        var problem = new SchedulingProblem(
            period: CreatePeriod(),
            resources: [resource],
            shifts: [shift],
            availabilityWindows: [],
            resourcePreferences: []);

        var candidate = new ScheduleCandidate([]);

        var evaluator = new ScheduleConstraintEvaluator();

        Assert.Empty(evaluator.Evaluate(problem, candidate));
    }

    [Fact]
    public void Evaluate_detects_understaffed_shift_when_preference_triggers_minimum_assignment()
    {
        var resource = CreateResource();

        var shift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 1, 1, 22, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 2, 6, 30, 0, DateTimeKind.Utc),
            kind: ShiftKind.Morning,
            minResourceCount: 0,
            maxResourceCount: 2,
            requiresMinimumWhenPreferenceExists: true);

        var preferPreference = new ResourcePreference(
            resource.Id,
            new DateTime(2026, 1, 1, 22, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 2, 6, 30, 0, DateTimeKind.Utc),
            ResourcePreferenceType.Prefer,
            ResourcePreferencePriority.High);

        var problem = new SchedulingProblem(
            period: CreatePeriod(),
            resources: [resource],
            shifts: [shift],
            availabilityWindows: [],
            resourcePreferences: [preferPreference]);

        var candidate = new ScheduleCandidate([]);

        var evaluator = new ScheduleConstraintEvaluator();

        var violations = evaluator.Evaluate(problem, candidate);

        var violation = Assert.Single(
            violations,
            violation => violation.Type == ConstraintViolationType.ShiftUnderstaffed);

        Assert.Equal(ConstraintViolationType.ShiftUnderstaffed, violation.Type);
        Assert.Equal(ConstraintViolationSeverity.Hard, violation.Severity);
        Assert.Null(violation.ResourceId);
        Assert.Equal(shift.Id, violation.ShiftId);
    }

    [Fact]
    public void Evaluate_does_not_report_understaffed_shift_when_preference_triggered_minimum_has_no_preference()
    {
        var resource = CreateResource();

        var shift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 1, 1, 22, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 2, 6, 30, 0, DateTimeKind.Utc),
            kind: ShiftKind.Morning,
            minResourceCount: 0,
            maxResourceCount: 2,
            requiresMinimumWhenPreferenceExists: true);

        var problem = new SchedulingProblem(
            period: CreatePeriod(),
            resources: [resource],
            shifts: [shift],
            availabilityWindows: [],
            resourcePreferences: []);

        var candidate = new ScheduleCandidate([]);

        var evaluator = new ScheduleConstraintEvaluator();

        Assert.Empty(evaluator.Evaluate(problem, candidate));
    }

    [Fact]
    public void Evaluate_does_not_report_understaffed_shift_when_preference_triggered_minimum_is_satisfied()
    {
        var resource = CreateResource();

        var shift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 1, 1, 22, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 2, 6, 30, 0, DateTimeKind.Utc),
            kind: ShiftKind.Morning,
            minResourceCount: 0,
            maxResourceCount: 2,
            requiresMinimumWhenPreferenceExists: true);

        var availability = new AvailabilityWindow(
            resource.Id,
            new DateTime(2026, 1, 1, 22, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 2, 7, 0, 0, DateTimeKind.Utc));

        var preferPreference = new ResourcePreference(
            resource.Id,
            new DateTime(2026, 1, 1, 22, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 2, 6, 30, 0, DateTimeKind.Utc),
            ResourcePreferenceType.Prefer,
            ResourcePreferencePriority.High);

        var problem = new SchedulingProblem(
            period: CreatePeriod(),
            resources: [resource],
            shifts: [shift],
            availabilityWindows: [availability],
            resourcePreferences: [preferPreference]);

        var candidate = new ScheduleCandidate(
        [
            new Assignment(resource.Id, shift.Id)
        ]);

        var evaluator = new ScheduleConstraintEvaluator();

        Assert.Empty(evaluator.Evaluate(problem, candidate));
    }

    [Fact]
    public void Evaluate_detects_understaffed_shift()
    {
        var resource = CreateResource();

        var shift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc),
            kind: ShiftKind.Morning,
            minResourceCount: 2,
            maxResourceCount: 2);

        var availability = new AvailabilityWindow(
            resource.Id,
            new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 18, 0, 0, DateTimeKind.Utc));

        var problem = new SchedulingProblem(
            period: CreatePeriod(),
            resources: [resource],
            shifts: [shift],
            availabilityWindows: [availability],
            resourcePreferences: []);

        var candidate = new ScheduleCandidate(
        [
            new Assignment(resource.Id, shift.Id)
        ]);

        var evaluator = new ScheduleConstraintEvaluator();

        var violation = Assert.Single(evaluator.Evaluate(problem, candidate));

        Assert.Equal(ConstraintViolationType.ShiftUnderstaffed, violation.Type);
        Assert.Equal(ConstraintViolationSeverity.Hard, violation.Severity);
        Assert.Null(violation.ResourceId);
        Assert.Equal(shift.Id, violation.ShiftId);
    }

    [Fact]
    public void Evaluate_detects_overstaffed_shift()
    {
        var firstResource = CreateResource();
        var secondResource = CreateResource();

        var shift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc),
            kind: ShiftKind.Morning,
            minResourceCount: 1,
            maxResourceCount: 1);

        var firstAvailability = new AvailabilityWindow(
            firstResource.Id,
            new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 18, 0, 0, DateTimeKind.Utc));

        var secondAvailability = new AvailabilityWindow(
            secondResource.Id,
            new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 18, 0, 0, DateTimeKind.Utc));

        var problem = new SchedulingProblem(
            period: CreatePeriod(),
            resources: [firstResource, secondResource],
            shifts: [shift],
            availabilityWindows: [firstAvailability, secondAvailability],
            resourcePreferences: []);

        var candidate = new ScheduleCandidate(
        [
            new Assignment(firstResource.Id, shift.Id),
            new Assignment(secondResource.Id, shift.Id)
        ]);

        var evaluator = new ScheduleConstraintEvaluator();

        var violation = Assert.Single(evaluator.Evaluate(problem, candidate));

        Assert.Equal(ConstraintViolationType.ShiftOverstaffed, violation.Type);
        Assert.Equal(ConstraintViolationSeverity.Hard, violation.Severity);
        Assert.Null(violation.ResourceId);
        Assert.Equal(shift.Id, violation.ShiftId);
    }

    [Fact]
    public void Evaluate_rejects_candidate_with_unknown_resource_assignment()
    {
        var resource = CreateResource();
        var shift = CreateShift(
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc));

        var problem = new SchedulingProblem(
            period: CreatePeriod(),
            resources: [resource],
            shifts: [shift],
            availabilityWindows: [],
            resourcePreferences: []);

        var candidate = new ScheduleCandidate(
        [
            new Assignment(Guid.NewGuid(), shift.Id)
        ]);

        var evaluator = new ScheduleConstraintEvaluator();

        var exception = Assert.Throws<ArgumentException>(() =>
            evaluator.Evaluate(problem, candidate));

        Assert.Equal("candidate", exception.ParamName);
    }

    [Fact]
    public void Evaluate_rejects_candidate_with_unknown_shift_assignment()
    {
        var resource = CreateResource();
        var shift = CreateShift(
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc));

        var problem = new SchedulingProblem(
            period: CreatePeriod(),
            resources: [resource],
            shifts: [shift],
            availabilityWindows: [],
            resourcePreferences: []);

        var candidate = new ScheduleCandidate(
        [
            new Assignment(resource.Id, Guid.NewGuid())
        ]);

        var evaluator = new ScheduleConstraintEvaluator();

        var exception = Assert.Throws<ArgumentException>(() =>
            evaluator.Evaluate(problem, candidate));

        Assert.Equal("candidate", exception.ParamName);
    }

    [Fact]
    public void Evaluate_detects_assignment_without_required_preference()
    {
        var resource = CreateResource();

        var shift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc),
            kind: ShiftKind.Morning,
            minResourceCount: 0,
            maxResourceCount: 1,
            requiresPreferenceToAssign: true);

        var availability = new AvailabilityWindow(
            resource.Id,
            new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 18, 0, 0, DateTimeKind.Utc));

        var problem = new SchedulingProblem(
            period: CreatePeriod(),
            resources: [resource],
            shifts: [shift],
            availabilityWindows: [availability],
            resourcePreferences: []);

        var candidate = new ScheduleCandidate(
        [
            new Assignment(resource.Id, shift.Id)
        ]);

        var evaluator = new ScheduleConstraintEvaluator();

        var violation = Assert.Single(evaluator.Evaluate(problem, candidate));

        Assert.Equal(ConstraintViolationType.AssignedWithoutRequiredPreference, violation.Type);
        Assert.Equal(ConstraintViolationSeverity.Hard, violation.Severity);
        Assert.Equal(resource.Id, violation.ResourceId);
        Assert.Equal(shift.Id, violation.ShiftId);
    }

    [Fact]
    public void Evaluate_does_not_report_assignment_without_required_preference_when_prefer_preference_overlaps_shift()
    {
        var resource = CreateResource();

        var shift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc),
            kind: ShiftKind.Morning,
            minResourceCount: 0,
            maxResourceCount: 1,
            requiresPreferenceToAssign: true);

        var availability = new AvailabilityWindow(
            resource.Id,
            new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 18, 0, 0, DateTimeKind.Utc));

        var preferPreference = new ResourcePreference(
            resource.Id,
            new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 13, 0, 0, DateTimeKind.Utc),
            ResourcePreferenceType.Prefer,
            ResourcePreferencePriority.High);

        var problem = new SchedulingProblem(
            period: CreatePeriod(),
            resources: [resource],
            shifts: [shift],
            availabilityWindows: [availability],
            resourcePreferences: [preferPreference]);

        var candidate = new ScheduleCandidate(
        [
            new Assignment(resource.Id, shift.Id)
        ]);

        var evaluator = new ScheduleConstraintEvaluator();

        Assert.Empty(evaluator.Evaluate(problem, candidate));
    }

    [Fact]
    public void Evaluate_does_not_require_preference_for_regular_shift()
    {
        var resource = CreateResource();

        var shift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc),
            kind: ShiftKind.Morning,
            minResourceCount: 1,
            maxResourceCount: 1);

        var availability = new AvailabilityWindow(
            resource.Id,
            new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 18, 0, 0, DateTimeKind.Utc));

        var problem = new SchedulingProblem(
            period: CreatePeriod(),
            resources: [resource],
            shifts: [shift],
            availabilityWindows: [availability],
            resourcePreferences: []);

        var candidate = new ScheduleCandidate(
        [
            new Assignment(resource.Id, shift.Id)
        ]);

        var evaluator = new ScheduleConstraintEvaluator();

        Assert.Empty(evaluator.Evaluate(problem, candidate));
    }

    [Fact]
    public void Evaluate_reports_soft_violation_when_assigned_shift_overlaps_avoid_preference()
    {
        var resource = CreateResource();

        var shift = CreateShift(
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc));

        var availability = new AvailabilityWindow(
            resource.Id,
            new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 18, 0, 0, DateTimeKind.Utc));

        var avoidPreference = new ResourcePreference(
            resource.Id,
            new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 13, 0, 0, DateTimeKind.Utc),
            ResourcePreferenceType.Avoid,
            ResourcePreferencePriority.High);

        var problem = new SchedulingProblem(
            period: CreatePeriod(),
            resources: [resource],
            shifts: [shift],
            availabilityWindows: [availability],
            resourcePreferences: [avoidPreference]);

        var candidate = new ScheduleCandidate(
        [
            new Assignment(resource.Id, shift.Id)
        ]);

        var evaluator = new ScheduleConstraintEvaluator();

        var violations = evaluator.Evaluate(problem, candidate);

        var violation = Assert.Single(
            violations.Where(item => item.Type == ConstraintViolationType.IgnoredAvoidPreference));

        var burdenViolation = Assert.Single(
            violations.Where(item => item.Type == ConstraintViolationType.ResourceIgnoredAvoidPreferenceBurden));

        Assert.Equal(ConstraintViolationType.IgnoredAvoidPreference, violation.Type);
        Assert.Equal(ConstraintViolationSeverity.Soft, violation.Severity);
        Assert.Equal(resource.Id, violation.ResourceId);
        Assert.Equal(shift.Id, violation.ShiftId);

        Assert.Equal(ConstraintViolationSeverity.Soft, burdenViolation.Severity);
        Assert.Equal(resource.Id, burdenViolation.ResourceId);
        Assert.Null(burdenViolation.ShiftId);
    }

    [Fact]
    public void Evaluate_does_not_report_violation_when_avoid_preference_does_not_overlap_assigned_shift()
    {
        var resource = CreateResource();

        var shift = CreateShift(
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc));

        var availability = new AvailabilityWindow(
            resource.Id,
            new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 18, 0, 0, DateTimeKind.Utc));

        var avoidPreference = new ResourcePreference(
            resource.Id,
            new DateTime(2026, 1, 1, 18, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 20, 0, 0, DateTimeKind.Utc),
            ResourcePreferenceType.Avoid,
            ResourcePreferencePriority.High);

        var problem = new SchedulingProblem(
            period: CreatePeriod(),
            resources: [resource],
            shifts: [shift],
            availabilityWindows: [availability],
            resourcePreferences: [avoidPreference]);

        var candidate = new ScheduleCandidate(
        [
            new Assignment(resource.Id, shift.Id)
        ]);

        var evaluator = new ScheduleConstraintEvaluator();

        Assert.Empty(evaluator.Evaluate(problem, candidate));
    }

    [Fact]
    public void Evaluate_does_not_report_violation_for_prefer_preference()
    {
        var resource = CreateResource();

        var shift = CreateShift(
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc));

        var availability = new AvailabilityWindow(
            resource.Id,
            new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 18, 0, 0, DateTimeKind.Utc));

        var preferPreference = new ResourcePreference(
            resource.Id,
            new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 13, 0, 0, DateTimeKind.Utc),
            ResourcePreferenceType.Prefer,
            ResourcePreferencePriority.High);

        var problem = new SchedulingProblem(
            period: CreatePeriod(),
            resources: [resource],
            shifts: [shift],
            availabilityWindows: [availability],
            resourcePreferences: [preferPreference]);

        var candidate = new ScheduleCandidate(
        [
            new Assignment(resource.Id, shift.Id)
        ]);

        var evaluator = new ScheduleConstraintEvaluator();

        Assert.Empty(evaluator.Evaluate(problem, candidate));
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
}
