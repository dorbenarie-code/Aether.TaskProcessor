using Aether.Application.Scheduling.Contracts;
using Aether.Application.Scheduling.Optimization;
using Aether.Application.Scheduling.Reports;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class SchedulingRunReportFormatterTests
{
    [Fact]
    public void Format_ShouldPrintBestGeneticScheduleAndScore()
    {
        var scenario = CreateScenario();

        var deterministicEvaluation = CreateEvaluation(
            scoreValue: 0,
            hardViolationCount: 1,
            totalPenalty: 50_000,
            violations:
            [
                new ConstraintViolation(
                    ConstraintViolationType.ShiftUnderstaffed,
                    ConstraintViolationSeverity.Hard,
                    "Shift does not have enough assigned resources.",
                    shiftId: scenario.FirstShift.Id)
            ]);

        var geneticCandidate = new ScheduleCandidate(
        [
            new Assignment(scenario.Dana.Id, scenario.FirstShift.Id),
            new Assignment(scenario.Yossi.Id, scenario.SecondShift.Id)
        ]);

        var geneticEvaluation = CreateEvaluation(scoreValue: 1000);

        var result = CreateRunResult(
            scenario,
            deterministicResult: CreateRunOptimizationResult(
                scenario.Problem,
                new ScheduleCandidate([]),
                deterministicEvaluation,
                generationDiagnostics: []),
            geneticResult: CreateRunOptimizationResult(
                scenario.Problem,
                geneticCandidate,
                geneticEvaluation,
                generationDiagnostics:
                [
                    new GeneticGenerationDiagnostic(
                        GenerationIndex: 0,
                        PopulationSize: 10,
                        FeasibleCandidateCount: 1,
                        BestScoreValue: 1000,
                        BestTotalPenalty: 0,
                        BestHardViolationCount: 0,
                        BestSoftViolationCount: 0)
                ]));

        var formatter = new SchedulingRunReportFormatter();

        var report = formatter.Format(result);

        Assert.Contains("Scheduling Run Report", report);
        Assert.Contains("BestResult: Genetic", report);
        Assert.Contains("IsFeasible: True", report);
        Assert.Contains("Score.Value: 1000", report);
        Assert.Contains("TotalPenalty: 0", report);
        Assert.Contains("AssignmentsByShift", report);
        Assert.Contains("Morning 2026-06-01 06:30-2026-06-01 14:30 UTC -> Dana", report);
        Assert.Contains("Afternoon 2026-06-01 14:30-2026-06-01 22:30 UTC -> Yossi", report);
        Assert.Contains("GenerationDiagnostics", report);
        Assert.Contains("Generation 0", report);
    }

    [Fact]
    public void Format_ShouldPrintDomainInputSummary()
    {
        var scenario = CreateScenario();

        var result = CreateRunResult(
            scenario,
            deterministicResult: CreateRunOptimizationResult(
                scenario.Problem,
                new ScheduleCandidate([]),
                CreateEvaluation(),
                generationDiagnostics: []),
            geneticResult: CreateRunOptimizationResult(
                scenario.Problem,
                new ScheduleCandidate([]),
                CreateEvaluation(),
                generationDiagnostics: []));

        var formatter = new SchedulingRunReportFormatter();

        var report = formatter.Format(result);

        Assert.Contains("Input Summary", report);
        Assert.Contains("Resources: 2", report);
        Assert.Contains("Shifts: 2", report);
        Assert.Contains("AvailabilityWindows: 4", report);
        Assert.Contains("ResourcePreferences: 1", report);
        Assert.Contains("MinimumAssignedHoursPerResource: 8", report);
        Assert.Contains("MinimumMorningShiftsPerResourcePerFullWeek: 1", report);
        Assert.Contains("MinimumAfternoonShiftsPerResourcePerFullWeek: 1", report);
        Assert.Contains("Dana", report);
        Assert.Contains("Yossi", report);
        Assert.Contains("RequiresPreferenceToAssign=True", report);
        Assert.Contains("RequiresMinimumWhenPreferenceExists=True", report);
    }

    [Fact]
    public void Format_ShouldPrintViolationsWithResourceAndShiftDetails()
    {
        var scenario = CreateScenario();

        var violation = new ConstraintViolation(
            ConstraintViolationType.IgnoredAvoidPreference,
            ConstraintViolationSeverity.Soft,
            "Resource is assigned to a shift that overlaps an avoid preference.",
            scenario.Dana.Id,
            scenario.FirstShift.Id);

        var evaluation = CreateEvaluation(
            scoreValue: 700,
            softViolationCount: 1,
            totalPenalty: 300,
            violations: [violation]);

        var candidate = new ScheduleCandidate(
        [
            new Assignment(scenario.Dana.Id, scenario.FirstShift.Id)
        ]);

        var result = CreateRunResult(
            scenario,
            deterministicResult: CreateRunOptimizationResult(
                scenario.Problem,
                candidate,
                evaluation,
                generationDiagnostics: []),
            geneticResult: CreateRunOptimizationResult(
                scenario.Problem,
                candidate,
                evaluation,
                generationDiagnostics: []));

        var formatter = new SchedulingRunReportFormatter();

        var report = formatter.Format(result);

        Assert.Contains("ViolationsByType:", report);
        Assert.Contains("IgnoredAvoidPreference: 1", report);
        Assert.Contains("Violations:", report);
        Assert.Contains("Resource=Dana", report);
        Assert.Contains("IgnoredAvoidPreference", report);
        Assert.Contains("Resource is assigned to a shift that overlaps an avoid preference.", report);
    }

    [Fact]
    public void Format_ShouldThrow_WhenResultIsNull()
    {
        var formatter = new SchedulingRunReportFormatter();

        var exception = Assert.Throws<ArgumentNullException>(() =>
            formatter.Format(null!));

        Assert.Equal("result", exception.ParamName);
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
        int scoreValue = 1000,
        int hardViolationCount = 0,
        int softViolationCount = 0,
        int totalPenalty = 0,
        IReadOnlyCollection<ConstraintViolation>? violations = null)
    {
        var score = new ScheduleScore(
            value: scoreValue,
            hardViolationCount: hardViolationCount,
            softViolationCount: softViolationCount,
            totalPenalty: totalPenalty);

        return new ScheduleEvaluationResult(
            score,
            violations ?? []);
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
            maxResourceCount: 2);

        var secondShift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 22, 30, 0, DateTimeKind.Utc),
            ShiftKind.Afternoon,
            minResourceCount: 0,
            maxResourceCount: 1,
            requiresPreferenceToAssign: true,
            requiresMinimumWhenPreferenceExists: true);

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
