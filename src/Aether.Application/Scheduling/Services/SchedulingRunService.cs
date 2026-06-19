using Aether.Application.Scheduling.Contracts;
using Aether.Application.Scheduling.Interfaces;
using Aether.Application.Scheduling.Optimization;
using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.Services;

public sealed class SchedulingRunService : ISchedulingRunService
{
    private readonly ISchedulingProblemBuilder _problemBuilder;
    private readonly IScheduleOptimizer _deterministicOptimizer;
    private readonly Func<IGeneticOptimizerDiagnosticsSink, IScheduleOptimizer> _geneticOptimizerFactory;
    private readonly ScheduleEvaluationResultRanker _ranker = new();

    public SchedulingRunService(
        ISchedulingProblemBuilder problemBuilder,
        IScheduleOptimizer deterministicOptimizer,
        Func<IGeneticOptimizerDiagnosticsSink, IScheduleOptimizer> geneticOptimizerFactory)
    {
        _problemBuilder = problemBuilder ?? throw new ArgumentNullException(nameof(problemBuilder));
        _deterministicOptimizer = deterministicOptimizer ?? throw new ArgumentNullException(nameof(deterministicOptimizer));
        _geneticOptimizerFactory = geneticOptimizerFactory ?? throw new ArgumentNullException(nameof(geneticOptimizerFactory));
    }

    public SchedulingRunResult Run(SchedulingProblemBuildRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var buildResult = _problemBuilder.Build(request);

        var deterministicOptimizationResult = _deterministicOptimizer.Optimize(
            buildResult.Problem);

        var diagnosticsSink = new CollectingGeneticOptimizerDiagnosticsSink();

        var geneticOptimizer = _geneticOptimizerFactory(diagnosticsSink)
            ?? throw new InvalidOperationException("Genetic optimizer factory returned null.");

        var geneticOptimizationResult = geneticOptimizer.Optimize(
            buildResult.Problem);

        var deterministicResult = CreateOptimizationResult(
            buildResult.Problem,
            deterministicOptimizationResult,
            []);

        var geneticResult = CreateOptimizationResult(
            buildResult.Problem,
            geneticOptimizationResult,
            diagnosticsSink.Diagnostics);

        var comparison = CreateComparison(
            deterministicResult,
            geneticResult);

        return new SchedulingRunResult(
            buildResult.Problem,
            buildResult.Warnings,
            deterministicResult,
            geneticResult,
            comparison);
    }

    private SchedulingRunOptimizationResult CreateOptimizationResult(
        SchedulingProblem problem,
        ScheduleOptimizationResult optimizationResult,
        IReadOnlyCollection<GeneticGenerationDiagnostic> generationDiagnostics)
    {
        return new SchedulingRunOptimizationResult(
            optimizationResult.Candidate,
            optimizationResult.Evaluation,
            CreateLoadByResource(problem, optimizationResult.Candidate),
            CreateViolationsByType(optimizationResult.Evaluation),
            generationDiagnostics.ToArray());
    }

    private static IReadOnlyCollection<ResourceLoadSummary> CreateLoadByResource(
        SchedulingProblem problem,
        ScheduleCandidate candidate)
    {
        var shiftsById = problem.Shifts.ToDictionary(shift => shift.Id);

        var hoursByResourceId = candidate.Assignments
            .GroupBy(assignment => assignment.ResourceId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(assignment =>
                {
                    var shift = shiftsById[assignment.ShiftId];

                    return (shift.EndUtc - shift.StartUtc).TotalHours;
                }));

        var assignmentCountByResourceId = candidate.Assignments
            .GroupBy(assignment => assignment.ResourceId)
            .ToDictionary(
                group => group.Key,
                group => group.Count());

        return problem.Resources
            .Select(resource => new ResourceLoadSummary(
                resource.Id,
                resource.Name,
                hoursByResourceId.GetValueOrDefault(resource.Id, 0),
                assignmentCountByResourceId.GetValueOrDefault(resource.Id, 0)))
            .ToArray();
    }

    private static IReadOnlyDictionary<ConstraintViolationType, int> CreateViolationsByType(
        ScheduleEvaluationResult evaluation)
    {
        return evaluation.Violations
            .GroupBy(violation => violation.Type)
            .ToDictionary(
                group => group.Key,
                group => group.Count());
    }

    private SchedulingRunComparison CreateComparison(
        SchedulingRunOptimizationResult deterministicResult,
        SchedulingRunOptimizationResult geneticResult)
    {
        return new SchedulingRunComparison(
            GeneticRankedBetter: _ranker.IsBetterThan(
                geneticResult.Evaluation,
                deterministicResult.Evaluation),
            DeterministicHardViolationCount: deterministicResult.Evaluation.Score.HardViolationCount,
            GeneticHardViolationCount: geneticResult.Evaluation.Score.HardViolationCount,
            DeterministicTotalPenalty: deterministicResult.Evaluation.Score.TotalPenalty,
            GeneticTotalPenalty: geneticResult.Evaluation.Score.TotalPenalty,
            DeterministicIgnoredAvoidPreferenceViolations: CountViolations(
                deterministicResult,
                ConstraintViolationType.IgnoredAvoidPreference),
            GeneticIgnoredAvoidPreferenceViolations: CountViolations(
                geneticResult,
                ConstraintViolationType.IgnoredAvoidPreference),
            DeterministicShiftSequenceQuotaViolations: CountViolations(
                deterministicResult,
                ConstraintViolationType.ShiftSequenceQuotaExceeded),
            GeneticShiftSequenceQuotaViolations: CountViolations(
                geneticResult,
                ConstraintViolationType.ShiftSequenceQuotaExceeded));
    }

    private static int CountViolations(
        SchedulingRunOptimizationResult result,
        ConstraintViolationType type)
    {
        return result.ViolationsByType.TryGetValue(type, out var count)
            ? count
            : 0;
    }
}
