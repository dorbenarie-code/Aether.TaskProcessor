using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ScheduleEvaluatorTests
{
    [Fact]
    public void Evaluate_returns_feasible_result_when_candidate_has_no_violations()
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

        var evaluator = new ScheduleEvaluator();

        var result = evaluator.Evaluate(problem, candidate);

        Assert.True(result.IsFeasible);
        Assert.Empty(result.Violations);
        Assert.Equal(1000, result.Score.Value);
    }

    [Fact]
    public void Evaluate_returns_violations_and_score_when_candidate_breaks_constraints()
    {
        var resource = CreateResource();

        var shift = CreateShift(
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc));

        var unavailableWindow = new AvailabilityWindow(
            resource.Id,
            new DateTime(2026, 1, 1, 18, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 22, 0, 0, DateTimeKind.Utc));

        var problem = new SchedulingProblem(
            period: CreatePeriod(),
            resources: [resource],
            shifts: [shift],
            availabilityWindows: [unavailableWindow],
            resourcePreferences: []);

        var candidate = new ScheduleCandidate(
        [
            new Assignment(resource.Id, shift.Id)
        ]);

        var evaluator = new ScheduleEvaluator();

        var result = evaluator.Evaluate(problem, candidate);

        var violation = Assert.Single(result.Violations);

        Assert.False(result.IsFeasible);
        Assert.Equal(ConstraintViolationType.ResourceUnavailable, violation.Type);
        Assert.Equal(0, result.Score.Value);
        Assert.Equal(1, result.Score.HardViolationCount);
        Assert.Equal(0, result.Score.SoftViolationCount);
    }

    [Fact]
    public void Evaluate_keeps_result_feasible_when_avoid_preference_is_ignored()
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

        var evaluator = new ScheduleEvaluator();

        var result = evaluator.Evaluate(problem, candidate);

        var violation = Assert.Single(
            result.Violations.Where(item => item.Type == ConstraintViolationType.IgnoredAvoidPreference));

        var burdenViolation = Assert.Single(
            result.Violations.Where(item => item.Type == ConstraintViolationType.ResourceIgnoredAvoidPreferenceBurden));

        Assert.True(result.IsFeasible);
        Assert.Equal(ConstraintViolationType.IgnoredAvoidPreference, violation.Type);

        Assert.Equal(ConstraintViolationSeverity.Soft, burdenViolation.Severity);
        Assert.Equal(violation.ResourceId, burdenViolation.ResourceId);
        Assert.Null(burdenViolation.ShiftId);
        Assert.Equal(ConstraintViolationSeverity.Soft, violation.Severity);
        Assert.Equal(700, result.Score.Value);
        Assert.Equal(0, result.Score.HardViolationCount);
        Assert.Equal(2, result.Score.SoftViolationCount);
        Assert.Equal(300, result.Score.TotalPenalty);
    }

    [Fact]
    public void Schedule_evaluation_result_copies_violations()
    {
        var violations = new List<ConstraintViolation>
        {
            new(
                ConstraintViolationType.ResourceUnavailable,
                ConstraintViolationSeverity.Hard,
                "Resource is unavailable.")
        };

        var score = new ScheduleScore(
            value: 900,
            hardViolationCount: 1,
            softViolationCount: 0,
            totalPenalty: 100);

        var result = new ScheduleEvaluationResult(score, violations);

        violations.Clear();

        Assert.Single(result.Violations);
    }

    [Fact]
    public void Schedule_evaluation_result_rejects_null_score()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new ScheduleEvaluationResult(
                null!,
                []));

        Assert.Equal("score", exception.ParamName);
    }

    [Fact]
    public void Schedule_evaluation_result_rejects_null_violations()
    {
        var score = new ScheduleScore(
            value: 1000,
            hardViolationCount: 0,
            softViolationCount: 0,
            totalPenalty: 0);

        var exception = Assert.Throws<ArgumentNullException>(() =>
            new ScheduleEvaluationResult(
                score,
                null!));

        Assert.Equal("violations", exception.ParamName);
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
