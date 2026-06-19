using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ScheduleScoreCalculatorMonthlyNightShiftPreferenceTests
{
    [Fact]
    public void Calculate_applies_soft_penalty_for_resource_monthly_night_shift_preference_not_satisfied_violation()
    {
        var calculator = new ScheduleScoreCalculator();

        var score = calculator.Calculate(
        [
            new ConstraintViolation(
                ConstraintViolationType.ResourceMonthlyNightShiftPreferenceNotSatisfied,
                ConstraintViolationSeverity.Soft,
                "Resource requested a monthly night shift but did not receive it.")
        ]);

        Assert.Equal(500, score.Value);
        Assert.Equal(0, score.HardViolationCount);
        Assert.Equal(1, score.SoftViolationCount);
        Assert.Equal(500, score.TotalPenalty);
        Assert.True(score.IsFeasible);
    }
}
