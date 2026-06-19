using Aether.Application.Scheduling.Contracts;
using Aether.Application.Scheduling.Optimization;
using Aether.Application.Scheduling.Reports;
using Aether.Domain.Optimization;
using Xunit.Abstractions;

namespace Aether.Tests.Application.Scheduling;

public sealed class SchedulingRunReportFormatterOutputTests
{
    private readonly ITestOutputHelper _output;

    public SchedulingRunReportFormatterOutputTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Format_ShouldPrintFullSchedulingRunReport()
    {
        var scenario = CreateScenario();

        var deterministicViolation = new ConstraintViolation(
            ConstraintViolationType.ShiftUnderstaffed,
            ConstraintViolationSeverity.Hard,
            "Shift does not have enough assigned resources.",
            shiftId: scenario.FirstShift.Id);

        var deterministicCandidate = new ScheduleCandidate([]);

        var deterministicEvaluation = CreateEvaluation(
            scoreValue: 0,
            hardViolationCount: 1,
            softViolationCount: 0,
            totalPenalty: 50_000,
            violations: [deterministicViolation]);

        var geneticCandidate = new ScheduleCandidate(
        [
            new Assignment(scenario.Dana.Id, scenario.FirstShift.Id),
            new Assignment(scenario.Yossi.Id, scenario.SecondShift.Id)
        ]);

        var geneticEvaluation = CreateEvaluation(
            scoreValue: 1000,
            hardViolationCount: 0,
            softViolationCount: 0,
            totalPenalty: 0,
            violations: []);

        var deterministicResult = CreateRunOptimizationResult(
            scenario.Problem,
            deterministicCandidate,
            deterministicEvaluation,
            generationDiagnostics: []);

        var geneticResult = CreateRunOptimizationResult(
            scenario.Problem,
            geneticCandidate,
            geneticEvaluation,
            generationDiagnostics:
            [
                new GeneticGenerationDiagnostic(
                    GenerationIndex: 0,
                    PopulationSize: 10,
                    FeasibleCandidateCount: 2,
                    BestScoreValue: 1000,
                    BestTotalPenalty: 0,
                    BestHardViolationCount: 0,
                    BestSoftViolationCount: 0,
                    BestSoFarScoreValue: 1000,
                    BestSoFarTotalPenalty: 0,
                    BestSoFarHardViolationCount: 0,
                    BestSoFarSoftViolationCount: 0)
            ]);

        var runResult = CreateRunResult(
            scenario,
            deterministicResult,
            geneticResult);

        var formatter = new SchedulingRunReportFormatter();

        var report = formatter.Format(runResult);

        _output.WriteLine(report);
        System.Console.WriteLine(report);

        Assert.Contains("Scheduling Run Report", report);
        Assert.Contains("Input Summary", report);
        Assert.Contains("Comparison", report);
        Assert.Contains("BestResult: Genetic", report);
        Assert.Contains("Best Result", report);
        Assert.Contains("Score.Value: 1000", report);
        Assert.Contains("AssignmentsByShift", report);
        Assert.Contains("GenerationDiagnostics", report);
        Assert.Contains("GenerationBestScoreValue", report);
        Assert.Contains("BestSoFarScoreValue", report);
    }

    private static SchedulingRunResult CreateRunResult(
        Scenario scenario,
        SchedulingRunOptimizationResult deterministicResult,
        SchedulingRunOptimizationResult geneticResult)
    {
        var ranker = new ScheduleEvaluationResultRanker();

        var comparison = new SchedulingRunComparison(
            GeneticRankedBetter: ranker.IsBetterThan(
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

        return new SchedulingRunResult(
            scenario.Problem,
            Warnings: [],
            deterministicResult,
            geneticResult,
            comparison);
    }

    private static SchedulingRunOptimizationResult CreateRunOptimizationResult(
        SchedulingProblem problem,
        ScheduleCandidate candidate,
        ScheduleEvaluationResult evaluation,
        IReadOnlyCollection<GeneticGenerationDiagnostic> generationDiagnostics)
    {
        var shiftsById = problem.Shifts.ToDictionary(shift => shift.Id);

        var loadByResource = problem.Resources
            .Select(resource =>
            {
                var resourceAssignments = candidate.Assignments
                    .Where(assignment => assignment.ResourceId == resource.Id)
                    .ToArray();

                var assignedHours = resourceAssignments.Sum(assignment =>
                {
                    var shift = shiftsById[assignment.ShiftId];

                    return (shift.EndUtc - shift.StartUtc).TotalHours;
                });

                return new ResourceLoadSummary(
                    resource.Id,
                    resource.Name,
                    assignedHours,
                    resourceAssignments.Length);
            })
            .ToArray();

        var violationsByType = evaluation.Violations
            .GroupBy(violation => violation.Type)
            .ToDictionary(
                group => group.Key,
                group => group.Count());

        return new SchedulingRunOptimizationResult(
            candidate,
            evaluation,
            loadByResource,
            violationsByType,
            generationDiagnostics);
    }

    private static int CountViolations(
        SchedulingRunOptimizationResult result,
        ConstraintViolationType type)
    {
        return result.ViolationsByType.TryGetValue(type, out var count)
            ? count
            : 0;
    }

    private static ScheduleEvaluationResult CreateEvaluation(
        int scoreValue,
        int hardViolationCount,
        int softViolationCount,
        int totalPenalty,
        IReadOnlyCollection<ConstraintViolation> violations)
    {
        var score = new ScheduleScore(
            value: scoreValue,
            hardViolationCount: hardViolationCount,
            softViolationCount: softViolationCount,
            totalPenalty: totalPenalty);

        return new ScheduleEvaluationResult(score, violations);
    }

    private static Scenario CreateScenario()
    {
        var dana = new Resource(
            Guid.NewGuid(),
            "Dana",
            hourlyCost: 100m);

        var yossi = new Resource(
            Guid.NewGuid(),
            "Yossi",
            hourlyCost: 100m);

        var firstShift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning,
            minResourceCount: 1,
            maxResourceCount: 1);

        var secondShift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 22, 30, 0, DateTimeKind.Utc),
            ShiftKind.Afternoon,
            minResourceCount: 1,
            maxResourceCount: 1);

        var availabilityWindows = new[]
        {
            new AvailabilityWindow(dana.Id, firstShift.StartUtc, firstShift.EndUtc),
            new AvailabilityWindow(dana.Id, secondShift.StartUtc, secondShift.EndUtc),
            new AvailabilityWindow(yossi.Id, firstShift.StartUtc, firstShift.EndUtc),
            new AvailabilityWindow(yossi.Id, secondShift.StartUtc, secondShift.EndUtc)
        };

        var preferences = new[]
        {
            new ResourcePreference(
                yossi.Id,
                secondShift.StartUtc,
                secondShift.EndUtc,
                ResourcePreferenceType.Prefer,
                ResourcePreferencePriority.High)
        };

        var problem = new SchedulingProblem(
            period: new SchedulePeriod(
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc)),
            resources: [dana, yossi],
            shifts: [firstShift, secondShift],
            availabilityWindows: availabilityWindows,
            resourcePreferences: preferences,
            minimumAssignedHoursPerResource: 8,
            minimumMorningShiftsPerResourcePerFullWeek: 1,
            minimumAfternoonShiftsPerResourcePerFullWeek: 1);

        return new Scenario(
            Problem: problem,
            Dana: dana,
            Yossi: yossi,
            FirstShift: firstShift,
            SecondShift: secondShift);
    }

    private sealed record Scenario(
        SchedulingProblem Problem,
        Resource Dana,
        Resource Yossi,
        Shift FirstShift,
        Shift SecondShift);
}
