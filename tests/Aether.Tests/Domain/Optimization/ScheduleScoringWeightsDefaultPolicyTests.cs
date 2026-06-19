using Aether.Domain.Optimization;
using Xunit;

namespace Aether.Tests.Domain.Optimization;

public sealed class ScheduleScoringWeightsDefaultPolicyTests
{
    [Fact]
    public void CreateDefault_uses_variant_c_as_default_scoring_policy()
    {
        var weights = ScheduleScoringWeights.CreateDefault();

        Assert.Equal(
            15,
            weights.ResourceRequestedPreferredHoursNotSatisfiedPenaltyPerHour);

        Assert.Equal(
            20,
            weights.ResourceEffectiveTargetAssignedHoursAboveTargetPenaltyPerHour);

        Assert.Equal(
            10,
            weights.ResourceEffectiveTargetAssignedHoursBelowTargetPenaltyPerHour);

        Assert.Equal(
            500,
            weights.ResourceMonthlyNightShiftPreferenceNotSatisfiedPenalty);
    }
}
