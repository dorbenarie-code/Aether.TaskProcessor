using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ScheduleScoreCalculatorWeeklyMinimumShiftMixTests
{
    [Fact]
    public void Calculate_applies_strong_soft_penalty_for_weekly_minimum_shift_mix_violation()
    {
        var calculator = new ScheduleScoreCalculator();

        var score = calculator.Calculate(
        [
            new ConstraintViolation(
                ConstraintViolationType.ResourceWeeklyMinimumShiftMixNotMet,
                ConstraintViolationSeverity.Soft,
                "Resource did not meet weekly minimum shift mix.")
        ]);

        Assert.Equal(0, score.Value);
        Assert.Equal(0, score.HardViolationCount);
        Assert.Equal(1, score.SoftViolationCount);
        Assert.Equal(1000, score.TotalPenalty);
        Assert.True(score.IsFeasible);
    }
}
