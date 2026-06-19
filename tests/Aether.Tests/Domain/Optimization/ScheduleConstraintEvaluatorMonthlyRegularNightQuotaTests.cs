using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ScheduleConstraintEvaluatorMonthlyRegularNightQuotaTests
{
    [Fact]
    public void Evaluate_reports_hard_violation_when_candidate_contains_two_regular_nights_in_same_month()
    {
        var resource = CreateResource();

        var firstShift = CreateRegularNightShift(
            new DateTime(2026, 1, 4, 22, 30, 0, DateTimeKind.Utc));

        var secondShift = CreateRegularNightShift(
            new DateTime(2026, 1, 5, 22, 30, 0, DateTimeKind.Utc));

        var problem = CreateProblem([resource], [firstShift, secondShift]);
        var candidate = CreateCandidate(resource, [firstShift, secondShift]);

        var evaluator = new ScheduleConstraintEvaluator();

        var violation = Assert.Single(evaluator.Evaluate(problem, candidate));

        Assert.Equal(ConstraintViolationType.ResourceMonthlyNightShiftQuotaExceeded, violation.Type);
        Assert.Equal(ConstraintViolationSeverity.Hard, violation.Severity);
        Assert.Equal(resource.Id, violation.ResourceId);
        Assert.Null(violation.ShiftId);
        Assert.Contains("Regular", violation.Message);
    }

    [Fact]
    public void Evaluate_reports_hard_violation_when_history_and_candidate_exceed_monthly_regular_night_quota()
    {
        var resource = CreateResource();

        var shift = CreateRegularNightShift(
            new DateTime(2026, 1, 5, 22, 30, 0, DateTimeKind.Utc));

        var history = new ResourceMonthlyNightShiftHistory(
            resource.Id,
            year: 2026,
            month: 1,
            NightShiftCategory.Regular,
            assignedCount: 1);

        var problem = CreateProblem([resource], [shift], [history]);
        var candidate = CreateCandidate(resource, [shift]);

        var evaluator = new ScheduleConstraintEvaluator();

        var violation = Assert.Single(evaluator.Evaluate(problem, candidate));

        Assert.Equal(ConstraintViolationType.ResourceMonthlyNightShiftQuotaExceeded, violation.Type);
        Assert.Equal(ConstraintViolationSeverity.Hard, violation.Severity);
        Assert.Equal(resource.Id, violation.ResourceId);
        Assert.Null(violation.ShiftId);
        Assert.Contains("Regular", violation.Message);
    }

    [Fact]
    public void Evaluate_allows_one_regular_and_one_motzei_shabbat_night_in_same_month()
    {
        var resource = CreateResource();

        var regularShift = CreateRegularNightShift(
            new DateTime(2026, 1, 4, 22, 30, 0, DateTimeKind.Utc));

        var motzeiShabbatShift = CreateMotzeiShabbatNightShift(
            new DateTime(2026, 1, 10, 22, 30, 0, DateTimeKind.Utc));

        var problem = CreateProblem([resource], [regularShift, motzeiShabbatShift]);
        var candidate = CreateCandidate(resource, [regularShift, motzeiShabbatShift]);

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
                new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)),
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

    private static Shift CreateRegularNightShift(DateTime startUtc)
    {
        return CreateNightShift(startUtc, NightShiftCategory.Regular);
    }

    private static Shift CreateMotzeiShabbatNightShift(DateTime startUtc)
    {
        return CreateNightShift(startUtc, NightShiftCategory.MotzeiShabbatNight);
    }

    private static Shift CreateNightShift(
        DateTime startUtc,
        NightShiftCategory category)
    {
        return new Shift(
            Guid.NewGuid(),
            startUtc,
            startUtc.AddHours(8),
            ShiftKind.Night,
            minResourceCount: 0,
            maxResourceCount: 1,
            nightShiftCategory: category);
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
