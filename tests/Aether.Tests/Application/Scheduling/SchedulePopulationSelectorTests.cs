using Aether.Application.Scheduling.Contracts;
using Aether.Application.Scheduling.Optimization;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class SchedulePopulationSelectorTests
{
    [Fact]
    public void Constructor_ShouldThrow_WhenTournamentSizeIsZero()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            new SchedulePopulationSelector(tournamentSize: 0);
        });

        Assert.Equal("tournamentSize", exception.ParamName);
    }

    [Fact]
    public void SelectElites_ShouldThrow_WhenPopulationIsNull()
    {
        var selector = new SchedulePopulationSelector(
            tournamentSize: 2,
            seed: 1);

        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            selector.SelectElites(null!, eliteCount: 1);
        });

        Assert.Equal("population", exception.ParamName);
    }

    [Fact]
    public void SelectElites_ShouldThrow_WhenEliteCountIsNegative()
    {
        var selector = new SchedulePopulationSelector(
            tournamentSize: 2,
            seed: 1);

        var population = new[]
        {
            CreateResult(scoreValue: 1000, hardViolations: 0, totalPenalty: 0)
        };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            selector.SelectElites(population, eliteCount: -1);
        });

        Assert.Equal("eliteCount", exception.ParamName);
    }

    [Fact]
    public void SelectElites_ShouldReturnEmpty_WhenEliteCountIsZero()
    {
        var selector = new SchedulePopulationSelector(
            tournamentSize: 2,
            seed: 1);

        var population = new[]
        {
            CreateResult(scoreValue: 1000, hardViolations: 0, totalPenalty: 0)
        };

        var elites = selector.SelectElites(population, eliteCount: 0);

        Assert.Empty(elites);
    }

    [Fact]
    public void SelectElites_ShouldReturnBestResults_ByRankingPolicy()
    {
        var infeasibleLowPenalty = CreateResult(
            scoreValue: 900,
            hardViolations: 1,
            totalPenalty: 100);

        var feasibleHighPenalty = CreateResult(
            scoreValue: 700,
            hardViolations: 0,
            totalPenalty: 300);

        var feasibleBest = CreateResult(
            scoreValue: 950,
            hardViolations: 0,
            totalPenalty: 50);

        var population = new[]
        {
            infeasibleLowPenalty,
            feasibleHighPenalty,
            feasibleBest
        };

        var selector = new SchedulePopulationSelector(
            tournamentSize: 2,
            seed: 1);

        var elites = selector.SelectElites(population, eliteCount: 2);

        Assert.Equal(2, elites.Count);
        Assert.Same(feasibleBest, elites[0]);
        Assert.Same(feasibleHighPenalty, elites[1]);
    }

    [Fact]
    public void SelectElites_ShouldPreserveEliteInstances_WithoutCloning()
    {
        var best = CreateResult(
            scoreValue: 1000,
            hardViolations: 0,
            totalPenalty: 0);

        var worse = CreateResult(
            scoreValue: 700,
            hardViolations: 0,
            totalPenalty: 300);

        var selector = new SchedulePopulationSelector(
            tournamentSize: 2,
            seed: 1);

        var elites = selector.SelectElites(
            [worse, best],
            eliteCount: 1);

        var elite = Assert.Single(elites);

        Assert.Same(best, elite);
        Assert.Same(best.Candidate, elite.Candidate);
        Assert.Same(best.Evaluation, elite.Evaluation);
    }

    [Fact]
    public void SelectElites_ShouldClampToPopulationSize_WhenEliteCountExceedsPopulationCount()
    {
        var best = CreateResult(
            scoreValue: 1000,
            hardViolations: 0,
            totalPenalty: 0);

        var worse = CreateResult(
            scoreValue: 800,
            hardViolations: 0,
            totalPenalty: 200);

        var selector = new SchedulePopulationSelector(
            tournamentSize: 2,
            seed: 1);

        var elites = selector.SelectElites(
            [worse, best],
            eliteCount: 10);

        Assert.Equal(2, elites.Count);
        Assert.Same(best, elites[0]);
        Assert.Same(worse, elites[1]);
    }

    [Fact]
    public void SelectTournamentParent_ShouldThrow_WhenPopulationIsNull()
    {
        var selector = new SchedulePopulationSelector(
            tournamentSize: 2,
            seed: 1);

        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            selector.SelectTournamentParent(null!);
        });

        Assert.Equal("population", exception.ParamName);
    }

    [Fact]
    public void SelectTournamentParent_ShouldThrow_WhenPopulationIsEmpty()
    {
        var selector = new SchedulePopulationSelector(
            tournamentSize: 2,
            seed: 1);

        var exception = Assert.Throws<ArgumentException>(() =>
        {
            selector.SelectTournamentParent([]);
        });

        Assert.Equal("population", exception.ParamName);
    }

    [Fact]
    public void SelectTournamentParent_ShouldSelectBestCandidateFromTournament_ByRankingPolicy()
    {
        var infeasibleLowPenalty = CreateResult(
            scoreValue: 900,
            hardViolations: 1,
            totalPenalty: 100);

        var feasibleHighPenalty = CreateResult(
            scoreValue: 700,
            hardViolations: 0,
            totalPenalty: 300);

        var feasibleBest = CreateResult(
            scoreValue: 950,
            hardViolations: 0,
            totalPenalty: 50);

        var population = new[]
        {
            infeasibleLowPenalty,
            feasibleHighPenalty,
            feasibleBest
        };

        var selector = new SchedulePopulationSelector(
            tournamentSize: 3,
            seed: 1);

        var selected = selector.SelectTournamentParent(population);

        Assert.Same(feasibleBest, selected);
    }

    [Fact]
    public void SelectTournamentParent_ShouldBeDeterministic_WhenSeedIsFixed()
    {
        var population = new[]
        {
            CreateResult(scoreValue: 1000, hardViolations: 0, totalPenalty: 0),
            CreateResult(scoreValue: 900, hardViolations: 0, totalPenalty: 100),
            CreateResult(scoreValue: 800, hardViolations: 0, totalPenalty: 200),
            CreateResult(scoreValue: 700, hardViolations: 0, totalPenalty: 300)
        };

        var firstSelector = new SchedulePopulationSelector(
            tournamentSize: 2,
            seed: 42);

        var secondSelector = new SchedulePopulationSelector(
            tournamentSize: 2,
            seed: 42);

        var firstSelected = firstSelector.SelectTournamentParent(population);
        var secondSelected = secondSelector.SelectTournamentParent(population);

        Assert.Same(firstSelected, secondSelected);
    }

    [Fact]
    public void SelectTournamentParent_ShouldPreferLowerDamage_WhenAllScoresAreZero()
    {
        var nearFeasible = CreateResult(
            scoreValue: 0,
            hardViolations: 1,
            totalPenalty: 50_000);

        var catastrophic = CreateResult(
            scoreValue: 0,
            hardViolations: 20,
            totalPenalty: 1_500_000);

        var selector = new SchedulePopulationSelector(
            tournamentSize: 2,
            seed: 1);

        var selected = selector.SelectTournamentParent(
            [catastrophic, nearFeasible]);

        Assert.Same(nearFeasible, selected);
    }

    [Fact]
    public void SelectElites_ShouldPreferLowerTotalPenalty_WhenAllScoresAreZeroAndHardCountsEqual()
    {
        var better = CreateResult(
            scoreValue: 0,
            hardViolations: 2,
            totalPenalty: 100_000);

        var worse = CreateResult(
            scoreValue: 0,
            hardViolations: 2,
            totalPenalty: 300_000);

        var selector = new SchedulePopulationSelector(
            tournamentSize: 2,
            seed: 1);

        var elites = selector.SelectElites(
            [worse, better],
            eliteCount: 1);

        Assert.Same(better, elites[0]);
    }

    private static ScheduleOptimizationResult CreateResult(
        int scoreValue,
        int hardViolations,
        int totalPenalty)
    {
        var candidate = new ScheduleCandidate([]);

        var score = new ScheduleScore(
            value: scoreValue,
            hardViolationCount: hardViolations,
            softViolationCount: 0,
            totalPenalty: totalPenalty);

        var evaluation = new ScheduleEvaluationResult(
            score,
            violations: []);

        return new ScheduleOptimizationResult(
            candidate,
            evaluation);
    }
}
