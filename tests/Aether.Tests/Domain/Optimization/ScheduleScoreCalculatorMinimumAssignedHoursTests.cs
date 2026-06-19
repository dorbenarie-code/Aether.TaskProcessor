using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ScheduleScoreCalculatorMinimumAssignedHoursTests
{
    [Fact]
    public void Calculate_applies_large_penalty_for_resource_minimum_assigned_hours_violation()
    {
        var calculator = new ScheduleScoreCalculator();

        var score = calculator.Calculate(
        [
            new ConstraintViolation(
                ConstraintViolationType.ResourceMinimumAssignedHoursNotMet,
                ConstraintViolationSeverity.Hard,
                "Resource did not reach minimum assigned hours.")
        ]);

        Assert.Equal(0, score.Value);
        Assert.Equal(1, score.HardViolationCount);
        Assert.Equal(0, score.SoftViolationCount);
        Assert.Equal(50000, score.TotalPenalty);
        Assert.False(score.IsFeasible);
    }
}
