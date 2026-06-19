using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ScheduleEvaluationResultRankerTests
{
    [Fact]
    public void IsBetterThan_prefers_feasible_result_over_infeasible_result()
    {
        var ranker = new ScheduleEvaluationResultRanker();

        var feasible = CreateResult(
            value: 800,
            hardViolationCount: 0,
            softViolationCount: 1,
            totalPenalty: 200);

        var infeasible = CreateResult(
            value: 900,
            hardViolationCount: 1,
            softViolationCount: 0,
            totalPenalty: 100);

        var isBetter = ranker.IsBetterThan(feasible, infeasible);

        Assert.True(isBetter);
    }

    [Fact]
    public void IsBetterThan_prefers_fewer_hard_violations()
    {
        var ranker = new ScheduleEvaluationResultRanker();

        var fewerHardViolations = CreateResult(
            value: 0,
            hardViolationCount: 1,
            softViolationCount: 0,
            totalPenalty: 200000);

        var moreHardViolations = CreateResult(
            value: 0,
            hardViolationCount: 2,
            softViolationCount: 0,
            totalPenalty: 150000);

        var isBetter = ranker.IsBetterThan(
            fewerHardViolations,
            moreHardViolations);

        Assert.True(isBetter);
    }

    [Fact]
    public void IsBetterThan_prefers_lower_total_penalty()
    {
        var ranker = new ScheduleEvaluationResultRanker();

        var lowerPenalty = CreateResult(
            value: 0,
            hardViolationCount: 1,
            softViolationCount: 0,
            totalPenalty: 50000);

        var higherPenalty = CreateResult(
            value: 0,
            hardViolationCount: 1,
            softViolationCount: 0,
            totalPenalty: 100000);

        var isBetter = ranker.IsBetterThan(lowerPenalty, higherPenalty);

        Assert.True(isBetter);
    }

    [Fact]
    public void IsBetterThan_prefers_higher_score_value_when_penalty_is_equal()
    {
        var ranker = new ScheduleEvaluationResultRanker();

        var higherScore = CreateResult(
            value: 900,
            hardViolationCount: 0,
            softViolationCount: 1,
            totalPenalty: 100);

        var lowerScore = CreateResult(
            value: 800,
            hardViolationCount: 0,
            softViolationCount: 1,
            totalPenalty: 100);

        var isBetter = ranker.IsBetterThan(higherScore, lowerScore);

        Assert.True(isBetter);
    }

    [Fact]
    public void IsBetterThan_does_not_prefer_equal_results()
    {
        var ranker = new ScheduleEvaluationResultRanker();

        var first = CreateResult(
            value: 800,
            hardViolationCount: 0,
            softViolationCount: 1,
            totalPenalty: 200);

        var second = CreateResult(
            value: 800,
            hardViolationCount: 0,
            softViolationCount: 1,
            totalPenalty: 200);

        var isBetter = ranker.IsBetterThan(first, second);

        Assert.False(isBetter);
    }

    [Fact]
    public void IsBetterThan_rejects_null_candidate()
    {
        var ranker = new ScheduleEvaluationResultRanker();

        var currentBest = CreateResult(
            value: 1000,
            hardViolationCount: 0,
            softViolationCount: 0,
            totalPenalty: 0);

        var exception = Assert.Throws<ArgumentNullException>(() =>
            ranker.IsBetterThan(null!, currentBest));

        Assert.Equal("candidate", exception.ParamName);
    }

    [Fact]
    public void IsBetterThan_rejects_null_current_best()
    {
        var ranker = new ScheduleEvaluationResultRanker();

        var candidate = CreateResult(
            value: 1000,
            hardViolationCount: 0,
            softViolationCount: 0,
            totalPenalty: 0);

        var exception = Assert.Throws<ArgumentNullException>(() =>
            ranker.IsBetterThan(candidate, null!));

        Assert.Equal("currentBest", exception.ParamName);
    }

    private static ScheduleEvaluationResult CreateResult(
        int value,
        int hardViolationCount,
        int softViolationCount,
        int totalPenalty)
    {
        var score = new ScheduleScore(
            value,
            hardViolationCount,
            softViolationCount,
            totalPenalty);

        return new ScheduleEvaluationResult(score, []);
    }
}
