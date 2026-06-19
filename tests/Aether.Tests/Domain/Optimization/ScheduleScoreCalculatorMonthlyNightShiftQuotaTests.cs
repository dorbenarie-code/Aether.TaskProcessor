using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ScheduleScoreCalculatorMonthlyNightShiftQuotaTests
{
    [Fact]
    public void Calculate_applies_large_penalty_for_resource_monthly_night_shift_quota_exceeded_violation()
    {
        var calculator = new ScheduleScoreCalculator();

        var score = calculator.Calculate(
        [
            new ConstraintViolation(
                ConstraintViolationType.ResourceMonthlyNightShiftQuotaExceeded,
                ConstraintViolationSeverity.Hard,
                "Resource exceeded the monthly night shift quota.")
        ]);

        Assert.Equal(0, score.Value);
        Assert.Equal(1, score.HardViolationCount);
        Assert.Equal(0, score.SoftViolationCount);
        Assert.Equal(30000, score.TotalPenalty);
        Assert.False(score.IsFeasible);
    }
}
