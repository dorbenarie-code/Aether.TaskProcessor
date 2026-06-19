using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ScheduleScoreCalculatorTests
{
    [Fact]
    public void Calculate_returns_max_score_when_there_are_no_violations()
    {
        var calculator = new ScheduleScoreCalculator();

        var score = calculator.Calculate([]);

        Assert.Equal(1000, score.Value);
        Assert.Equal(0, score.HardViolationCount);
        Assert.Equal(0, score.SoftViolationCount);
        Assert.Equal(0, score.TotalPenalty);
        Assert.True(score.IsFeasible);
    }

    [Fact]
    public void Calculate_applies_large_penalty_for_resource_unavailable_violation()
    {
        var calculator = new ScheduleScoreCalculator();

        var score = calculator.Calculate(
        [
            new ConstraintViolation(
                ConstraintViolationType.ResourceUnavailable,
                ConstraintViolationSeverity.Hard,
                "Resource is unavailable.")
        ]);

        Assert.Equal(0, score.Value);
        Assert.Equal(1, score.HardViolationCount);
        Assert.Equal(0, score.SoftViolationCount);
        Assert.Equal(100000, score.TotalPenalty);
        Assert.False(score.IsFeasible);
    }

    [Fact]
    public void Calculate_applies_large_penalty_for_overlapping_shift_violation()
    {
        var calculator = new ScheduleScoreCalculator();

        var score = calculator.Calculate(
        [
            new ConstraintViolation(
                ConstraintViolationType.ResourceAssignedToOverlappingShifts,
                ConstraintViolationSeverity.Hard,
                "Resource is assigned to overlapping shifts.")
        ]);

        Assert.Equal(0, score.Value);
        Assert.Equal(1, score.HardViolationCount);
        Assert.Equal(0, score.SoftViolationCount);
        Assert.Equal(100000, score.TotalPenalty);
        Assert.False(score.IsFeasible);
    }

    [Fact]
    public void Calculate_applies_large_penalty_for_understaffed_shift_violation()
    {
        var calculator = new ScheduleScoreCalculator();

        var score = calculator.Calculate(
        [
            new ConstraintViolation(
                ConstraintViolationType.ShiftUnderstaffed,
                ConstraintViolationSeverity.Hard,
                "Shift is understaffed.")
        ]);

        Assert.Equal(0, score.Value);
        Assert.Equal(1, score.HardViolationCount);
        Assert.Equal(0, score.SoftViolationCount);
        Assert.Equal(50000, score.TotalPenalty);
        Assert.False(score.IsFeasible);
    }

    [Fact]
    public void Calculate_applies_large_penalty_for_overstaffed_shift_violation()
    {
        var calculator = new ScheduleScoreCalculator();

        var score = calculator.Calculate(
        [
            new ConstraintViolation(
                ConstraintViolationType.ShiftOverstaffed,
                ConstraintViolationSeverity.Hard,
                "Shift is overstaffed.")
        ]);

        Assert.Equal(0, score.Value);
        Assert.Equal(1, score.HardViolationCount);
        Assert.Equal(0, score.SoftViolationCount);
        Assert.Equal(50000, score.TotalPenalty);
        Assert.False(score.IsFeasible);
    }

    [Fact]
    public void Calculate_applies_large_penalty_for_assignment_without_required_preference_violation()
    {
        var calculator = new ScheduleScoreCalculator();

        var score = calculator.Calculate(
        [
            new ConstraintViolation(
                ConstraintViolationType.AssignedWithoutRequiredPreference,
                ConstraintViolationSeverity.Hard,
                "Resource is assigned without required preference.")
        ]);

        Assert.Equal(0, score.Value);
        Assert.Equal(1, score.HardViolationCount);
        Assert.Equal(0, score.SoftViolationCount);
        Assert.Equal(50000, score.TotalPenalty);
        Assert.False(score.IsFeasible);
    }


    [Fact]
    public void Calculate_applies_large_penalty_for_shift_sequence_quota_exceeded_violation()
    {
        var calculator = new ScheduleScoreCalculator();

        var score = calculator.Calculate(
        [
            new ConstraintViolation(
                ConstraintViolationType.ShiftSequenceQuotaExceeded,
                ConstraintViolationSeverity.Hard,
                "Resource exceeded the monthly shift sequence quota.")
        ]);

        Assert.Equal(0, score.Value);
        Assert.Equal(1, score.HardViolationCount);
        Assert.Equal(0, score.SoftViolationCount);
        Assert.Equal(30000, score.TotalPenalty);
        Assert.False(score.IsFeasible);
    }

    [Theory]
    [InlineData(ConstraintViolationType.UnbalancedAssignments, 200, 800)]
    [InlineData(ConstraintViolationType.BudgetExceeded, 300, 700)]
    [InlineData(ConstraintViolationType.IgnoredAvoidPreference, 300, 700)]
    public void Calculate_applies_smaller_penalty_for_soft_violation(
        ConstraintViolationType violationType,
        int expectedPenalty,
        int expectedScoreValue)
    {
        var calculator = new ScheduleScoreCalculator();

        var score = calculator.Calculate(
        [
            new ConstraintViolation(
                violationType,
                ConstraintViolationSeverity.Soft,
                "Soft scheduling issue.")
        ]);

        Assert.Equal(expectedScoreValue, score.Value);
        Assert.Equal(0, score.HardViolationCount);
        Assert.Equal(1, score.SoftViolationCount);
        Assert.Equal(expectedPenalty, score.TotalPenalty);
        Assert.True(score.IsFeasible);
    }

    [Fact]
    public void Calculate_keeps_score_value_at_zero_when_total_penalty_exceeds_max_score()
    {
        var calculator = new ScheduleScoreCalculator();

        var score = calculator.Calculate(
        [
            new ConstraintViolation(
                ConstraintViolationType.ResourceUnavailable,
                ConstraintViolationSeverity.Hard,
                "Resource is unavailable.")
        ]);

        Assert.Equal(0, score.Value);
        Assert.Equal(100000, score.TotalPenalty);
    }

    [Fact]
    public void Calculate_preserves_total_penalty_even_when_score_value_is_zero()
    {
        var calculator = new ScheduleScoreCalculator();

        var score = calculator.Calculate(
        [
            new ConstraintViolation(
                ConstraintViolationType.ResourceUnavailable,
                ConstraintViolationSeverity.Hard,
                "Resource is unavailable."),
            new ConstraintViolation(
                ConstraintViolationType.ShiftUnderstaffed,
                ConstraintViolationSeverity.Hard,
                "Shift is understaffed.")
        ]);

        Assert.Equal(0, score.Value);
        Assert.Equal(2, score.HardViolationCount);
        Assert.Equal(0, score.SoftViolationCount);
        Assert.Equal(150000, score.TotalPenalty);
        Assert.False(score.IsFeasible);
    }

    [Fact]
    public void Calculate_marks_score_as_not_feasible_when_hard_violations_exist()
    {
        var calculator = new ScheduleScoreCalculator();

        var score = calculator.Calculate(
        [
            new ConstraintViolation(
                ConstraintViolationType.ShiftUnderstaffed,
                ConstraintViolationSeverity.Hard,
                "Shift is understaffed."),
            new ConstraintViolation(
                ConstraintViolationType.BudgetExceeded,
                ConstraintViolationSeverity.Soft,
                "Budget is exceeded.")
        ]);

        Assert.Equal(0, score.Value);
        Assert.Equal(1, score.HardViolationCount);
        Assert.Equal(1, score.SoftViolationCount);
        Assert.Equal(50300, score.TotalPenalty);
        Assert.False(score.IsFeasible);
    }

    [Fact]
    public void Calculate_rejects_null_violations()
    {
        var calculator = new ScheduleScoreCalculator();

        var exception = Assert.Throws<ArgumentNullException>(() =>
            calculator.Calculate(null!));

        Assert.Equal("violations", exception.ParamName);
    }
}
