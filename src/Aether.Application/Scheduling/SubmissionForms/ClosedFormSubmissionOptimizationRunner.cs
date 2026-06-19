using Aether.Application.Scheduling.Contracts;
using Aether.Application.Scheduling.Optimization;
using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.SubmissionForms;

public sealed class ClosedFormSubmissionOptimizationRunner
{
    private const int AcceptedPopulationSize = 120;
    private const int AcceptedGenerationCount = 100;
    private const int AcceptedEliteCount = 1;
    private const int AcceptedTournamentSize = 3;

    public ClosedFormSubmissionOptimizationResult Run(
        ClosedFormSubmissionOptimizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var buildResult = new FormSubmissionSchedulingProblemBuilder()
            .Build(new FormSubmissionSchedulingProblemBuildRequest(
                request.Period,
                request.Resources,
                request.Shifts,
                request.WorkerSubmissions,
                TotalEffectiveTargetHours: request.TotalEffectiveTargetHours,
                ResourceMonthlyNightShiftHistories: request.ResourceMonthlyNightShiftHistories,
                MaximumAssignedHoursDeviationFromAverageHours: request.MaximumAssignedHoursDeviationFromAverageHours,
                ApplyMandatoryShiftAvailabilityPolicy: true,
                ManagerConstraintSet: request.ManagerConstraintSet));

        var diagnosticsSink = new CollectingGeneticOptimizerDiagnosticsSink();
        var scoringWeights = ScheduleScoringWeights.CreateDefault();

        var optimizer = new GeneticScheduleOptimizer(
            populationSize: AcceptedPopulationSize,
            seed: request.Seed,
            generationCount: AcceptedGenerationCount,
            eliteCount: AcceptedEliteCount,
            tournamentSize: AcceptedTournamentSize,
            diagnosticsSink: diagnosticsSink,
            evolutionMode: GeneticEvolutionMode.Clean,
            scoringWeights: scoringWeights);

        var optimizationResult = optimizer.Optimize(buildResult.Problem);

        PostRunLocalAddImprovementResult? postRunLocalAddImprovementResult = null;

        if (request.ApplyPostRunLocalAddImprovement)
        {
            postRunLocalAddImprovementResult = new PostRunLocalAddImprovementOptimizer()
                .Improve(
                    buildResult.Problem,
                    optimizationResult.Candidate,
                    optimizationResult.Evaluation,
                    scoringWeights);

            optimizationResult = new ScheduleOptimizationResult(
                postRunLocalAddImprovementResult.Candidate,
                postRunLocalAddImprovementResult.Evaluation);
        }

        var geneticResult = CreateOptimizationResult(
            buildResult.Problem,
            optimizationResult,
            diagnosticsSink.Diagnostics);

        return new ClosedFormSubmissionOptimizationResult(
            buildResult.Problem,
            buildResult.Warnings,
            geneticResult,
            postRunLocalAddImprovementResult);
    }

    private static SchedulingRunOptimizationResult CreateOptimizationResult(
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
}
