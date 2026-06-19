using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ScheduleScoringWeightsTests
{
    [Fact]
    public void CreateDefault_exposes_current_scoring_weights()
    {
        var weights = ScheduleScoringWeights.CreateDefault();

        Assert.Equal(100000, weights.ResourceUnavailablePenalty);
        Assert.Equal(100000, weights.ResourceAssignedToOverlappingShiftsPenalty);
        Assert.Equal(50000, weights.ShiftUnderstaffedPenalty);
        Assert.Equal(50000, weights.ShiftOverstaffedPenalty);
        Assert.Equal(50000, weights.AssignedWithoutRequiredPreferencePenalty);
        Assert.Equal(30000, weights.ShiftSequenceQuotaExceededPenalty);
        Assert.Equal(30000, weights.ResourceMonthlyNightShiftQuotaExceededPenalty);
        Assert.Equal(500, weights.ResourceMonthlyNightShiftPreferenceNotSatisfiedPenalty);
        Assert.Equal(50000, weights.ResourceMinimumAssignedHoursNotMetPenalty);
        Assert.Equal(1000, weights.ResourceWeeklyMinimumShiftMixNotMetPenalty);
        Assert.Equal(400, weights.ResourceAssignedHoursBalanceExceededPenalty);
        Assert.Equal(100, weights.ResourceAssignedHoursBalanceExceededPenaltyPerExcessHour);
        Assert.Equal(10, weights.ResourceEffectiveTargetAssignedHoursBelowTargetPenaltyPerHour);
        Assert.Equal(20, weights.ResourceEffectiveTargetAssignedHoursAboveTargetPenaltyPerHour);
        Assert.Equal(15, weights.ResourceRequestedPreferredHoursNotSatisfiedPenaltyPerHour);
        Assert.Equal(200, weights.UnbalancedAssignmentsPenalty);
        Assert.Equal(300, weights.BudgetExceededPenalty);
        Assert.Equal(300, weights.IgnoredAvoidPreferencePenalty);
    }

    [Fact]
    public void Validate_rejects_negative_weight()
    {
        var weights = ScheduleScoringWeights.CreateDefault() with
        {
            IgnoredAvoidPreferencePenalty = -1
        };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(weights.Validate);

        Assert.Equal(
            nameof(ScheduleScoringWeights.IgnoredAvoidPreferencePenalty),
            exception.ParamName);
    }

    [Fact]
    public void Validate_rejects_negative_balance_excess_hour_penalty()
    {
        var weights = ScheduleScoringWeights.CreateDefault() with
        {
            ResourceAssignedHoursBalanceExceededPenaltyPerExcessHour = -1
        };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            weights.Validate);

        Assert.Equal(
            nameof(ScheduleScoringWeights.ResourceAssignedHoursBalanceExceededPenaltyPerExcessHour),
            exception.ParamName);
    }

}
