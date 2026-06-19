using Aether.Application.Scheduling.Contracts;
using Aether.Application.Scheduling.Interfaces;
using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.Optimization;

public sealed class GeneticScheduleOptimizer : IScheduleOptimizer
{
    private const int DefaultPopulationSize = 100;
    private const int DefaultGenerationCount = 100;
    private const int DefaultEliteCount = 1;
    private const int DefaultTournamentSize = 3;

    private readonly int _populationSize;
    private readonly int _generationCount;
    private readonly int _eliteCount;
    private readonly Random _random;
    private readonly ScheduleEvaluator _evaluator;
    private readonly ScheduleEvaluationResultRanker _ranker = new();
    private readonly ScheduleMutationOperator _mutationOperator;
    private readonly ScheduleCrossoverOperator _crossoverOperator;
    private readonly CleanScheduleMutationOperator _cleanMutationOperator;
    private readonly SchedulePopulationSelector _selector;
    private readonly GeneticEvolutionMode _evolutionMode;
    private readonly IGeneticOptimizerDiagnosticsSink? _diagnosticsSink;

    public GeneticScheduleOptimizer(
        int populationSize = DefaultPopulationSize,
        int? seed = null,
        int generationCount = DefaultGenerationCount,
        int eliteCount = DefaultEliteCount,
        int tournamentSize = DefaultTournamentSize,
        IGeneticOptimizerDiagnosticsSink? diagnosticsSink = null,
        GeneticEvolutionMode evolutionMode = GeneticEvolutionMode.RepairAssisted,
        ScheduleScoringWeights? scoringWeights = null)
    {
        if (populationSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(populationSize),
                "Population size must be greater than zero.");
        }

        if (generationCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(generationCount),
                "Generation count cannot be negative.");
        }

        if (eliteCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(eliteCount),
                "Elite count cannot be negative.");
        }

        if (tournamentSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tournamentSize),
                "Tournament size must be greater than zero.");
        }

        if (!Enum.IsDefined(typeof(GeneticEvolutionMode), evolutionMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(evolutionMode),
                "Genetic evolution mode is not supported.");
        }

        _populationSize = populationSize;
        _generationCount = generationCount;
        _eliteCount = eliteCount;
        _random = seed.HasValue
            ? new Random(seed.Value)
            : new Random();

        _evaluator = new ScheduleEvaluator(
            scoringWeights ?? ScheduleScoringWeights.CreateDefault());

        _mutationOperator = new ScheduleMutationOperator(seed);
        _crossoverOperator = new ScheduleCrossoverOperator(seed);
        _cleanMutationOperator = new CleanScheduleMutationOperator(seed);
        _selector = new SchedulePopulationSelector(
            tournamentSize: tournamentSize,
            seed: seed);

        _evolutionMode = evolutionMode;
        _diagnosticsSink = diagnosticsSink;
    }

    public ScheduleOptimizationResult Optimize(SchedulingProblem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);

        var population = CreateInitialPopulation(problem);
        var bestResult = GetBest(population);

        ReportGeneration(
            generationIndex: 0,
            population: population,
            bestSoFar: bestResult);

        for (var generation = 0; generation < _generationCount; generation++)
        {
            population = CreateNextGeneration(problem, population);

            var generationBest = GetBest(population);

            if (_ranker.IsBetterThan(generationBest.Evaluation, bestResult.Evaluation))
            {
                bestResult = generationBest;
            }

            ReportGeneration(
                generationIndex: generation + 1,
                population: population,
                bestSoFar: bestResult);
        }

        return bestResult;
    }

    private IReadOnlyList<ScheduleOptimizationResult> CreateInitialPopulation(
        SchedulingProblem problem)
    {
        var population = new List<ScheduleOptimizationResult>(_populationSize);

        for (var i = 0; i < _populationSize; i++)
        {
            var candidate = CreateRandomCandidate(problem);
            population.Add(EvaluateCandidate(problem, candidate));
        }

        return population;
    }

    private IReadOnlyList<ScheduleOptimizationResult> CreateNextGeneration(
        SchedulingProblem problem,
        IReadOnlyCollection<ScheduleOptimizationResult> currentPopulation)
    {
        var nextGeneration = new List<ScheduleOptimizationResult>(_populationSize);

        var elites = _selector.SelectElites(currentPopulation, _eliteCount);

        foreach (var elite in elites.Take(_populationSize))
        {
            nextGeneration.Add(elite);
        }

        while (nextGeneration.Count < _populationSize)
        {
            var childCandidate = CreateChildCandidate(
                problem,
                currentPopulation);

            var childResult = EvaluateCandidate(problem, childCandidate);

            nextGeneration.Add(childResult);
        }

        return nextGeneration;
    }

    private ScheduleCandidate CreateChildCandidate(
        SchedulingProblem problem,
        IReadOnlyCollection<ScheduleOptimizationResult> currentPopulation)
    {
        return _evolutionMode switch
        {
            GeneticEvolutionMode.RepairAssisted => CreateRepairAssistedChildCandidate(
                problem,
                currentPopulation),

            GeneticEvolutionMode.Clean => CreateCleanChildCandidate(
                problem,
                currentPopulation),

            _ => throw new InvalidOperationException(
                "Unsupported genetic evolution mode.")
        };
    }

    private ScheduleCandidate CreateRepairAssistedChildCandidate(
        SchedulingProblem problem,
        IReadOnlyCollection<ScheduleOptimizationResult> currentPopulation)
    {
        var parent = _selector.SelectTournamentParent(currentPopulation);

        return _mutationOperator.Mutate(
            problem,
            parent.Candidate,
            parent.Evaluation);
    }

    private ScheduleCandidate CreateCleanChildCandidate(
        SchedulingProblem problem,
        IReadOnlyCollection<ScheduleOptimizationResult> currentPopulation)
    {
        var firstParent = _selector.SelectTournamentParent(currentPopulation);
        var secondParent = _selector.SelectTournamentParent(currentPopulation);

        var crossoverCandidate = _crossoverOperator.Crossover(
            problem,
            firstParent.Candidate,
            secondParent.Candidate);

        return _cleanMutationOperator.Mutate(
            problem,
            crossoverCandidate);
    }

    private ScheduleOptimizationResult EvaluateCandidate(
        SchedulingProblem problem,
        ScheduleCandidate candidate)
    {
        var evaluation = _evaluator.Evaluate(problem, candidate);

        return new ScheduleOptimizationResult(candidate, evaluation);
    }

    private ScheduleOptimizationResult GetBest(
        IReadOnlyCollection<ScheduleOptimizationResult> population)
    {
        return _selector.SelectElites(population, eliteCount: 1)[0];
    }

    private void ReportGeneration(
        int generationIndex,
        IReadOnlyCollection<ScheduleOptimizationResult> population,
        ScheduleOptimizationResult bestSoFar)
    {
        if (_diagnosticsSink is null)
        {
            return;
        }

        var generationBest = GetBest(population);
        var generationBestScore = generationBest.Evaluation.Score;
        var bestSoFarScore = bestSoFar.Evaluation.Score;

        var diagnostic = new GeneticGenerationDiagnostic(
            GenerationIndex: generationIndex,
            PopulationSize: population.Count,
            FeasibleCandidateCount: population.Count(result => result.Evaluation.IsFeasible),
            BestScoreValue: generationBestScore.Value,
            BestTotalPenalty: generationBestScore.TotalPenalty,
            BestHardViolationCount: generationBestScore.HardViolationCount,
            BestSoftViolationCount: generationBestScore.SoftViolationCount,
            BestSoFarScoreValue: bestSoFarScore.Value,
            BestSoFarTotalPenalty: bestSoFarScore.TotalPenalty,
            BestSoFarHardViolationCount: bestSoFarScore.HardViolationCount,
            BestSoFarSoftViolationCount: bestSoFarScore.SoftViolationCount);

        _diagnosticsSink.ReportGeneration(diagnostic);
    }

    private ScheduleCandidate CreateRandomCandidate(SchedulingProblem problem)
    {
        var assignments = new List<Assignment>();

        var shiftsById = problem.Shifts
            .ToDictionary(shift => shift.Id);

        foreach (var shift in problem.Shifts.OrderBy(shift => shift.StartUtc))
        {
            var targetAssignmentCount = GetTargetAssignmentCount(problem, shift);

            if (targetAssignmentCount <= 0)
            {
                continue;
            }

            var assignableResources = problem.Resources
                .Where(resource => CanAssignResource(
                    problem,
                    resource,
                    shift,
                    assignments,
                    shiftsById))
                .ToList();

            Shuffle(assignableResources);

            foreach (var resource in assignableResources.Take(targetAssignmentCount))
            {
                assignments.Add(new Assignment(resource.Id, shift.Id));
            }
        }

        return new ScheduleCandidate(assignments);
    }

    private int GetTargetAssignmentCount(
        SchedulingProblem problem,
        Shift shift)
    {
        var effectiveMinResourceCount = GetEffectiveMinResourceCount(problem, shift);

        if (effectiveMinResourceCount >= shift.MaxResourceCount)
        {
            return shift.MaxResourceCount;
        }

        return _random.Next(
            effectiveMinResourceCount,
            shift.MaxResourceCount + 1);
    }

    private static int GetEffectiveMinResourceCount(
        SchedulingProblem problem,
        Shift shift)
    {
        var effectiveMinResourceCount = shift.MinResourceCount;
        var hasPreferDemand = HasAnyPreferPreferenceForShift(problem, shift);

        if (hasPreferDemand &&
            (shift.RequiresMinimumWhenPreferenceExists ||
             ShouldSeedOptionalPreferDemand(shift)))
        {
            effectiveMinResourceCount = Math.Max(effectiveMinResourceCount, 1);
        }

        return Math.Min(effectiveMinResourceCount, shift.MaxResourceCount);
    }

    private static bool ShouldSeedOptionalPreferDemand(Shift shift)
    {
        return shift.MinResourceCount == 0 &&
               shift.MaxResourceCount > 0 &&
               shift.RequiresPreferenceToAssign;
    }

    private static bool CanAssignResource(
        SchedulingProblem problem,
        Resource resource,
        Shift shift,
        IReadOnlyCollection<Assignment> existingAssignments,
        IReadOnlyDictionary<Guid, Shift> shiftsById)
    {
        if (!IsAvailableForShift(problem, resource, shift))
        {
            return false;
        }

        if (shift.RequiresPreferenceToAssign &&
            !HasPreferPreferenceForShift(problem, resource, shift))
        {
            return false;
        }

        if (HasOverlappingAssignment(resource, shift, existingAssignments, shiftsById))
        {
            return false;
        }

        return true;
    }

    private static bool IsAvailableForShift(
        SchedulingProblem problem,
        Resource resource,
        Shift shift)
    {
        return problem.AvailabilityWindows.Any(window =>
            window.ResourceId == resource.Id &&
            window.Covers(shift));
    }

    private static bool HasPreferPreferenceForShift(
        SchedulingProblem problem,
        Resource resource,
        Shift shift)
    {
        return problem.ResourcePreferences.Any(preference =>
            preference.ResourceId == resource.Id &&
            preference.Type == ResourcePreferenceType.Prefer &&
            Overlaps(preference.StartUtc, preference.EndUtc, shift.StartUtc, shift.EndUtc));
    }

    private static bool HasAnyPreferPreferenceForShift(
        SchedulingProblem problem,
        Shift shift)
    {
        return problem.ResourcePreferences.Any(preference =>
            preference.Type == ResourcePreferenceType.Prefer &&
            Overlaps(preference.StartUtc, preference.EndUtc, shift.StartUtc, shift.EndUtc));
    }

    private static bool HasOverlappingAssignment(
        Resource resource,
        Shift candidateShift,
        IReadOnlyCollection<Assignment> existingAssignments,
        IReadOnlyDictionary<Guid, Shift> shiftsById)
    {
        return existingAssignments
            .Where(assignment => assignment.ResourceId == resource.Id)
            .Select(assignment => shiftsById[assignment.ShiftId])
            .Any(existingShift => Overlaps(
                existingShift.StartUtc,
                existingShift.EndUtc,
                candidateShift.StartUtc,
                candidateShift.EndUtc));
    }

    private static bool Overlaps(
        DateTime firstStartUtc,
        DateTime firstEndUtc,
        DateTime secondStartUtc,
        DateTime secondEndUtc)
    {
        return firstStartUtc < secondEndUtc &&
               secondStartUtc < firstEndUtc;
    }

    private void Shuffle<T>(IList<T> items)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var randomIndex = _random.Next(i + 1);

            (items[i], items[randomIndex]) = (items[randomIndex], items[i]);
        }
    }
}
