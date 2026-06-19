using Aether.Application.Scheduling.Contracts;
using Aether.Application.Scheduling.Optimization;
using Aether.Application.Scheduling.Reports;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class SchedulingRunReportFormatterLoadBalanceTests
{
    [Fact]
    public void Format_ShouldPrintLoadBalanceDiagnostic()
    {
        var amit = CreateResource("Amit");
        var dana = CreateResource("Dana");
        var maya = CreateResource("Maya");

        var shift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning,
            minResourceCount: 0,
            maxResourceCount: 3);

        var problem = new SchedulingProblem(
            period: new SchedulePeriod(
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc)),
            resources: [amit, dana, maya],
            shifts: [shift],
            availabilityWindows: [],
            resourcePreferences: []);

        var deterministicResult = CreateOptimizationResult(
            candidate: new ScheduleCandidate([]),
            evaluation: CreateEvaluation(scoreValue: 0, hardViolations: 1, totalPenalty: 50_000),
            loadByResource:
            [
                new ResourceLoadSummary(amit.Id, amit.Name, 0, 0),
                new ResourceLoadSummary(dana.Id, dana.Name, 0, 0),
                new ResourceLoadSummary(maya.Id, maya.Name, 0, 0)
            ]);

        var geneticResult = CreateOptimizationResult(
            candidate: new ScheduleCandidate([]),
            evaluation: CreateEvaluation(scoreValue: 1000, hardViolations: 0, totalPenalty: 0),
            loadByResource:
            [
                new ResourceLoadSummary(amit.Id, amit.Name, 24, 3),
                new ResourceLoadSummary(dana.Id, dana.Name, 24, 3),
                new ResourceLoadSummary(maya.Id, maya.Name, 48, 6)
            ]);

        var runResult = CreateRunResult(
            problem,
            deterministicResult,
            geneticResult);

        var formatter = new SchedulingRunReportFormatter();

        var report = formatter.Format(runResult);

        Assert.Contains("LoadBalance:", report);
        Assert.Contains("TotalAssignedHours: 96.0", report);
        Assert.Contains("AverageAssignedHours: 32.0", report);
        Assert.Contains("MinimumAssignedHours: 24.0", report);
        Assert.Contains("MaximumAssignedHours: 48.0", report);
        Assert.Contains("LoadSpreadHours: 24.0", report);
        Assert.Contains("LowestLoadedResources: Amit, Dana", report);
        Assert.Contains("HighestLoadedResources: Maya", report);
    }

    private static SchedulingRunOptimizationResult CreateOptimizationResult(
        ScheduleCandidate candidate,
        ScheduleEvaluationResult evaluation,
        IReadOnlyCollection<ResourceLoadSummary> loadByResource)
    {
        return new SchedulingRunOptimizationResult(
            candidate,
            evaluation,
            loadByResource,
            evaluation.Violations
                .GroupBy(violation => violation.Type)
                .ToDictionary(
                    group => group.Key,
                    group => group.Count()),
            GenerationDiagnostics: []);
    }

    private static SchedulingRunResult CreateRunResult(
        SchedulingProblem problem,
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
            DeterministicIgnoredAvoidPreferenceViolations: 0,
            GeneticIgnoredAvoidPreferenceViolations: 0,
            DeterministicShiftSequenceQuotaViolations: 0,
            GeneticShiftSequenceQuotaViolations: 0);

        return new SchedulingRunResult(
            problem,
            Warnings: [],
            deterministicResult,
            geneticResult,
            comparison);
    }

    private static ScheduleEvaluationResult CreateEvaluation(
        int scoreValue,
        int hardViolations,
        int totalPenalty)
    {
        var score = new ScheduleScore(
            value: scoreValue,
            hardViolationCount: hardViolations,
            softViolationCount: 0,
            totalPenalty: totalPenalty);

        return new ScheduleEvaluationResult(score, []);
    }

    private static Resource CreateResource(string name)
    {
        return new Resource(
            Guid.NewGuid(),
            name,
            hourlyCost: 100m);
    }
}
