using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ScheduleScoreCalculatorAssignedHoursBalanceTests
{
    [Fact]
    public void Calculate_applies_default_candidate_balance_excess_penalty_for_assigned_hours_balance_violation()
    {
        var calculator = new ScheduleScoreCalculator();

        var score = calculator.Calculate(
        [
            new ConstraintViolation(
                ConstraintViolationType.ResourceAssignedHoursBalanceExceeded,
                ConstraintViolationSeverity.Soft,
                "Resource assigned hours deviation from candidate average exceeds the allowed tolerance.",
                resourceId: Guid.NewGuid(),
                magnitude: 1.25)
        ]);

        Assert.Equal(475, score.Value);
        Assert.Equal(0, score.HardViolationCount);
        Assert.Equal(1, score.SoftViolationCount);
        Assert.Equal(525, score.TotalPenalty);
        Assert.True(score.IsFeasible);
    }

    [Fact]
    public void Calculate_applies_configurable_balance_excess_hours_penalty_when_enabled()
    {
        var calculator = new ScheduleScoreCalculator(
            ScheduleScoringWeights.CreateDefault() with
            {
                ResourceAssignedHoursBalanceExceededPenaltyPerExcessHour = 50
            });

        var score = calculator.Calculate(
        [
            new ConstraintViolation(
                ConstraintViolationType.ResourceAssignedHoursBalanceExceeded,
                ConstraintViolationSeverity.Soft,
                "Resource assigned hours deviation from candidate average exceeds the allowed tolerance.",
                resourceId: Guid.NewGuid(),
                magnitude: 1.25)
        ]);

        Assert.Equal(537, score.Value);
        Assert.Equal(0, score.HardViolationCount);
        Assert.Equal(1, score.SoftViolationCount);
        Assert.Equal(463, score.TotalPenalty);
        Assert.True(score.IsFeasible);
    }

    [Fact]
    public void Calculate_requires_magnitude_for_balance_excess_hours_penalty_when_enabled()
    {
        var calculator = new ScheduleScoreCalculator(
            ScheduleScoringWeights.CreateDefault() with
            {
                ResourceAssignedHoursBalanceExceededPenaltyPerExcessHour = 50
            });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            calculator.Calculate(
            [
                new ConstraintViolation(
                    ConstraintViolationType.ResourceAssignedHoursBalanceExceeded,
                    ConstraintViolationSeverity.Soft,
                    "Resource assigned hours deviation from candidate average exceeds the allowed tolerance.",
                    resourceId: Guid.NewGuid())
            ]));

        Assert.Contains(
            nameof(ConstraintViolationType.ResourceAssignedHoursBalanceExceeded),
            exception.Message);
    }

}
