using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ScheduleConstraintEvaluatorMonthlyNightShiftHistoryTests
{
    [Fact]
    public void Evaluate_allows_one_motzei_shabbat_night_without_history()
    {
        var resource = CreateResource();
        var shift = CreateMotzeiShabbatNightShift(new DateTime(2026, 1, 3, 22, 30, 0, DateTimeKind.Utc));

        var problem = CreateProblem([resource], [shift]);
        var candidate = CreateCandidate(resource, [shift]);

        var evaluator = new ScheduleConstraintEvaluator();

        Assert.Empty(evaluator.Evaluate(problem, candidate));
    }

    [Fact]
    public void Evaluate_reports_hard_violation_when_history_and_candidate_exceed_monthly_motzei_shabbat_quota()
    {
        var resource = CreateResource();
        var shift = CreateMotzeiShabbatNightShift(new DateTime(2026, 1, 10, 22, 30, 0, DateTimeKind.Utc));

        var history = new ResourceMonthlyNightShiftHistory(
            resource.Id,
            year: 2026,
            month: 1,
            NightShiftCategory.MotzeiShabbatNight,
            assignedCount: 1);

        var problem = CreateProblem([resource], [shift], [history]);
        var candidate = CreateCandidate(resource, [shift]);

        var evaluator = new ScheduleConstraintEvaluator();

        var violation = Assert.Single(evaluator.Evaluate(problem, candidate));

        Assert.Equal(ConstraintViolationType.ResourceMonthlyNightShiftQuotaExceeded, violation.Type);
        Assert.Equal(ConstraintViolationSeverity.Hard, violation.Severity);
        Assert.Equal(resource.Id, violation.ResourceId);
        Assert.Null(violation.ShiftId);
    }

    [Fact]
    public void Evaluate_allows_two_different_resources_to_each_receive_one_motzei_shabbat_night()
    {
        var firstResource = CreateResource("Guard 1");
        var secondResource = CreateResource("Guard 2");

        var shift = CreateMotzeiShabbatNightShift(
            new DateTime(2026, 1, 3, 22, 30, 0, DateTimeKind.Utc),
            maxResourceCount: 2);

        var problem = CreateProblem([firstResource, secondResource], [shift]);

        var candidate = new ScheduleCandidate(
        [
            new Assignment(firstResource.Id, shift.Id),
            new Assignment(secondResource.Id, shift.Id)
        ]);

        var evaluator = new ScheduleConstraintEvaluator();

        Assert.Empty(evaluator.Evaluate(problem, candidate));
    }

    [Fact]
    public void Evaluate_allows_existing_history_when_candidate_does_not_add_motzei_shabbat_night()
    {
        var resource = CreateResource();
        var shift = CreateMotzeiShabbatNightShift(new DateTime(2026, 1, 10, 22, 30, 0, DateTimeKind.Utc));

        var history = new ResourceMonthlyNightShiftHistory(
            resource.Id,
            year: 2026,
            month: 1,
            NightShiftCategory.MotzeiShabbatNight,
            assignedCount: 1);

        var problem = CreateProblem([resource], [shift], [history]);
        var candidate = new ScheduleCandidate([]);

        var evaluator = new ScheduleConstraintEvaluator();

        Assert.Empty(evaluator.Evaluate(problem, candidate));
    }

    [Fact]
    public void Evaluate_reports_hard_violation_when_candidate_contains_two_motzei_shabbat_nights_in_same_month()
    {
        var resource = CreateResource();

        var firstShift = CreateMotzeiShabbatNightShift(
            new DateTime(2026, 1, 3, 22, 30, 0, DateTimeKind.Utc));

        var secondShift = CreateMotzeiShabbatNightShift(
            new DateTime(2026, 1, 10, 22, 30, 0, DateTimeKind.Utc));

        var problem = CreateProblem([resource], [firstShift, secondShift]);
        var candidate = CreateCandidate(resource, [firstShift, secondShift]);

        var evaluator = new ScheduleConstraintEvaluator();

        var violation = Assert.Single(evaluator.Evaluate(problem, candidate));

        Assert.Equal(ConstraintViolationType.ResourceMonthlyNightShiftQuotaExceeded, violation.Type);
        Assert.Equal(ConstraintViolationSeverity.Hard, violation.Severity);
        Assert.Equal(resource.Id, violation.ResourceId);
        Assert.Null(violation.ShiftId);
    }

    [Fact]
    public void Evaluate_allows_one_motzei_shabbat_night_per_month()
    {
        var resource = CreateResource();

        var januaryShift = CreateMotzeiShabbatNightShift(
            new DateTime(2026, 1, 31, 22, 30, 0, DateTimeKind.Utc));

        var februaryShift = CreateMotzeiShabbatNightShift(
            new DateTime(2026, 2, 7, 22, 30, 0, DateTimeKind.Utc));

        var problem = CreateProblem([resource], [januaryShift, februaryShift]);
        var candidate = CreateCandidate(resource, [januaryShift, februaryShift]);

        var evaluator = new ScheduleConstraintEvaluator();

        Assert.Empty(evaluator.Evaluate(problem, candidate));
    }

    private static SchedulingProblem CreateProblem(
        IReadOnlyCollection<Resource> resources,
        IReadOnlyCollection<Shift> shifts,
        IReadOnlyCollection<ResourceMonthlyNightShiftHistory>? histories = null)
    {
        return new SchedulingProblem(
            period: new SchedulePeriod(
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)),
            resources: resources,
            shifts: shifts,
            availabilityWindows: resources
                .Select(CreateAvailability)
                .ToArray(),
            resourcePreferences: [],
            resourceMonthlyNightShiftHistories: histories);
    }

    private static Resource CreateResource(string name = "Guard")
    {
        return new Resource(
            Guid.NewGuid(),
            name,
            hourlyCost: 100m);
    }

    private static Shift CreateMotzeiShabbatNightShift(
        DateTime startUtc,
        int maxResourceCount = 1)
    {
        return new Shift(
            Guid.NewGuid(),
            startUtc,
            startUtc.AddHours(8),
            ShiftKind.Night,
            minResourceCount: 0,
            maxResourceCount: maxResourceCount,
            nightShiftCategory: NightShiftCategory.MotzeiShabbatNight);
    }

    private static AvailabilityWindow CreateAvailability(Resource resource)
    {
        return new AvailabilityWindow(
            resource.Id,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));
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
