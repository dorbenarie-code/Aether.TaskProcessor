using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ScheduleConstraintEvaluatorWeeklyMinimumShiftMixHardeningTests
{
    [Fact]
    public void Evaluate_reports_weekly_mix_violation_for_each_failed_full_week()
    {
        var resource = CreateResource();

        var shifts = new[]
        {
            CreateShift(new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc), ShiftKind.Morning),
            CreateShift(new DateTime(2026, 1, 2, 14, 0, 0, DateTimeKind.Utc), ShiftKind.Afternoon),
            CreateShift(new DateTime(2026, 1, 8, 9, 0, 0, DateTimeKind.Utc), ShiftKind.Morning),
            CreateShift(new DateTime(2026, 1, 9, 14, 0, 0, DateTimeKind.Utc), ShiftKind.Afternoon)
        };

        var problem = CreateProblem([resource], shifts, days: 14);
        var candidate = CreateCandidate(resource, shifts);

        var evaluator = new ScheduleConstraintEvaluator();

        var violations = evaluator
            .Evaluate(problem, candidate)
            .Where(violation => violation.Type == ConstraintViolationType.ResourceWeeklyMinimumShiftMixNotMet)
            .ToArray();

        Assert.Equal(2, violations.Length);
        Assert.All(violations, violation => Assert.Equal(resource.Id, violation.ResourceId));
        Assert.All(violations, violation => Assert.Equal(ConstraintViolationSeverity.Soft, violation.Severity));
    }

    [Fact]
    public void Evaluate_does_not_count_night_shifts_toward_weekly_minimum_shift_mix()
    {
        var resource = CreateResource();

        var shifts = Enumerable
            .Range(0, 5)
            .Select(day => CreateShift(
                new DateTime(2026, 1, 1, 22, 0, 0, DateTimeKind.Utc).AddDays(day),
                ShiftKind.Night))
            .ToArray();

        var problem = CreateProblem([resource], shifts, days: 7);
        var candidate = CreateCandidate(resource, shifts);

        var evaluator = new ScheduleConstraintEvaluator();

        var violation = Assert.Single(evaluator.Evaluate(problem, candidate));

        Assert.Equal(ConstraintViolationType.ResourceWeeklyMinimumShiftMixNotMet, violation.Type);
        Assert.Equal(ConstraintViolationSeverity.Soft, violation.Severity);
        Assert.Equal(resource.Id, violation.ResourceId);
        Assert.Null(violation.ShiftId);
    }

    [Fact]
    public void Evaluate_reports_weekly_mix_violations_for_empty_candidate()
    {
        var firstResource = CreateResource("Dana");
        var secondResource = CreateResource("Eli");

        var optionalShift = CreateShift(
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            ShiftKind.Morning);

        var problem = CreateProblem(
            [firstResource, secondResource],
            [optionalShift],
            days: 14);

        var candidate = new ScheduleCandidate([]);

        var evaluator = new ScheduleConstraintEvaluator();

        var violations = evaluator
            .Evaluate(problem, candidate)
            .Where(violation => violation.Type == ConstraintViolationType.ResourceWeeklyMinimumShiftMixNotMet)
            .ToArray();

        Assert.Equal(4, violations.Length);
        Assert.All(violations, violation => Assert.Equal(ConstraintViolationSeverity.Soft, violation.Severity));
    }

    [Fact]
    public void Evaluate_accumulates_weekly_mix_penalty_across_failed_weeks()
    {
        var resource = CreateResource();

        var shifts = new[]
        {
            CreateShift(new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc), ShiftKind.Morning),
            CreateShift(new DateTime(2026, 1, 2, 14, 0, 0, DateTimeKind.Utc), ShiftKind.Afternoon),
            CreateShift(new DateTime(2026, 1, 8, 9, 0, 0, DateTimeKind.Utc), ShiftKind.Morning),
            CreateShift(new DateTime(2026, 1, 9, 14, 0, 0, DateTimeKind.Utc), ShiftKind.Afternoon)
        };

        var problem = CreateProblem([resource], shifts, days: 14);
        var candidate = CreateCandidate(resource, shifts);

        var evaluator = new ScheduleEvaluator();

        var result = evaluator.Evaluate(problem, candidate);

        Assert.True(result.IsFeasible);
        Assert.Equal(0, result.Score.HardViolationCount);
        Assert.Equal(2, result.Score.SoftViolationCount);
        Assert.Equal(2000, result.Score.TotalPenalty);
        Assert.Equal(0, result.Score.Value);
    }

    private static SchedulingProblem CreateProblem(
        IReadOnlyCollection<Resource> resources,
        IReadOnlyCollection<Shift> shifts,
        int days)
    {
        return new SchedulingProblem(
            period: CreatePeriod(days),
            resources: resources,
            shifts: shifts,
            availabilityWindows: resources
                .Select(resource => CreateAvailability(resource, days))
                .ToArray(),
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

    private static Resource CreateResource(string name = "Dana")
    {
        return new Resource(
            Guid.NewGuid(),
            name,
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
