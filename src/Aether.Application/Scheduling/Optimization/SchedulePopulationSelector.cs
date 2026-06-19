using Aether.Application.Scheduling.Contracts;
using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.Optimization;

public sealed class SchedulePopulationSelector
{
    private readonly int _tournamentSize;
    private readonly Random _random;
    private readonly ScheduleEvaluationResultRanker _ranker = new();

    public SchedulePopulationSelector(
        int tournamentSize,
        int? seed = null)
    {
        if (tournamentSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tournamentSize),
                "Tournament size must be greater than zero.");
        }

        _tournamentSize = tournamentSize;
        _random = seed.HasValue
            ? new Random(seed.Value)
            : new Random();
    }

    public IReadOnlyList<ScheduleOptimizationResult> SelectElites(
        IReadOnlyCollection<ScheduleOptimizationResult> population,
        int eliteCount)
    {
        ArgumentNullException.ThrowIfNull(population);

        if (eliteCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(eliteCount),
                "Elite count cannot be negative.");
        }

        if (eliteCount == 0 || population.Count == 0)
        {
            return [];
        }

        var orderedPopulation = OrderByRanking(population);

        return orderedPopulation
            .Take(Math.Min(eliteCount, orderedPopulation.Count))
            .ToList();
    }

    public ScheduleOptimizationResult SelectTournamentParent(
        IReadOnlyCollection<ScheduleOptimizationResult> population)
    {
        ArgumentNullException.ThrowIfNull(population);

        if (population.Count == 0)
        {
            throw new ArgumentException(
                "Population cannot be empty.",
                nameof(population));
        }

        var populationList = population.ToList();
        var tournamentCount = Math.Min(_tournamentSize, populationList.Count);

        var tournament = new List<ScheduleOptimizationResult>(
            capacity: tournamentCount);

        var selectedIndexes = new HashSet<int>();

        while (tournament.Count < tournamentCount)
        {
            var index = _random.Next(populationList.Count);

            if (!selectedIndexes.Add(index))
            {
                continue;
            }

            tournament.Add(populationList[index]);
        }

        return OrderByRanking(tournament)[0];
    }

    private List<ScheduleOptimizationResult> OrderByRanking(
        IEnumerable<ScheduleOptimizationResult> population)
    {
        var orderedPopulation = population.ToList();

        orderedPopulation.Sort((left, right) =>
        {
            if (_ranker.IsBetterThan(left.Evaluation, right.Evaluation))
            {
                return -1;
            }

            if (_ranker.IsBetterThan(right.Evaluation, left.Evaluation))
            {
                return 1;
            }

            return 0;
        });

        return orderedPopulation;
    }
}
