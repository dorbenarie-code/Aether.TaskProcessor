using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ScheduleScoreCalculatorRequestedPreferredHoursTests
{
    [Fact]
    public void Calculate_applies_requested_preferred_hours_not_satisfied_penalty_per_hour()
    {
        var calculator = new ScheduleScoreCalculator();

        var score = calculator.Calculate(
        [
            new ConstraintViolation(
                ConstraintViolationType.ResourceRequestedPreferredHoursNotSatisfied,
                ConstraintViolationSeverity.Soft,
                "Resource requested a preferred shift but was not assigned to it.",
                magnitude: 8)
        ]);

        Assert.Equal(880, score.Value);
        Assert.Equal(0, score.HardViolationCount);
        Assert.Equal(1, score.SoftViolationCount);
        Assert.Equal(120, score.TotalPenalty);
        Assert.True(score.IsFeasible);
    }

    [Fact]
    public void Calculate_requires_magnitude_for_requested_preferred_hours_not_satisfied_violation()
    {
        var calculator = new ScheduleScoreCalculator();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            calculator.Calculate(
            [
                new ConstraintViolation(
                    ConstraintViolationType.ResourceRequestedPreferredHoursNotSatisfied,
                    ConstraintViolationSeverity.Soft,
                    "Resource requested a preferred shift but was not assigned to it.")
            ]));

        Assert.Contains(
            nameof(ConstraintViolationType.ResourceRequestedPreferredHoursNotSatisfied),
            exception.Message);
    }
}
