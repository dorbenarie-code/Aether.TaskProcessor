using System.Text;
using Aether.Application.Scheduling.Contracts;
using Aether.Application.Scheduling.Optimization;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class RealisticFlexibleCapacityAvoidBurdenCleanGeneticDiagnosticTests
{
    private const int ResourceCount = 16;
    private const int DaysInSchedule = 14;
    private const int ExpectedShiftCount = DaysInSchedule * 3;
    private const double ExpectedTotalEffectiveTargetHours = 736.0;
    private const double BalanceToleranceHours = 5.0;
    private const double HoursTolerance = 0.000001;
    private const int PopulationSize = 120;
    private const int GenerationCount = 30;
    private const int Seed = 20260603;

    [Fact]
    public void CreateScenario_ShouldCreateRealisticFlexibleCapacityAvoidBurdenShape()
    {
        var scenario = CreateScenario();

        Assert.Equal(ResourceCount, scenario.Resources.Count);
        Assert.Equal(ExpectedShiftCount, scenario.Shifts.Count);
        Assert.Equal(ResourceCount, scenario.ResourceWorkloadDemands.Count);
        Assert.Equal(BalanceToleranceHours, scenario.Problem.MaximumAssignedHoursDeviationFromAverageHours);

        Assert.Equal(14, scenario.Shifts.Count(shift => shift.Kind == ShiftKind.Morning));
        Assert.Equal(14, scenario.Shifts.Count(shift => shift.Kind == ShiftKind.Afternoon));
        Assert.Equal(14, scenario.Shifts.Count(shift => shift.Kind == ShiftKind.Night));

        Assert.InRange(CalculateTotalMinimumCapacityHours(scenario.Problem), 526.99, 527.01);
        Assert.InRange(CalculateTotalMaximumCapacityHours(scenario.Problem), 1024.66, 1024.68);
        Assert.Equal(
            ExpectedTotalEffectiveTargetHours,
            scenario.ResourceWorkloadDemands.Sum(demand => demand.EffectiveTargetHours),
            precision: 6);

        var weekdayMorningShifts = scenario.Shifts
            .Where(IsWeekdayMorning)
            .ToArray();

        Assert.Equal(10, weekdayMorningShifts.Length);

        Assert.All(weekdayMorningShifts, shift =>
        {
            Assert.False(shift.RequiresPreferenceToAssign);
            Assert.Equal(ResourceCount, CountAvailableResourcesForShift(scenario, shift));
            Assert.Equal(1, CountPreferredResourcesForShift(scenario, shift));
            Assert.Equal(ResourceCount - 1, CountAvoidingResourcesForShift(scenario, shift));
            Assert.Equal(3, shift.MinResourceCount);
            Assert.Equal(6, shift.MaxResourceCount);
        });

        var firstMorning = scenario.Shifts.First(shift => shift.Kind == ShiftKind.Morning);
        var firstAfternoon = scenario.Shifts.First(shift => shift.Kind == ShiftKind.Afternoon);
        var firstNight = scenario.Shifts.First(shift => shift.Kind == ShiftKind.Night);

        Assert.Equal(new TimeOnly(6, 30), TimeOnly.FromDateTime(firstMorning.StartUtc));
        Assert.Equal(new TimeOnly(14, 20), TimeOnly.FromDateTime(firstMorning.EndUtc));
        Assert.Equal(new TimeOnly(14, 20), TimeOnly.FromDateTime(firstAfternoon.StartUtc));
        Assert.Equal(new TimeOnly(22, 40), TimeOnly.FromDateTime(firstAfternoon.EndUtc));
        Assert.Equal(new TimeOnly(22, 40), TimeOnly.FromDateTime(firstNight.StartUtc));
        Assert.Equal(new TimeOnly(6, 30), TimeOnly.FromDateTime(firstNight.EndUtc));

        Assert.Equal(10, scenario.Shifts.Count(shift => shift.NightShiftCategory == NightShiftCategory.Regular));
        Assert.Equal(2, scenario.Shifts.Count(shift => shift.NightShiftCategory == NightShiftCategory.FridayNight));
        Assert.Equal(2, scenario.Shifts.Count(shift => shift.NightShiftCategory == NightShiftCategory.MotzeiShabbatNight));

        Assert.All(
            scenario.Shifts.Where(shift => shift.NightShiftCategory == NightShiftCategory.MotzeiShabbatNight),
            shift =>
            {
                Assert.Equal(3, shift.MinResourceCount);
                Assert.Equal(3, shift.MaxResourceCount);
            });

        Assert.True(
            CountPotentialSequenceTemplatePairs(scenario.Shifts, ShiftSequenceType.AfternoonToMorning) > 0);

        Assert.True(
            CountPotentialSequenceTemplatePairs(scenario.Shifts, ShiftSequenceType.NightToAfternoon) > 0);
    }

    [Fact]
    public void CreateNormalWeekdayMorningScenario_ShouldCreateExpectedRealisticNormalMorningShape()
    {
        var scenario = CreateNormalWeekdayMorningScenario();

        Assert.Equal(ResourceCount, scenario.Resources.Count);
        Assert.Equal(ExpectedShiftCount, scenario.Shifts.Count);
        Assert.Equal(ResourceCount, scenario.ResourceWorkloadDemands.Count);
        Assert.Equal(BalanceToleranceHours, scenario.Problem.MaximumAssignedHoursDeviationFromAverageHours);

        Assert.InRange(CalculateTotalMinimumCapacityHours(scenario.Problem), 526.99, 527.01);
        Assert.InRange(CalculateTotalMaximumCapacityHours(scenario.Problem), 1024.66, 1024.68);
        Assert.Equal(
            ExpectedTotalEffectiveTargetHours,
            scenario.ResourceWorkloadDemands.Sum(demand => demand.EffectiveTargetHours),
            precision: 6);

        var weekdayMorningShifts = scenario.Shifts
            .Where(IsWeekdayMorning)
            .ToArray();

        Assert.Equal(10, weekdayMorningShifts.Length);

        Assert.All(weekdayMorningShifts, shift =>
        {
            Assert.False(shift.RequiresPreferenceToAssign);
            Assert.Equal(ResourceCount, CountAvailableResourcesForShift(scenario, shift));
            Assert.Equal(3, CountPreferredResourcesForShift(scenario, shift));
            Assert.Equal(ResourceCount - 3, CountAvoidingResourcesForShift(scenario, shift));
            Assert.Equal(3, shift.MinResourceCount);
            Assert.Equal(6, shift.MaxResourceCount);
        });

        var theoreticalMinimumAvoidedMorningAssignments = weekdayMorningShifts
            .Sum(shift => Math.Max(0, shift.MinResourceCount - CountPreferredResourcesForShift(scenario, shift)));

        Assert.Equal(0, theoreticalMinimumAvoidedMorningAssignments);

        Assert.True(
            CountPotentialSequenceTemplatePairs(scenario.Shifts, ShiftSequenceType.AfternoonToMorning) > 0);

        Assert.True(
            CountPotentialSequenceTemplatePairs(scenario.Shifts, ShiftSequenceType.NightToAfternoon) > 0);
    }

    [Fact]
    [Trait("Category", "SchedulingExperiment")]
    public void CleanGeneticOptimizer_ShouldPrintRealisticNormalWeekdayMorningAvoidBurdenDiagnostic()
    {
        var scenario = CreateNormalWeekdayMorningScenario();

        var variants = new[]
        {
            CreateVariant("AvoidBurden0", 0),
            CreateVariant("AvoidBurden25", 25),
            CreateVariant("AvoidBurden50", 50)
        };

        var report = new StringBuilder();

        report.AppendLine("Stage 7.7F.5 Realistic Normal Weekday Morning AvoidBurden Diagnostic");
        report.AppendLine("Scenario: realistic normal weekday morning coverage with sequence pressure");
        report.AppendLine("Mode: Clean GA");
        report.AppendLine("Variant base: Variant C default scoring policy + BalanceExcess100 candidate default");
        report.AppendLine($"Seed: {Seed}");
        report.AppendLine($"PopulationSize: {PopulationSize}");
        report.AppendLine($"GenerationCount: {GenerationCount}");
        report.AppendLine();

        AppendScenarioSummary(report, scenario);

        foreach (var variant in variants)
        {
            var diagnosticsSink = new CollectingGeneticOptimizerDiagnosticsSink();

            var optimizer = new GeneticScheduleOptimizer(
                populationSize: PopulationSize,
                seed: Seed,
                generationCount: GenerationCount,
                eliteCount: 1,
                tournamentSize: 3,
                diagnosticsSink: diagnosticsSink,
                evolutionMode: GeneticEvolutionMode.Clean,
                scoringWeights: variant.Weights);

            var result = optimizer.Optimize(scenario.Problem);

            AssertCandidateReferencesKnownProblemEntities(scenario, result.Candidate);
            AssertNoBasicStructuralViolations(result.Evaluation);
            Assert.True(result.Evaluation.IsFeasible);
            Assert.Equal(GenerationCount + 1, diagnosticsSink.Diagnostics.Count);
            Assert.True(
                diagnosticsSink.Diagnostics[^1].BestSoFarTotalPenalty <=
                diagnosticsSink.Diagnostics[0].BestSoFarTotalPenalty);

            AppendVariantSummary(
                report,
                scenario,
                variant,
                result,
                diagnosticsSink.Diagnostics);
        }

        var output = report.ToString();

        System.Console.WriteLine(output);

        Assert.Contains("Stage 7.7F.5 Realistic Normal Weekday Morning AvoidBurden Diagnostic", output);
        Assert.Contains("AvoidBurden0", output);
        Assert.Contains("AvoidBurden25", output);
        Assert.Contains("AvoidBurden50", output);
        Assert.Contains("MorningScarcitySummary:", output);
        Assert.Contains("MinPreferredResources: 3", output);
        Assert.Contains("MaxPreferredResources: 3", output);
        Assert.Contains("MinAvoidResources: 13", output);
        Assert.Contains("MaxAvoidResources: 13", output);
        Assert.Contains("IgnoredAvoidBurdenVariantSummary:", output);
        Assert.Contains("PenaltyBreakdownByType:", output);
    }

    [Fact]
    [Trait("Category", "SchedulingExperiment")]
    public void CleanGeneticOptimizer_ShouldPrintRealisticNormalAndStressAvoidBurdenMultiSeedDiagnostic()
    {
        var scenarioCases = new[]
        {
            new AvoidBurdenScenarioCase("Stress", CreateScenario()),
            new AvoidBurdenScenarioCase("Normal", CreateNormalWeekdayMorningScenario())
        };

        var seeds = new[]
        {
            20260603,
            20260604,
            20260605,
            20260606,
            20260607
        };

        var variants = new[]
        {
            CreateVariant("AvoidBurden0", 0),
            CreateVariant("AvoidBurden25", 25),
            CreateVariant("AvoidBurden50", 50)
        };

        var summaries = new List<AvoidBurdenMultiSeedRunSummary>();
        var report = new StringBuilder();

        report.AppendLine("Stage 7.7F.6 Realistic Normal + Stress AvoidBurden Multi-Seed Diagnostic");
        report.AppendLine("Mode: Clean GA");
        report.AppendLine("Variant base: Variant C default scoring policy + BalanceExcess100 candidate default");
        report.AppendLine($"PopulationSize: {PopulationSize}");
        report.AppendLine($"GenerationCount: {GenerationCount}");
        report.AppendLine($"SeedCount: {seeds.Length}");
        report.AppendLine($"ScenarioCount: {scenarioCases.Length}");
        report.AppendLine($"VariantCount: {variants.Length}");
        report.AppendLine();

        foreach (var scenarioCase in scenarioCases)
        {
            report.AppendLine("AvoidBurdenScenarioCaseSummary:");
            report.AppendLine($"ScenarioName: {scenarioCase.Name}");
            AppendScenarioSummary(report, scenarioCase.Scenario);

            foreach (var seed in seeds)
            {
                foreach (var variant in variants)
                {
                    var summary = RunAvoidBurdenMultiSeedCase(
                        scenarioCase.Name,
                        scenarioCase.Scenario,
                        seed,
                        variant);

                    summaries.Add(summary);
                    AppendAvoidBurdenMultiSeedRunSummary(report, summary);
                }
            }
        }

        AppendAvoidBurdenMultiSeedAggregateSummary(report, summaries);

        var output = report.ToString();

        System.Console.WriteLine(output);

        Assert.Contains("Stage 7.7F.6 Realistic Normal + Stress AvoidBurden Multi-Seed Diagnostic", output);
        Assert.Contains("ScenarioName: Stress", output);
        Assert.Contains("ScenarioName: Normal", output);
        Assert.Contains("AvoidBurden0", output);
        Assert.Contains("AvoidBurden25", output);
        Assert.Contains("AvoidBurden50", output);
        Assert.Contains("AvoidBurdenMultiSeedRunSummary:", output);
        Assert.Contains("AvoidBurdenMultiSeedAggregateSummary:", output);
        Assert.Contains("VariantAggregateSummary:", output);
        Assert.Contains("AllRunsFeasible:", output);
        Assert.Contains("AllRunsImprovedBestSoFar:", output);
    }

    [Fact]
    [Trait("Category", "SchedulingExperiment")]
    public void CleanGeneticOptimizer_ShouldPrintRealisticFlexibleCapacityAvoidBurdenDiagnostic()
    {
        var scenario = CreateScenario();

        var variants = new[]
        {
            CreateVariant("AvoidBurden0", 0),
            CreateVariant("AvoidBurden25", 25),
            CreateVariant("AvoidBurden50", 50)
        };

        var report = new StringBuilder();

        report.AppendLine("Stage 7.7F.4 Realistic Flexible Capacity AvoidBurden Diagnostic");
        report.AppendLine("Scenario: realistic flexible capacity individual shortage with sequence pressure");
        report.AppendLine("Mode: Clean GA");
        report.AppendLine("Variant base: Variant C default scoring policy + BalanceExcess100 candidate default");
        report.AppendLine($"Seed: {Seed}");
        report.AppendLine($"PopulationSize: {PopulationSize}");
        report.AppendLine($"GenerationCount: {GenerationCount}");
        report.AppendLine();

        AppendScenarioSummary(report, scenario);

        foreach (var variant in variants)
        {
            var diagnosticsSink = new CollectingGeneticOptimizerDiagnosticsSink();

            var optimizer = new GeneticScheduleOptimizer(
                populationSize: PopulationSize,
                seed: Seed,
                generationCount: GenerationCount,
                eliteCount: 1,
                tournamentSize: 3,
                diagnosticsSink: diagnosticsSink,
                evolutionMode: GeneticEvolutionMode.Clean,
                scoringWeights: variant.Weights);

            var result = optimizer.Optimize(scenario.Problem);

            AssertCandidateReferencesKnownProblemEntities(scenario, result.Candidate);
            AssertNoBasicStructuralViolations(result.Evaluation);
            Assert.True(result.Evaluation.IsFeasible);
            Assert.Equal(GenerationCount + 1, diagnosticsSink.Diagnostics.Count);
            Assert.True(
                diagnosticsSink.Diagnostics[^1].BestSoFarTotalPenalty <=
                diagnosticsSink.Diagnostics[0].BestSoFarTotalPenalty);

            AppendVariantSummary(
                report,
                scenario,
                variant,
                result,
                diagnosticsSink.Diagnostics);
        }

        var output = report.ToString();

        System.Console.WriteLine(output);

        Assert.Contains("Stage 7.7F.4 Realistic Flexible Capacity AvoidBurden Diagnostic", output);
        Assert.Contains("AvoidBurden0", output);
        Assert.Contains("AvoidBurden25", output);
        Assert.Contains("AvoidBurden50", output);
        Assert.Contains("MorningScarcitySummary:", output);
        Assert.Contains("WeekendAndNightSummary:", output);
        Assert.Contains("SequenceTemplatePressureSummary:", output);
        Assert.Contains("IgnoredAvoidBurdenVariantSummary:", output);
        Assert.Contains("RequestedPreferredFulfillmentMetrics:", output);
        Assert.Contains("WeekdayMorningCoverageMetrics:", output);
        Assert.Contains("AssignedHoursBalanceMetrics:", output);
        Assert.Contains("PenaltyBreakdownByType:", output);
    }


    private static AvoidBurdenMultiSeedRunSummary RunAvoidBurdenMultiSeedCase(
        string scenarioName,
        ExperimentScenario scenario,
        int seed,
        ScoringWeightVariant variant)
    {
        var diagnosticsSink = new CollectingGeneticOptimizerDiagnosticsSink();

        var optimizer = new GeneticScheduleOptimizer(
            populationSize: PopulationSize,
            seed: seed,
            generationCount: GenerationCount,
            eliteCount: 1,
            tournamentSize: 3,
            diagnosticsSink: diagnosticsSink,
            evolutionMode: GeneticEvolutionMode.Clean,
            scoringWeights: variant.Weights);

        var result = optimizer.Optimize(scenario.Problem);

        AssertCandidateReferencesKnownProblemEntities(scenario, result.Candidate);
        AssertNoBasicStructuralViolations(result.Evaluation);
        Assert.True(result.Evaluation.IsFeasible);

        Assert.Equal(
            GenerationCount + 1,
            diagnosticsSink.Diagnostics.Count);

        Assert.True(
            diagnosticsSink.Diagnostics[^1].BestSoFarTotalPenalty <=
            diagnosticsSink.Diagnostics[0].BestSoFarTotalPenalty,
            $"Scenario {scenarioName}, variant {variant.Name}, seed {seed} should not return a best-so-far penalty worse than generation 0.");

        var assignedHours = CalculateAssignedHoursByResource(
            scenario,
            result.Candidate);

        var targetGap = CalculateTargetGap(
            scenario,
            assignedHours);

        var ignoredAvoidCounts = CalculateIgnoredAvoidAssignmentCountsByResourceForMultiSeed(
            scenario,
            result.Candidate);

        var burdenViolations = result.Evaluation.Violations
            .Where(violation => violation.Type == ConstraintViolationType.ResourceIgnoredAvoidPreferenceBurden)
            .ToArray();

        var morningMetrics = CalculateWeekdayMorningCoverageMetrics(
            scenario,
            result.Candidate);

        var balanceMetrics = CalculateAssignedHoursBalanceMetrics(
            scenario,
            result.Candidate);

        var sequenceMetrics = CalculateSequenceAssignmentMetrics(
            scenario,
            result.Candidate);

        var balanceViolationMagnitude = result.Evaluation.Violations
            .Where(violation => violation.Type == ConstraintViolationType.ResourceAssignedHoursBalanceExceeded)
            .Sum(violation => violation.Magnitude ?? 0);

        return new AvoidBurdenMultiSeedRunSummary(
            ScenarioName: scenarioName,
            VariantName: variant.Name,
            Seed: seed,
            AvoidBurdenPenaltyPerHour: variant.Weights.ResourceIgnoredAvoidPreferenceBurdenPenaltyPerHour,
            IsFeasible: result.Evaluation.IsFeasible,
            TotalPenalty: result.Evaluation.Score.TotalPenalty,
            HardViolationCount: result.Evaluation.Score.HardViolationCount,
            SoftViolationCount: result.Evaluation.Score.SoftViolationCount,
            AssignmentCount: result.Candidate.Assignments.Count,
            TotalAssignedHours: assignedHours.Values.Sum(),
            TotalOverTargetHours: targetGap.TotalOverTargetHours,
            TotalUnderTargetHours: targetGap.TotalUnderTargetHours,
            IgnoredAvoidPreferenceViolationCount: CountViolations(
                result.Evaluation,
                ConstraintViolationType.IgnoredAvoidPreference),
            TotalIgnoredAvoidAssignments: ignoredAvoidCounts.Values.Sum(),
            MaxIgnoredAvoidAssignmentsForSingleResource: ignoredAvoidCounts.Values.Max(),
            AverageIgnoredAvoidAssignmentsPerResource: ignoredAvoidCounts.Values.Average(),
            ResourcesWithIgnoredAvoidAssignmentsCount: ignoredAvoidCounts.Count(pair => pair.Value > 0),
            ResourceIgnoredAvoidPreferenceBurdenViolationCount: burdenViolations.Length,
            TotalIgnoredAvoidBurdenMagnitude: burdenViolations.Sum(violation => violation.Magnitude ?? 0),
            MaxIgnoredAvoidBurdenMagnitudeForSingleResource: burdenViolations.Length == 0
                ? 0
                : burdenViolations.Max(violation => violation.Magnitude ?? 0),
            TotalWeekdayMorningAssignments: morningMetrics.TotalWeekdayMorningAssignments,
            MorningAssignmentsAboveMinimum: morningMetrics.MorningAssignmentsAboveMinimum,
            PreferredMorningAssignments: morningMetrics.PreferredMorningAssignments,
            AvoidedMorningAssignments: morningMetrics.AvoidedMorningAssignments,
            AvoidedMorningAssignmentsAboveTheoreticalMinimum: morningMetrics.AvoidedMorningAssignmentsAboveTheoreticalMinimum,
            ResourcesOutsideBalanceToleranceCount: balanceMetrics.ResourcesOutsideBalanceToleranceCount,
            BalanceViolationTotalMagnitudeFromEvaluation: balanceViolationMagnitude,
            TotalSupportedSequences: sequenceMetrics.TotalSupportedSequences,
            ShiftSequenceQuotaExceededViolationCount: CountViolations(
                result.Evaluation,
                ConstraintViolationType.ShiftSequenceQuotaExceeded),
            FirstBestSoFarTotalPenalty: diagnosticsSink.Diagnostics[0].BestSoFarTotalPenalty,
            LastBestSoFarTotalPenalty: diagnosticsSink.Diagnostics[^1].BestSoFarTotalPenalty,
            FirstFeasibleCandidateCount: diagnosticsSink.Diagnostics[0].FeasibleCandidateCount,
            LastFeasibleCandidateCount: diagnosticsSink.Diagnostics[^1].FeasibleCandidateCount);
    }

    private static IReadOnlyDictionary<Guid, int> CalculateIgnoredAvoidAssignmentCountsByResourceForMultiSeed(
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        var shiftsById = scenario.Shifts
            .ToDictionary(shift => shift.Id);

        var counts = scenario.Resources
            .ToDictionary(resource => resource.Id, _ => 0);

        foreach (var assignment in candidate.Assignments)
        {
            var shift = shiftsById[assignment.ShiftId];

            if (!HasPreferenceForShift(
                    scenario,
                    assignment.ResourceId,
                    shift,
                    ResourcePreferenceType.Avoid))
            {
                continue;
            }

            counts[assignment.ResourceId]++;
        }

        return counts;
    }

    private static void AppendAvoidBurdenMultiSeedRunSummary(
        StringBuilder report,
        AvoidBurdenMultiSeedRunSummary summary)
    {
        report.AppendLine("AvoidBurdenMultiSeedRunSummary:");
        report.AppendLine($"ScenarioName: {summary.ScenarioName}");
        report.AppendLine($"VariantName: {summary.VariantName}");
        report.AppendLine($"Seed: {summary.Seed}");
        report.AppendLine($"AvoidBurdenPenaltyPerHour: {summary.AvoidBurdenPenaltyPerHour}");
        report.AppendLine($"IsFeasible: {summary.IsFeasible}");
        report.AppendLine($"TotalPenalty: {summary.TotalPenalty}");
        report.AppendLine($"HardViolationCount: {summary.HardViolationCount}");
        report.AppendLine($"SoftViolationCount: {summary.SoftViolationCount}");
        report.AppendLine($"Assignments.Count: {summary.AssignmentCount}");
        report.AppendLine($"TotalAssignedHours: {summary.TotalAssignedHours:0.##}");
        report.AppendLine($"TotalOverTargetHours: {summary.TotalOverTargetHours:0.##}");
        report.AppendLine($"TotalUnderTargetHours: {summary.TotalUnderTargetHours:0.##}");
        report.AppendLine($"IgnoredAvoidPreferenceViolationCount: {summary.IgnoredAvoidPreferenceViolationCount}");
        report.AppendLine($"TotalIgnoredAvoidAssignments: {summary.TotalIgnoredAvoidAssignments}");
        report.AppendLine($"MaxIgnoredAvoidAssignmentsForSingleResource: {summary.MaxIgnoredAvoidAssignmentsForSingleResource}");
        report.AppendLine($"AverageIgnoredAvoidAssignmentsPerResource: {summary.AverageIgnoredAvoidAssignmentsPerResource:0.##}");
        report.AppendLine($"ResourcesWithIgnoredAvoidAssignmentsCount: {summary.ResourcesWithIgnoredAvoidAssignmentsCount}");
        report.AppendLine($"ResourceIgnoredAvoidPreferenceBurdenViolationCount: {summary.ResourceIgnoredAvoidPreferenceBurdenViolationCount}");
        report.AppendLine($"TotalIgnoredAvoidBurdenMagnitude: {summary.TotalIgnoredAvoidBurdenMagnitude:0.##}");
        report.AppendLine($"MaxIgnoredAvoidBurdenMagnitudeForSingleResource: {summary.MaxIgnoredAvoidBurdenMagnitudeForSingleResource:0.##}");
        report.AppendLine($"TotalWeekdayMorningAssignments: {summary.TotalWeekdayMorningAssignments}");
        report.AppendLine($"MorningAssignmentsAboveMinimum: {summary.MorningAssignmentsAboveMinimum}");
        report.AppendLine($"PreferredMorningAssignments: {summary.PreferredMorningAssignments}");
        report.AppendLine($"AvoidedMorningAssignments: {summary.AvoidedMorningAssignments}");
        report.AppendLine($"AvoidedMorningAssignmentsAboveTheoreticalMinimum: {summary.AvoidedMorningAssignmentsAboveTheoreticalMinimum}");
        report.AppendLine($"ResourcesOutsideBalanceToleranceCount: {summary.ResourcesOutsideBalanceToleranceCount}");
        report.AppendLine($"BalanceViolationTotalMagnitudeFromEvaluation: {summary.BalanceViolationTotalMagnitudeFromEvaluation:0.##}");
        report.AppendLine($"TotalSupportedSequences: {summary.TotalSupportedSequences}");
        report.AppendLine($"ShiftSequenceQuotaExceededViolationCount: {summary.ShiftSequenceQuotaExceededViolationCount}");
        report.AppendLine($"FirstBestSoFarTotalPenalty: {summary.FirstBestSoFarTotalPenalty}");
        report.AppendLine($"LastBestSoFarTotalPenalty: {summary.LastBestSoFarTotalPenalty}");
        report.AppendLine($"Improvement: {summary.FirstBestSoFarTotalPenalty - summary.LastBestSoFarTotalPenalty}");
        report.AppendLine($"FirstFeasibleCandidateCount: {summary.FirstFeasibleCandidateCount}");
        report.AppendLine($"LastFeasibleCandidateCount: {summary.LastFeasibleCandidateCount}");
        report.AppendLine();
    }


    private static void AppendAvoidBurdenMultiSeedAggregateSummary(
        StringBuilder report,
        IReadOnlyCollection<AvoidBurdenMultiSeedRunSummary> summaries)
    {
        report.AppendLine("AvoidBurdenMultiSeedAggregateSummary:");

        foreach (var scenarioGroup in summaries
                     .GroupBy(summary => summary.ScenarioName)
                     .OrderBy(group => group.Key))
        {
            foreach (var variantGroup in scenarioGroup
                         .GroupBy(summary => summary.VariantName)
                         .OrderBy(group => group.Key))
            {
                var runs = variantGroup.ToArray();

                report.AppendLine("VariantAggregateSummary:");
                report.AppendLine($"ScenarioName: {scenarioGroup.Key}");
                report.AppendLine($"VariantName: {variantGroup.Key}");
                report.AppendLine($"RunCount: {runs.Length}");
                report.AppendLine($"AllRunsFeasible: {runs.All(run => run.IsFeasible)}");
                report.AppendLine($"AnyRunHasHardViolations: {runs.Any(run => run.HardViolationCount > 0)}");
                report.AppendLine($"AnyRunHasSequenceQuotaViolations: {runs.Any(run => run.ShiftSequenceQuotaExceededViolationCount > 0)}");
                report.AppendLine($"AllRunsImprovedBestSoFar: {runs.All(run => run.LastBestSoFarTotalPenalty <= run.FirstBestSoFarTotalPenalty)}");

                AppendIntRange(report, "TotalPenalty", runs.Select(run => run.TotalPenalty));
                AppendDoubleRange(report, "TotalAssignedHours", runs.Select(run => run.TotalAssignedHours));
                AppendDoubleRange(report, "TotalOverTargetHours", runs.Select(run => run.TotalOverTargetHours));
                AppendDoubleRange(report, "TotalUnderTargetHours", runs.Select(run => run.TotalUnderTargetHours));
                AppendIntRange(report, "TotalIgnoredAvoidAssignments", runs.Select(run => run.TotalIgnoredAvoidAssignments));
                AppendIntRange(report, "MaxIgnoredAvoidAssignmentsForSingleResource", runs.Select(run => run.MaxIgnoredAvoidAssignmentsForSingleResource));
                AppendDoubleRange(report, "TotalIgnoredAvoidBurdenMagnitude", runs.Select(run => run.TotalIgnoredAvoidBurdenMagnitude));
                AppendDoubleRange(report, "MaxIgnoredAvoidBurdenMagnitudeForSingleResource", runs.Select(run => run.MaxIgnoredAvoidBurdenMagnitudeForSingleResource));
                AppendIntRange(report, "MorningAssignmentsAboveMinimum", runs.Select(run => run.MorningAssignmentsAboveMinimum));
                AppendIntRange(report, "AvoidedMorningAssignmentsAboveTheoreticalMinimum", runs.Select(run => run.AvoidedMorningAssignmentsAboveTheoreticalMinimum));
                AppendDoubleRange(report, "BalanceViolationTotalMagnitudeFromEvaluation", runs.Select(run => run.BalanceViolationTotalMagnitudeFromEvaluation));
                AppendIntRange(report, "TotalSupportedSequences", runs.Select(run => run.TotalSupportedSequences));

                report.AppendLine();
            }
        }
    }

    private static void AppendIntRange(
        StringBuilder report,
        string metricName,
        IEnumerable<int> values)
    {
        var array = values.ToArray();

        report.AppendLine($"{metricName}Range: {array.Min()}..{array.Max()}");
        report.AppendLine($"Average{metricName}: {array.Average():0.##}");
    }

    private static void AppendDoubleRange(
        StringBuilder report,
        string metricName,
        IEnumerable<double> values)
    {
        var array = values.ToArray();

        report.AppendLine($"{metricName}Range: {array.Min():0.##}..{array.Max():0.##}");
        report.AppendLine($"Average{metricName}: {array.Average():0.##}");
    }

    private static ExperimentScenario CreateScenario()
    {
        var resources = CreateResources(ResourceCount);
        var shifts = CreateBiWeeklySequencePressureShifts(RequiresPreferenceForIndividualShortageShift);

        var availabilityWindows = new List<AvailabilityWindow>();
        var preferences = new List<ResourcePreference>();

        AddIndividualAvailabilityShortageProfiles(
            resources,
            shifts,
            availabilityWindows,
            preferences);

        return CreateScenarioFromSubmissions(
            resources,
            shifts,
            availabilityWindows,
            preferences);
    }

    private static ExperimentScenario CreateNormalWeekdayMorningScenario()
    {
        var resources = CreateResources(ResourceCount);
        var shifts = CreateBiWeeklySequencePressureShifts(RequiresPreferenceForIndividualShortageShift);

        var availabilityWindows = new List<AvailabilityWindow>();
        var preferences = new List<ResourcePreference>();

        AddIndividualAvailabilityShortageProfiles(
            resources,
            shifts,
            availabilityWindows,
            preferences);

        AddNormalWeekdayMorningPreferredCoverage(
            resources,
            shifts,
            availabilityWindows,
            preferences);

        return CreateScenarioFromSubmissions(
            resources,
            shifts,
            availabilityWindows,
            preferences);
    }

    private static ExperimentScenario CreateScenarioFromSubmissions(
        IReadOnlyList<Resource> resources,
        IReadOnlyList<Shift> shifts,
        IReadOnlyCollection<AvailabilityWindow> availabilityWindows,
        IReadOnlyCollection<ResourcePreference> preferences)
    {
        var workloadDemands = CreateWorkloadDemands(
            resources,
            shifts,
            preferences);

        var problem = new SchedulingProblem(
            period: CreateBiWeeklyPeriod(),
            resources: resources,
            shifts: shifts,
            availabilityWindows: availabilityWindows,
            resourcePreferences: preferences,
            minimumAssignedHoursPerResource: 0,
            minimumMorningShiftsPerResourcePerFullWeek: 0,
            minimumAfternoonShiftsPerResourcePerFullWeek: 0,
            maximumAssignedHoursDeviationFromAverageHours: BalanceToleranceHours,
            resourceWorkloadDemands: workloadDemands);

        return new ExperimentScenario(
            Problem: problem,
            Resources: resources,
            Shifts: shifts,
            AvailabilityWindows: availabilityWindows.ToArray(),
            ResourcePreferences: preferences.ToArray(),
            ResourceWorkloadDemands: workloadDemands);
    }

    private static bool RequiresPreferenceForIndividualShortageShift(
        DateOnly date,
        ShiftKind kind)
    {
        return kind != ShiftKind.Morning ||
               date.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday;
    }

    private static void AddIndividualAvailabilityShortageProfiles(
        IReadOnlyList<Resource> resources,
        IReadOnlyList<Shift> shifts,
        ICollection<AvailabilityWindow> availabilityWindows,
        ICollection<ResourcePreference> preferences)
    {
        AddIndividualShortageProfile(resources[0], shifts, availabilityWindows, preferences,
            prefer: shift =>
                IsWeekdayMorningOn(shift, DayOfWeek.Sunday, DayOfWeek.Thursday) ||
                IsWeekdayAfternoonOn(shift, DayOfWeek.Monday) ||
                IsRegularNightOn(shift, DayOfWeek.Sunday) ||
                IsMotzeiShabbatNight(shift),
            avoid: IsWeekdayMorning);

        AddIndividualShortageProfile(resources[1], shifts, availabilityWindows, preferences,
            prefer: shift =>
                IsWeekdayAfternoonOn(shift, DayOfWeek.Sunday, DayOfWeek.Tuesday) ||
                IsRegularNightOn(shift, DayOfWeek.Monday) ||
                IsMotzeiShabbatNight(shift),
            avoid: IsWeekdayMorning);

        AddIndividualShortageProfile(resources[2], shifts, availabilityWindows, preferences,
            prefer: shift =>
                IsWeekdayAfternoonOn(shift, DayOfWeek.Sunday, DayOfWeek.Wednesday) ||
                IsRegularNightOn(shift, DayOfWeek.Sunday, DayOfWeek.Tuesday) ||
                IsMotzeiShabbatNight(shift),
            avoid: IsWeekdayMorning);

        AddIndividualShortageProfile(resources[3], shifts, availabilityWindows, preferences,
            prefer: shift =>
                IsWeekdayAfternoonOn(shift, DayOfWeek.Monday, DayOfWeek.Thursday) ||
                IsRegularNightOn(shift, DayOfWeek.Monday, DayOfWeek.Wednesday) ||
                IsFridayMorning(shift) ||
                IsMotzeiShabbatNight(shift),
            avoid: IsWeekdayMorning);

        AddIndividualShortageProfile(resources[4], shifts, availabilityWindows, preferences,
            prefer: shift =>
                IsWeekdayMorningOn(shift, DayOfWeek.Monday) ||
                IsWeekdayAfternoonOn(shift, DayOfWeek.Tuesday) ||
                IsRegularNightOn(shift, DayOfWeek.Tuesday, DayOfWeek.Thursday) ||
                IsFridayAfternoon(shift) ||
                IsMotzeiShabbatNight(shift),
            avoid: IsWeekdayMorning);

        AddIndividualShortageProfile(resources[5], shifts, availabilityWindows, preferences,
            prefer: shift =>
                IsWeekdayAfternoonOn(shift, DayOfWeek.Wednesday) ||
                IsRegularNightOn(shift, DayOfWeek.Sunday, DayOfWeek.Wednesday) ||
                IsSaturdayMorning(shift) ||
                IsMotzeiShabbatNight(shift),
            avoid: IsWeekdayMorning);

        AddIndividualShortageProfile(resources[6], shifts, availabilityWindows, preferences,
            prefer: shift =>
                IsWeekdayAfternoonOn(shift, DayOfWeek.Thursday) ||
                IsRegularNightOn(shift, DayOfWeek.Monday, DayOfWeek.Thursday) ||
                IsSaturdayAfternoon(shift) ||
                IsMotzeiShabbatNight(shift),
            avoid: IsWeekdayMorning);

        AddIndividualShortageProfile(resources[7], shifts, availabilityWindows, preferences,
            prefer: shift =>
                IsWeekdayAfternoonOn(shift, DayOfWeek.Sunday) ||
                IsRegularNightOn(shift, DayOfWeek.Tuesday) ||
                IsMotzeiShabbatNight(shift),
            avoid: IsWeekdayMorning);

        AddIndividualShortageProfile(resources[8], shifts, availabilityWindows, preferences,
            prefer: shift =>
                IsWeekdayMorningOn(shift, DayOfWeek.Tuesday) ||
                IsWeekdayAfternoonOn(shift, DayOfWeek.Monday) ||
                IsRegularNightOn(shift, DayOfWeek.Wednesday),
            avoid: IsWeekdayMorning);

        AddIndividualShortageProfile(resources[9], shifts, availabilityWindows, preferences,
            prefer: shift =>
                IsWeekdayAfternoonOn(shift, DayOfWeek.Tuesday) ||
                IsRegularNightOn(shift, DayOfWeek.Sunday, DayOfWeek.Thursday) ||
                IsFridayMorning(shift),
            avoid: IsWeekdayMorning);

        AddIndividualShortageProfile(resources[10], shifts, availabilityWindows, preferences,
            prefer: shift =>
                IsWeekdayAfternoonOn(shift, DayOfWeek.Wednesday) ||
                IsRegularNightOn(shift, DayOfWeek.Monday) ||
                IsFridayNight(shift),
            avoid: IsWeekdayMorning);

        AddIndividualShortageProfile(resources[11], shifts, availabilityWindows, preferences,
            prefer: shift =>
                IsWeekdayAfternoonOn(shift, DayOfWeek.Sunday, DayOfWeek.Thursday) ||
                IsRegularNightOn(shift, DayOfWeek.Tuesday),
            avoid: IsWeekdayMorning);

        AddIndividualShortageProfile(resources[12], shifts, availabilityWindows, preferences,
            prefer: shift =>
                IsWeekdayMorningOn(shift, DayOfWeek.Wednesday) ||
                IsWeekdayAfternoonOn(shift, DayOfWeek.Monday) ||
                IsRegularNightOn(shift, DayOfWeek.Wednesday),
            avoid: IsWeekdayMorning);

        AddIndividualShortageProfile(resources[13], shifts, availabilityWindows, preferences,
            prefer: shift =>
                IsWeekdayAfternoonOn(shift, DayOfWeek.Tuesday) ||
                IsRegularNightOn(shift, DayOfWeek.Thursday),
            avoid: IsWeekdayMorning);

        AddIndividualShortageProfile(resources[14], shifts, availabilityWindows, preferences,
            prefer: shift =>
                IsWeekdayAfternoonOn(shift, DayOfWeek.Wednesday) ||
                IsFridayNight(shift),
            avoid: IsWeekdayMorning);

        AddIndividualShortageProfile(resources[15], shifts, availabilityWindows, preferences,
            prefer: shift => IsWeekdayAfternoonOn(shift, DayOfWeek.Thursday),
            avoid: IsWeekdayMorning);
    }

    private static void AddNormalWeekdayMorningPreferredCoverage(
        IReadOnlyList<Resource> resources,
        IReadOnlyList<Shift> shifts,
        ICollection<AvailabilityWindow> availabilityWindows,
        ICollection<ResourcePreference> preferences)
    {
        var weekdayMorningShifts = shifts
            .Where(IsWeekdayMorning)
            .OrderBy(shift => shift.StartUtc)
            .ToArray();

        for (var shiftIndex = 0; shiftIndex < weekdayMorningShifts.Length; shiftIndex++)
        {
            var shift = weekdayMorningShifts[shiftIndex];

            while (CountResourcesWithPreferenceForShift(
                       preferences,
                       shift,
                       ResourcePreferenceType.Prefer) < shift.MinResourceCount)
            {
                var candidate = SelectNextMorningPreferCandidate(
                    resources,
                    shift,
                    shiftIndex,
                    preferences);

                AddPreferReplacingAvoid(
                    candidate,
                    shift,
                    availabilityWindows,
                    preferences);
            }
        }
    }

    private static Resource SelectNextMorningPreferCandidate(
        IReadOnlyList<Resource> resources,
        Shift shift,
        int shiftIndex,
        IEnumerable<ResourcePreference> preferences)
    {
        var orderedResources = resources
            .Skip((shiftIndex * 3) % resources.Count)
            .Concat(resources.Take((shiftIndex * 3) % resources.Count));

        foreach (var resource in orderedResources)
        {
            var alreadyPrefers = preferences
                .Where(preference => preference.ResourceId == resource.Id)
                .Where(preference => preference.Type == ResourcePreferenceType.Prefer)
                .Any(preference => Overlaps(
                    preference.StartUtc,
                    preference.EndUtc,
                    shift.StartUtc,
                    shift.EndUtc));

            if (alreadyPrefers)
            {
                continue;
            }

            return resource;
        }

        throw new InvalidOperationException(
            "Could not select an additional preferred resource for weekday morning coverage.");
    }

    private static void AddPreferReplacingAvoid(
        Resource resource,
        Shift shift,
        ICollection<AvailabilityWindow> availabilityWindows,
        ICollection<ResourcePreference> preferences)
    {
        AddAvailabilityIfMissing(resource, shift, availabilityWindows);

        RemovePreferenceIfExists(
            resource,
            shift,
            ResourcePreferenceType.Avoid,
            preferences);

        AddPreferenceIfMissing(
            resource,
            shift,
            ResourcePreferenceType.Prefer,
            preferences);
    }

    private static void RemovePreferenceIfExists(
        Resource resource,
        Shift shift,
        ResourcePreferenceType preferenceType,
        ICollection<ResourcePreference> preferences)
    {
        var matchingPreferences = preferences
            .Where(preference => preference.ResourceId == resource.Id)
            .Where(preference => preference.Type == preferenceType)
            .Where(preference => Overlaps(
                preference.StartUtc,
                preference.EndUtc,
                shift.StartUtc,
                shift.EndUtc))
            .ToArray();

        foreach (var preference in matchingPreferences)
        {
            preferences.Remove(preference);
        }
    }

    private static int CountResourcesWithPreferenceForShift(
        IEnumerable<ResourcePreference> preferences,
        Shift shift,
        ResourcePreferenceType preferenceType)
    {
        return preferences
            .Where(preference => preference.Type == preferenceType)
            .Where(preference => Overlaps(
                preference.StartUtc,
                preference.EndUtc,
                shift.StartUtc,
                shift.EndUtc))
            .Select(preference => preference.ResourceId)
            .Distinct()
            .Count();
    }

    private static void AddIndividualShortageProfile(
        Resource resource,
        IReadOnlyList<Shift> shifts,
        ICollection<AvailabilityWindow> availabilityWindows,
        ICollection<ResourcePreference> preferences,
        Func<Shift, bool> prefer,
        Func<Shift, bool> avoid)
    {
        foreach (var shift in shifts.Where(prefer))
        {
            AddPrefer(resource, shift, availabilityWindows, preferences);
        }

        foreach (var shift in shifts.Where(avoid))
        {
            if (HasAnyPreferenceForShift(resource, shift, preferences))
            {
                continue;
            }

            AddAvoid(resource, shift, availabilityWindows, preferences);
        }
    }

    private static IReadOnlyList<Resource> CreateResources(int count)
    {
        return Enumerable
            .Range(1, count)
            .Select(index => new Resource(Guid.NewGuid(), $"Guard{index:00}", hourlyCost: 100m))
            .ToArray();
    }

    private static IReadOnlyList<Shift> CreateBiWeeklySequencePressureShifts(
        Func<DateOnly, ShiftKind, bool> requiresPreferenceToAssign)
    {
        var shifts = new List<Shift>();

        for (var dayOffset = 0; dayOffset < DaysInSchedule; dayOffset++)
        {
            var date = new DateOnly(2026, 5, 31).AddDays(dayOffset);

            shifts.Add(CreateSequencePressureShift(date, ShiftKind.Morning, requiresPreferenceToAssign(date, ShiftKind.Morning)));
            shifts.Add(CreateSequencePressureShift(date, ShiftKind.Afternoon, requiresPreferenceToAssign(date, ShiftKind.Afternoon)));
            shifts.Add(CreateSequencePressureShift(date, ShiftKind.Night, requiresPreferenceToAssign(date, ShiftKind.Night)));
        }

        return shifts
            .OrderBy(shift => shift.StartUtc)
            .ToArray();
    }

    private static Shift CreateSequencePressureShift(
        DateOnly date,
        ShiftKind kind,
        bool requiresPreferenceToAssign)
    {
        var startUtc = GetSequencePressureStartUtc(date, kind);
        var endUtc = GetSequencePressureEndUtc(date, kind);
        var capacity = GetCapacityRule(date, kind);

        return new Shift(
            Guid.NewGuid(),
            startUtc,
            endUtc,
            kind,
            minResourceCount: capacity.MinResourceCount,
            maxResourceCount: capacity.MaxResourceCount,
            requiresPreferenceToAssign: requiresPreferenceToAssign,
            requiresMinimumWhenPreferenceExists: false,
            nightShiftCategory: GetNightShiftCategory(date, kind));
    }

    private static SchedulePeriod CreateBiWeeklyPeriod()
    {
        return new SchedulePeriod(
            new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc));
    }

    private static IReadOnlyList<ResourceWorkloadDemand> CreateWorkloadDemands(
        IReadOnlyList<Resource> resources,
        IReadOnlyList<Shift> shifts,
        IReadOnlyCollection<ResourcePreference> preferences)
    {
        var submittedPreferredHoursByResourceId = resources.ToDictionary(
            resource => resource.Id,
            resource => GetSubmittedPreferredHours(resource, shifts, preferences));

        var totalSubmittedPreferredHours = submittedPreferredHoursByResourceId
            .Values
            .Sum();

        if (totalSubmittedPreferredHours <= 0)
        {
            throw new InvalidOperationException(
                "Cannot create workload demands without submitted preferred hours.");
        }

        return resources
            .Select(resource =>
            {
                var requestedPreferredHours =
                    ExpectedTotalEffectiveTargetHours *
                    submittedPreferredHoursByResourceId[resource.Id] /
                    totalSubmittedPreferredHours;

                return new ResourceWorkloadDemand(
                    resource.Id,
                    requestedPreferredHours: requestedPreferredHours,
                    minimumRequiredHours: 0);
            })
            .ToArray();
    }

    private static void AddPrefer(
        Resource resource,
        Shift shift,
        ICollection<AvailabilityWindow> availabilityWindows,
        ICollection<ResourcePreference> preferences)
    {
        AddAvailabilityIfMissing(resource, shift, availabilityWindows);
        AddPreferenceIfMissing(resource, shift, ResourcePreferenceType.Prefer, preferences);
    }

    private static void AddAvoid(
        Resource resource,
        Shift shift,
        ICollection<AvailabilityWindow> availabilityWindows,
        ICollection<ResourcePreference> preferences)
    {
        AddAvailabilityIfMissing(resource, shift, availabilityWindows);
        AddPreferenceIfMissing(resource, shift, ResourcePreferenceType.Avoid, preferences);
    }

    private static void AddAvailabilityIfMissing(
        Resource resource,
        Shift shift,
        ICollection<AvailabilityWindow> availabilityWindows)
    {
        var exists = availabilityWindows.Any(window =>
            window.ResourceId == resource.Id &&
            window.StartUtc == shift.StartUtc &&
            window.EndUtc == shift.EndUtc);

        if (exists)
        {
            return;
        }

        availabilityWindows.Add(new AvailabilityWindow(
            resource.Id,
            shift.StartUtc,
            shift.EndUtc));
    }

    private static void AddPreferenceIfMissing(
        Resource resource,
        Shift shift,
        ResourcePreferenceType preferenceType,
        ICollection<ResourcePreference> preferences)
    {
        var exists = preferences.Any(preference =>
            preference.ResourceId == resource.Id &&
            preference.StartUtc == shift.StartUtc &&
            preference.EndUtc == shift.EndUtc &&
            preference.Type == preferenceType);

        if (exists)
        {
            return;
        }

        preferences.Add(new ResourcePreference(
            resource.Id,
            shift.StartUtc,
            shift.EndUtc,
            preferenceType,
            ResourcePreferencePriority.High));
    }

    private static bool HasAnyPreferenceForShift(
        Resource resource,
        Shift shift,
        IEnumerable<ResourcePreference> preferences)
    {
        return preferences
            .Where(preference => preference.ResourceId == resource.Id)
            .Any(preference => Overlaps(
                preference.StartUtc,
                preference.EndUtc,
                shift.StartUtc,
                shift.EndUtc));
    }

    private static (int MinResourceCount, int MaxResourceCount) GetCapacityRule(
        DateOnly date,
        ShiftKind kind)
    {
        if (date.DayOfWeek == DayOfWeek.Friday)
        {
            return kind switch
            {
                ShiftKind.Morning => (0, 2),
                ShiftKind.Afternoon => (0, 1),
                ShiftKind.Night => (0, 1),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
        }

        if (date.DayOfWeek == DayOfWeek.Saturday)
        {
            return kind switch
            {
                ShiftKind.Morning => (0, 1),
                ShiftKind.Afternoon => (0, 1),
                ShiftKind.Night => (3, 3),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
        }

        return kind switch
        {
            ShiftKind.Morning => (3, 6),
            ShiftKind.Afternoon => (2, 4),
            ShiftKind.Night => (1, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private static DateTime GetSequencePressureStartUtc(
        DateOnly date,
        ShiftKind kind)
    {
        return kind switch
        {
            ShiftKind.Morning => date.ToDateTime(new TimeOnly(6, 30), DateTimeKind.Utc),
            ShiftKind.Afternoon => date.ToDateTime(new TimeOnly(14, 20), DateTimeKind.Utc),
            ShiftKind.Night => date.ToDateTime(new TimeOnly(22, 40), DateTimeKind.Utc),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private static DateTime GetSequencePressureEndUtc(
        DateOnly date,
        ShiftKind kind)
    {
        return kind switch
        {
            ShiftKind.Morning => date.ToDateTime(new TimeOnly(14, 20), DateTimeKind.Utc),
            ShiftKind.Afternoon => date.ToDateTime(new TimeOnly(22, 40), DateTimeKind.Utc),
            ShiftKind.Night => date.AddDays(1).ToDateTime(new TimeOnly(6, 30), DateTimeKind.Utc),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private static NightShiftCategory? GetNightShiftCategory(
        DateOnly date,
        ShiftKind kind)
    {
        if (kind != ShiftKind.Night)
        {
            return null;
        }

        return date.DayOfWeek switch
        {
            DayOfWeek.Friday => NightShiftCategory.FridayNight,
            DayOfWeek.Saturday => NightShiftCategory.MotzeiShabbatNight,
            _ => NightShiftCategory.Regular
        };
    }

    private static ScoringWeightVariant CreateVariant(
        string name,
        int avoidBurdenPenaltyPerHour)
    {
        return new ScoringWeightVariant(
            name,
            ScheduleScoringWeights.CreateDefault() with
            {
                ResourceRequestedPreferredHoursNotSatisfiedPenaltyPerHour = 15,
                ResourceEffectiveTargetAssignedHoursAboveTargetPenaltyPerHour = 20,
                ResourceIgnoredAvoidPreferenceBurdenPenaltyPerHour = avoidBurdenPenaltyPerHour
            });
    }

    private static void AppendScenarioSummary(
        StringBuilder report,
        ExperimentScenario scenario)
    {
        report.AppendLine("ScenarioCapacitySummary:");
        report.AppendLine($"ResourceCount: {scenario.Resources.Count}");
        report.AppendLine($"ShiftCount: {scenario.Shifts.Count}");
        report.AppendLine($"TotalMinimumCapacityHours: {CalculateTotalMinimumCapacityHours(scenario.Problem):0.##}");
        report.AppendLine($"TotalMaximumCapacityHours: {CalculateTotalMaximumCapacityHours(scenario.Problem):0.##}");
        report.AppendLine($"TotalEffectiveTargetHours: {scenario.ResourceWorkloadDemands.Sum(demand => demand.EffectiveTargetHours):0.##}");
        report.AppendLine($"BalanceToleranceHours: {scenario.Problem.MaximumAssignedHoursDeviationFromAverageHours:0.##}");
        report.AppendLine();

        AppendMorningScarcitySummary(report, scenario);
        AppendWeekendAndNightSummary(report, scenario);
        AppendSequenceTemplatePressureSummary(report, scenario);
    }

    private static void AppendMorningScarcitySummary(
        StringBuilder report,
        ExperimentScenario scenario)
    {
        var weekdayMorningShifts = scenario.Shifts
            .Where(IsWeekdayMorning)
            .ToArray();

        report.AppendLine("MorningScarcitySummary:");
        report.AppendLine($"WeekdayMorningShiftCount: {weekdayMorningShifts.Length}");
        report.AppendLine($"TotalMinimumMorningAssignments: {weekdayMorningShifts.Sum(shift => shift.MinResourceCount)}");
        report.AppendLine($"TotalMaximumMorningAssignments: {weekdayMorningShifts.Sum(shift => shift.MaxResourceCount)}");
        report.AppendLine($"MinAvailableResources: {weekdayMorningShifts.Min(shift => CountAvailableResourcesForShift(scenario, shift))}");
        report.AppendLine($"MaxAvailableResources: {weekdayMorningShifts.Max(shift => CountAvailableResourcesForShift(scenario, shift))}");
        report.AppendLine($"MinPreferredResources: {weekdayMorningShifts.Min(shift => CountPreferredResourcesForShift(scenario, shift))}");
        report.AppendLine($"MaxPreferredResources: {weekdayMorningShifts.Max(shift => CountPreferredResourcesForShift(scenario, shift))}");
        report.AppendLine($"MinAvoidResources: {weekdayMorningShifts.Min(shift => CountAvoidingResourcesForShift(scenario, shift))}");
        report.AppendLine($"MaxAvoidResources: {weekdayMorningShifts.Max(shift => CountAvoidingResourcesForShift(scenario, shift))}");
        report.AppendLine($"ShiftsRequiringPreferenceToAssign: {weekdayMorningShifts.Count(shift => shift.RequiresPreferenceToAssign)}");
        report.AppendLine();
    }

    private static void AppendWeekendAndNightSummary(
        StringBuilder report,
        ExperimentScenario scenario)
    {
        report.AppendLine("WeekendAndNightSummary:");
        report.AppendLine($"FridayMorningShiftCount: {scenario.Shifts.Count(IsFridayMorning)}");
        report.AppendLine($"FridayAfternoonShiftCount: {scenario.Shifts.Count(IsFridayAfternoon)}");
        report.AppendLine($"SaturdayMorningShiftCount: {scenario.Shifts.Count(IsSaturdayMorning)}");
        report.AppendLine($"SaturdayAfternoonShiftCount: {scenario.Shifts.Count(IsSaturdayAfternoon)}");
        report.AppendLine($"RegularNightShiftCount: {scenario.Shifts.Count(shift => shift.NightShiftCategory == NightShiftCategory.Regular)}");
        report.AppendLine($"FridayNightShiftCount: {scenario.Shifts.Count(shift => shift.NightShiftCategory == NightShiftCategory.FridayNight)}");
        report.AppendLine($"MotzeiShabbatNightShiftCount: {scenario.Shifts.Count(shift => shift.NightShiftCategory == NightShiftCategory.MotzeiShabbatNight)}");
        report.AppendLine($"TotalMotzeiShabbatMinimumAssignments: {scenario.Shifts.Where(IsMotzeiShabbatNight).Sum(shift => shift.MinResourceCount)}");
        report.AppendLine($"TotalMotzeiShabbatMaximumAssignments: {scenario.Shifts.Where(IsMotzeiShabbatNight).Sum(shift => shift.MaxResourceCount)}");
        report.AppendLine();
    }

    private static void AppendSequenceTemplatePressureSummary(
        StringBuilder report,
        ExperimentScenario scenario)
    {
        report.AppendLine("SequenceTemplatePressureSummary:");
        report.AppendLine("MinimumRestHours: 8");
        report.AppendLine($"PotentialAfternoonToMorningTemplatePairs: {CountPotentialSequenceTemplatePairs(scenario.Shifts, ShiftSequenceType.AfternoonToMorning)}");
        report.AppendLine($"PotentialNightToAfternoonTemplatePairs: {CountPotentialSequenceTemplatePairs(scenario.Shifts, ShiftSequenceType.NightToAfternoon)}");
        report.AppendLine();
    }

    private static void AppendVariantSummary(
        StringBuilder report,
        ExperimentScenario scenario,
        ScoringWeightVariant variant,
        ScheduleOptimizationResult result,
        IReadOnlyList<GeneticGenerationDiagnostic> diagnostics)
    {
        var assignedHoursByResource = CalculateAssignedHoursByResource(scenario, result.Candidate);
        var targetGap = CalculateTargetGap(scenario, assignedHoursByResource);
        var ignoredAvoidCounts = CalculateIgnoredAvoidAssignmentCountsByResource(scenario, result.Candidate);
        var avoidedMorningCounts = CalculateAvoidedMorningAssignmentCountsByResource(scenario, result.Candidate);
        var burdenViolations = result.Evaluation.Violations
            .Where(violation => violation.Type == ConstraintViolationType.ResourceIgnoredAvoidPreferenceBurden)
            .ToArray();

        var burdenPenalty = new ScheduleScoreCalculator(variant.Weights)
            .Calculate(burdenViolations)
            .TotalPenalty;

        var sequenceMetrics = CalculateSequenceAssignmentMetrics(scenario, result.Candidate);
        var balanceMetrics = CalculateAssignedHoursBalanceMetrics(scenario, result.Candidate);
        var weekdayMorningCoverageMetrics = CalculateWeekdayMorningCoverageMetrics(scenario, result.Candidate);
        var requestedPreferredFulfillmentMetrics = CalculateRequestedPreferredFulfillmentMetrics(scenario, result.Candidate);

        Assert.Equal(
            ignoredAvoidCounts.Count(item => item.Count > 0),
            burdenViolations.Length);

        report.AppendLine("IgnoredAvoidBurdenVariantSummary:");
        report.AppendLine($"VariantName: {variant.Name}");
        report.AppendLine($"AvoidBurdenPenaltyPerHour: {variant.Weights.ResourceIgnoredAvoidPreferenceBurdenPenaltyPerHour}");
        report.AppendLine($"IsFeasible: {result.Evaluation.IsFeasible}");
        report.AppendLine($"TotalPenalty: {result.Evaluation.Score.TotalPenalty}");
        report.AppendLine($"HardViolationCount: {result.Evaluation.Score.HardViolationCount}");
        report.AppendLine($"SoftViolationCount: {result.Evaluation.Score.SoftViolationCount}");
        report.AppendLine($"Assignments.Count: {result.Candidate.Assignments.Count}");
        report.AppendLine($"TotalAssignedHours: {assignedHoursByResource.Values.Sum():0.##}");
        report.AppendLine($"TotalOverTargetHours: {targetGap.TotalOverTargetHours:0.##}");
        report.AppendLine($"TotalUnderTargetHours: {targetGap.TotalUnderTargetHours:0.##}");
        report.AppendLine($"IgnoredAvoidPreferenceViolationCount: {CountViolations(result.Evaluation, ConstraintViolationType.IgnoredAvoidPreference)}");
        report.AppendLine($"EstimatedIgnoredAvoidPreferencePenalty: {CountViolations(result.Evaluation, ConstraintViolationType.IgnoredAvoidPreference) * variant.Weights.IgnoredAvoidPreferencePenalty}");
        report.AppendLine($"TotalIgnoredAvoidAssignments: {ignoredAvoidCounts.Sum(item => item.Count)}");
        report.AppendLine($"MaxIgnoredAvoidAssignmentsForSingleResource: {ignoredAvoidCounts.Max(item => item.Count)}");
        report.AppendLine($"AverageIgnoredAvoidAssignmentsPerResource: {ignoredAvoidCounts.Average(item => item.Count):0.##}");
        report.AppendLine($"ResourcesWithIgnoredAvoidAssignmentsCount: {ignoredAvoidCounts.Count(item => item.Count > 0)}");
        report.AppendLine($"TotalAvoidedMorningAssignments: {avoidedMorningCounts.Sum(item => item.Count)}");
        report.AppendLine($"MaxAvoidedMorningAssignmentsForSingleResource: {avoidedMorningCounts.Max(item => item.Count)}");
        report.AppendLine($"AverageAvoidedMorningAssignmentsPerResource: {avoidedMorningCounts.Average(item => item.Count):0.##}");
        report.AppendLine($"ResourceIgnoredAvoidPreferenceBurdenViolationCount: {burdenViolations.Length}");
        report.AppendLine($"TotalIgnoredAvoidBurdenMagnitude: {burdenViolations.Sum(violation => violation.Magnitude ?? 0):0.##}");
        report.AppendLine($"MaxIgnoredAvoidBurdenMagnitudeForSingleResource: {(burdenViolations.Length == 0 ? 0 : burdenViolations.Max(violation => violation.Magnitude ?? 0)):0.##}");
        report.AppendLine($"EstimatedIgnoredAvoidBurdenPenalty: {burdenPenalty}");
        report.AppendLine($"FirstBestSoFarTotalPenalty: {diagnostics[0].BestSoFarTotalPenalty}");
        report.AppendLine($"MiddleBestSoFarTotalPenalty: {diagnostics[diagnostics.Count / 2].BestSoFarTotalPenalty}");
        report.AppendLine($"LastBestSoFarTotalPenalty: {diagnostics[^1].BestSoFarTotalPenalty}");
        report.AppendLine($"Improvement: {diagnostics[0].BestSoFarTotalPenalty - diagnostics[^1].BestSoFarTotalPenalty}");
        report.AppendLine($"FirstFeasibleCandidateCount: {diagnostics[0].FeasibleCandidateCount}");
        report.AppendLine($"LastFeasibleCandidateCount: {diagnostics[^1].FeasibleCandidateCount}");
        report.AppendLine();

        AppendRequestedPreferredFulfillmentMetrics(report, requestedPreferredFulfillmentMetrics);
        AppendWeekdayMorningCoverageMetrics(report, weekdayMorningCoverageMetrics);
        AppendSequenceAssignmentMetrics(report, sequenceMetrics, result.Evaluation);
        AppendAssignedHoursBalanceMetrics(report, balanceMetrics, result.Evaluation);
        AppendPenaltyBreakdownByType(report, result.Evaluation, variant.Weights);
        AppendIgnoredAvoidAssignmentsByResource(report, ignoredAvoidCounts, avoidedMorningCounts);
        report.AppendLine();
    }

    private static void AppendRequestedPreferredFulfillmentMetrics(
        StringBuilder report,
        RequestedPreferredFulfillmentMetrics metrics)
    {
        report.AppendLine("RequestedPreferredFulfillmentMetrics:");
        report.AppendLine($"RequestedPreferredShiftCount: {metrics.RequestedPreferredShiftCount}");
        report.AppendLine($"AssignedRequestedPreferredShiftCount: {metrics.AssignedRequestedPreferredShiftCount}");
        report.AppendLine($"UnsatisfiedRequestedPreferredShiftCount: {metrics.UnsatisfiedRequestedPreferredShiftCount}");
        report.AppendLine($"TotalRequestedPreferredHours: {metrics.TotalRequestedPreferredHours:0.##}");
        report.AppendLine($"AssignedRequestedPreferredHours: {metrics.AssignedRequestedPreferredHours:0.##}");
        report.AppendLine($"UnsatisfiedRequestedPreferredHours: {metrics.UnsatisfiedRequestedPreferredHours:0.##}");
        report.AppendLine();
    }

    private static void AppendWeekdayMorningCoverageMetrics(
        StringBuilder report,
        WeekdayMorningCoverageMetrics metrics)
    {
        report.AppendLine("WeekdayMorningCoverageMetrics:");
        report.AppendLine($"WeekdayMorningShiftCount: {metrics.WeekdayMorningShiftCount}");
        report.AppendLine($"TotalWeekdayMorningAssignments: {metrics.TotalWeekdayMorningAssignments}");
        report.AppendLine($"TotalMinimumWeekdayMorningAssignments: {metrics.TotalMinimumWeekdayMorningAssignments}");
        report.AppendLine($"TotalMaximumWeekdayMorningAssignments: {metrics.TotalMaximumWeekdayMorningAssignments}");
        report.AppendLine($"MorningAssignmentsAboveMinimum: {metrics.MorningAssignmentsAboveMinimum}");
        report.AppendLine($"WeekdayMorningShiftsAboveMinimumCount: {metrics.WeekdayMorningShiftsAboveMinimumCount}");
        report.AppendLine($"MaxWeekdayMorningAssignmentsForSingleShift: {metrics.MaxWeekdayMorningAssignmentsForSingleShift}");
        report.AppendLine($"PreferredMorningAssignments: {metrics.PreferredMorningAssignments}");
        report.AppendLine($"AvoidedMorningAssignments: {metrics.AvoidedMorningAssignments}");
        report.AppendLine($"TheoreticalMinimumAvoidedMorningAssignments: {metrics.TheoreticalMinimumAvoidedMorningAssignments}");
        report.AppendLine($"AvoidedMorningAssignmentsAboveTheoreticalMinimum: {metrics.AvoidedMorningAssignmentsAboveTheoreticalMinimum}");
        report.AppendLine();
    }

    private static void AppendSequenceAssignmentMetrics(
        StringBuilder report,
        SequenceAssignmentMetrics metrics,
        ScheduleEvaluationResult evaluation)
    {
        report.AppendLine("SequenceAssignmentMetrics:");
        report.AppendLine($"TotalAfternoonToMorningSequences: {metrics.TotalAfternoonToMorningSequences}");
        report.AppendLine($"TotalNightToAfternoonSequences: {metrics.TotalNightToAfternoonSequences}");
        report.AppendLine($"TotalSupportedSequences: {metrics.TotalSupportedSequences}");
        report.AppendLine($"MaxMonthlyAfternoonToMorningSequencesForSingleResource: {metrics.MaxAfternoonToMorningSequencesForSingleResource}");
        report.AppendLine($"MaxMonthlyNightToAfternoonSequencesForSingleResource: {metrics.MaxNightToAfternoonSequencesForSingleResource}");
        report.AppendLine($"MaxMonthlyTotalSequencesForSingleResource: {metrics.MaxTotalSequencesForSingleResource}");
        report.AppendLine($"ResourcesWithAnySequenceCount: {metrics.ResourcesWithAnySequenceCount}");
        report.AppendLine($"ShiftSequenceQuotaExceededViolationCount: {CountViolations(evaluation, ConstraintViolationType.ShiftSequenceQuotaExceeded)}");
        report.AppendLine();
    }

    private static void AppendAssignedHoursBalanceMetrics(
        StringBuilder report,
        AssignedHoursBalanceMetrics metrics,
        ScheduleEvaluationResult evaluation)
    {
        var balanceViolations = evaluation.Violations
            .Where(violation => violation.Type == ConstraintViolationType.ResourceAssignedHoursBalanceExceeded)
            .ToArray();

        report.AppendLine("AssignedHoursBalanceMetrics:");
        report.AppendLine($"AverageAssignedHours: {metrics.AverageAssignedHours:0.##}");
        report.AppendLine($"MaxAssignedHoursDeviationFromAverage: {metrics.MaxAssignedHoursDeviationFromAverage:0.##}");
        report.AppendLine($"AverageAssignedHoursDeviationFromAverage: {metrics.AverageAssignedHoursDeviationFromAverage:0.##}");
        report.AppendLine($"ResourcesOutsideBalanceToleranceCount: {metrics.ResourcesOutsideBalanceToleranceCount}");
        report.AppendLine($"TotalBalanceExcessMagnitudeHours: {metrics.TotalExcessDeviationHours:0.##}");
        report.AppendLine($"MaxBalanceExcessMagnitudeHours: {metrics.MaxExcessDeviationHours:0.##}");
        report.AppendLine($"BalanceViolationCountFromEvaluation: {balanceViolations.Length}");
        report.AppendLine($"BalanceViolationTotalMagnitudeFromEvaluation: {balanceViolations.Sum(violation => violation.Magnitude ?? 0):0.##}");
        report.AppendLine();
    }

    private static void AppendPenaltyBreakdownByType(
        StringBuilder report,
        ScheduleEvaluationResult evaluation,
        ScheduleScoringWeights weights)
    {
        report.AppendLine("PenaltyBreakdownByType:");

        foreach (var group in evaluation.Violations.GroupBy(violation => violation.Type).OrderBy(group => group.Key.ToString()))
        {
            var violations = group.ToArray();
            var estimatedPenalty = new ScheduleScoreCalculator(weights)
                .Calculate(violations)
                .TotalPenalty;

            report.AppendLine($"- {group.Key}:");
            report.AppendLine($"  Count: {violations.Length}");
            report.AppendLine($"  TotalMagnitude: {violations.Sum(violation => violation.Magnitude ?? 0):0.##}");
            report.AppendLine($"  EstimatedPenalty: {estimatedPenalty}");
        }

        report.AppendLine();
    }

    private static void AppendIgnoredAvoidAssignmentsByResource(
        StringBuilder report,
        IReadOnlyList<ResourceAssignmentCount> ignoredAvoidCounts,
        IReadOnlyList<ResourceAssignmentCount> avoidedMorningCounts)
    {
        report.AppendLine("IgnoredAvoidAssignmentsByResource:");

        foreach (var item in ignoredAvoidCounts)
        {
            var avoidedMorningCount = avoidedMorningCounts.Single(count => count.ResourceId == item.ResourceId).Count;
            report.AppendLine($"- {item.ResourceName}: IgnoredAvoidAssignments={item.Count}, AvoidedMorningAssignments={avoidedMorningCount}");
        }
    }

    private static IReadOnlyDictionary<Guid, double> CalculateAssignedHoursByResource(
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        var shiftsById = scenario.Shifts.ToDictionary(shift => shift.Id);

        return scenario.Resources.ToDictionary(
            resource => resource.Id,
            resource => candidate.Assignments
                .Where(assignment => assignment.ResourceId == resource.Id)
                .Sum(assignment => GetShiftHours(shiftsById[assignment.ShiftId])));
    }

    private static TargetGapMetrics CalculateTargetGap(
        ExperimentScenario scenario,
        IReadOnlyDictionary<Guid, double> assignedHoursByResource)
    {
        var totalOverTargetHours = 0.0;
        var totalUnderTargetHours = 0.0;

        foreach (var demand in scenario.ResourceWorkloadDemands)
        {
            var assignedHours = assignedHoursByResource.GetValueOrDefault(demand.ResourceId, 0.0);

            if (assignedHours > demand.EffectiveTargetHours + HoursTolerance)
            {
                totalOverTargetHours += assignedHours - demand.EffectiveTargetHours;
                continue;
            }

            if (assignedHours + HoursTolerance < demand.EffectiveTargetHours)
            {
                totalUnderTargetHours += demand.EffectiveTargetHours - assignedHours;
            }
        }

        return new TargetGapMetrics(totalOverTargetHours, totalUnderTargetHours);
    }

    private static IReadOnlyList<ResourceAssignmentCount> CalculateIgnoredAvoidAssignmentCountsByResource(
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        var shiftsById = scenario.Shifts.ToDictionary(shift => shift.Id);

        return scenario.Resources
            .Select(resource => new ResourceAssignmentCount(
                resource.Id,
                resource.Name,
                candidate.Assignments
                    .Where(assignment => assignment.ResourceId == resource.Id)
                    .Select(assignment => shiftsById[assignment.ShiftId])
                    .Count(shift => HasPreferenceForShift(scenario, resource.Id, shift, ResourcePreferenceType.Avoid))))
            .ToArray();
    }

    private static IReadOnlyList<ResourceAssignmentCount> CalculateAvoidedMorningAssignmentCountsByResource(
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        var shiftsById = scenario.Shifts.ToDictionary(shift => shift.Id);

        return scenario.Resources
            .Select(resource => new ResourceAssignmentCount(
                resource.Id,
                resource.Name,
                candidate.Assignments
                    .Where(assignment => assignment.ResourceId == resource.Id)
                    .Select(assignment => shiftsById[assignment.ShiftId])
                    .Where(IsWeekdayMorning)
                    .Count(shift => HasPreferenceForShift(scenario, resource.Id, shift, ResourcePreferenceType.Avoid))))
            .ToArray();
    }

    private static WeekdayMorningCoverageMetrics CalculateWeekdayMorningCoverageMetrics(
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        var shiftsById = scenario.Shifts.ToDictionary(shift => shift.Id);
        var weekdayMorningShifts = scenario.Shifts
            .Where(IsWeekdayMorning)
            .ToArray();

        var assignmentCountByShiftId = candidate.Assignments
            .GroupBy(assignment => assignment.ShiftId)
            .ToDictionary(group => group.Key, group => group.Count());

        var totalWeekdayMorningAssignments = 0;
        var preferredMorningAssignments = 0;
        var avoidedMorningAssignments = 0;
        var weekdayMorningShiftsAboveMinimumCount = 0;
        var maxWeekdayMorningAssignmentsForSingleShift = 0;

        foreach (var shift in weekdayMorningShifts)
        {
            assignmentCountByShiftId.TryGetValue(shift.Id, out var assignmentCount);

            totalWeekdayMorningAssignments += assignmentCount;
            maxWeekdayMorningAssignmentsForSingleShift = Math.Max(
                maxWeekdayMorningAssignmentsForSingleShift,
                assignmentCount);

            if (assignmentCount > shift.MinResourceCount)
            {
                weekdayMorningShiftsAboveMinimumCount++;
            }
        }

        foreach (var assignment in candidate.Assignments)
        {
            var shift = shiftsById[assignment.ShiftId];

            if (!IsWeekdayMorning(shift))
            {
                continue;
            }

            if (HasPreferenceForShift(scenario, assignment.ResourceId, shift, ResourcePreferenceType.Prefer))
            {
                preferredMorningAssignments++;
            }

            if (HasPreferenceForShift(scenario, assignment.ResourceId, shift, ResourcePreferenceType.Avoid))
            {
                avoidedMorningAssignments++;
            }
        }

        var totalMinimumWeekdayMorningAssignments = weekdayMorningShifts
            .Sum(shift => shift.MinResourceCount);

        var totalMaximumWeekdayMorningAssignments = weekdayMorningShifts
            .Sum(shift => shift.MaxResourceCount);

        var theoreticalMinimumAvoidedMorningAssignments = weekdayMorningShifts
            .Sum(shift => Math.Max(0, shift.MinResourceCount - CountPreferredResourcesForShift(scenario, shift)));

        return new WeekdayMorningCoverageMetrics(
            WeekdayMorningShiftCount: weekdayMorningShifts.Length,
            TotalWeekdayMorningAssignments: totalWeekdayMorningAssignments,
            TotalMinimumWeekdayMorningAssignments: totalMinimumWeekdayMorningAssignments,
            TotalMaximumWeekdayMorningAssignments: totalMaximumWeekdayMorningAssignments,
            MorningAssignmentsAboveMinimum: totalWeekdayMorningAssignments - totalMinimumWeekdayMorningAssignments,
            WeekdayMorningShiftsAboveMinimumCount: weekdayMorningShiftsAboveMinimumCount,
            MaxWeekdayMorningAssignmentsForSingleShift: maxWeekdayMorningAssignmentsForSingleShift,
            PreferredMorningAssignments: preferredMorningAssignments,
            AvoidedMorningAssignments: avoidedMorningAssignments,
            TheoreticalMinimumAvoidedMorningAssignments: theoreticalMinimumAvoidedMorningAssignments,
            AvoidedMorningAssignmentsAboveTheoreticalMinimum: avoidedMorningAssignments - theoreticalMinimumAvoidedMorningAssignments);
    }

    private static RequestedPreferredFulfillmentMetrics CalculateRequestedPreferredFulfillmentMetrics(
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        var assignedKeys = candidate.Assignments
            .Select(assignment => (assignment.ResourceId, assignment.ShiftId))
            .ToHashSet();

        var preferredRequests = scenario.ResourcePreferences
            .Where(preference => preference.Type == ResourcePreferenceType.Prefer)
            .Select(preference => new
            {
                preference.ResourceId,
                Shift = scenario.Shifts.Single(shift =>
                    shift.StartUtc == preference.StartUtc &&
                    shift.EndUtc == preference.EndUtc)
            })
            .GroupBy(request => new
            {
                request.ResourceId,
                request.Shift.Id
            })
            .Select(group => group.First())
            .ToArray();

        var assignedPreferredRequests = preferredRequests
            .Where(request => assignedKeys.Contains((request.ResourceId, request.Shift.Id)))
            .ToArray();

        var unsatisfiedPreferredRequests = preferredRequests
            .Where(request => !assignedKeys.Contains((request.ResourceId, request.Shift.Id)))
            .ToArray();

        return new RequestedPreferredFulfillmentMetrics(
            RequestedPreferredShiftCount: preferredRequests.Length,
            AssignedRequestedPreferredShiftCount: assignedPreferredRequests.Length,
            UnsatisfiedRequestedPreferredShiftCount: unsatisfiedPreferredRequests.Length,
            TotalRequestedPreferredHours: preferredRequests.Sum(request => GetShiftHours(request.Shift)),
            AssignedRequestedPreferredHours: assignedPreferredRequests.Sum(request => GetShiftHours(request.Shift)),
            UnsatisfiedRequestedPreferredHours: unsatisfiedPreferredRequests.Sum(request => GetShiftHours(request.Shift)));
    }

    private static SequenceAssignmentMetrics CalculateSequenceAssignmentMetrics(
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        var shiftsById = scenario.Shifts.ToDictionary(shift => shift.Id);
        var classifier = new ShiftSequenceClassifier();

        var totalAfternoonToMorning = 0;
        var totalNightToAfternoon = 0;
        var maxAfternoonToMorning = 0;
        var maxNightToAfternoon = 0;
        var maxTotal = 0;
        var resourcesWithAny = 0;

        foreach (var resource in scenario.Resources)
        {
            var assignedShifts = candidate.Assignments
                .Where(assignment => assignment.ResourceId == resource.Id)
                .Select(assignment => shiftsById[assignment.ShiftId])
                .OrderBy(shift => shift.StartUtc)
                .ToArray();

            var resourceAfternoonToMorning = 0;
            var resourceNightToAfternoon = 0;

            for (var i = 0; i < assignedShifts.Length - 1; i++)
            {
                var sequenceType = classifier.Classify(
                    assignedShifts[i],
                    assignedShifts[i + 1]);

                if (sequenceType == ShiftSequenceType.AfternoonToMorning)
                {
                    resourceAfternoonToMorning++;
                    totalAfternoonToMorning++;
                    continue;
                }

                if (sequenceType == ShiftSequenceType.NightToAfternoon)
                {
                    resourceNightToAfternoon++;
                    totalNightToAfternoon++;
                }
            }

            var resourceTotal = resourceAfternoonToMorning + resourceNightToAfternoon;

            if (resourceTotal > 0)
            {
                resourcesWithAny++;
            }

            maxAfternoonToMorning = Math.Max(maxAfternoonToMorning, resourceAfternoonToMorning);
            maxNightToAfternoon = Math.Max(maxNightToAfternoon, resourceNightToAfternoon);
            maxTotal = Math.Max(maxTotal, resourceTotal);
        }

        return new SequenceAssignmentMetrics(
            totalAfternoonToMorning,
            totalNightToAfternoon,
            totalAfternoonToMorning + totalNightToAfternoon,
            maxAfternoonToMorning,
            maxNightToAfternoon,
            maxTotal,
            resourcesWithAny);
    }

    private static AssignedHoursBalanceMetrics CalculateAssignedHoursBalanceMetrics(
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        var assignedHoursByResource = CalculateAssignedHoursByResource(scenario, candidate);
        var averageAssignedHours = assignedHoursByResource.Values.Sum() / scenario.Resources.Count;
        var toleranceHours = scenario.Problem.MaximumAssignedHoursDeviationFromAverageHours ?? 0.0;

        var deviations = scenario.Resources
            .Select(resource => Math.Abs(assignedHoursByResource.GetValueOrDefault(resource.Id, 0.0) - averageAssignedHours))
            .ToArray();

        var excessDeviationHours = deviations
            .Select(deviation => Math.Max(0.0, deviation - toleranceHours))
            .ToArray();

        return new AssignedHoursBalanceMetrics(
            averageAssignedHours,
            deviations.Max(),
            deviations.Average(),
            excessDeviationHours.Count(excess => excess > HoursTolerance),
            excessDeviationHours.Sum(),
            excessDeviationHours.Max());
    }

    private static int CountPotentialSequenceTemplatePairs(
        IReadOnlyList<Shift> shifts,
        ShiftSequenceType sequenceType)
    {
        var classifier = new ShiftSequenceClassifier();
        var count = 0;

        foreach (var previousShift in shifts)
        {
            foreach (var nextShift in shifts)
            {
                if (previousShift.Id == nextShift.Id)
                {
                    continue;
                }

                if (classifier.Classify(previousShift, nextShift) == sequenceType)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static int CountAvailableResourcesForShift(
        ExperimentScenario scenario,
        Shift shift)
    {
        return scenario.AvailabilityWindows
            .Where(window => window.Covers(shift))
            .Select(window => window.ResourceId)
            .Distinct()
            .Count();
    }

    private static int CountPreferredResourcesForShift(
        ExperimentScenario scenario,
        Shift shift)
    {
        return CountResourcesWithPreferenceForShift(scenario, shift, ResourcePreferenceType.Prefer);
    }

    private static int CountAvoidingResourcesForShift(
        ExperimentScenario scenario,
        Shift shift)
    {
        return CountResourcesWithPreferenceForShift(scenario, shift, ResourcePreferenceType.Avoid);
    }

    private static int CountResourcesWithPreferenceForShift(
        ExperimentScenario scenario,
        Shift shift,
        ResourcePreferenceType preferenceType)
    {
        return scenario.ResourcePreferences
            .Where(preference => preference.Type == preferenceType)
            .Where(preference => Overlaps(preference.StartUtc, preference.EndUtc, shift.StartUtc, shift.EndUtc))
            .Select(preference => preference.ResourceId)
            .Distinct()
            .Count();
    }

    private static double CalculateTotalMinimumCapacityHours(SchedulingProblem problem)
    {
        return problem.Shifts.Sum(shift => GetShiftHours(shift) * shift.MinResourceCount);
    }

    private static double CalculateTotalMaximumCapacityHours(SchedulingProblem problem)
    {
        return problem.Shifts.Sum(shift => GetShiftHours(shift) * shift.MaxResourceCount);
    }

    private static double GetSubmittedPreferredHours(
        Resource resource,
        IReadOnlyList<Shift> shifts,
        IReadOnlyCollection<ResourcePreference> preferences)
    {
        return preferences
            .Where(preference => preference.ResourceId == resource.Id)
            .Where(preference => preference.Type == ResourcePreferenceType.Prefer)
            .Sum(preference =>
            {
                var shift = shifts.Single(candidate =>
                    candidate.StartUtc == preference.StartUtc &&
                    candidate.EndUtc == preference.EndUtc);

                return GetShiftHours(shift);
            });
    }

    private static double GetShiftHours(Shift shift)
    {
        return (shift.EndUtc - shift.StartUtc).TotalHours;
    }

    private static int CountViolations(
        ScheduleEvaluationResult evaluation,
        ConstraintViolationType type)
    {
        return evaluation.Violations.Count(violation => violation.Type == type);
    }

    private static bool HasPreferenceForShift(
        ExperimentScenario scenario,
        Guid resourceId,
        Shift shift,
        ResourcePreferenceType preferenceType)
    {
        return scenario.ResourcePreferences
            .Where(preference => preference.ResourceId == resourceId)
            .Where(preference => preference.Type == preferenceType)
            .Any(preference => Overlaps(preference.StartUtc, preference.EndUtc, shift.StartUtc, shift.EndUtc));
    }

    private static bool IsWeekday(Shift shift)
    {
        var dayOfWeek = DateOnly.FromDateTime(shift.StartUtc).DayOfWeek;

        return dayOfWeek is not DayOfWeek.Friday and not DayOfWeek.Saturday;
    }

    private static bool IsWeekdayMorning(Shift shift)
    {
        return IsWeekday(shift) && shift.Kind == ShiftKind.Morning;
    }

    private static bool IsWeekdayMorningOn(
        Shift shift,
        params DayOfWeek[] daysOfWeek)
    {
        return IsWeekdayMorning(shift) &&
               daysOfWeek.Contains(DateOnly.FromDateTime(shift.StartUtc).DayOfWeek);
    }

    private static bool IsWeekdayAfternoonOn(
        Shift shift,
        params DayOfWeek[] daysOfWeek)
    {
        return IsWeekday(shift) &&
               shift.Kind == ShiftKind.Afternoon &&
               daysOfWeek.Contains(DateOnly.FromDateTime(shift.StartUtc).DayOfWeek);
    }

    private static bool IsRegularNightOn(
        Shift shift,
        params DayOfWeek[] daysOfWeek)
    {
        return shift.Kind == ShiftKind.Night &&
               shift.NightShiftCategory == NightShiftCategory.Regular &&
               daysOfWeek.Contains(DateOnly.FromDateTime(shift.StartUtc).DayOfWeek);
    }

    private static bool IsFridayMorning(Shift shift)
    {
        return DateOnly.FromDateTime(shift.StartUtc).DayOfWeek == DayOfWeek.Friday &&
               shift.Kind == ShiftKind.Morning;
    }

    private static bool IsFridayAfternoon(Shift shift)
    {
        return DateOnly.FromDateTime(shift.StartUtc).DayOfWeek == DayOfWeek.Friday &&
               shift.Kind == ShiftKind.Afternoon;
    }

    private static bool IsFridayNight(Shift shift)
    {
        return shift.NightShiftCategory == NightShiftCategory.FridayNight;
    }

    private static bool IsSaturdayMorning(Shift shift)
    {
        return DateOnly.FromDateTime(shift.StartUtc).DayOfWeek == DayOfWeek.Saturday &&
               shift.Kind == ShiftKind.Morning;
    }

    private static bool IsSaturdayAfternoon(Shift shift)
    {
        return DateOnly.FromDateTime(shift.StartUtc).DayOfWeek == DayOfWeek.Saturday &&
               shift.Kind == ShiftKind.Afternoon;
    }

    private static bool IsMotzeiShabbatNight(Shift shift)
    {
        return shift.NightShiftCategory == NightShiftCategory.MotzeiShabbatNight;
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

    private static void AssertCandidateReferencesKnownProblemEntities(
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        var resourceIds = scenario.Resources.Select(resource => resource.Id).ToHashSet();
        var shiftIds = scenario.Shifts.Select(shift => shift.Id).ToHashSet();

        Assert.All(candidate.Assignments, assignment =>
        {
            Assert.Contains(assignment.ResourceId, resourceIds);
            Assert.Contains(assignment.ShiftId, shiftIds);
        });
    }

    private static void AssertNoBasicStructuralViolations(ScheduleEvaluationResult evaluation)
    {
        Assert.Equal(0, CountViolations(evaluation, ConstraintViolationType.ResourceUnavailable));
        Assert.Equal(0, CountViolations(evaluation, ConstraintViolationType.ResourceAssignedToOverlappingShifts));
        Assert.Equal(0, CountViolations(evaluation, ConstraintViolationType.AssignedWithoutRequiredPreference));
        Assert.Equal(0, CountViolations(evaluation, ConstraintViolationType.ShiftUnderstaffed));
        Assert.Equal(0, CountViolations(evaluation, ConstraintViolationType.ShiftOverstaffed));
    }

    private sealed record AvoidBurdenScenarioCase(
        string Name,
        ExperimentScenario Scenario);

    private sealed record AvoidBurdenMultiSeedRunSummary(
        string ScenarioName,
        string VariantName,
        int Seed,
        int AvoidBurdenPenaltyPerHour,
        bool IsFeasible,
        int TotalPenalty,
        int HardViolationCount,
        int SoftViolationCount,
        int AssignmentCount,
        double TotalAssignedHours,
        double TotalOverTargetHours,
        double TotalUnderTargetHours,
        int IgnoredAvoidPreferenceViolationCount,
        int TotalIgnoredAvoidAssignments,
        int MaxIgnoredAvoidAssignmentsForSingleResource,
        double AverageIgnoredAvoidAssignmentsPerResource,
        int ResourcesWithIgnoredAvoidAssignmentsCount,
        int ResourceIgnoredAvoidPreferenceBurdenViolationCount,
        double TotalIgnoredAvoidBurdenMagnitude,
        double MaxIgnoredAvoidBurdenMagnitudeForSingleResource,
        int TotalWeekdayMorningAssignments,
        int MorningAssignmentsAboveMinimum,
        int PreferredMorningAssignments,
        int AvoidedMorningAssignments,
        int AvoidedMorningAssignmentsAboveTheoreticalMinimum,
        int ResourcesOutsideBalanceToleranceCount,
        double BalanceViolationTotalMagnitudeFromEvaluation,
        int TotalSupportedSequences,
        int ShiftSequenceQuotaExceededViolationCount,
        int FirstBestSoFarTotalPenalty,
        int LastBestSoFarTotalPenalty,
        int FirstFeasibleCandidateCount,
        int LastFeasibleCandidateCount);

    private sealed record ScoringWeightVariant(
        string Name,
        ScheduleScoringWeights Weights);

    private sealed record ExperimentScenario(
        SchedulingProblem Problem,
        IReadOnlyList<Resource> Resources,
        IReadOnlyList<Shift> Shifts,
        IReadOnlyList<AvailabilityWindow> AvailabilityWindows,
        IReadOnlyList<ResourcePreference> ResourcePreferences,
        IReadOnlyList<ResourceWorkloadDemand> ResourceWorkloadDemands);

    private sealed record TargetGapMetrics(
        double TotalOverTargetHours,
        double TotalUnderTargetHours);

    private sealed record WeekdayMorningCoverageMetrics(
        int WeekdayMorningShiftCount,
        int TotalWeekdayMorningAssignments,
        int TotalMinimumWeekdayMorningAssignments,
        int TotalMaximumWeekdayMorningAssignments,
        int MorningAssignmentsAboveMinimum,
        int WeekdayMorningShiftsAboveMinimumCount,
        int MaxWeekdayMorningAssignmentsForSingleShift,
        int PreferredMorningAssignments,
        int AvoidedMorningAssignments,
        int TheoreticalMinimumAvoidedMorningAssignments,
        int AvoidedMorningAssignmentsAboveTheoreticalMinimum);

    private sealed record RequestedPreferredFulfillmentMetrics(
        int RequestedPreferredShiftCount,
        int AssignedRequestedPreferredShiftCount,
        int UnsatisfiedRequestedPreferredShiftCount,
        double TotalRequestedPreferredHours,
        double AssignedRequestedPreferredHours,
        double UnsatisfiedRequestedPreferredHours);

    private sealed record ResourceAssignmentCount(
        Guid ResourceId,
        string ResourceName,
        int Count);

    private sealed record SequenceAssignmentMetrics(
        int TotalAfternoonToMorningSequences,
        int TotalNightToAfternoonSequences,
        int TotalSupportedSequences,
        int MaxAfternoonToMorningSequencesForSingleResource,
        int MaxNightToAfternoonSequencesForSingleResource,
        int MaxTotalSequencesForSingleResource,
        int ResourcesWithAnySequenceCount);

    private sealed record AssignedHoursBalanceMetrics(
        double AverageAssignedHours,
        double MaxAssignedHoursDeviationFromAverage,
        double AverageAssignedHoursDeviationFromAverage,
        int ResourcesOutsideBalanceToleranceCount,
        double TotalExcessDeviationHours,
        double MaxExcessDeviationHours);
}
