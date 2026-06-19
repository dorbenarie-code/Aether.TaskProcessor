using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ScheduleConstraintEvaluatorMonthlyNightShiftPreferenceTests
{
    [Fact]
    public void Evaluate_does_not_report_preference_not_satisfied_when_resource_prefers_and_receives_motzei_shabbat_night()
    {
        var resource = CreateResource();
        var shift = CreateMotzeiShabbatNightShift(new DateTime(2026, 1, 3, 22, 30, 0, DateTimeKind.Utc));
        var preference = CreatePreferPreference(resource, shift);

        var problem = CreateProblem(
            resources: [resource],
            shifts: [shift],
            preferences: [preference]);

        var candidate = CreateCandidate(resource, [shift]);

        var evaluator = new ScheduleConstraintEvaluator();

        Assert.Empty(evaluator.Evaluate(problem, candidate));
    }

    [Fact]
    public void Evaluate_does_not_report_preference_not_satisfied_when_resource_prefers_and_history_already_satisfied_it()
    {
        var resource = CreateResource();
        var shift = CreateMotzeiShabbatNightShift(new DateTime(2026, 1, 10, 22, 30, 0, DateTimeKind.Utc));
        var preference = CreatePreferPreference(resource, shift);

        var history = new ResourceMonthlyNightShiftHistory(
            resource.Id,
            year: 2026,
            month: 1,
            NightShiftCategory.MotzeiShabbatNight,
            assignedCount: 1);

        var problem = CreateProblem(
            resources: [resource],
            shifts: [shift],
            preferences: [preference],
            histories: [history]);

        var candidate = new ScheduleCandidate([]);

        var evaluator = new ScheduleConstraintEvaluator();

        var violations = evaluator.Evaluate(problem, candidate);

        Assert.DoesNotContain(
            violations,
            violation => violation.Type == ConstraintViolationType.ResourceMonthlyNightShiftPreferenceNotSatisfied);
    }

    [Fact]
    public void Evaluate_reports_soft_violation_when_resource_prefers_motzei_shabbat_night_and_receives_none()
    {
        var resource = CreateResource();
        var shift = CreateMotzeiShabbatNightShift(new DateTime(2026, 1, 10, 22, 30, 0, DateTimeKind.Utc));
        var preference = CreatePreferPreference(resource, shift);

        var problem = CreateProblem(
            resources: [resource],
            shifts: [shift],
            preferences: [preference]);

        var candidate = new ScheduleCandidate([]);

        var evaluator = new ScheduleConstraintEvaluator();

        var violations = evaluator.Evaluate(problem, candidate);

        var violation = Assert.Single(
            violations,
            violation => violation.Type == ConstraintViolationType.ResourceMonthlyNightShiftPreferenceNotSatisfied);

        Assert.Equal(ConstraintViolationType.ResourceMonthlyNightShiftPreferenceNotSatisfied, violation.Type);
        Assert.Equal(ConstraintViolationSeverity.Soft, violation.Severity);
        Assert.Equal(resource.Id, violation.ResourceId);
        Assert.Null(violation.ShiftId);
    }

    [Fact]
    public void Evaluate_does_not_report_preference_not_satisfied_for_avoid_preference()
    {
        var resource = CreateResource();
        var shift = CreateMotzeiShabbatNightShift(new DateTime(2026, 1, 10, 22, 30, 0, DateTimeKind.Utc));

        var preference = new ResourcePreference(
            resource.Id,
            shift.StartUtc,
            shift.EndUtc,
            ResourcePreferenceType.Avoid,
            ResourcePreferencePriority.High);

        var problem = CreateProblem(
            resources: [resource],
            shifts: [shift],
            preferences: [preference]);

        var candidate = new ScheduleCandidate([]);

        var evaluator = new ScheduleConstraintEvaluator();

        Assert.Empty(evaluator.Evaluate(problem, candidate));
    }

    [Fact]
    public void Evaluate_reports_one_soft_violation_when_resource_prefers_multiple_motzei_shabbat_nights_in_same_month_and_receives_none()
    {
        var resource = CreateResource();

        var firstShift = CreateMotzeiShabbatNightShift(
            new DateTime(2026, 1, 3, 22, 30, 0, DateTimeKind.Utc));

        var secondShift = CreateMotzeiShabbatNightShift(
            new DateTime(2026, 1, 10, 22, 30, 0, DateTimeKind.Utc));

        var firstPreference = CreatePreferPreference(resource, firstShift);
        var secondPreference = CreatePreferPreference(resource, secondShift);

        var problem = CreateProblem(
            resources: [resource],
            shifts: [firstShift, secondShift],
            preferences: [firstPreference, secondPreference]);

        var candidate = new ScheduleCandidate([]);

        var evaluator = new ScheduleConstraintEvaluator();

        var violations = evaluator.Evaluate(problem, candidate);

        var violation = Assert.Single(
            violations,
            violation => violation.Type == ConstraintViolationType.ResourceMonthlyNightShiftPreferenceNotSatisfied);

        Assert.Equal(ConstraintViolationType.ResourceMonthlyNightShiftPreferenceNotSatisfied, violation.Type);
        Assert.Equal(ConstraintViolationSeverity.Soft, violation.Severity);
        Assert.Equal(resource.Id, violation.ResourceId);
        Assert.Null(violation.ShiftId);
    }

    private static SchedulingProblem CreateProblem(
        IReadOnlyCollection<Resource> resources,
        IReadOnlyCollection<Shift> shifts,
        IReadOnlyCollection<ResourcePreference> preferences,
        IReadOnlyCollection<ResourceMonthlyNightShiftHistory>? histories = null)
    {
        return new SchedulingProblem(
            period: new SchedulePeriod(
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)),
            resources: resources,
            shifts: shifts,
            availabilityWindows: resources
                .Select(CreateAvailability)
                .ToArray(),
            resourcePreferences: preferences,
            resourceMonthlyNightShiftHistories: histories);
    }

    private static Resource CreateResource(string name = "Guard")
    {
        return new Resource(
            Guid.NewGuid(),
            name,
            hourlyCost: 100m);
    }

    private static Shift CreateMotzeiShabbatNightShift(DateTime startUtc)
    {
        return new Shift(
            Guid.NewGuid(),
            startUtc,
            startUtc.AddHours(8),
            ShiftKind.Night,
            minResourceCount: 0,
            maxResourceCount: 1,
            nightShiftCategory: NightShiftCategory.MotzeiShabbatNight);
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

    private static AvailabilityWindow CreateAvailability(Resource resource)
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
