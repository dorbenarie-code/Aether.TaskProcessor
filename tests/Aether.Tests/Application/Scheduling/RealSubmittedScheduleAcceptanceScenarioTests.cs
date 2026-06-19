using System.Globalization;
using System.Text;
using Aether.Application.Scheduling.Contracts;
using Aether.Application.Scheduling.Optimization;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class RealSubmittedScheduleAcceptanceScenarioTests
{
    private const int ResourceCount = 19;
    private const int DaysInSchedule = 14;
    private const int ExpectedShiftCount = DaysInSchedule * 3;
    private const double ExpectedTotalEffectiveTargetHours = 736.0;
    private const double BalanceToleranceHours = 5.0;
    private const int PopulationSize = 120;
    private const int GenerationCount = 30;
    private const int Seed = 20260603;

    [Fact]
    [Trait("Category", "SchedulingExperiment")]
    public void CleanGeneticOptimizer_ShouldAcceptRealSubmittedScheduleScenario()
    {
        var scenario = CreateScenario();
        var weights = ScheduleScoringWeights.CreateDefault();
        var diagnosticsSink = new CollectingGeneticOptimizerDiagnosticsSink();

        AssertScenarioShape(scenario);

        var result = new GeneticScheduleOptimizer(
                populationSize: PopulationSize,
                seed: Seed,
                generationCount: GenerationCount,
                eliteCount: 1,
                tournamentSize: 3,
                diagnosticsSink: diagnosticsSink,
                evolutionMode: GeneticEvolutionMode.Clean,
                scoringWeights: weights)
            .Optimize(scenario.Problem);

        var report = FormatReport(
            scenario,
            weights,
            result,
            diagnosticsSink.Diagnostics);

        System.Console.WriteLine(report);

        Assert.Contains("Stage 8.0 Real Submitted Schedule Acceptance Scenario", report);
        Assert.Contains("Stage80ScheduleTable:", report);
        Assert.Contains("PenaltyBreakdownByType:", report);
        Assert.Contains("AssignedHoursByWorker:", report);
        Assert.Contains("TargetGapByWorker:", report);
        Assert.Contains("IgnoredAvoidAssignmentsByWorker:", report);
        Assert.Contains("NightAssignmentsByWorker:", report);
        Assert.Contains("GenerationDiagnostics:", report);

        Assert.Equal(GenerationCount + 1, diagnosticsSink.Diagnostics.Count);
        Assert.True(
            diagnosticsSink.Diagnostics[^1].BestSoFarTotalPenalty <=
            diagnosticsSink.Diagnostics[0].BestSoFarTotalPenalty,
            report);

        AssertCandidateReferencesKnownProblemEntities(scenario, result.Candidate);
        AssertNoBasicStructuralViolations(result.Evaluation);
        Assert.True(result.Evaluation.IsFeasible, report);
    }


    [Fact]
    [Trait("Category", "SchedulingExperiment")]
    public void CleanGeneticOptimizer_ShouldPrintGenerationCountSensitivityReport()
    {
        var scenario = CreateScenario();
        var weights = ScheduleScoringWeights.CreateDefault();

        var generationCounts = new[]
        {
            30,
            50,
            100
        };

        var report = new StringBuilder();

        report.AppendLine("Stage 8.0A Real Submitted Schedule Generation Count Sensitivity");
        report.AppendLine("Mode: Clean GA");
        report.AppendLine($"Seed: {Seed}");
        report.AppendLine($"PopulationSize: {PopulationSize}");
        report.AppendLine($"GenerationCountVariants: {string.Join(", ", generationCounts)}");
        report.AppendLine($"ResourceCount: {scenario.Resources.Count}");
        report.AppendLine($"ShiftCount: {scenario.Shifts.Count}");
        report.AppendLine($"TotalEffectiveTargetHours: {scenario.ResourceWorkloadDemands.Sum(demand => demand.EffectiveTargetHours):0.##}");
        report.AppendLine($"ResourceIgnoredAvoidPreferenceBurdenPenaltyPerHour: {weights.ResourceIgnoredAvoidPreferenceBurdenPenaltyPerHour}");
        report.AppendLine();

        var previousTotalPenalty = int.MaxValue;

        foreach (var generationCount in generationCounts)
        {
            var diagnosticsSink = new CollectingGeneticOptimizerDiagnosticsSink();

            var result = new GeneticScheduleOptimizer(
                    populationSize: PopulationSize,
                    seed: Seed,
                    generationCount: generationCount,
                    eliteCount: 1,
                    tournamentSize: 3,
                    diagnosticsSink: diagnosticsSink,
                    evolutionMode: GeneticEvolutionMode.Clean,
                    scoringWeights: weights)
                .Optimize(scenario.Problem);

            Assert.Equal(generationCount + 1, diagnosticsSink.Diagnostics.Count);
            AssertCandidateReferencesKnownProblemEntities(scenario, result.Candidate);
            AssertNoBasicStructuralViolations(result.Evaluation);
            Assert.True(result.Evaluation.IsFeasible);

            Assert.True(
                diagnosticsSink.Diagnostics[^1].BestSoFarTotalPenalty <=
                diagnosticsSink.Diagnostics[0].BestSoFarTotalPenalty,
                $"GenerationCount {generationCount} should not return a best-so-far penalty worse than generation 0.");

            var assignedHours = CalculateAssignedHoursByResource(scenario, result.Candidate);
            var ignoredAvoidCounts = CalculateIgnoredAvoidCounts(scenario, result.Candidate);
            var nightCounts = CalculateNightCounts(scenario, result.Candidate);

            report.AppendLine("GenerationSensitivityRun:");
            report.AppendLine($"GenerationCount: {generationCount}");
            report.AppendLine($"IsFeasible: {result.Evaluation.IsFeasible}");
            report.AppendLine($"Score.Value: {result.Evaluation.Score.Value}");
            report.AppendLine($"TotalPenalty: {result.Evaluation.Score.TotalPenalty}");
            report.AppendLine($"HardViolationCount: {result.Evaluation.Score.HardViolationCount}");
            report.AppendLine($"SoftViolationCount: {result.Evaluation.Score.SoftViolationCount}");
            report.AppendLine($"Assignments.Count: {result.Candidate.Assignments.Count}");
            report.AppendLine($"TotalAssignedHours: {assignedHours.Values.Sum():0.##}");
            report.AppendLine($"IgnoredAvoidPreferenceViolationCount: {CountViolations(result.Evaluation, ConstraintViolationType.IgnoredAvoidPreference)}");
            report.AppendLine($"ResourceIgnoredAvoidPreferenceBurdenViolationCount: {CountViolations(result.Evaluation, ConstraintViolationType.ResourceIgnoredAvoidPreferenceBurden)}");
            report.AppendLine($"ResourceAssignedHoursBalanceExceededViolationCount: {CountViolations(result.Evaluation, ConstraintViolationType.ResourceAssignedHoursBalanceExceeded)}");
            report.AppendLine($"ResourceRequestedPreferredHoursNotSatisfiedViolationCount: {CountViolations(result.Evaluation, ConstraintViolationType.ResourceRequestedPreferredHoursNotSatisfied)}");
            report.AppendLine($"ResourceEffectiveTargetAssignedHoursAboveTargetViolationCount: {CountViolations(result.Evaluation, ConstraintViolationType.ResourceEffectiveTargetAssignedHoursAboveTarget)}");
            report.AppendLine($"ResourceEffectiveTargetAssignedHoursBelowTargetViolationCount: {CountViolations(result.Evaluation, ConstraintViolationType.ResourceEffectiveTargetAssignedHoursBelowTarget)}");
            report.AppendLine($"MaxIgnoredAvoidAssignmentsForSingleWorker: {ignoredAvoidCounts.Values.Max()}");
            report.AppendLine($"TotalIgnoredAvoidAssignments: {ignoredAvoidCounts.Values.Sum()}");
            report.AppendLine($"RegularNightAssignments: {nightCounts.Values.Sum(count => count.Regular)}");
            report.AppendLine($"FridayNightAssignments: {nightCounts.Values.Sum(count => count.FridayNight)}");
            report.AppendLine($"MotzeiShabbatAssignments: {nightCounts.Values.Sum(count => count.MotzeiShabbat)}");
            report.AppendLine($"FirstBestSoFarTotalPenalty: {diagnosticsSink.Diagnostics[0].BestSoFarTotalPenalty}");
            report.AppendLine($"MiddleBestSoFarTotalPenalty: {diagnosticsSink.Diagnostics[diagnosticsSink.Diagnostics.Count / 2].BestSoFarTotalPenalty}");
            report.AppendLine($"LastBestSoFarTotalPenalty: {diagnosticsSink.Diagnostics[^1].BestSoFarTotalPenalty}");
            report.AppendLine($"FirstFeasibleCandidateCount: {diagnosticsSink.Diagnostics[0].FeasibleCandidateCount}");
            report.AppendLine($"MiddleFeasibleCandidateCount: {diagnosticsSink.Diagnostics[diagnosticsSink.Diagnostics.Count / 2].FeasibleCandidateCount}");
            report.AppendLine($"LastFeasibleCandidateCount: {diagnosticsSink.Diagnostics[^1].FeasibleCandidateCount}");

            if (previousTotalPenalty != int.MaxValue)
            {
                report.AppendLine($"PenaltyImprovementFromPreviousGenerationVariant: {previousTotalPenalty - result.Evaluation.Score.TotalPenalty}");
            }

            previousTotalPenalty = result.Evaluation.Score.TotalPenalty;
            report.AppendLine();
        }

        var output = report.ToString();

        System.Console.WriteLine(output);

        Assert.Contains("Stage 8.0A Real Submitted Schedule Generation Count Sensitivity", output);
        Assert.Contains("GenerationCount: 30", output);
        Assert.Contains("GenerationCount: 50", output);
        Assert.Contains("GenerationCount: 100", output);
        Assert.Contains("PenaltyImprovementFromPreviousGenerationVariant:", output);
    }


    [Fact]
    [Trait("Category", "SchedulingExperiment")]
    public void CleanGeneticOptimizer_ShouldPrintRealSubmittedAcceptanceMultiSeedStabilityReport()
    {
        const int multiSeedGenerationCount = 100;

        var scenario = CreateScenario();
        var weights = ScheduleScoringWeights.CreateDefault();

        var seeds = new[]
        {
            20260603,
            20260604,
            20260605
        };

        var summaries = new List<(
            int Seed,
            bool IsFeasible,
            int TotalPenalty,
            int HardViolationCount,
            int SoftViolationCount,
            int AssignmentCount,
            double TotalAssignedHours,
            int TotalIgnoredAvoidAssignments,
            int MaxIgnoredAvoidAssignmentsForSingleWorker,
            int BalanceViolationCount,
            int SequenceQuotaViolationCount,
            int MonthlyNightQuotaViolationCount,
            int RegularNightAssignments,
            int FridayNightAssignments,
            int MotzeiShabbatAssignments,
            int FirstBestPenalty,
            int MiddleBestPenalty,
            int LastBestPenalty,
            int FirstFeasibleCount,
            int MiddleFeasibleCount,
            int LastFeasibleCount)>();

        var report = new StringBuilder();

        report.AppendLine("Stage 8.0B Real Submitted Schedule Acceptance Multi-Seed Stability");
        report.AppendLine("Mode: Clean GA");
        report.AppendLine($"PopulationSize: {PopulationSize}");
        report.AppendLine($"GenerationCount: {multiSeedGenerationCount}");
        report.AppendLine($"SeedCount: {seeds.Length}");
        report.AppendLine($"ResourceCount: {scenario.Resources.Count}");
        report.AppendLine($"ShiftCount: {scenario.Shifts.Count}");
        report.AppendLine($"TotalEffectiveTargetHours: {scenario.ResourceWorkloadDemands.Sum(demand => demand.EffectiveTargetHours):0.##}");
        report.AppendLine($"ResourceIgnoredAvoidPreferenceBurdenPenaltyPerHour: {weights.ResourceIgnoredAvoidPreferenceBurdenPenaltyPerHour}");
        report.AppendLine();

        foreach (var seed in seeds)
        {
            var diagnosticsSink = new CollectingGeneticOptimizerDiagnosticsSink();

            var result = new GeneticScheduleOptimizer(
                    populationSize: PopulationSize,
                    seed: seed,
                    generationCount: multiSeedGenerationCount,
                    eliteCount: 1,
                    tournamentSize: 3,
                    diagnosticsSink: diagnosticsSink,
                    evolutionMode: GeneticEvolutionMode.Clean,
                    scoringWeights: weights)
                .Optimize(scenario.Problem);

            Assert.Equal(multiSeedGenerationCount + 1, diagnosticsSink.Diagnostics.Count);
            AssertCandidateReferencesKnownProblemEntities(scenario, result.Candidate);
            AssertNoBasicStructuralViolations(result.Evaluation);
            Assert.True(result.Evaluation.IsFeasible);

            Assert.True(
                diagnosticsSink.Diagnostics[^1].BestSoFarTotalPenalty <=
                diagnosticsSink.Diagnostics[0].BestSoFarTotalPenalty,
                $"Seed {seed} should not return a best-so-far penalty worse than generation 0.");

            var assignedHours = CalculateAssignedHoursByResource(scenario, result.Candidate);
            var ignoredAvoidCounts = CalculateIgnoredAvoidCounts(scenario, result.Candidate);
            var nightCounts = CalculateNightCounts(scenario, result.Candidate);

            var summary = (
                Seed: seed,
                IsFeasible: result.Evaluation.IsFeasible,
                TotalPenalty: result.Evaluation.Score.TotalPenalty,
                HardViolationCount: result.Evaluation.Score.HardViolationCount,
                SoftViolationCount: result.Evaluation.Score.SoftViolationCount,
                AssignmentCount: result.Candidate.Assignments.Count,
                TotalAssignedHours: assignedHours.Values.Sum(),
                TotalIgnoredAvoidAssignments: ignoredAvoidCounts.Values.Sum(),
                MaxIgnoredAvoidAssignmentsForSingleWorker: ignoredAvoidCounts.Values.Max(),
                BalanceViolationCount: CountViolations(result.Evaluation, ConstraintViolationType.ResourceAssignedHoursBalanceExceeded),
                SequenceQuotaViolationCount: CountViolations(result.Evaluation, ConstraintViolationType.ShiftSequenceQuotaExceeded),
                MonthlyNightQuotaViolationCount: CountViolations(result.Evaluation, ConstraintViolationType.ResourceMonthlyNightShiftQuotaExceeded),
                RegularNightAssignments: nightCounts.Values.Sum(count => count.Regular),
                FridayNightAssignments: nightCounts.Values.Sum(count => count.FridayNight),
                MotzeiShabbatAssignments: nightCounts.Values.Sum(count => count.MotzeiShabbat),
                FirstBestPenalty: diagnosticsSink.Diagnostics[0].BestSoFarTotalPenalty,
                MiddleBestPenalty: diagnosticsSink.Diagnostics[diagnosticsSink.Diagnostics.Count / 2].BestSoFarTotalPenalty,
                LastBestPenalty: diagnosticsSink.Diagnostics[^1].BestSoFarTotalPenalty,
                FirstFeasibleCount: diagnosticsSink.Diagnostics[0].FeasibleCandidateCount,
                MiddleFeasibleCount: diagnosticsSink.Diagnostics[diagnosticsSink.Diagnostics.Count / 2].FeasibleCandidateCount,
                LastFeasibleCount: diagnosticsSink.Diagnostics[^1].FeasibleCandidateCount);

            summaries.Add(summary);

            report.AppendLine("RealSubmittedAcceptanceSeedSummary:");
            report.AppendLine($"Seed: {summary.Seed}");
            report.AppendLine($"IsFeasible: {summary.IsFeasible}");
            report.AppendLine($"TotalPenalty: {summary.TotalPenalty}");
            report.AppendLine($"HardViolationCount: {summary.HardViolationCount}");
            report.AppendLine($"SoftViolationCount: {summary.SoftViolationCount}");
            report.AppendLine($"Assignments.Count: {summary.AssignmentCount}");
            report.AppendLine($"TotalAssignedHours: {summary.TotalAssignedHours:0.##}");
            report.AppendLine($"TotalIgnoredAvoidAssignments: {summary.TotalIgnoredAvoidAssignments}");
            report.AppendLine($"MaxIgnoredAvoidAssignmentsForSingleWorker: {summary.MaxIgnoredAvoidAssignmentsForSingleWorker}");
            report.AppendLine($"BalanceViolationCount: {summary.BalanceViolationCount}");
            report.AppendLine($"SequenceQuotaViolationCount: {summary.SequenceQuotaViolationCount}");
            report.AppendLine($"MonthlyNightQuotaViolationCount: {summary.MonthlyNightQuotaViolationCount}");
            report.AppendLine($"RegularNightAssignments: {summary.RegularNightAssignments}");
            report.AppendLine($"FridayNightAssignments: {summary.FridayNightAssignments}");
            report.AppendLine($"MotzeiShabbatAssignments: {summary.MotzeiShabbatAssignments}");
            report.AppendLine($"FirstBestSoFarTotalPenalty: {summary.FirstBestPenalty}");
            report.AppendLine($"MiddleBestSoFarTotalPenalty: {summary.MiddleBestPenalty}");
            report.AppendLine($"LastBestSoFarTotalPenalty: {summary.LastBestPenalty}");
            report.AppendLine($"Improvement: {summary.FirstBestPenalty - summary.LastBestPenalty}");
            report.AppendLine($"FirstFeasibleCandidateCount: {summary.FirstFeasibleCount}");
            report.AppendLine($"MiddleFeasibleCandidateCount: {summary.MiddleFeasibleCount}");
            report.AppendLine($"LastFeasibleCandidateCount: {summary.LastFeasibleCount}");
            report.AppendLine();
        }

        report.AppendLine("RealSubmittedAcceptanceMultiSeedAggregateSummary:");
        report.AppendLine($"AllRunsFeasible: {summaries.All(summary => summary.IsFeasible)}");
        report.AppendLine($"AnyRunHasHardViolations: {summaries.Any(summary => summary.HardViolationCount > 0)}");
        report.AppendLine($"AnyRunHasSequenceQuotaViolations: {summaries.Any(summary => summary.SequenceQuotaViolationCount > 0)}");
        report.AppendLine($"AnyRunHasMonthlyNightQuotaViolations: {summaries.Any(summary => summary.MonthlyNightQuotaViolationCount > 0)}");
        report.AppendLine($"AllRunsImprovedBestSoFar: {summaries.All(summary => summary.LastBestPenalty <= summary.FirstBestPenalty)}");
        report.AppendLine($"MinTotalPenalty: {summaries.Min(summary => summary.TotalPenalty)}");
        report.AppendLine($"MaxTotalPenalty: {summaries.Max(summary => summary.TotalPenalty)}");
        report.AppendLine($"AverageTotalPenalty: {summaries.Average(summary => summary.TotalPenalty):0.##}");
        report.AppendLine($"TotalPenaltyRange: {summaries.Max(summary => summary.TotalPenalty) - summaries.Min(summary => summary.TotalPenalty)}");
        report.AppendLine($"MinTotalAssignedHours: {summaries.Min(summary => summary.TotalAssignedHours):0.##}");
        report.AppendLine($"MaxTotalAssignedHours: {summaries.Max(summary => summary.TotalAssignedHours):0.##}");
        report.AppendLine($"AverageTotalAssignedHours: {summaries.Average(summary => summary.TotalAssignedHours):0.##}");
        report.AppendLine($"TotalAssignedHoursRange: {summaries.Max(summary => summary.TotalAssignedHours) - summaries.Min(summary => summary.TotalAssignedHours):0.##}");
        report.AppendLine($"MinSoftViolationCount: {summaries.Min(summary => summary.SoftViolationCount)}");
        report.AppendLine($"MaxSoftViolationCount: {summaries.Max(summary => summary.SoftViolationCount)}");
        report.AppendLine($"AverageSoftViolationCount: {summaries.Average(summary => summary.SoftViolationCount):0.##}");
        report.AppendLine($"MinIgnoredAvoidAssignments: {summaries.Min(summary => summary.TotalIgnoredAvoidAssignments)}");
        report.AppendLine($"MaxIgnoredAvoidAssignments: {summaries.Max(summary => summary.TotalIgnoredAvoidAssignments)}");
        report.AppendLine($"AverageIgnoredAvoidAssignments: {summaries.Average(summary => summary.TotalIgnoredAvoidAssignments):0.##}");
        report.AppendLine($"MaxIgnoredAvoidAssignmentsForSingleWorkerRange: {summaries.Min(summary => summary.MaxIgnoredAvoidAssignmentsForSingleWorker)}..{summaries.Max(summary => summary.MaxIgnoredAvoidAssignmentsForSingleWorker)}");
        report.AppendLine($"MaxBalanceViolationCount: {summaries.Max(summary => summary.BalanceViolationCount)}");
        report.AppendLine($"MinLastFeasibleCandidateCount: {summaries.Min(summary => summary.LastFeasibleCount)}");
        report.AppendLine($"MaxLastFeasibleCandidateCount: {summaries.Max(summary => summary.LastFeasibleCount)}");

        var output = report.ToString();

        System.Console.WriteLine(output);

        Assert.Contains("Stage 8.0B Real Submitted Schedule Acceptance Multi-Seed Stability", output);
        Assert.Contains("RealSubmittedAcceptanceSeedSummary:", output);
        Assert.Contains("RealSubmittedAcceptanceMultiSeedAggregateSummary:", output);
        Assert.Contains("AllRunsFeasible:", output);
        Assert.Contains("AllRunsImprovedBestSoFar:", output);

        Assert.True(summaries.All(summary => summary.IsFeasible), output);
        Assert.True(summaries.All(summary => summary.HardViolationCount == 0), output);
        Assert.True(summaries.All(summary => summary.SequenceQuotaViolationCount == 0), output);
        Assert.True(summaries.All(summary => summary.MonthlyNightQuotaViolationCount == 0), output);
        Assert.True(summaries.All(summary => summary.LastBestPenalty <= summary.FirstBestPenalty), output);
    }


    private static ExperimentScenario CreateScenario()
    {
        var resources = CreateResources();
        var shifts = CreateBiWeeklySequencePressureShifts();
        var submittedShifts = CreateSubmittedShifts(resources);

        var availabilityWindows = new List<AvailabilityWindow>();
        var preferences = new List<ResourcePreference>();

        foreach (var shift in shifts)
        {
            if (IsWeekdayMorning(shift) || IsMotzeiShabbatNight(shift))
            {
                foreach (var resource in resources)
                {
                    AddAvailability(resource, shift, availabilityWindows);

                    AddPreference(
                        resource,
                        shift,
                        HasSubmittedShift(submittedShifts, resource.Id, shift)
                            ? ResourcePreferenceType.Prefer
                            : ResourcePreferenceType.Avoid,
                        preferences);
                }

                continue;
            }

            foreach (var resource in resources)
            {
                if (!HasSubmittedShift(submittedShifts, resource.Id, shift))
                {
                    continue;
                }

                AddAvailability(resource, shift, availabilityWindows);
                AddPreference(resource, shift, ResourcePreferenceType.Prefer, preferences);
            }
        }

        var workloadDemands = CreateWorkloadDemands(
            resources,
            shifts,
            preferences);

        var problem = new SchedulingProblem(
            period: new SchedulePeriod(
                new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc)),
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
            OriginalNamesByResourceId: CreateOriginalNamesByResourceId(resources),
            Shifts: shifts,
            AvailabilityWindows: availabilityWindows,
            ResourcePreferences: preferences,
            ResourceWorkloadDemands: workloadDemands,
            SubmittedShifts: submittedShifts);
    }

    private static IReadOnlyList<Resource> CreateResources()
    {
        return Enumerable
            .Range(1, ResourceCount)
            .Select(index => new Resource(
                CreateGuid(index),
                $"עובד {index:00}",
                hourlyCost: 100m))
            .ToArray();
    }

    private static IReadOnlyDictionary<Guid, string> CreateOriginalNamesByResourceId(
        IReadOnlyList<Resource> resources)
    {
        var names = new[]
        {
            "Worker14", "Worker02", "עיWorker05י", "Worker07 כחלון", "Worker11",
            "Worker03", "Worker10", "Worker12", "Worker13", "Worker09 סעדון",
            "Worker04", "Worker05", "Worker08", "עדנה", "Worker16 אלדר",
            "Worker19 אטיאס", "Worker17 Worker17", "Worker15י", "Worker18 שלו"
        };

        return resources
            .Select((resource, index) => new { resource.Id, Name = names[index] })
            .ToDictionary(item => item.Id, item => item.Name);
    }

    private static IReadOnlyList<SubmittedShift> CreateSubmittedShifts(
        IReadOnlyList<Resource> resources)
    {
        var submissions = new List<SubmittedShift>();

        AddSubmissions(submissions, resources[0], "2026-05-31:A;2026-06-01:M;2026-06-02:N;2026-06-03:A;2026-06-04:M;2026-06-07:A;2026-06-08:M;2026-06-09:A;2026-06-10:A;2026-06-11:M");
        AddSubmissions(submissions, resources[1], "2026-05-31:M,A;2026-06-07:M,A");
        AddSubmissions(submissions, resources[2], "2026-05-31:N;2026-06-04:M,A;2026-06-05:M;2026-06-07:N;2026-06-08:N;2026-06-09:M");
        AddSubmissions(submissions, resources[3], "2026-05-31:M;2026-06-01:M;2026-06-02:M;2026-06-03:M;2026-06-04:M;2026-06-07:M;2026-06-08:M;2026-06-09:M;2026-06-10:M;2026-06-11:M");
        AddSubmissions(submissions, resources[4], "2026-05-31:A,N;2026-06-02:M,A;2026-06-04:N;2026-06-05:M;2026-06-06:N;2026-06-07:A,N;2026-06-09:M,A;2026-06-11:N;2026-06-12:M;2026-06-13:N");
        AddSubmissions(submissions, resources[5], "2026-05-31:A;2026-06-03:M;2026-06-05:M;2026-06-07:A;2026-06-10:M;2026-06-12:M");
        AddSubmissions(submissions, resources[6], "2026-05-31:M,A;2026-06-01:A;2026-06-02:A;2026-06-03:M;2026-06-07:A;2026-06-08:M;2026-06-09:M;2026-06-10:A;2026-06-11:A");
        AddSubmissions(submissions, resources[7], "2026-05-31:N;2026-06-01:M,A;2026-06-02:N;2026-06-03:A,N;2026-06-04:M,A;2026-06-07:M,A,N;2026-06-08:M,A;2026-06-10:A,N;2026-06-11:M,A");
        AddSubmissions(submissions, resources[8], "2026-06-01:M;2026-06-02:N;2026-06-03:A;2026-06-04:M;2026-06-07:M;2026-06-08:M;2026-06-09:A;2026-06-10:N;2026-06-11:A");
        AddSubmissions(submissions, resources[9], "2026-06-01:N;2026-06-02:M,N;2026-06-03:M,A,N;2026-06-04:M,A,N;2026-06-07:M,N;2026-06-08:M,A,N;2026-06-09:M,N;2026-06-10:A,N;2026-06-11:M,A,N");
        AddSubmissions(submissions, resources[10], "2026-06-01:M;2026-06-02:M;2026-06-03:A;2026-06-04:M;2026-06-07:M;2026-06-08:M;2026-06-09:M;2026-06-10:A;2026-06-11:M");
        AddSubmissions(submissions, resources[11], "2026-05-31:A;2026-06-01:A;2026-06-02:M,N;2026-06-03:A;2026-06-04:M;2026-06-07:M,N;2026-06-08:A;2026-06-09:M;2026-06-11:A;2026-06-12:M");
        AddSubmissions(submissions, resources[12], "2026-05-31:M;2026-06-02:A;2026-06-03:A;2026-06-07:M;2026-06-09:A;2026-06-10:M");
        AddSubmissions(submissions, resources[13], "2026-06-03:A;2026-06-04:M;2026-06-06:N;2026-06-07:A;2026-06-08:M");
        AddSubmissions(submissions, resources[14], "2026-05-31:M,N;2026-06-01:A,N;2026-06-02:A;2026-06-03:N;2026-06-04:A,N;2026-06-05:M;2026-06-06:N;2026-06-07:M,N;2026-06-08:A,N;2026-06-10:A,N;2026-06-11:M,A,N;2026-06-12:M;2026-06-13:N");
        AddSubmissions(submissions, resources[15], "2026-05-31:M,A,N;2026-06-01:N;2026-06-02:N;2026-06-03:M,A,N;2026-06-04:N;2026-06-05:M;2026-06-06:N;2026-06-07:M,A,N;2026-06-08:N;2026-06-09:N;2026-06-10:M,N;2026-06-11:N;2026-06-12:M;2026-06-13:N");
        AddSubmissions(submissions, resources[16], "2026-06-03:M;2026-06-05:N;2026-06-06:M;2026-06-10:M;2026-06-12:N;2026-06-13:M");
        AddSubmissions(submissions, resources[17], "2026-05-31:M,N;2026-06-01:A;2026-06-02:M");
        AddSubmissions(submissions, resources[18], "2026-05-31:A;2026-06-01:N;2026-06-02:M;2026-06-04:A,N;2026-06-05:M;2026-06-07:M;2026-06-09:N;2026-06-11:M,A,N;2026-06-12:M");

        return submissions.Distinct().ToArray();
    }

    private static void AddSubmissions(
        ICollection<SubmittedShift> submissions,
        Resource resource,
        string encoded)
    {
        foreach (var block in encoded.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = block.Split(':', StringSplitOptions.TrimEntries);
            var date = DateOnly.ParseExact(parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture);

            foreach (var token in parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                submissions.Add(new SubmittedShift(
                    resource.Id,
                    date,
                    token switch
                    {
                        "M" => ShiftKind.Morning,
                        "A" => ShiftKind.Afternoon,
                        "N" => ShiftKind.Night,
                        _ => throw new ArgumentOutOfRangeException(nameof(token), token, "Unsupported token.")
                    }));
            }
        }
    }

    private static IReadOnlyList<Shift> CreateBiWeeklySequencePressureShifts()
    {
        var shifts = new List<Shift>();

        for (var dayOffset = 0; dayOffset < DaysInSchedule; dayOffset++)
        {
            var date = new DateOnly(2026, 5, 31).AddDays(dayOffset);

            shifts.Add(CreateShift(date, ShiftKind.Morning));
            shifts.Add(CreateShift(date, ShiftKind.Afternoon));
            shifts.Add(CreateShift(date, ShiftKind.Night));
        }

        return shifts.OrderBy(shift => shift.StartUtc).ToArray();
    }

    private static Shift CreateShift(DateOnly date, ShiftKind kind)
    {
        var capacity = GetCapacityRule(date, kind);

        return new Shift(
            CreateShiftGuid(date, kind),
            GetStartUtc(date, kind),
            GetEndUtc(date, kind),
            kind,
            capacity.Min,
            capacity.Max,
            requiresPreferenceToAssign: capacity.RequiresPreference,
            requiresMinimumWhenPreferenceExists: false,
            nightShiftCategory: GetNightShiftCategory(date, kind));
    }

    private static CapacityRule GetCapacityRule(DateOnly date, ShiftKind kind)
    {
        if (date.DayOfWeek == DayOfWeek.Friday)
        {
            return kind switch
            {
                ShiftKind.Morning => new CapacityRule(0, 2, true),
                ShiftKind.Afternoon => new CapacityRule(0, 1, true),
                ShiftKind.Night => new CapacityRule(0, 1, true),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
        }

        if (date.DayOfWeek == DayOfWeek.Saturday)
        {
            return kind switch
            {
                ShiftKind.Morning => new CapacityRule(0, 1, true),
                ShiftKind.Afternoon => new CapacityRule(0, 1, true),
                ShiftKind.Night => new CapacityRule(3, 3, false),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
        }

        return kind switch
        {
            ShiftKind.Morning => new CapacityRule(3, 6, false),
            ShiftKind.Afternoon => new CapacityRule(2, 4, true),
            ShiftKind.Night => new CapacityRule(0, 1, true),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private static DateTime GetStartUtc(DateOnly date, ShiftKind kind)
    {
        var time = kind switch
        {
            ShiftKind.Morning => new TimeOnly(6, 30),
            ShiftKind.Afternoon => new TimeOnly(14, 20),
            ShiftKind.Night => new TimeOnly(22, 40),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        return date.ToDateTime(time, DateTimeKind.Utc);
    }

    private static DateTime GetEndUtc(DateOnly date, ShiftKind kind)
    {
        var endDate = kind == ShiftKind.Night ? date.AddDays(1) : date;

        var time = kind switch
        {
            ShiftKind.Morning => new TimeOnly(14, 20),
            ShiftKind.Afternoon => new TimeOnly(22, 40),
            ShiftKind.Night => new TimeOnly(6, 30),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        return endDate.ToDateTime(time, DateTimeKind.Utc);
    }

    private static NightShiftCategory? GetNightShiftCategory(DateOnly date, ShiftKind kind)
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

    private static bool HasSubmittedShift(
        IReadOnlyCollection<SubmittedShift> submissions,
        Guid resourceId,
        Shift shift)
    {
        var date = DateOnly.FromDateTime(shift.StartUtc);

        return submissions.Any(submission =>
            submission.ResourceId == resourceId &&
            submission.Date == date &&
            submission.Kind == shift.Kind);
    }

    private static void AddAvailability(
        Resource resource,
        Shift shift,
        ICollection<AvailabilityWindow> availabilityWindows)
    {
        if (availabilityWindows.Any(window =>
                window.ResourceId == resource.Id &&
                window.StartUtc == shift.StartUtc &&
                window.EndUtc == shift.EndUtc))
        {
            return;
        }

        availabilityWindows.Add(new AvailabilityWindow(resource.Id, shift.StartUtc, shift.EndUtc));
    }

    private static void AddPreference(
        Resource resource,
        Shift shift,
        ResourcePreferenceType type,
        ICollection<ResourcePreference> preferences)
    {
        if (preferences.Any(preference =>
                preference.ResourceId == resource.Id &&
                preference.StartUtc == shift.StartUtc &&
                preference.EndUtc == shift.EndUtc &&
                preference.Type == type))
        {
            return;
        }

        preferences.Add(new ResourcePreference(
            resource.Id,
            shift.StartUtc,
            shift.EndUtc,
            type,
            ResourcePreferencePriority.High));
    }

    private static IReadOnlyList<ResourceWorkloadDemand> CreateWorkloadDemands(
        IReadOnlyList<Resource> resources,
        IReadOnlyList<Shift> shifts,
        IReadOnlyCollection<ResourcePreference> preferences)
    {
        var preferredHoursByResourceId = resources.ToDictionary(
            resource => resource.Id,
            resource => preferences
                .Where(preference => preference.ResourceId == resource.Id)
                .Where(preference => preference.Type == ResourcePreferenceType.Prefer)
                .Sum(preference => shifts
                    .Where(shift => Overlaps(shift.StartUtc, shift.EndUtc, preference.StartUtc, preference.EndUtc))
                    .Sum(GetShiftHours)));

        var totalPreferredHours = preferredHoursByResourceId.Values.Sum();

        if (totalPreferredHours <= 0)
        {
            throw new InvalidOperationException("Cannot create workload demands without submitted preferred hours.");
        }

        return resources
            .Select(resource => new ResourceWorkloadDemand(
                resource.Id,
                requestedPreferredHours: ExpectedTotalEffectiveTargetHours * preferredHoursByResourceId[resource.Id] / totalPreferredHours,
                minimumRequiredHours: 0))
            .ToArray();
    }

    private static string FormatReport<TDiagnostic>(
        ExperimentScenario scenario,
        ScheduleScoringWeights weights,
        ScheduleOptimizationResult result,
        IReadOnlyList<TDiagnostic> diagnostics)
    {
        var builder = new StringBuilder();

        builder.AppendLine("Stage 8.0 Real Submitted Schedule Acceptance Scenario");
        builder.AppendLine("Mode: Clean GA");
        builder.AppendLine($"Seed: {Seed}");
        builder.AppendLine($"PopulationSize: {PopulationSize}");
        builder.AppendLine($"GenerationCount: {GenerationCount}");
        builder.AppendLine($"ResourceCount: {scenario.Resources.Count}");
        builder.AppendLine($"ShiftCount: {scenario.Shifts.Count}");
        builder.AppendLine($"TotalEffectiveTargetHours: {scenario.ResourceWorkloadDemands.Sum(demand => demand.EffectiveTargetHours):0.##}");
        builder.AppendLine($"ResourceIgnoredAvoidPreferenceBurdenPenaltyPerHour: {weights.ResourceIgnoredAvoidPreferenceBurdenPenaltyPerHour}");
        builder.AppendLine();

        AppendRosterMapping(builder, scenario);
        AppendScenarioSummary(builder, scenario);
        AppendScheduleTable(builder, scenario, result.Candidate);
        AppendPenaltyBreakdown(builder, weights, result.Evaluation);
        AppendAssignedHours(builder, scenario, result.Candidate);
        AppendTargetGap(builder, scenario, result.Candidate);
        AppendIgnoredAvoid(builder, scenario, result.Candidate);
        AppendNightAssignments(builder, scenario, result.Candidate);
        AppendGenerationDiagnostics(builder, diagnostics);

        builder.AppendLine("FinalResult:");
        builder.AppendLine($"IsFeasible: {result.Evaluation.IsFeasible}");
        builder.AppendLine($"Score.Value: {result.Evaluation.Score.Value}");
        builder.AppendLine($"TotalPenalty: {result.Evaluation.Score.TotalPenalty}");
        builder.AppendLine($"HardViolationCount: {result.Evaluation.Score.HardViolationCount}");
        builder.AppendLine($"SoftViolationCount: {result.Evaluation.Score.SoftViolationCount}");
        builder.AppendLine($"Assignments.Count: {result.Candidate.Assignments.Count}");

        return builder.ToString();
    }

    private static void AppendRosterMapping(StringBuilder builder, ExperimentScenario scenario)
    {
        builder.AppendLine("RosterMapping:");

        foreach (var resource in scenario.Resources)
        {
            builder.AppendLine($"- {resource.Name} = {scenario.OriginalNamesByResourceId[resource.Id]}");
        }

        builder.AppendLine();
    }

    private static void AppendScenarioSummary(StringBuilder builder, ExperimentScenario scenario)
    {
        builder.AppendLine("ScenarioSummary:");
        builder.AppendLine($"SubmittedPreferredShiftCount: {scenario.SubmittedShifts.Count}");
        builder.AppendLine($"AvailabilityWindowCount: {scenario.AvailabilityWindows.Count}");
        builder.AppendLine($"PreferPreferenceCount: {scenario.ResourcePreferences.Count(preference => preference.Type == ResourcePreferenceType.Prefer)}");
        builder.AppendLine($"AvoidPreferenceCount: {scenario.ResourcePreferences.Count(preference => preference.Type == ResourcePreferenceType.Avoid)}");
        builder.AppendLine($"TotalMinimumCapacityHours: {scenario.Shifts.Sum(shift => GetShiftHours(shift) * shift.MinResourceCount):0.##}");
        builder.AppendLine($"TotalMaximumCapacityHours: {scenario.Shifts.Sum(shift => GetShiftHours(shift) * shift.MaxResourceCount):0.##}");
        builder.AppendLine();
    }

    private static void AppendScheduleTable(StringBuilder builder, ExperimentScenario scenario, ScheduleCandidate candidate)
    {
        var resourcesById = scenario.Resources.ToDictionary(resource => resource.Id);
        var assignmentsByShiftId = candidate.Assignments
            .GroupBy(assignment => assignment.ShiftId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(assignment => resourcesById[assignment.ResourceId].Name)
                    .OrderBy(name => name)
                    .ToArray());

        builder.AppendLine("Stage80ScheduleTable:");
        builder.AppendLine("Date | Day | Morning | Afternoon | Night");

        for (var dayOffset = 0; dayOffset < DaysInSchedule; dayOffset++)
        {
            var date = new DateOnly(2026, 5, 31).AddDays(dayOffset);

            builder.AppendLine(
                $"{date:dd/MM/yyyy} | {GetDayName(date.DayOfWeek)} | " +
                $"{GetAssignedNames(scenario, assignmentsByShiftId, date, ShiftKind.Morning)} | " +
                $"{GetAssignedNames(scenario, assignmentsByShiftId, date, ShiftKind.Afternoon)} | " +
                $"{GetAssignedNames(scenario, assignmentsByShiftId, date, ShiftKind.Night)}");
        }

        builder.AppendLine();
    }

    private static string GetAssignedNames(
        ExperimentScenario scenario,
        IReadOnlyDictionary<Guid, string[]> assignmentsByShiftId,
        DateOnly date,
        ShiftKind kind)
    {
        var shift = scenario.Shifts.Single(item =>
            DateOnly.FromDateTime(item.StartUtc) == date &&
            item.Kind == kind);

        return assignmentsByShiftId.TryGetValue(shift.Id, out var names) && names.Length > 0
            ? string.Join(", ", names)
            : "-";
    }

    private static void AppendPenaltyBreakdown(
        StringBuilder builder,
        ScheduleScoringWeights weights,
        ScheduleEvaluationResult evaluation)
    {
        var calculator = new ScheduleScoreCalculator(weights);

        builder.AppendLine("PenaltyBreakdownByType:");

        foreach (var group in evaluation.Violations.GroupBy(violation => violation.Type).OrderBy(group => group.Key.ToString()))
        {
            var violations = group.ToArray();
            var score = calculator.Calculate(violations);

            builder.AppendLine(
                $"- {group.Key}: Count={violations.Length}, Magnitude={violations.Sum(violation => violation.Magnitude ?? 0):0.##}, Penalty={score.TotalPenalty}");
        }

        if (!evaluation.Violations.Any())
        {
            builder.AppendLine("- none");
        }

        builder.AppendLine();
    }

    private static void AppendAssignedHours(StringBuilder builder, ExperimentScenario scenario, ScheduleCandidate candidate)
    {
        var assignedHours = CalculateAssignedHoursByResource(scenario, candidate);

        builder.AppendLine("AssignedHoursByWorker:");

        foreach (var resource in scenario.Resources)
        {
            builder.AppendLine($"- {resource.Name} ({scenario.OriginalNamesByResourceId[resource.Id]}): {assignedHours.GetValueOrDefault(resource.Id, 0):0.##}");
        }

        builder.AppendLine();
    }

    private static void AppendTargetGap(StringBuilder builder, ExperimentScenario scenario, ScheduleCandidate candidate)
    {
        var assignedHours = CalculateAssignedHoursByResource(scenario, candidate);

        builder.AppendLine("TargetGapByWorker:");

        foreach (var demand in scenario.ResourceWorkloadDemands.OrderBy(demand => scenario.Resources.Single(resource => resource.Id == demand.ResourceId).Name))
        {
            var assigned = assignedHours.GetValueOrDefault(demand.ResourceId, 0);
            var name = scenario.Resources.Single(resource => resource.Id == demand.ResourceId).Name;

            builder.AppendLine(
                $"- {name} ({scenario.OriginalNamesByResourceId[demand.ResourceId]}): Target={demand.EffectiveTargetHours:0.##}, Assigned={assigned:0.##}, Gap={assigned - demand.EffectiveTargetHours:0.##}");
        }

        builder.AppendLine();
    }

    private static void AppendIgnoredAvoid(StringBuilder builder, ExperimentScenario scenario, ScheduleCandidate candidate)
    {
        var counts = CalculateIgnoredAvoidCounts(scenario, candidate);

        builder.AppendLine("IgnoredAvoidAssignmentsByWorker:");

        foreach (var resource in scenario.Resources)
        {
            builder.AppendLine($"- {resource.Name} ({scenario.OriginalNamesByResourceId[resource.Id]}): {counts.GetValueOrDefault(resource.Id, 0)}");
        }

        builder.AppendLine();
    }

    private static void AppendNightAssignments(StringBuilder builder, ExperimentScenario scenario, ScheduleCandidate candidate)
    {
        var counts = CalculateNightCounts(scenario, candidate);

        builder.AppendLine("NightAssignmentsByWorker:");
        builder.AppendLine("Worker | Regular | FridayNight | MotzeiShabbat");

        foreach (var resource in scenario.Resources)
        {
            var count = counts[resource.Id];

            builder.AppendLine(
                $"{resource.Name} ({scenario.OriginalNamesByResourceId[resource.Id]}) | {count.Regular} | {count.FridayNight} | {count.MotzeiShabbat}");
        }

        builder.AppendLine();
    }

    private static void AppendGenerationDiagnostics<TDiagnostic>(
        StringBuilder builder,
        IReadOnlyList<TDiagnostic> diagnostics)
    {
        builder.AppendLine("GenerationDiagnostics:");

        foreach (var index in new[] { 0, diagnostics.Count / 2, diagnostics.Count - 1 }.Distinct())
        {
            dynamic diagnostic = diagnostics[index]!;

            builder.AppendLine(
                $"- Generation {diagnostic.GenerationIndex}: BestSoFarTotalPenalty={diagnostic.BestSoFarTotalPenalty}, FeasibleCandidateCount={diagnostic.FeasibleCandidateCount}, PopulationSize={diagnostic.PopulationSize}");
        }

        builder.AppendLine();
    }

    private static IReadOnlyDictionary<Guid, double> CalculateAssignedHoursByResource(
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        var shiftsById = scenario.Shifts.ToDictionary(shift => shift.Id);

        return candidate.Assignments
            .GroupBy(assignment => assignment.ResourceId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(assignment => GetShiftHours(shiftsById[assignment.ShiftId])));
    }

    private static IReadOnlyDictionary<Guid, int> CalculateIgnoredAvoidCounts(
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        var shiftsById = scenario.Shifts.ToDictionary(shift => shift.Id);
        var counts = scenario.Resources.ToDictionary(resource => resource.Id, _ => 0);

        foreach (var assignment in candidate.Assignments)
        {
            var shift = shiftsById[assignment.ShiftId];

            var hasAvoid = scenario.ResourcePreferences
                .Where(preference => preference.ResourceId == assignment.ResourceId)
                .Where(preference => preference.Type == ResourcePreferenceType.Avoid)
                .Any(preference => Overlaps(shift.StartUtc, shift.EndUtc, preference.StartUtc, preference.EndUtc));

            if (hasAvoid)
            {
                counts[assignment.ResourceId]++;
            }
        }

        return counts;
    }

    private static IReadOnlyDictionary<Guid, NightCount> CalculateNightCounts(
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        var shiftsById = scenario.Shifts.ToDictionary(shift => shift.Id);
        var counts = scenario.Resources.ToDictionary(resource => resource.Id, _ => new NightCount());

        foreach (var assignment in candidate.Assignments)
        {
            var shift = shiftsById[assignment.ShiftId];
            var count = counts[assignment.ResourceId];

            if (shift.NightShiftCategory == NightShiftCategory.Regular)
            {
                count.Regular++;
            }

            if (shift.NightShiftCategory == NightShiftCategory.FridayNight)
            {
                count.FridayNight++;
            }

            if (shift.NightShiftCategory == NightShiftCategory.MotzeiShabbatNight)
            {
                count.MotzeiShabbat++;
            }
        }

        return counts;
    }

    private static void AssertScenarioShape(ExperimentScenario scenario)
    {
        Assert.Equal(ResourceCount, scenario.Resources.Count);
        Assert.Equal(ExpectedShiftCount, scenario.Shifts.Count);
        Assert.Equal(ResourceCount, scenario.ResourceWorkloadDemands.Count);
        Assert.Equal(ExpectedTotalEffectiveTargetHours, scenario.ResourceWorkloadDemands.Sum(demand => demand.EffectiveTargetHours), 0.000001);

        var weekdayMornings = scenario.Shifts.Where(IsWeekdayMorning).ToArray();
        Assert.Equal(10, weekdayMornings.Length);

        Assert.All(weekdayMornings, shift =>
        {
            Assert.False(shift.RequiresPreferenceToAssign);
            Assert.Equal(ResourceCount, CountAvailable(scenario, shift));
            Assert.True(CountPrefer(scenario, shift) >= shift.MinResourceCount);
            Assert.Equal(ResourceCount, CountPrefer(scenario, shift) + CountAvoid(scenario, shift));
        });

        var weekdayAfternoons = scenario.Shifts.Where(IsWeekdayAfternoon).ToArray();
        Assert.Equal(10, weekdayAfternoons.Length);

        Assert.All(weekdayAfternoons, shift =>
        {
            Assert.True(shift.RequiresPreferenceToAssign);
            Assert.Equal(2, shift.MinResourceCount);
            Assert.Equal(4, shift.MaxResourceCount);
            Assert.True(CountPrefer(scenario, shift) >= shift.MinResourceCount);
        });

        var motzeiShabbatShifts = scenario.Shifts.Where(IsMotzeiShabbatNight).ToArray();
        Assert.Equal(2, motzeiShabbatShifts.Length);

        Assert.All(motzeiShabbatShifts, shift =>
        {
            Assert.False(shift.RequiresPreferenceToAssign);
            Assert.Equal(3, shift.MinResourceCount);
            Assert.Equal(3, shift.MaxResourceCount);
            Assert.Equal(ResourceCount, CountAvailable(scenario, shift));
            Assert.True(CountPrefer(scenario, shift) >= shift.MinResourceCount);
            Assert.Equal(ResourceCount, CountPrefer(scenario, shift) + CountAvoid(scenario, shift));
        });
    }

    private static int CountAvailable(ExperimentScenario scenario, Shift shift)
    {
        return scenario.AvailabilityWindows.Count(window => window.Covers(shift));
    }

    private static int CountPrefer(ExperimentScenario scenario, Shift shift)
    {
        return scenario.ResourcePreferences
            .Where(preference => preference.Type == ResourcePreferenceType.Prefer)
            .Count(preference => Overlaps(shift.StartUtc, shift.EndUtc, preference.StartUtc, preference.EndUtc));
    }

    private static int CountAvoid(ExperimentScenario scenario, Shift shift)
    {
        return scenario.ResourcePreferences
            .Where(preference => preference.Type == ResourcePreferenceType.Avoid)
            .Count(preference => Overlaps(shift.StartUtc, shift.EndUtc, preference.StartUtc, preference.EndUtc));
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

        Assert.Equal(
            candidate.Assignments.Count,
            candidate.Assignments.Select(assignment => $"{assignment.ResourceId}:{assignment.ShiftId}").Distinct().Count());
    }

    private static void AssertNoBasicStructuralViolations(ScheduleEvaluationResult evaluation)
    {
        Assert.Equal(0, CountViolations(evaluation, ConstraintViolationType.ResourceUnavailable));
        Assert.Equal(0, CountViolations(evaluation, ConstraintViolationType.ResourceAssignedToOverlappingShifts));
        Assert.Equal(0, CountViolations(evaluation, ConstraintViolationType.AssignedWithoutRequiredPreference));
        Assert.Equal(0, CountViolations(evaluation, ConstraintViolationType.ShiftUnderstaffed));
        Assert.Equal(0, CountViolations(evaluation, ConstraintViolationType.ShiftOverstaffed));
        Assert.Equal(0, CountViolations(evaluation, ConstraintViolationType.ResourceMonthlyNightShiftQuotaExceeded));
        Assert.Equal(0, CountViolations(evaluation, ConstraintViolationType.ShiftSequenceQuotaExceeded));
        Assert.Equal(0, evaluation.Score.HardViolationCount);
    }

    private static int CountViolations(ScheduleEvaluationResult evaluation, ConstraintViolationType type)
    {
        return evaluation.Violations.Count(violation => violation.Type == type);
    }

    private static bool IsWeekdayMorning(Shift shift)
    {
        var day = DateOnly.FromDateTime(shift.StartUtc).DayOfWeek;

        return shift.Kind == ShiftKind.Morning &&
               day is not DayOfWeek.Friday and not DayOfWeek.Saturday;
    }

    private static bool IsWeekdayAfternoon(Shift shift)
    {
        var day = DateOnly.FromDateTime(shift.StartUtc).DayOfWeek;

        return shift.Kind == ShiftKind.Afternoon &&
               day is not DayOfWeek.Friday and not DayOfWeek.Saturday;
    }

    private static bool IsMotzeiShabbatNight(Shift shift)
    {
        return shift.NightShiftCategory == NightShiftCategory.MotzeiShabbatNight;
    }

    private static bool Overlaps(DateTime firstStart, DateTime firstEnd, DateTime secondStart, DateTime secondEnd)
    {
        return firstStart < secondEnd && secondStart < firstEnd;
    }

    private static double GetShiftHours(Shift shift)
    {
        return (shift.EndUtc - shift.StartUtc).TotalHours;
    }

    private static string GetDayName(DayOfWeek day)
    {
        return day switch
        {
            DayOfWeek.Sunday => "ראשון",
            DayOfWeek.Monday => "שני",
            DayOfWeek.Tuesday => "שלWorker19",
            DayOfWeek.Wednesday => "רביעי",
            DayOfWeek.Thursday => "חמWorker19",
            DayOfWeek.Friday => "שWorker19",
            DayOfWeek.Saturday => "שבת",
            _ => throw new ArgumentOutOfRangeException(nameof(day))
        };
    }

    private static Guid CreateGuid(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }

    private static Guid CreateShiftGuid(DateOnly date, ShiftKind kind)
    {
        var kindValue = kind switch
        {
            ShiftKind.Morning => 1,
            ShiftKind.Afternoon => 2,
            ShiftKind.Night => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        return CreateGuid((date.DayNumber * 10) + kindValue);
    }

    private sealed record ExperimentScenario(
        SchedulingProblem Problem,
        IReadOnlyList<Resource> Resources,
        IReadOnlyDictionary<Guid, string> OriginalNamesByResourceId,
        IReadOnlyList<Shift> Shifts,
        IReadOnlyCollection<AvailabilityWindow> AvailabilityWindows,
        IReadOnlyCollection<ResourcePreference> ResourcePreferences,
        IReadOnlyCollection<ResourceWorkloadDemand> ResourceWorkloadDemands,
        IReadOnlyCollection<SubmittedShift> SubmittedShifts);

    private sealed record SubmittedShift(Guid ResourceId, DateOnly Date, ShiftKind Kind);
    private sealed record CapacityRule(int Min, int Max, bool RequiresPreference);

    private sealed class NightCount
    {
        public int Regular { get; set; }
        public int FridayNight { get; set; }
        public int MotzeiShabbat { get; set; }
    }
}
