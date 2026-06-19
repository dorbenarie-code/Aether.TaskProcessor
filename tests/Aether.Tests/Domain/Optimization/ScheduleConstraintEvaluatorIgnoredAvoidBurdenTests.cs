using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ScheduleConstraintEvaluatorIgnoredAvoidBurdenTests
{
    [Fact]
    public void Evaluate_adds_ignored_avoid_burden_violation_with_normalized_quadratic_magnitude()
    {
        var resourceId = Guid.NewGuid();

        var firstShift = CreateShift(new DateTime(2026, 6, 1, 6, 0, 0, DateTimeKind.Utc));
        var secondShift = CreateShift(new DateTime(2026, 6, 2, 6, 0, 0, DateTimeKind.Utc));
        var thirdShift = CreateShift(new DateTime(2026, 6, 3, 6, 0, 0, DateTimeKind.Utc));

        var problem = new SchedulingProblem(
            new SchedulePeriod(
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 4, 0, 0, 0, DateTimeKind.Utc)),
            new[]
            {
                new Resource(resourceId, "Guard 1", hourlyCost: 0)
            },
            new[]
            {
                firstShift,
                secondShift,
                thirdShift
            },
            new[]
            {
                new AvailabilityWindow(
                    resourceId,
                    new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 6, 4, 0, 0, 0, DateTimeKind.Utc))
            },
            new[]
            {
                CreateAvoidPreference(resourceId, firstShift),
                CreateAvoidPreference(resourceId, secondShift)
            });

        var candidate = new ScheduleCandidate(new[]
        {
            new Assignment(resourceId, firstShift.Id),
            new Assignment(resourceId, secondShift.Id),
            new Assignment(resourceId, thirdShift.Id)
        });

        var evaluator = new ScheduleConstraintEvaluator();

        var violations = evaluator.Evaluate(problem, candidate);

        Assert.Equal(
            2,
            violations.Count(violation => violation.Type == ConstraintViolationType.IgnoredAvoidPreference));

        var burdenViolation = Assert.Single(
            violations.Where(violation => violation.Type == ConstraintViolationType.ResourceIgnoredAvoidPreferenceBurden));

        Assert.Equal(ConstraintViolationSeverity.Soft, burdenViolation.Severity);
        Assert.Equal(resourceId, burdenViolation.ResourceId);
        Assert.Null(burdenViolation.ShiftId);

        var expectedMagnitude = 16d * 16d / 24d;

        Assert.NotNull(burdenViolation.Magnitude);
        Assert.Equal(expectedMagnitude, burdenViolation.Magnitude.Value, precision: 6);
    }

    [Fact]
    public void Evaluate_does_not_add_ignored_avoid_burden_violation_when_resource_has_no_ignored_avoid_assignments()
    {
        var resourceId = Guid.NewGuid();

        var firstShift = CreateShift(new DateTime(2026, 6, 1, 6, 0, 0, DateTimeKind.Utc));
        var secondShift = CreateShift(new DateTime(2026, 6, 2, 6, 0, 0, DateTimeKind.Utc));

        var problem = new SchedulingProblem(
            new SchedulePeriod(
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc)),
            new[]
            {
                new Resource(resourceId, "Guard 1", hourlyCost: 0)
            },
            new[]
            {
                firstShift,
                secondShift
            },
            new[]
            {
                new AvailabilityWindow(
                    resourceId,
                    new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc))
            },
            Array.Empty<ResourcePreference>());

        var candidate = new ScheduleCandidate(new[]
        {
            new Assignment(resourceId, firstShift.Id),
            new Assignment(resourceId, secondShift.Id)
        });

        var evaluator = new ScheduleConstraintEvaluator();

        var violations = evaluator.Evaluate(problem, candidate);

        Assert.DoesNotContain(
            violations,
            violation => violation.Type == ConstraintViolationType.ResourceIgnoredAvoidPreferenceBurden);
    }

    private static Shift CreateShift(DateTime startUtc)
    {
        return new Shift(
            Guid.NewGuid(),
            startUtc,
            startUtc.AddHours(8),
            ShiftKind.Morning,
            minResourceCount: 0,
            maxResourceCount: 1);
    }

    private static ResourcePreference CreateAvoidPreference(
        Guid resourceId,
        Shift shift)
    {
        return new ResourcePreference(
            resourceId,
            shift.StartUtc,
            shift.EndUtc,
            ResourcePreferenceType.Avoid,
            ResourcePreferencePriority.Medium);
    }
}
