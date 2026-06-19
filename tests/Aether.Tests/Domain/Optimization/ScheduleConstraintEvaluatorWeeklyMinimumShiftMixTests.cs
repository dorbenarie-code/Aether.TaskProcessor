using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ScheduleConstraintEvaluatorWeeklyMinimumShiftMixTests
{
    [Fact]
    public void Evaluate_does_not_report_weekly_mix_violation_when_policy_is_disabled()
    {
        var resource = CreateResource();

        var shift = CreateShift(
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            ShiftKind.Morning);

        var problem = new SchedulingProblem(
            period: CreatePeriod(days: 14),
            resources: [resource],
            shifts: [shift],
            availabilityWindows: [],
            resourcePreferences: []);

        var candidate = new ScheduleCandidate([]);

        var evaluator = new ScheduleConstraintEvaluator();

        Assert.Empty(evaluator.Evaluate(problem, candidate));
    }

    [Fact]
    public void Evaluate_does_not_report_weekly_mix_violation_when_resource_meets_policy()
    {
        var resource = CreateResource();

        var shifts = new[]
        {
            CreateShift(new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc), ShiftKind.Morning),
            CreateShift(new DateTime(2026, 1, 2, 9, 0, 0, DateTimeKind.Utc), ShiftKind.Morning),
            CreateShift(new DateTime(2026, 1, 3, 14, 0, 0, DateTimeKind.Utc), ShiftKind.Afternoon)
        };

        var problem = CreateProblem(resource, shifts, days: 7);

        var candidate = CreateCandidate(resource, shifts);

        var evaluator = new ScheduleConstraintEvaluator();

        Assert.Empty(evaluator.Evaluate(problem, candidate));
    }

    [Fact]
    public void Evaluate_reports_soft_violation_when_resource_is_missing_morning_shift()
    {
        var resource = CreateResource();

        var shifts = new[]
        {
            CreateShift(new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc), ShiftKind.Morning),
            CreateShift(new DateTime(2026, 1, 2, 14, 0, 0, DateTimeKind.Utc), ShiftKind.Afternoon)
        };

        var problem = CreateProblem(resource, shifts, days: 7);

        var candidate = CreateCandidate(resource, shifts);

        var evaluator = new ScheduleConstraintEvaluator();

        var violation = Assert.Single(evaluator.Evaluate(problem, candidate));

        Assert.Equal(ConstraintViolationType.ResourceWeeklyMinimumShiftMixNotMet, violation.Type);
        Assert.Equal(ConstraintViolationSeverity.Soft, violation.Severity);
        Assert.Equal(resource.Id, violation.ResourceId);
        Assert.Null(violation.ShiftId);
    }

    [Fact]
    public void Evaluate_reports_soft_violation_when_resource_is_missing_afternoon_shift()
    {
        var resource = CreateResource();

        var shifts = new[]
        {
            CreateShift(new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc), ShiftKind.Morning),
            CreateShift(new DateTime(2026, 1, 2, 9, 0, 0, DateTimeKind.Utc), ShiftKind.Morning)
        };

        var problem = CreateProblem(resource, shifts, days: 7);

        var candidate = CreateCandidate(resource, shifts);

        var evaluator = new ScheduleConstraintEvaluator();

        var violation = Assert.Single(evaluator.Evaluate(problem, candidate));

        Assert.Equal(ConstraintViolationType.ResourceWeeklyMinimumShiftMixNotMet, violation.Type);
        Assert.Equal(ConstraintViolationSeverity.Soft, violation.Severity);
        Assert.Equal(resource.Id, violation.ResourceId);
        Assert.Null(violation.ShiftId);
    }

    [Fact]
    public void Evaluate_checks_each_full_week_independently()
    {
        var resource = CreateResource();

        var shifts = new[]
        {
            CreateShift(new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc), ShiftKind.Morning),
            CreateShift(new DateTime(2026, 1, 2, 9, 0, 0, DateTimeKind.Utc), ShiftKind.Morning),
            CreateShift(new DateTime(2026, 1, 3, 14, 0, 0, DateTimeKind.Utc), ShiftKind.Afternoon),
            CreateShift(new DateTime(2026, 1, 8, 9, 0, 0, DateTimeKind.Utc), ShiftKind.Morning),
            CreateShift(new DateTime(2026, 1, 9, 9, 0, 0, DateTimeKind.Utc), ShiftKind.Morning)
        };

        var problem = CreateProblem(resource, shifts, days: 14);

        var candidate = CreateCandidate(resource, shifts);

        var evaluator = new ScheduleConstraintEvaluator();

        var violation = Assert.Single(evaluator.Evaluate(problem, candidate));

        Assert.Equal(ConstraintViolationType.ResourceWeeklyMinimumShiftMixNotMet, violation.Type);
        Assert.Equal(ConstraintViolationSeverity.Soft, violation.Severity);
        Assert.Equal(resource.Id, violation.ResourceId);
    }

    [Fact]
    public void Evaluate_ignores_partial_trailing_week()
    {
        var resource = CreateResource();

        var shifts = new[]
        {
            CreateShift(new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc), ShiftKind.Morning),
            CreateShift(new DateTime(2026, 1, 2, 9, 0, 0, DateTimeKind.Utc), ShiftKind.Morning),
            CreateShift(new DateTime(2026, 1, 3, 14, 0, 0, DateTimeKind.Utc), ShiftKind.Afternoon)
        };

        var problem = CreateProblem(resource, shifts, days: 10);

        var candidate = CreateCandidate(resource, shifts);

        var evaluator = new ScheduleConstraintEvaluator();

        Assert.Empty(evaluator.Evaluate(problem, candidate));
    }

    private static SchedulingProblem CreateProblem(
        Resource resource,
        IReadOnlyCollection<Shift> shifts,
        int days)
    {
        return new SchedulingProblem(
            period: CreatePeriod(days),
            resources: [resource],
            shifts: shifts,
            availabilityWindows: [CreateAvailability(resource, days)],
            resourcePreferences: [],
            minimumMorningShiftsPerResourcePerFullWeek: 2,
            minimumAfternoonShiftsPerResourcePerFullWeek: 1);
    }

    private static SchedulePeriod CreatePeriod(int days)
    {
        return new SchedulePeriod(
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(days));
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
        ShiftKind kind)
    {
        return new Shift(
            Guid.NewGuid(),
            startUtc,
            startUtc.AddHours(8),
            kind,
            minResourceCount: 0,
            maxResourceCount: 1);
    }

    private static AvailabilityWindow CreateAvailability(
        Resource resource,
        int days)
    {
        return new AvailabilityWindow(
            resource.Id,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(days));
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
