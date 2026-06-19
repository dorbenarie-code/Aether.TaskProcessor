using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ScheduleScoringWeightsIgnoredAvoidBurdenTests
{
    [Fact]
    public void CreateDefault_sets_ignored_avoid_burden_penalty_to_candidate_50()
    {
        var weights = ScheduleScoringWeights.CreateDefault();

        Assert.Equal(50, weights.ResourceIgnoredAvoidPreferenceBurdenPenaltyPerHour);
    }

    [Fact]
    public void Validate_rejects_negative_ignored_avoid_burden_penalty()
    {
        var weights = ScheduleScoringWeights.CreateDefault() with
        {
            ResourceIgnoredAvoidPreferenceBurdenPenaltyPerHour = -1
        };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => weights.Validate());

        Assert.Equal(
            nameof(ScheduleScoringWeights.ResourceIgnoredAvoidPreferenceBurdenPenaltyPerHour),
            exception.ParamName);
    }
}
