using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ScheduleScoreCalculatorScoringWeightsTests
{
    [Fact]
    public void Calculate_uses_custom_requested_preferred_hours_not_satisfied_penalty_per_hour()
    {
        var weights = ScheduleScoringWeights.CreateDefault() with
        {
            ResourceRequestedPreferredHoursNotSatisfiedPenaltyPerHour = 3
        };

        var calculator = new ScheduleScoreCalculator(weights);

        var score = calculator.Calculate(
        [
            new ConstraintViolation(
                ConstraintViolationType.ResourceRequestedPreferredHoursNotSatisfied,
                ConstraintViolationSeverity.Soft,
                "Resource requested a preferred shift but was not assigned to it.",
                magnitude: 8)
        ]);

        Assert.Equal(976, score.Value);
        Assert.Equal(0, score.HardViolationCount);
        Assert.Equal(1, score.SoftViolationCount);
        Assert.Equal(24, score.TotalPenalty);
        Assert.True(score.IsFeasible);
    }

    [Fact]
    public void Constructor_rejects_null_weights()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new ScheduleScoreCalculator(null!));

        Assert.Equal("weights", exception.ParamName);
    }

    [Fact]
    public void Constructor_rejects_invalid_weights()
    {
        var weights = ScheduleScoringWeights.CreateDefault() with
        {
            ResourceEffectiveTargetAssignedHoursBelowTargetPenaltyPerHour = -1
        };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ScheduleScoreCalculator(weights));

        Assert.Equal(
            nameof(ScheduleScoringWeights.ResourceEffectiveTargetAssignedHoursBelowTargetPenaltyPerHour),
            exception.ParamName);
    }
}
