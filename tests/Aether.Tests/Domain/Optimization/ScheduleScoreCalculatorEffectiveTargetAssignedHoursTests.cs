using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ScheduleScoreCalculatorEffectiveTargetAssignedHoursTests
{
    [Fact]
    public void Calculate_applies_proportional_penalty_for_effective_target_below_target_gap()
    {
        var calculator = new ScheduleScoreCalculator();

        var score = calculator.Calculate(
        [
            new ConstraintViolation(
                ConstraintViolationType.ResourceEffectiveTargetAssignedHoursBelowTarget,
                ConstraintViolationSeverity.Soft,
                "Resource assigned hours are below effective target.",
                magnitude: 12)
        ]);

        Assert.Equal(880, score.Value);
        Assert.Equal(0, score.HardViolationCount);
        Assert.Equal(1, score.SoftViolationCount);
        Assert.Equal(120, score.TotalPenalty);
        Assert.True(score.IsFeasible);
    }

    [Fact]
    public void Calculate_applies_default_variant_c_proportional_penalty_for_effective_target_above_target_gap()
    {
        var calculator = new ScheduleScoreCalculator();

        var score = calculator.Calculate(
        [
            new ConstraintViolation(
                ConstraintViolationType.ResourceEffectiveTargetAssignedHoursAboveTarget,
                ConstraintViolationSeverity.Soft,
                "Resource assigned hours are above effective target.",
                magnitude: 12)
        ]);

        Assert.Equal(760, score.Value);
        Assert.Equal(0, score.HardViolationCount);
        Assert.Equal(1, score.SoftViolationCount);
        Assert.Equal(240, score.TotalPenalty);
        Assert.True(score.IsFeasible);
    }

    [Fact]
    public void Calculate_rounds_fractional_effective_target_gap_penalty_up()
    {
        var calculator = new ScheduleScoreCalculator();

        var score = calculator.Calculate(
        [
            new ConstraintViolation(
                ConstraintViolationType.ResourceEffectiveTargetAssignedHoursAboveTarget,
                ConstraintViolationSeverity.Soft,
                "Resource assigned hours are above effective target.",
                magnitude: 0.1)
        ]);

        Assert.Equal(998, score.Value);
        Assert.Equal(2, score.TotalPenalty);
    }

    [Fact]
    public void Calculate_requires_magnitude_for_effective_target_below_target_gap()
    {
        var calculator = new ScheduleScoreCalculator();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            calculator.Calculate(
            [
                new ConstraintViolation(
                    ConstraintViolationType.ResourceEffectiveTargetAssignedHoursBelowTarget,
                    ConstraintViolationSeverity.Soft,
                    "Resource assigned hours are below effective target.")
            ]));

        Assert.Contains("magnitude", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Calculate_requires_magnitude_for_effective_target_above_target_gap()
    {
        var calculator = new ScheduleScoreCalculator();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            calculator.Calculate(
            [
                new ConstraintViolation(
                    ConstraintViolationType.ResourceEffectiveTargetAssignedHoursAboveTarget,
                    ConstraintViolationSeverity.Soft,
                    "Resource assigned hours are above effective target.")
            ]));

        Assert.Contains("magnitude", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
