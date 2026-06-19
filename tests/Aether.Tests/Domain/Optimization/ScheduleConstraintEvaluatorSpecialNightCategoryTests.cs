using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ScheduleConstraintEvaluatorSpecialNightCategoryTests
{
    [Fact]
    public void Evaluate_allows_motzei_shabbat_night_with_exactly_three_resources()
    {
        var resources = CreateResources(count: 3);

        var shift = CreateNightShift(
            NightShiftCategory.MotzeiShabbatNight,
            minResourceCount: 3,
            maxResourceCount: 3);

        var problem = CreateProblem(resources, [shift]);
        var candidate = CreateCandidate(resources, shift);

        var evaluator = new ScheduleConstraintEvaluator();

        Assert.Empty(evaluator.Evaluate(problem, candidate));
    }

    [Fact]
    public void Evaluate_reports_understaffed_for_motzei_shabbat_night_below_three_resources()
    {
        var resources = CreateResources(count: 2);

        var shift = CreateNightShift(
            NightShiftCategory.MotzeiShabbatNight,
            minResourceCount: 3,
            maxResourceCount: 3);

        var problem = CreateProblem(resources, [shift]);
        var candidate = CreateCandidate(resources, shift);

        var evaluator = new ScheduleConstraintEvaluator();

        var violation = Assert.Single(evaluator.Evaluate(problem, candidate));

        Assert.Equal(ConstraintViolationType.ShiftUnderstaffed, violation.Type);
        Assert.Equal(ConstraintViolationSeverity.Hard, violation.Severity);
        Assert.Null(violation.ResourceId);
        Assert.Equal(shift.Id, violation.ShiftId);
    }

    [Fact]
    public void Evaluate_reports_overstaffed_for_motzei_shabbat_night_above_three_resources()
    {
        var resources = CreateResources(count: 4);

        var shift = CreateNightShift(
            NightShiftCategory.MotzeiShabbatNight,
            minResourceCount: 3,
            maxResourceCount: 3);

        var problem = CreateProblem(resources, [shift]);
        var candidate = CreateCandidate(resources, shift);

        var evaluator = new ScheduleConstraintEvaluator();

        var violation = Assert.Single(evaluator.Evaluate(problem, candidate));

        Assert.Equal(ConstraintViolationType.ShiftOverstaffed, violation.Type);
        Assert.Equal(ConstraintViolationSeverity.Hard, violation.Severity);
        Assert.Null(violation.ResourceId);
        Assert.Equal(shift.Id, violation.ShiftId);
    }

    [Fact]
    public void Evaluate_requires_prefer_preference_for_request_only_friday_night()
    {
        var resource = CreateResource();

        var shift = CreateNightShift(
            NightShiftCategory.FridayNight,
            minResourceCount: 0,
            maxResourceCount: 1,
            requiresPreferenceToAssign: true);

        var problem = CreateProblem([resource], [shift]);
        var candidate = CreateCandidate([resource], shift);

        var evaluator = new ScheduleConstraintEvaluator();

        var violation = Assert.Single(evaluator.Evaluate(problem, candidate));

        Assert.Equal(ConstraintViolationType.AssignedWithoutRequiredPreference, violation.Type);
        Assert.Equal(ConstraintViolationSeverity.Hard, violation.Severity);
        Assert.Equal(resource.Id, violation.ResourceId);
        Assert.Equal(shift.Id, violation.ShiftId);
    }

    private static SchedulingProblem CreateProblem(
        IReadOnlyCollection<Resource> resources,
        IReadOnlyCollection<Shift> shifts)
    {
        return new SchedulingProblem(
            period: CreatePeriod(),
            resources: resources,
            shifts: shifts,
            availabilityWindows: resources
                .Select(CreateAvailability)
                .ToArray(),
            resourcePreferences: []);
    }

    private static SchedulePeriod CreatePeriod()
    {
        return new SchedulePeriod(
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc));
    }

    private static Resource[] CreateResources(int count)
    {
        return Enumerable
            .Range(1, count)
            .Select(index => CreateResource($"Guard {index}"))
            .ToArray();
    }

    private static Resource CreateResource(string name = "Guard")
    {
        return new Resource(
            Guid.NewGuid(),
            name,
            hourlyCost: 100m);
    }

    private static Shift CreateNightShift(
        NightShiftCategory nightShiftCategory,
        int minResourceCount,
        int maxResourceCount,
        bool requiresPreferenceToAssign = false)
    {
        return new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 1, 3, 22, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 4, 6, 30, 0, DateTimeKind.Utc),
            ShiftKind.Night,
            minResourceCount,
            maxResourceCount,
            requiresPreferenceToAssign: requiresPreferenceToAssign,
            nightShiftCategory: nightShiftCategory);
    }

    private static AvailabilityWindow CreateAvailability(Resource resource)
    {
        return new AvailabilityWindow(
            resource.Id,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc));
    }

    private static ScheduleCandidate CreateCandidate(
        IReadOnlyCollection<Resource> resources,
        Shift shift)
    {
        return new ScheduleCandidate(
            resources
                .Select(resource => new Assignment(resource.Id, shift.Id))
                .ToArray());
    }
}
