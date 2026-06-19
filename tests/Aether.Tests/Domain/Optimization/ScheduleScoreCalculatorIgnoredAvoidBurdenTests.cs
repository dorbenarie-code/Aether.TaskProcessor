using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ScheduleScoreCalculatorIgnoredAvoidBurdenTests
{
    [Fact]
    public void Calculate_uses_configured_ignored_avoid_burden_penalty_per_hour()
    {
        var weights = ScheduleScoringWeights.CreateDefault() with
        {
            ResourceIgnoredAvoidPreferenceBurdenPenaltyPerHour = 10
        };

        var calculator = new ScheduleScoreCalculator(weights);

        var score = calculator.Calculate(new[]
        {
            new ConstraintViolation(
                ConstraintViolationType.ResourceIgnoredAvoidPreferenceBurden,
                ConstraintViolationSeverity.Soft,
                "Resource has concentrated ignored avoid preference burden.",
                resourceId: Guid.NewGuid(),
                magnitude: 16d * 16d / 24d)
        });

        Assert.Equal(107, score.TotalPenalty);
        Assert.Equal(893, score.Value);
        Assert.Equal(0, score.HardViolationCount);
        Assert.Equal(1, score.SoftViolationCount);
    }

    [Fact]
    public void Calculate_applies_default_candidate_ignored_avoid_burden_penalty()
    {
        var calculator = new ScheduleScoreCalculator();

        var score = calculator.Calculate(new[]
        {
            new ConstraintViolation(
                ConstraintViolationType.ResourceIgnoredAvoidPreferenceBurden,
                ConstraintViolationSeverity.Soft,
                "Resource has concentrated ignored avoid preference burden.",
                resourceId: Guid.NewGuid(),
                magnitude: 16d * 16d / 24d)
        });

        Assert.Equal(534, score.TotalPenalty);
        Assert.Equal(466, score.Value);
        Assert.Equal(0, score.HardViolationCount);
        Assert.Equal(1, score.SoftViolationCount);
    }
}
