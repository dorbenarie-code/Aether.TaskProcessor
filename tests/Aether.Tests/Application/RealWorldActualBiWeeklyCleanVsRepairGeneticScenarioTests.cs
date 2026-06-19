using System.Diagnostics;
using System.Text;
using Aether.Application.Scheduling.Contracts;
using Aether.Application.Scheduling.Interfaces;
using Aether.Application.Scheduling.Optimization;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class RealWorldActualBiWeeklyCleanVsRepairGeneticScenarioTests
{
    private const int ResourceCount = 18;
    private const int DaysInSchedule = 14;
    private const int ExpectedShiftCount = DaysInSchedule * 3;
    private const int ExpectedSubmissionCount = 107;
    private const double ExpectedTotalSubmittedPreferredHours = 856.0;
    private const double ExpectedTotalEffectiveTargetHours = 816.0;
    private const double HoursTolerance = 0.000001;
    private const int GeneticPopulationSize = 120;
    private const int CleanGenerationCount = 30;
    private const int GeneticSeed = 20260603;
    private static readonly int[] CleanEngineDiagnosticSeeds =
    [
        20260603,
        20260604,
        20260605,
        20260606,
        20260607
    ];
    private static readonly int[] MultiSeedComparisonSeeds =
    [
        20260603,
        20260604,
        20260605,
        20260606,
        20260607
    ];

    [Fact]
    public void CreateScenario_ShouldCreateExpectedRealGuardResources()
    {
        var scenario = CreateScenario();

        Assert.Equal(ResourceCount, scenario.Resources.Count);
        Assert.Equal(GetExpectedResourceNames(), scenario.Resources.Select(resource => resource.Name).ToArray());
        Assert.DoesNotContain(scenario.Resources, resource => resource.Name == "Worker07");

        Assert.Equal(
            ResourceCount,
            scenario.Resources
                .Select(resource => resource.Id)
                .Distinct()
                .Count());

        Assert.All(scenario.Resources, resource =>
        {
            Assert.False(resource.Id == Guid.Empty);
            Assert.Equal(100m, resource.HourlyCost);
        });
    }

    [Fact]
    public void CreateScenario_ShouldCreateBiWeeklyShiftTemplate()
    {
        var scenario = CreateScenario();

        Assert.Equal(ExpectedShiftCount, scenario.Shifts.Count);

        Assert.Equal(
            ExpectedShiftCount,
            scenario.Shifts
                .Select(shift => shift.Id)
                .Distinct()
                .Count());

        Assert.All(scenario.Shifts, shift =>
        {
            Assert.False(shift.Id == Guid.Empty);
            Assert.True(shift.StartUtc < shift.EndUtc);
            Assert.True(shift.RequiresPreferenceToAssign);
            Assert.False(shift.RequiresMinimumWhenPreferenceExists);
        });

        Assert.Equal(
            DaysInSchedule,
            scenario.Shifts.Count(shift => shift.Kind == ShiftKind.Morning));

        Assert.Equal(
            DaysInSchedule,
            scenario.Shifts.Count(shift => shift.Kind == ShiftKind.Afternoon));

        Assert.Equal(
            DaysInSchedule,
            scenario.Shifts.Count(shift => shift.Kind == ShiftKind.Night));
    }

    [Fact]
    public void CreateScenario_ShouldApplyApprovedBusinessCapacityRules()
    {
        var scenario = CreateScenario();

        Assert.All(scenario.Shifts, shift =>
        {
            var date = DateOnly.FromDateTime(shift.StartUtc);

            if (date.DayOfWeek == DayOfWeek.Friday)
            {
                AssertFridayCapacity(shift);
                return;
            }

            if (date.DayOfWeek == DayOfWeek.Saturday)
            {
                AssertSaturdayCapacity(shift);
                return;
            }

            AssertWeekdayCapacity(shift);
        });
    }

    [Fact]
    public void CreateScenario_ShouldCreateActualSubmissionDrivenAvailabilityAndPreferPreferences()
    {
        var scenario = CreateScenario();

        Assert.Equal(ExpectedSubmissionCount, scenario.AvailabilityWindows.Count);
        Assert.Equal(ExpectedSubmissionCount, scenario.ResourcePreferences.Count);

        Assert.Equal(
            scenario.AvailabilityWindows.Count,
            scenario.ResourcePreferences.Count);

        var resourceIds = scenario.Resources
            .Select(resource => resource.Id)
            .ToHashSet();

        var shiftWindows = scenario.Shifts
            .Select(shift => ToWindowKey(shift.StartUtc, shift.EndUtc))
            .ToHashSet();

        Assert.All(scenario.AvailabilityWindows, window =>
        {
            Assert.Contains(window.ResourceId, resourceIds);
            Assert.Contains(ToWindowKey(window.StartUtc, window.EndUtc), shiftWindows);
        });

        Assert.All(scenario.ResourcePreferences, preference =>
        {
            Assert.Contains(preference.ResourceId, resourceIds);
            Assert.Contains(ToWindowKey(preference.StartUtc, preference.EndUtc), shiftWindows);
            Assert.Equal(ResourcePreferenceType.Prefer, preference.Type);
            Assert.Equal(ResourcePreferencePriority.High, preference.Priority);
        });

        var availabilityKeys = scenario.AvailabilityWindows
            .Select(window => ToSubmissionKey(
                window.ResourceId,
                window.StartUtc,
                window.EndUtc))
            .ToArray();

        var preferenceKeys = scenario.ResourcePreferences
            .Select(preference => ToSubmissionKey(
                preference.ResourceId,
                preference.StartUtc,
                preference.EndUtc))
            .ToArray();

        Assert.Equal(
            availabilityKeys.Length,
            availabilityKeys.Distinct().Count());

        Assert.Equal(
            preferenceKeys.Length,
            preferenceKeys.Distinct().Count());

        Assert.Equal(
            availabilityKeys.OrderBy(key => key),
            preferenceKeys.OrderBy(key => key));
    }

    [Fact]
    public void CreateScenario_ShouldExposeOnlyTheTwoKnownRealMandatoryMorningSubmissionShortages()
    {
        var scenario = CreateScenario();

        var mandatoryWeekdayDaytimeShifts = scenario.Shifts
            .Where(shift => IsWeekday(shift))
            .Where(shift => shift.Kind is ShiftKind.Morning or ShiftKind.Afternoon)
            .ToArray();

        Assert.NotEmpty(mandatoryWeekdayDaytimeShifts);

        var underSubmitted = mandatoryWeekdayDaytimeShifts
            .Select(shift => new
            {
                Shift = shift,
                SubmittedResourceCount = CountSubmittedResourcesForShift(scenario, shift)
            })
            .Where(item => item.SubmittedResourceCount < item.Shift.MinResourceCount)
            .ToArray();

        Assert.Equal(2, underSubmitted.Length);

        Assert.All(underSubmitted, item =>
        {
            Assert.True(
                IsKnownRealUnderstaffedShift(item.Shift),
                $"Unexpected under-submitted mandatory shift: {FormatShift(item.Shift)} submitted={item.SubmittedResourceCount}, min={item.Shift.MinResourceCount}.");
        });

        Assert.Contains(
            underSubmitted,
            item => DateOnly.FromDateTime(item.Shift.StartUtc) == new DateOnly(2026, 6, 3) &&
                    item.Shift.Kind == ShiftKind.Morning &&
                    item.SubmittedResourceCount == 3);

        Assert.Contains(
            underSubmitted,
            item => DateOnly.FromDateTime(item.Shift.StartUtc) == new DateOnly(2026, 6, 10) &&
                    item.Shift.Kind == ShiftKind.Morning &&
                    item.SubmittedResourceCount == 3);
    }

    [Fact]
    public void CreateScenario_ShouldCreateActualRegularAndFridayNightRequests()
    {
        var scenario = CreateScenario();

        var regularNightShifts = scenario.Shifts
            .Where(shift => shift.NightShiftCategory == NightShiftCategory.Regular)
            .OrderBy(shift => shift.StartUtc)
            .ToArray();

        var fridayNightShifts = scenario.Shifts
            .Where(shift => shift.NightShiftCategory == NightShiftCategory.FridayNight)
            .OrderBy(shift => shift.StartUtc)
            .ToArray();

        Assert.Equal(10, regularNightShifts.Length);
        Assert.Equal(2, fridayNightShifts.Length);

        Assert.All(regularNightShifts, shift =>
        {
            Assert.Equal(0, shift.MinResourceCount);
            Assert.Equal(1, shift.MaxResourceCount);
        });

        Assert.All(fridayNightShifts, shift =>
        {
            Assert.Equal(0, shift.MinResourceCount);
            Assert.Equal(1, shift.MaxResourceCount);
        });

        AssertSubmittedResourceNamesForShift(scenario, new DateOnly(2026, 5, 31), ShiftKind.Night, ["עיWorker05י"]);
        AssertSubmittedResourceNamesForShift(scenario, new DateOnly(2026, 6, 1), ShiftKind.Night, ["Worker15י"]);
        AssertSubmittedResourceNamesForShift(scenario, new DateOnly(2026, 6, 2), ShiftKind.Night, ["Worker14"]);
        AssertSubmittedResourceNamesForShift(scenario, new DateOnly(2026, 6, 3), ShiftKind.Night, ["Worker12"]);
        AssertSubmittedResourceNamesForShift(scenario, new DateOnly(2026, 6, 4), ShiftKind.Night, ["Worker11"]);
        AssertSubmittedResourceNamesForShift(scenario, new DateOnly(2026, 6, 7), ShiftKind.Night, ["Worker05"]);
        AssertSubmittedResourceNamesForShift(scenario, new DateOnly(2026, 6, 8), ShiftKind.Night, ["Worker09 סעדון"]);
        AssertSubmittedResourceNamesForShift(scenario, new DateOnly(2026, 6, 9), ShiftKind.Night, ["Worker10"]);
        AssertSubmittedResourceNamesForShift(scenario, new DateOnly(2026, 6, 10), ShiftKind.Night, ["Worker13"]);
        AssertSubmittedResourceNamesForShift(scenario, new DateOnly(2026, 6, 11), ShiftKind.Night, []);

        AssertSubmittedResourceNamesForShift(scenario, new DateOnly(2026, 6, 5), ShiftKind.Night, ["Worker17 Worker17"]);
        AssertSubmittedResourceNamesForShift(scenario, new DateOnly(2026, 6, 12), ShiftKind.Night, []);

        foreach (var resource in scenario.Resources)
        {
            var regularNightSubmissionCount = scenario.ResourcePreferences
                .Where(preference => preference.ResourceId == resource.Id)
                .Count(preference => scenario.Shifts.Any(shift =>
                    shift.NightShiftCategory == NightShiftCategory.Regular &&
                    Overlaps(
                        preference.StartUtc,
                        preference.EndUtc,
                        shift.StartUtc,
                        shift.EndUtc)));

            Assert.InRange(regularNightSubmissionCount, 0, 1);
        }
    }

    [Fact]
    public void CreateScenario_ShouldCreateApprovedMotzeiShabbatRotation()
    {
        var scenario = CreateScenario();

        var motzeiShabbatNightShifts = scenario.Shifts
            .Where(shift => shift.NightShiftCategory == NightShiftCategory.MotzeiShabbatNight)
            .OrderBy(shift => shift.StartUtc)
            .ToArray();

        Assert.Equal(2, motzeiShabbatNightShifts.Length);

        Assert.All(motzeiShabbatNightShifts, shift =>
        {
            Assert.Equal(3, shift.MinResourceCount);
            Assert.Equal(3, shift.MaxResourceCount);
        });

        AssertSubmittedResourceNamesForShift(scenario, new DateOnly(2026, 6, 6), ShiftKind.Night, ["Worker14", "Worker10", "עדנה"]);
        AssertSubmittedResourceNamesForShift(scenario, new DateOnly(2026, 6, 13), ShiftKind.Night, ["Worker04", "Worker16 אלדר", "Worker15י"]);

        var firstPool = GetSubmittedResourceIdsForShift(
                scenario,
                motzeiShabbatNightShifts[0])
            .ToHashSet();

        var secondPool = GetSubmittedResourceIdsForShift(
                scenario,
                motzeiShabbatNightShifts[1])
            .ToHashSet();

        Assert.Equal(3, firstPool.Count);
        Assert.Equal(3, secondPool.Count);
        Assert.Empty(firstPool.Intersect(secondPool));
    }

    [Fact]
    public void CreateScenario_ShouldBuildSchedulingProblemFromFactories()
    {
        var scenario = CreateScenario();

        Assert.Equal(
            scenario.Resources.Select(resource => resource.Id),
            scenario.Problem.Resources.Select(resource => resource.Id));

        Assert.Equal(
            scenario.Shifts.Select(shift => shift.Id),
            scenario.Problem.Shifts.Select(shift => shift.Id));

        Assert.Equal(
            scenario.AvailabilityWindows.Select(window => ToSubmissionKey(
                window.ResourceId,
                window.StartUtc,
                window.EndUtc)).OrderBy(key => key),
            scenario.Problem.AvailabilityWindows.Select(window => ToSubmissionKey(
                window.ResourceId,
                window.StartUtc,
                window.EndUtc)).OrderBy(key => key));

        Assert.Equal(
            scenario.ResourcePreferences.Select(preference => ToSubmissionKey(
                preference.ResourceId,
                preference.StartUtc,
                preference.EndUtc)).OrderBy(key => key),
            scenario.Problem.ResourcePreferences.Select(preference => ToSubmissionKey(
                preference.ResourceId,
                preference.StartUtc,
                preference.EndUtc)).OrderBy(key => key));

        Assert.Equal(
            scenario.ResourceWorkloadDemands.Select(demand => demand.ResourceId),
            scenario.Problem.ResourceWorkloadDemands.Select(demand => demand.ResourceId));

        Assert.Equal(ResourceCount, scenario.Problem.Resources.Count);
        Assert.Equal(ExpectedShiftCount, scenario.Problem.Shifts.Count);
        Assert.Equal(ResourceCount, scenario.Problem.ResourceWorkloadDemands.Count);

        Assert.Equal(0, scenario.Problem.MinimumAssignedHoursPerResource);
        Assert.Equal(0, scenario.Problem.MinimumMorningShiftsPerResourcePerFullWeek);
        Assert.Equal(0, scenario.Problem.MinimumAfternoonShiftsPerResourcePerFullWeek);
    }

    [Fact]
    public void CreateScenario_ShouldCreateSubmissionWeightedWorkloadDemands()
    {
        var scenario = CreateScenario();

        Assert.Equal(ResourceCount, scenario.ResourceWorkloadDemands.Count);

        var totalSubmittedPreferredHours = scenario.Resources.Sum(resource =>
            GetSubmittedPreferredHours(
                resource,
                scenario.Shifts,
                scenario.ResourcePreferences));

        Assert.True(
            Math.Abs(ExpectedTotalSubmittedPreferredHours - totalSubmittedPreferredHours) < HoursTolerance,
            $"Expected total submitted preferred hours {ExpectedTotalSubmittedPreferredHours:0.##}h but got {totalSubmittedPreferredHours:0.##}h.");

        foreach (var resource in scenario.Resources)
        {
            var submittedPreferredHours = GetSubmittedPreferredHours(
                resource,
                scenario.Shifts,
                scenario.ResourcePreferences);

            var expectedTargetHours =
                ExpectedTotalEffectiveTargetHours *
                submittedPreferredHours /
                totalSubmittedPreferredHours;

            var demand = scenario.ResourceWorkloadDemands.Single(item =>
                item.ResourceId == resource.Id);

            Assert.True(
                submittedPreferredHours > 0,
                $"{resource.Name} should have at least one submitted preferred shift.");

            Assert.True(
                Math.Abs(expectedTargetHours - demand.RequestedPreferredHours) < HoursTolerance,
                $"{resource.Name} requested preferred hours should be proportional to submitted preferred hours.");

            Assert.Equal(0.0, demand.MinimumRequiredHours);

            Assert.True(
                Math.Abs(demand.RequestedPreferredHours - demand.EffectiveTargetHours) < HoursTolerance,
                $"{resource.Name} effective target should equal requested preferred hours when minimum is zero.");
        }
    }

    [Fact]
    public void CreateScenario_ShouldKeepTotalEffectiveTargetInsideScheduleCapacity()
    {
        var scenario = CreateScenario();

        var totalEffectiveTargetHours = scenario.ResourceWorkloadDemands
            .Sum(demand => demand.EffectiveTargetHours);

        var totalMinimumCapacityHours = scenario.Shifts
            .Sum(shift => GetShiftHours(shift) * shift.MinResourceCount);

        var totalMaximumCapacityHours = scenario.Shifts
            .Sum(shift => GetShiftHours(shift) * shift.MaxResourceCount);

        var totalCappedSubmittedAssignableHours = scenario.Shifts
            .Sum(shift => GetShiftHours(shift) * Math.Min(
                shift.MaxResourceCount,
                CountSubmittedResourcesForShift(scenario, shift)));

        Assert.True(
            Math.Abs(ExpectedTotalEffectiveTargetHours - totalEffectiveTargetHours) < HoursTolerance,
            $"Expected total effective target {ExpectedTotalEffectiveTargetHours:0.##}h but got {totalEffectiveTargetHours:0.##}h.");

        Assert.True(
            Math.Abs(ExpectedTotalEffectiveTargetHours - totalCappedSubmittedAssignableHours) < HoursTolerance,
            $"Expected capped submitted assignable hours {ExpectedTotalEffectiveTargetHours:0.##}h but got {totalCappedSubmittedAssignableHours:0.##}h.");

        Assert.True(
            totalMinimumCapacityHours <= totalEffectiveTargetHours,
            $"Minimum capacity {totalMinimumCapacityHours:0.##}h is above target {totalEffectiveTargetHours:0.##}h.");

        Assert.True(
            totalMaximumCapacityHours >= totalEffectiveTargetHours,
            $"Maximum capacity {totalMaximumCapacityHours:0.##}h is below target {totalEffectiveTargetHours:0.##}h.");
    }

    [Fact]
    public void DeterministicBaseline_ShouldExposeOnlyExpectedRealStructuralFailures()
    {
        var scenario = CreateScenario();

        var result = new DeterministicScheduleOptimizer()
            .Optimize(scenario.Problem);

        Assert.NotNull(result.Candidate);
        Assert.NotNull(result.Evaluation);
        Assert.NotEmpty(result.Candidate.Assignments);

        AssertCandidateReferencesKnownProblemEntities(
            scenario,
            result.Candidate);

        AssertOnlyExpectedRealStructuralViolations(
            scenario,
            result.Evaluation);

        AssertNoMoreThanOneRegularNightAssignmentPerResource(
            scenario,
            result.Candidate);

        Assert.True(
            result.Evaluation.Score.TotalPenalty > 0,
            "Deterministic baseline should expose real optimization pressure. If penalty is zero, the scenario is too easy.");
    }

    [Fact]
    public void CleanGeneticOptimizer_ShouldRunOnActualScenarioAndReportDiagnostics()
    {
        var scenario = CreateScenario();
        var diagnosticsSink = new CollectingGeneticOptimizerDiagnosticsSink();

        var result = new GeneticScheduleOptimizer(
                populationSize: GeneticPopulationSize,
                seed: GeneticSeed,
                generationCount: CleanGenerationCount,
                eliteCount: 1,
                tournamentSize: 3,
                diagnosticsSink: diagnosticsSink,
                evolutionMode: GeneticEvolutionMode.Clean)
            .Optimize(scenario.Problem);

        Assert.NotNull(result.Candidate);
        Assert.NotNull(result.Evaluation);
        Assert.NotEmpty(result.Candidate.Assignments);

        Assert.Equal(
            CleanGenerationCount + 1,
            diagnosticsSink.Diagnostics.Count);

        Assert.Equal(
            Enumerable.Range(0, CleanGenerationCount + 1).ToArray(),
            diagnosticsSink.Diagnostics
                .Select(diagnostic => diagnostic.GenerationIndex)
                .ToArray());

        Assert.All(diagnosticsSink.Diagnostics, diagnostic =>
        {
            Assert.Equal(GeneticPopulationSize, diagnostic.PopulationSize);
            Assert.InRange(diagnostic.FeasibleCandidateCount, 0, GeneticPopulationSize);
        });

        AssertCandidateReferencesKnownProblemEntities(
            scenario,
            result.Candidate);

        AssertOnlyExpectedRealStructuralViolations(
            scenario,
            result.Evaluation);

        AssertNoMoreThanOneRegularNightAssignmentPerResource(
            scenario,
            result.Candidate);
    }

    [Fact]
    [Trait("Category", "SchedulingExperiment")]
    public void CleanAndRepairGeneticOptimizers_ShouldPrintActualRealWorldComparison()
    {
        var scenario = CreateScenario();

        var deterministic = RunOptimizer(
            "Deterministic",
            new DeterministicScheduleOptimizer(),
            scenario.Problem);

        var cleanDiagnosticsSink = new CollectingGeneticOptimizerDiagnosticsSink();

        var clean = RunOptimizer(
            "Clean GA",
            new GeneticScheduleOptimizer(
                populationSize: GeneticPopulationSize,
                seed: GeneticSeed,
                generationCount: CleanGenerationCount,
                eliteCount: 1,
                tournamentSize: 3,
                diagnosticsSink: cleanDiagnosticsSink,
                evolutionMode: GeneticEvolutionMode.Clean),
            scenario.Problem,
            cleanDiagnosticsSink.Diagnostics);

        var repairDiagnosticsSink = new CollectingGeneticOptimizerDiagnosticsSink();

        var repair = RunOptimizer(
            "RepairAssisted GA",
            new GeneticScheduleOptimizer(
                populationSize: GeneticPopulationSize,
                seed: GeneticSeed,
                generationCount: CleanGenerationCount,
                eliteCount: 1,
                tournamentSize: 3,
                diagnosticsSink: repairDiagnosticsSink,
                evolutionMode: GeneticEvolutionMode.RepairAssisted),
            scenario.Problem,
            repairDiagnosticsSink.Diagnostics);

        var report = FormatActualRealWorldReport(
            scenario,
            deterministic,
            clean,
            repair);

        System.Console.WriteLine(report);

        Assert.Contains("Actual Real World Biweekly Clean vs RepairAssisted GA Comparison", report);
        Assert.Contains("KnownRealShortages:", report);
        Assert.Contains("2026-06-03 Morning", report);
        Assert.Contains("2026-06-10 Morning", report);
        Assert.Contains("Deterministic", report);
        Assert.Contains("Clean GA", report);
        Assert.Contains("RepairAssisted GA", report);
        Assert.Contains("Comparison:", report);
        Assert.Contains("RepairRankedBetterThanClean", report);
        Assert.Contains("CleanRankedBetterThanRepair", report);
        Assert.Contains("ResourceTargetSummary:", report);
        Assert.Contains("RegularNightAssignments", report);
        Assert.Contains("MotzeiShabbatNightAssignments", report);
        Assert.Contains("TargetGapSummary:", report);
        Assert.Contains("MonthlyNightQuotaSummary:", report);
        Assert.Contains("GenerationDiagnostics:", report);

        AssertCandidateReferencesKnownProblemEntities(
            scenario,
            deterministic.Result.Candidate);

        AssertCandidateReferencesKnownProblemEntities(
            scenario,
            clean.Result.Candidate);

        AssertCandidateReferencesKnownProblemEntities(
            scenario,
            repair.Result.Candidate);

        AssertOnlyExpectedRealStructuralViolations(
            scenario,
            deterministic.Result.Evaluation);

        AssertOnlyExpectedRealStructuralViolations(
            scenario,
            clean.Result.Evaluation);

        AssertOnlyExpectedRealStructuralViolations(
            scenario,
            repair.Result.Evaluation);

        AssertNoMoreThanOneRegularNightAssignmentPerResource(
            scenario,
            clean.Result.Candidate);

        AssertNoMoreThanOneRegularNightAssignmentPerResource(
            scenario,
            repair.Result.Candidate);

        AssertMonthlyNightQuotaPerCategory(
            scenario,
            clean.Result.Candidate,
            report);

        AssertMonthlyNightQuotaPerCategory(
            scenario,
            repair.Result.Candidate,
            report);

        Assert.Equal(
            CleanGenerationCount + 1,
            clean.Diagnostics.Count);

        Assert.Equal(
            CleanGenerationCount + 1,
            repair.Diagnostics.Count);

        Assert.True(
            clean.Diagnostics[^1].BestSoFarTotalPenalty <=
            clean.Diagnostics[0].BestSoFarTotalPenalty,
            report);

        Assert.True(
            repair.Diagnostics[^1].BestSoFarTotalPenalty <=
            repair.Diagnostics[0].BestSoFarTotalPenalty,
            report);
    }


    [Fact]
    [Trait("Category", "SchedulingExperiment")]
    public void CleanAndRepairGeneticOptimizers_ShouldPrintActualRealWorldMultiSeedComparison()
    {
        var scenario = CreateScenario();
        var ranker = new ScheduleEvaluationResultRanker();
        var comparisons = new List<MultiSeedComparison>();

        foreach (var seed in MultiSeedComparisonSeeds)
        {
            var clean = RunOptimizer(
                $"Clean GA seed {seed}",
                new GeneticScheduleOptimizer(
                    populationSize: GeneticPopulationSize,
                    seed: seed,
                    generationCount: CleanGenerationCount,
                    eliteCount: 1,
                    tournamentSize: 3,
                    evolutionMode: GeneticEvolutionMode.Clean),
                scenario.Problem);

            var repair = RunOptimizer(
                $"RepairAssisted GA seed {seed}",
                new GeneticScheduleOptimizer(
                    populationSize: GeneticPopulationSize,
                    seed: seed,
                    generationCount: CleanGenerationCount,
                    eliteCount: 1,
                    tournamentSize: 3,
                    evolutionMode: GeneticEvolutionMode.RepairAssisted),
                scenario.Problem);

            AssertValidActualScenarioRun(
                scenario,
                clean,
                $"Clean GA seed {seed}");

            AssertValidActualScenarioRun(
                scenario,
                repair,
                $"RepairAssisted GA seed {seed}");

            var cleanMetrics = CalculateTargetGapMetrics(
                scenario,
                clean.Result.Candidate);

            var repairMetrics = CalculateTargetGapMetrics(
                scenario,
                repair.Result.Candidate);

            comparisons.Add(new MultiSeedComparison(
                Seed: seed,
                CleanRankedBetterThanRepair: ranker.IsBetterThan(
                    clean.Result.Evaluation,
                    repair.Result.Evaluation),
                RepairRankedBetterThanClean: ranker.IsBetterThan(
                    repair.Result.Evaluation,
                    clean.Result.Evaluation),
                CleanTotalPenalty: clean.Result.Evaluation.Score.TotalPenalty,
                RepairTotalPenalty: repair.Result.Evaluation.Score.TotalPenalty,
                CleanHardViolationCount: clean.Result.Evaluation.Score.HardViolationCount,
                RepairHardViolationCount: repair.Result.Evaluation.Score.HardViolationCount,
                CleanSoftViolationCount: clean.Result.Evaluation.Score.SoftViolationCount,
                RepairSoftViolationCount: repair.Result.Evaluation.Score.SoftViolationCount,
                CleanAssignmentCount: clean.Result.Candidate.Assignments.Count,
                RepairAssignmentCount: repair.Result.Candidate.Assignments.Count,
                CleanTotalAbsoluteTargetGapHours: cleanMetrics.TotalAbsoluteTargetGapHours,
                RepairTotalAbsoluteTargetGapHours: repairMetrics.TotalAbsoluteTargetGapHours,
                CleanElapsed: clean.Elapsed,
                RepairElapsed: repair.Elapsed));
        }

        var report = FormatActualRealWorldMultiSeedReport(
            scenario,
            comparisons);

        System.Console.WriteLine(report);

        Assert.Equal(MultiSeedComparisonSeeds.Length, comparisons.Count);
        Assert.Contains("Actual Real World Biweekly Multi-Seed Clean vs RepairAssisted GA Comparison", report);
        Assert.Contains("PerSeedResults:", report);
        Assert.Contains("Summary:", report);
        Assert.Contains("CleanWinCount", report);
        Assert.Contains("RepairWinCount", report);
        Assert.Contains("TieCount", report);
        Assert.Contains("AveragePenaltyDeltaRepairMinusClean", report);

        Assert.All(comparisons, comparison =>
        {
            Assert.False(
                comparison.CleanRankedBetterThanRepair &&
                comparison.RepairRankedBetterThanClean);

            Assert.Equal(2, comparison.CleanHardViolationCount);
            Assert.Equal(2, comparison.RepairHardViolationCount);
            Assert.True(comparison.CleanTotalPenalty >= 0);
            Assert.True(comparison.RepairTotalPenalty >= 0);
            Assert.True(comparison.CleanAssignmentCount > 0);
            Assert.True(comparison.RepairAssignmentCount > 0);
            Assert.True(comparison.CleanTotalAbsoluteTargetGapHours >= 0);
            Assert.True(comparison.RepairTotalAbsoluteTargetGapHours >= 0);
        });
    }


    [Fact]
    [Trait("Category", "SchedulingExperiment")]
    public void RepairAssisted_ShouldPrintAssignmentLossAndTransferOpportunityDiagnostics()
    {
        var scenario = CreateScenario();
        const int diagnosticSeed = 20260603;

        var clean = RunOptimizer(
            $"Clean GA seed {diagnosticSeed}",
            new GeneticScheduleOptimizer(
                populationSize: GeneticPopulationSize,
                seed: diagnosticSeed,
                generationCount: CleanGenerationCount,
                eliteCount: 1,
                tournamentSize: 3,
                evolutionMode: GeneticEvolutionMode.Clean),
            scenario.Problem);

        var repair = RunOptimizer(
            $"RepairAssisted GA seed {diagnosticSeed}",
            new GeneticScheduleOptimizer(
                populationSize: GeneticPopulationSize,
                seed: diagnosticSeed,
                generationCount: CleanGenerationCount,
                eliteCount: 1,
                tournamentSize: 3,
                evolutionMode: GeneticEvolutionMode.RepairAssisted),
            scenario.Problem);

        AssertValidActualScenarioRun(
            scenario,
            clean,
            $"Clean GA seed {diagnosticSeed}");

        AssertValidActualScenarioRun(
            scenario,
            repair,
            $"RepairAssisted GA seed {diagnosticSeed}");

        var shiftsById = scenario.Shifts.ToDictionary(shift => shift.Id);
        var demandByResourceId = scenario.ResourceWorkloadDemands
            .ToDictionary(demand => demand.ResourceId);

        var cleanHoursByResourceId = GetAssignedHoursByResourceId(clean.Result.Candidate);
        var repairHoursByResourceId = GetAssignedHoursByResourceId(repair.Result.Candidate);

        var cleanMetrics = CalculateTargetGapMetrics(
            scenario,
            clean.Result.Candidate);

        var repairMetrics = CalculateTargetGapMetrics(
            scenario,
            repair.Result.Candidate);

        var repairAssignmentKeys = repair.Result.Candidate.Assignments
            .Select(assignment => (assignment.ResourceId, assignment.ShiftId))
            .ToHashSet();

        var cleanOnlyAssignments = clean.Result.Candidate.Assignments
            .Where(assignment => !repairAssignmentKeys.Contains((assignment.ResourceId, assignment.ShiftId)))
            .OrderBy(assignment => shiftsById[assignment.ShiftId].StartUtc)
            .ThenBy(assignment => GetResourceName(scenario, assignment.ResourceId))
            .ToArray();

        var cleanCountByShiftId = clean.Result.Candidate.Assignments
            .GroupBy(assignment => assignment.ShiftId)
            .ToDictionary(group => group.Key, group => group.Count());

        var repairCountByShiftId = repair.Result.Candidate.Assignments
            .GroupBy(assignment => assignment.ShiftId)
            .ToDictionary(group => group.Key, group => group.Count());

        var shiftAssignmentDeficits = scenario.Shifts
            .Select(shift => new
            {
                Shift = shift,
                CleanCount = cleanCountByShiftId.GetValueOrDefault(shift.Id, 0),
                RepairCount = repairCountByShiftId.GetValueOrDefault(shift.Id, 0)
            })
            .Where(item => item.CleanCount > item.RepairCount)
            .OrderBy(item => item.Shift.StartUtc)
            .ToArray();

        var evaluator = new ScheduleEvaluator();
        var ranker = new ScheduleEvaluationResultRanker();

        var legalTransferAttempts = 0;
        var improvingTransferAttempts = 0;
        var improvingTransferLines = new List<string>();

        var overTargetResourceIds = scenario.Resources
            .Where(resource =>
            {
                var repairHours = repairHoursByResourceId.GetValueOrDefault(resource.Id, 0.0);
                var effectiveTargetHours = demandByResourceId[resource.Id].EffectiveTargetHours;

                return repairHours > effectiveTargetHours + HoursTolerance;
            })
            .Select(resource => resource.Id)
            .ToHashSet();

        var underTargetResources = scenario.Resources
            .Where(resource =>
            {
                var repairHours = repairHoursByResourceId.GetValueOrDefault(resource.Id, 0.0);
                var effectiveTargetHours = demandByResourceId[resource.Id].EffectiveTargetHours;

                return repairHours + HoursTolerance < effectiveTargetHours;
            })
            .OrderByDescending(resource =>
                demandByResourceId[resource.Id].EffectiveTargetHours -
                repairHoursByResourceId.GetValueOrDefault(resource.Id, 0.0))
            .ToArray();

        foreach (var sourceAssignment in repair.Result.Candidate.Assignments)
        {
            if (!overTargetResourceIds.Contains(sourceAssignment.ResourceId))
            {
                continue;
            }

            var shift = shiftsById[sourceAssignment.ShiftId];

            foreach (var targetResource in underTargetResources)
            {
                if (targetResource.Id == sourceAssignment.ResourceId)
                {
                    continue;
                }

                if (!CanTransferToTargetResource(
                        targetResource,
                        shift))
                {
                    continue;
                }

                ScheduleCandidate transferredCandidate;

                try
                {
                    transferredCandidate = ReplaceAssignmentResource(
                        sourceAssignment,
                        targetResource.Id);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                if (!IsCandidateValidForActualScenario(transferredCandidate))
                {
                    continue;
                }

                var childEvaluation = evaluator.Evaluate(
                    scenario.Problem,
                    transferredCandidate);

                if (!HasOnlyExpectedStructuralViolations(childEvaluation))
                {
                    continue;
                }

                legalTransferAttempts++;

                if (!ranker.IsBetterThan(
                        childEvaluation,
                        repair.Result.Evaluation))
                {
                    continue;
                }

                improvingTransferAttempts++;

                if (improvingTransferLines.Count >= 40)
                {
                    continue;
                }

                var sourceName = GetResourceName(scenario, sourceAssignment.ResourceId);
                var targetName = targetResource.Name;

                var sourceRepairHours = repairHoursByResourceId.GetValueOrDefault(
                    sourceAssignment.ResourceId,
                    0.0);

                var targetRepairHours = repairHoursByResourceId.GetValueOrDefault(
                    targetResource.Id,
                    0.0);

                improvingTransferLines.Add(
                    $"- Move {FormatShift(shift)} from {sourceName} to {targetName}; " +
                    $"Penalty {repair.Result.Evaluation.Score.TotalPenalty}->{childEvaluation.Score.TotalPenalty}; " +
                    $"FromHours {sourceRepairHours:0.##}/{demandByResourceId[sourceAssignment.ResourceId].EffectiveTargetHours:0.##}; " +
                    $"ToHours {targetRepairHours:0.##}/{demandByResourceId[targetResource.Id].EffectiveTargetHours:0.##}");
            }
        }

        var builder = new StringBuilder();

        builder.AppendLine("RepairAssisted Assignment Loss And Transfer Opportunity Diagnostics");
        builder.AppendLine($"Seed: {diagnosticSeed}");
        builder.AppendLine($"PopulationSize: {GeneticPopulationSize}");
        builder.AppendLine($"GenerationCount: {CleanGenerationCount}");
        builder.AppendLine();

        builder.AppendLine("AssignmentCountComparison:");
        builder.AppendLine($"- CleanAssignments: {clean.Result.Candidate.Assignments.Count}");
        builder.AppendLine($"- RepairAssignments: {repair.Result.Candidate.Assignments.Count}");
        builder.AppendLine($"- DeltaCleanMinusRepair: {clean.Result.Candidate.Assignments.Count - repair.Result.Candidate.Assignments.Count}");
        builder.AppendLine();

        builder.AppendLine("PenaltyAndTargetGapComparison:");
        builder.AppendLine($"- CleanTotalPenalty: {clean.Result.Evaluation.Score.TotalPenalty}");
        builder.AppendLine($"- RepairTotalPenalty: {repair.Result.Evaluation.Score.TotalPenalty}");
        builder.AppendLine($"- PenaltyDeltaRepairMinusClean: {repair.Result.Evaluation.Score.TotalPenalty - clean.Result.Evaluation.Score.TotalPenalty}");
        builder.AppendLine($"- CleanTargetGapHours: {cleanMetrics.TotalAbsoluteTargetGapHours:0.##}");
        builder.AppendLine($"- RepairTargetGapHours: {repairMetrics.TotalAbsoluteTargetGapHours:0.##}");
        builder.AppendLine($"- TargetGapDeltaRepairMinusClean: {repairMetrics.TotalAbsoluteTargetGapHours - cleanMetrics.TotalAbsoluteTargetGapHours:0.##}");
        builder.AppendLine();

        builder.AppendLine($"CleanOnlyAssignments: {cleanOnlyAssignments.Length}");

        foreach (var assignment in cleanOnlyAssignments.Take(40))
        {
            var shift = shiftsById[assignment.ShiftId];
            var resourceName = GetResourceName(scenario, assignment.ResourceId);
            var repairHours = repairHoursByResourceId.GetValueOrDefault(assignment.ResourceId, 0.0);
            var targetHours = demandByResourceId[assignment.ResourceId].EffectiveTargetHours;

            builder.AppendLine(
                $"- {resourceName} on {FormatShift(shift)}; " +
                $"CleanHours={cleanHoursByResourceId.GetValueOrDefault(assignment.ResourceId, 0.0):0.##}, " +
                $"RepairHours={repairHours:0.##}, " +
                $"EffectiveTarget={targetHours:0.##}, " +
                $"RepairGapToTarget={targetHours - repairHours:0.##}");
        }

        if (cleanOnlyAssignments.Length > 40)
        {
            builder.AppendLine($"- ... truncated {cleanOnlyAssignments.Length - 40} additional clean-only assignments");
        }

        builder.AppendLine();
        builder.AppendLine($"ShiftAssignmentCountDeficits: {shiftAssignmentDeficits.Length}");

        foreach (var deficit in shiftAssignmentDeficits.Take(40))
        {
            builder.AppendLine(
                $"- {FormatShift(deficit.Shift)}; " +
                $"CleanCount={deficit.CleanCount}, " +
                $"RepairCount={deficit.RepairCount}, " +
                $"Delta={deficit.CleanCount - deficit.RepairCount}");
        }

        if (shiftAssignmentDeficits.Length > 40)
        {
            builder.AppendLine($"- ... truncated {shiftAssignmentDeficits.Length - 40} additional shift deficits");
        }

        builder.AppendLine();
        builder.AppendLine("ResourceHourDeltaCleanMinusRepair:");

        foreach (var resource in scenario.Resources)
        {
            var cleanHours = cleanHoursByResourceId.GetValueOrDefault(resource.Id, 0.0);
            var repairHours = repairHoursByResourceId.GetValueOrDefault(resource.Id, 0.0);
            var delta = cleanHours - repairHours;

            if (Math.Abs(delta) <= HoursTolerance)
            {
                continue;
            }

            var targetHours = demandByResourceId[resource.Id].EffectiveTargetHours;

            builder.AppendLine(
                $"- {resource.Name}: " +
                $"CleanHours={cleanHours:0.##}, " +
                $"RepairHours={repairHours:0.##}, " +
                $"CleanMinusRepair={delta:0.##}, " +
                $"EffectiveTarget={targetHours:0.##}, " +
                $"RepairGapToTarget={targetHours - repairHours:0.##}");
        }

        builder.AppendLine();
        builder.AppendLine("TransferOpportunityDiagnostics:");
        builder.AppendLine($"- OverTargetResourceCount: {overTargetResourceIds.Count}");
        builder.AppendLine($"- UnderTargetResourceCount: {underTargetResources.Length}");
        builder.AppendLine($"- LegalTransferAttempts: {legalTransferAttempts}");
        builder.AppendLine($"- ImprovingTransferAttempts: {improvingTransferAttempts}");

        if (improvingTransferLines.Count == 0)
        {
            builder.AppendLine("- No improving transfer opportunity was found from the final RepairAssisted candidate.");
        }
        else
        {
            foreach (var line in improvingTransferLines)
            {
                builder.AppendLine(line);
            }
        }

        var report = builder.ToString();
        System.Console.WriteLine(report);

        Assert.Contains("RepairAssisted Assignment Loss And Transfer Opportunity Diagnostics", report);
        Assert.Contains("AssignmentCountComparison:", report);
        Assert.Contains("CleanOnlyAssignments:", report);
        Assert.Contains("ShiftAssignmentCountDeficits:", report);
        Assert.Contains("ResourceHourDeltaCleanMinusRepair:", report);
        Assert.Contains("TransferOpportunityDiagnostics:", report);

        IReadOnlyDictionary<Guid, double> GetAssignedHoursByResourceId(
            ScheduleCandidate candidate)
        {
            return candidate.Assignments
                .GroupBy(assignment => assignment.ResourceId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(assignment =>
                        GetShiftHours(shiftsById[assignment.ShiftId])));
        }

        bool CanTransferToTargetResource(
            Resource targetResource,
            Shift shift)
        {
            if (repair.Result.Candidate.Assignments.Any(assignment =>
                    assignment.ResourceId == targetResource.Id &&
                    assignment.ShiftId == shift.Id))
            {
                return false;
            }

            if (!scenario.AvailabilityWindows.Any(window =>
                    window.ResourceId == targetResource.Id &&
                    window.Covers(shift)))
            {
                return false;
            }

            if (shift.RequiresPreferenceToAssign &&
                !scenario.ResourcePreferences.Any(preference =>
                    preference.ResourceId == targetResource.Id &&
                    preference.Type == ResourcePreferenceType.Prefer &&
                    Overlaps(
                        preference.StartUtc,
                        preference.EndUtc,
                        shift.StartUtc,
                        shift.EndUtc)))
            {
                return false;
            }

            return !repair.Result.Candidate.Assignments
                .Where(assignment => assignment.ResourceId == targetResource.Id)
                .Select(assignment => shiftsById[assignment.ShiftId])
                .Any(existingShift => Overlaps(
                    existingShift.StartUtc,
                    existingShift.EndUtc,
                    shift.StartUtc,
                    shift.EndUtc));
        }

        ScheduleCandidate ReplaceAssignmentResource(
            Assignment sourceAssignment,
            Guid replacementResourceId)
        {
            return new ScheduleCandidate(repair.Result.Candidate.Assignments
                .Select(assignment =>
                    assignment.ResourceId == sourceAssignment.ResourceId &&
                    assignment.ShiftId == sourceAssignment.ShiftId
                        ? new Assignment(replacementResourceId, sourceAssignment.ShiftId)
                        : assignment)
                .ToArray());
        }

        bool IsCandidateValidForActualScenario(ScheduleCandidate candidate)
        {
            return HasNoMoreThanOneRegularNightAssignmentPerResource(candidate) &&
                   HasValidMonthlyNightQuotaPerCategory(candidate);
        }

        bool HasNoMoreThanOneRegularNightAssignmentPerResource(ScheduleCandidate candidate)
        {
            return candidate.Assignments
                .Select(assignment => new
                {
                    assignment.ResourceId,
                    Shift = shiftsById[assignment.ShiftId]
                })
                .Where(item => item.Shift.NightShiftCategory == NightShiftCategory.Regular)
                .GroupBy(item => item.ResourceId)
                .All(group => group.Count() <= 1);
        }

        bool HasValidMonthlyNightQuotaPerCategory(ScheduleCandidate candidate)
        {
            return GetMonthlyNightQuotaGroups(
                    scenario,
                    candidate)
                .All(item => item.Count <= 1);
        }

        bool HasOnlyExpectedStructuralViolations(ScheduleEvaluationResult evaluation)
        {
            if (CountViolations(evaluation, ConstraintViolationType.ResourceUnavailable) != 0 ||
                CountViolations(evaluation, ConstraintViolationType.ResourceAssignedToOverlappingShifts) != 0 ||
                CountViolations(evaluation, ConstraintViolationType.AssignedWithoutRequiredPreference) != 0 ||
                CountViolations(evaluation, ConstraintViolationType.ShiftOverstaffed) != 0 ||
                CountViolations(evaluation, ConstraintViolationType.ShiftSequenceQuotaExceeded) != 0 ||
                CountViolations(evaluation, ConstraintViolationType.ResourceMonthlyNightShiftQuotaExceeded) != 0)
            {
                return false;
            }

            var understaffedViolations = evaluation.Violations
                .Where(violation => violation.Type == ConstraintViolationType.ShiftUnderstaffed)
                .ToArray();

            return understaffedViolations.Length == 2 &&
                   understaffedViolations.All(violation =>
                       violation.ShiftId is not null &&
                       IsKnownRealUnderstaffedShift(shiftsById[violation.ShiftId.Value]));
        }
    }


    [Fact]
    [Trait("Category", "SchedulingExperiment")]
    public void CleanGeneticOptimizer_ShouldPrintEngineProgressDiagnostics()
    {
        var scenario = CreateScenario();
        var diagnostics = new List<CleanGaEngineRunDiagnostic>();
        var generationTraceBySeed = new Dictionary<int, IReadOnlyList<GeneticGenerationDiagnostic>>();

        foreach (var seed in CleanEngineDiagnosticSeeds)
        {
            var diagnosticsSink = new CollectingGeneticOptimizerDiagnosticsSink();

            var run = RunOptimizer(
                $"Clean GA seed {seed}",
                new GeneticScheduleOptimizer(
                    populationSize: GeneticPopulationSize,
                    seed: seed,
                    generationCount: CleanGenerationCount,
                    eliteCount: 1,
                    tournamentSize: 3,
                    diagnosticsSink: diagnosticsSink,
                    evolutionMode: GeneticEvolutionMode.Clean),
                scenario.Problem,
                diagnosticsSink.Diagnostics);

            AssertCandidateReferencesKnownProblemEntities(
                scenario,
                run.Result.Candidate);

            AssertOnlyExpectedRealStructuralViolations(
                scenario,
                run.Result.Evaluation);

            AssertNoMoreThanOneRegularNightAssignmentPerResource(
                scenario,
                run.Result.Candidate);

            AssertMonthlyNightQuotaPerCategory(
                scenario,
                run.Result.Candidate,
                $"Clean GA seed {seed}");

            Assert.Equal(
                CleanGenerationCount + 1,
                diagnosticsSink.Diagnostics.Count);

            var metrics = CalculateTargetGapMetrics(
                scenario,
                run.Result.Candidate);

            var firstBestGeneration = FindFirstGenerationWithBestSoFarPenalty(
                diagnosticsSink.Diagnostics);

            var initialDiagnostic = diagnosticsSink.Diagnostics[0];
            var finalDiagnostic = diagnosticsSink.Diagnostics[^1];

            diagnostics.Add(new CleanGaEngineRunDiagnostic(
                Seed: seed,
                InitialBestTotalPenalty: initialDiagnostic.BestSoFarTotalPenalty,
                FinalBestTotalPenalty: finalDiagnostic.BestSoFarTotalPenalty,
                PenaltyImprovement: initialDiagnostic.BestSoFarTotalPenalty - finalDiagnostic.BestSoFarTotalPenalty,
                InitialBestHardViolationCount: initialDiagnostic.BestSoFarHardViolationCount,
                FinalBestHardViolationCount: finalDiagnostic.BestSoFarHardViolationCount,
                InitialBestSoftViolationCount: initialDiagnostic.BestSoFarSoftViolationCount,
                FinalBestSoftViolationCount: finalDiagnostic.BestSoFarSoftViolationCount,
                FirstBestGeneration: firstBestGeneration,
                AssignmentCount: run.Result.Candidate.Assignments.Count,
                TotalAbsoluteTargetGapHours: metrics.TotalAbsoluteTargetGapHours,
                Elapsed: run.Elapsed));

            generationTraceBySeed[seed] = diagnosticsSink.Diagnostics;
        }

        var report = FormatCleanGaEngineDiagnosticsReport(
            diagnostics,
            generationTraceBySeed);

        System.Console.WriteLine(report);

        Assert.Contains("Clean GA Engine Progress Diagnostics", report);
        Assert.Contains("PerSeedProgress:", report);
        Assert.Contains("Summary:", report);
        Assert.Contains("GenerationTraceForSeed", report);
        Assert.Contains("DiagnosticLimitations:", report);

        Assert.Equal(CleanEngineDiagnosticSeeds.Length, diagnostics.Count);

        Assert.All(diagnostics, diagnostic =>
        {
            Assert.True(diagnostic.FinalBestTotalPenalty <= diagnostic.InitialBestTotalPenalty);
            Assert.Equal(2, diagnostic.FinalBestHardViolationCount);
            Assert.True(diagnostic.AssignmentCount > 0);
            Assert.True(diagnostic.TotalAbsoluteTargetGapHours >= 0);
        });

        static int FindFirstGenerationWithBestSoFarPenalty(
            IReadOnlyList<GeneticGenerationDiagnostic> generationDiagnostics)
        {
            var bestPenalty = generationDiagnostics[^1].BestSoFarTotalPenalty;

            return generationDiagnostics
                .First(diagnostic => diagnostic.BestSoFarTotalPenalty == bestPenalty)
                .GenerationIndex;
        }
    }


    [Fact]
    [Trait("Category", "SchedulingExperiment")]
    public void CleanGeneticOptimizer_ShouldPrintComponentDiagnostics()
    {
        var scenario = CreateScenario();

        var seeds = new[]
        {
            20260603,
            20260604,
            20260605
        };

        const int componentGenerationCount = 10;

        var reports = new List<CleanGaComponentSeedDiagnostic>();
        var tracesBySeedAndMode = new Dictionary<(int Seed, CleanGaComponentMode Mode), IReadOnlyList<CleanGaComponentGenerationDiagnostic>>();

        foreach (var seed in seeds)
        {
            var productionDiagnosticsSink = new CollectingGeneticOptimizerDiagnosticsSink();

            var productionClean = RunOptimizer(
                $"Production Clean GA seed {seed}",
                new GeneticScheduleOptimizer(
                    populationSize: GeneticPopulationSize,
                    seed: seed,
                    generationCount: componentGenerationCount,
                    eliteCount: 1,
                    tournamentSize: 3,
                    diagnosticsSink: productionDiagnosticsSink,
                    evolutionMode: GeneticEvolutionMode.Clean),
                scenario.Problem,
                productionDiagnosticsSink.Diagnostics);

            AssertCandidateReferencesKnownProblemEntities(
                scenario,
                productionClean.Result.Candidate);

            AssertOnlyExpectedRealStructuralViolations(
                scenario,
                productionClean.Result.Evaluation);

            AssertNoMoreThanOneRegularNightAssignmentPerResource(
                scenario,
                productionClean.Result.Candidate);

            AssertMonthlyNightQuotaPerCategory(
                scenario,
                productionClean.Result.Candidate,
                $"Production Clean GA seed {seed}");

            var crossoverOnly = RunCleanGaComponentHarness(
                scenario,
                seed,
                componentGenerationCount,
                CleanGaComponentMode.CrossoverOnly);

            var mutationOnly = RunCleanGaComponentHarness(
                scenario,
                seed,
                componentGenerationCount,
                CleanGaComponentMode.MutationOnly);

            var crossoverThenMutation = RunCleanGaComponentHarness(
                scenario,
                seed,
                componentGenerationCount,
                CleanGaComponentMode.CrossoverThenMutation);

            tracesBySeedAndMode[(seed, CleanGaComponentMode.CrossoverOnly)] =
                crossoverOnly.GenerationDiagnostics;

            tracesBySeedAndMode[(seed, CleanGaComponentMode.MutationOnly)] =
                mutationOnly.GenerationDiagnostics;

            tracesBySeedAndMode[(seed, CleanGaComponentMode.CrossoverThenMutation)] =
                crossoverThenMutation.GenerationDiagnostics;

            reports.Add(new CleanGaComponentSeedDiagnostic(
                Seed: seed,
                ProductionClean: CreateComponentResultSummary(
                    scenario,
                    productionClean.Result,
                    productionDiagnosticsSink.Diagnostics[0].BestSoFarTotalPenalty,
                    productionDiagnosticsSink.Diagnostics[^1].BestSoFarTotalPenalty,
                    productionDiagnosticsSink.Diagnostics
                        .First(diagnostic => diagnostic.BestSoFarTotalPenalty ==
                                             productionDiagnosticsSink.Diagnostics[^1].BestSoFarTotalPenalty)
                        .GenerationIndex),
                CrossoverOnly: crossoverOnly.Summary,
                MutationOnly: mutationOnly.Summary,
                CrossoverThenMutation: crossoverThenMutation.Summary));
        }

        var report = FormatCleanGaComponentDiagnosticsReport(
            reports,
            tracesBySeedAndMode);

        System.Console.WriteLine(report);

        Assert.Contains("Stage 7.5C Clean GA Component Diagnostics", report);
        Assert.Contains("ComponentDefinitions:", report);
        Assert.Contains("PerSeedResults:", report);
        Assert.Contains("Summary:", report);
        Assert.Contains("DiagnosticLimitations:", report);

        Assert.Equal(seeds.Length, reports.Count);

        Assert.All(reports, seedReport =>
        {
            Assert.True(seedReport.ProductionClean.FinalPenalty <= seedReport.ProductionClean.InitialPenalty);
            Assert.True(seedReport.CrossoverOnly.FinalPenalty <= seedReport.CrossoverOnly.InitialPenalty);
            Assert.True(seedReport.MutationOnly.FinalPenalty <= seedReport.MutationOnly.InitialPenalty);
            Assert.True(seedReport.CrossoverThenMutation.FinalPenalty <= seedReport.CrossoverThenMutation.InitialPenalty);
        });
    }

    private static void AssertWeekdayCapacity(Shift shift)
    {
        if (shift.Kind == ShiftKind.Morning)
        {
            Assert.Equal(4, shift.MinResourceCount);
            Assert.Equal(6, shift.MaxResourceCount);
            Assert.Null(shift.NightShiftCategory);
            return;
        }

        if (shift.Kind == ShiftKind.Afternoon)
        {
            Assert.Equal(2, shift.MinResourceCount);
            Assert.Equal(4, shift.MaxResourceCount);
            Assert.Null(shift.NightShiftCategory);
            return;
        }

        Assert.Equal(ShiftKind.Night, shift.Kind);
        Assert.Equal(0, shift.MinResourceCount);
        Assert.Equal(1, shift.MaxResourceCount);
        Assert.Equal(NightShiftCategory.Regular, shift.NightShiftCategory);
    }

    private static void AssertFridayCapacity(Shift shift)
    {
        if (shift.Kind == ShiftKind.Morning)
        {
            Assert.Equal(0, shift.MinResourceCount);
            Assert.Equal(2, shift.MaxResourceCount);
            Assert.Null(shift.NightShiftCategory);
            return;
        }

        if (shift.Kind == ShiftKind.Afternoon)
        {
            Assert.Equal(0, shift.MinResourceCount);
            Assert.Equal(1, shift.MaxResourceCount);
            Assert.Null(shift.NightShiftCategory);
            return;
        }

        Assert.Equal(ShiftKind.Night, shift.Kind);
        Assert.Equal(0, shift.MinResourceCount);
        Assert.Equal(1, shift.MaxResourceCount);
        Assert.Equal(NightShiftCategory.FridayNight, shift.NightShiftCategory);
    }

    private static void AssertSaturdayCapacity(Shift shift)
    {
        if (shift.Kind == ShiftKind.Morning)
        {
            Assert.Equal(0, shift.MinResourceCount);
            Assert.Equal(1, shift.MaxResourceCount);
            Assert.Null(shift.NightShiftCategory);
            return;
        }

        if (shift.Kind == ShiftKind.Afternoon)
        {
            Assert.Equal(0, shift.MinResourceCount);
            Assert.Equal(1, shift.MaxResourceCount);
            Assert.Null(shift.NightShiftCategory);
            return;
        }

        Assert.Equal(ShiftKind.Night, shift.Kind);
        Assert.Equal(3, shift.MinResourceCount);
        Assert.Equal(3, shift.MaxResourceCount);
        Assert.Equal(NightShiftCategory.MotzeiShabbatNight, shift.NightShiftCategory);
    }

    private static ExperimentScenario CreateScenario()
    {
        var resources = CreateResources();
        var shifts = CreateBiWeeklyShifts();

        var availabilityWindows = new List<AvailabilityWindow>();
        var preferences = new List<ResourcePreference>();

        AddActualSubmissions(
            resources,
            shifts,
            availabilityWindows,
            preferences);

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
            resourceWorkloadDemands: workloadDemands);

        return new ExperimentScenario(
            Problem: problem,
            Resources: resources,
            Shifts: shifts,
            AvailabilityWindows: availabilityWindows,
            ResourcePreferences: preferences,
            ResourceWorkloadDemands: workloadDemands);
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
            resource => GetSubmittedPreferredHours(
                resource,
                shifts,
                preferences));

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
                var submittedPreferredHours = submittedPreferredHoursByResourceId[resource.Id];

                var requestedPreferredHours =
                    ExpectedTotalEffectiveTargetHours *
                    submittedPreferredHours /
                    totalSubmittedPreferredHours;

                return new ResourceWorkloadDemand(
                    resource.Id,
                    requestedPreferredHours: requestedPreferredHours,
                    minimumRequiredHours: 0);
            })
            .ToArray();
    }

    private static IReadOnlyList<Resource> CreateResources()
    {
        return GetExpectedResourceNames()
            .Select(CreateResource)
            .ToArray();
    }

    private static string[] GetExpectedResourceNames()
    {
        return
        [
            "Worker14",
            "Worker02",
            "עיWorker05י",
            "Worker11",
            "Worker03",
            "Worker10",
            "Worker12",
            "Worker13",
            "Worker09 סעדון",
            "Worker04",
            "Worker05",
            "Worker08",
            "עדנה",
            "Worker16 אלדר",
            "Worker19 א",
            "Worker17 Worker17",
            "Worker15י",
            "Worker18"
        ];
    }

    private static Resource CreateResource(string name)
    {
        return new Resource(
            Guid.NewGuid(),
            name,
            hourlyCost: 100m);
    }

    private static IReadOnlyList<Shift> CreateBiWeeklyShifts()
    {
        var shifts = new List<Shift>();

        for (var dayOffset = 0; dayOffset < DaysInSchedule; dayOffset++)
        {
            var date = new DateOnly(2026, 5, 31).AddDays(dayOffset);

            shifts.Add(CreateShift(date, ShiftKind.Morning));
            shifts.Add(CreateShift(date, ShiftKind.Afternoon));
            shifts.Add(CreateShift(date, ShiftKind.Night));
        }

        return shifts
            .OrderBy(shift => shift.StartUtc)
            .ToArray();
    }

    private static Shift CreateShift(
        DateOnly date,
        ShiftKind kind)
    {
        var startUtc = GetStartUtc(date, kind);
        var endUtc = GetEndUtc(date, kind);
        var capacity = GetCapacityRule(date, kind);

        return new Shift(
            Guid.NewGuid(),
            startUtc,
            endUtc,
            kind,
            minResourceCount: capacity.MinResourceCount,
            maxResourceCount: capacity.MaxResourceCount,
            requiresPreferenceToAssign: true,
            requiresMinimumWhenPreferenceExists: false,
            nightShiftCategory: GetNightShiftCategory(date, kind));
    }

    private static void AddActualSubmissions(
        IReadOnlyList<Resource> resources,
        IReadOnlyList<Shift> shifts,
        ICollection<AvailabilityWindow> availabilityWindows,
        ICollection<ResourcePreference> preferences)
    {
        var resourcesByName = resources.ToDictionary(resource => resource.Name);

        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker14", new DateOnly(2026, 5, 31), ShiftKind.Afternoon);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker14", new DateOnly(2026, 6, 1), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker14", new DateOnly(2026, 6, 2), ShiftKind.Night);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker14", new DateOnly(2026, 6, 3), ShiftKind.Afternoon);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker14", new DateOnly(2026, 6, 4), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker14", new DateOnly(2026, 6, 6), ShiftKind.Night);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker14", new DateOnly(2026, 6, 7), ShiftKind.Afternoon);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker14", new DateOnly(2026, 6, 8), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker14", new DateOnly(2026, 6, 9), ShiftKind.Afternoon);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker14", new DateOnly(2026, 6, 11), ShiftKind.Morning);

        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker02", new DateOnly(2026, 5, 31), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker02", new DateOnly(2026, 6, 1), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker02", new DateOnly(2026, 6, 7), ShiftKind.Morning);

        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "עיWorker05י", new DateOnly(2026, 5, 31), ShiftKind.Night);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "עיWorker05י", new DateOnly(2026, 6, 4), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "עיWorker05י", new DateOnly(2026, 6, 4), ShiftKind.Afternoon);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "עיWorker05י", new DateOnly(2026, 6, 5), ShiftKind.Morning);

        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker11", new DateOnly(2026, 5, 31), ShiftKind.Afternoon);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker11", new DateOnly(2026, 6, 4), ShiftKind.Night);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker11", new DateOnly(2026, 6, 7), ShiftKind.Afternoon);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker11", new DateOnly(2026, 6, 9), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker11", new DateOnly(2026, 6, 12), ShiftKind.Morning);

        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker03", new DateOnly(2026, 5, 31), ShiftKind.Afternoon);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker03", new DateOnly(2026, 6, 3), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker03", new DateOnly(2026, 6, 7), ShiftKind.Afternoon);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker03", new DateOnly(2026, 6, 10), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker03", new DateOnly(2026, 6, 12), ShiftKind.Morning);

        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker10", new DateOnly(2026, 6, 4), ShiftKind.Afternoon);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker10", new DateOnly(2026, 6, 6), ShiftKind.Night);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker10", new DateOnly(2026, 6, 9), ShiftKind.Night);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker10", new DateOnly(2026, 6, 12), ShiftKind.Morning);

        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker12", new DateOnly(2026, 6, 1), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker12", new DateOnly(2026, 6, 1), ShiftKind.Afternoon);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker12", new DateOnly(2026, 6, 3), ShiftKind.Night);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker12", new DateOnly(2026, 6, 8), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker12", new DateOnly(2026, 6, 10), ShiftKind.Afternoon);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker12", new DateOnly(2026, 6, 11), ShiftKind.Morning);

        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker13", new DateOnly(2026, 6, 1), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker13", new DateOnly(2026, 6, 3), ShiftKind.Afternoon);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker13", new DateOnly(2026, 6, 4), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker13", new DateOnly(2026, 6, 7), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker13", new DateOnly(2026, 6, 8), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker13", new DateOnly(2026, 6, 9), ShiftKind.Afternoon);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker13", new DateOnly(2026, 6, 10), ShiftKind.Night);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker13", new DateOnly(2026, 6, 11), ShiftKind.Afternoon);

        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker09 סעדון", new DateOnly(2026, 6, 2), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker09 סעדון", new DateOnly(2026, 6, 3), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker09 סעדון", new DateOnly(2026, 6, 4), ShiftKind.Afternoon);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker09 סעדון", new DateOnly(2026, 6, 8), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker09 סעדון", new DateOnly(2026, 6, 8), ShiftKind.Night);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker09 סעדון", new DateOnly(2026, 6, 9), ShiftKind.Afternoon);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker09 סעדון", new DateOnly(2026, 6, 10), ShiftKind.Afternoon);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker09 סעדון", new DateOnly(2026, 6, 11), ShiftKind.Morning);

        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker04", new DateOnly(2026, 6, 1), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker04", new DateOnly(2026, 6, 2), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker04", new DateOnly(2026, 6, 3), ShiftKind.Afternoon);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker04", new DateOnly(2026, 6, 4), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker04", new DateOnly(2026, 6, 4), ShiftKind.Afternoon);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker04", new DateOnly(2026, 6, 8), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker04", new DateOnly(2026, 6, 9), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker04", new DateOnly(2026, 6, 10), ShiftKind.Afternoon);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker04", new DateOnly(2026, 6, 11), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker04", new DateOnly(2026, 6, 13), ShiftKind.Night);

        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker05", new DateOnly(2026, 6, 1), ShiftKind.Afternoon);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker05", new DateOnly(2026, 6, 2), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker05", new DateOnly(2026, 6, 7), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker05", new DateOnly(2026, 6, 7), ShiftKind.Night);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker05", new DateOnly(2026, 6, 8), ShiftKind.Afternoon);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker05", new DateOnly(2026, 6, 9), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker05", new DateOnly(2026, 6, 11), ShiftKind.Afternoon);

        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker08", new DateOnly(2026, 5, 31), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker08", new DateOnly(2026, 6, 2), ShiftKind.Afternoon);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker08", new DateOnly(2026, 6, 3), ShiftKind.Afternoon);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker08", new DateOnly(2026, 6, 7), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker08", new DateOnly(2026, 6, 9), ShiftKind.Afternoon);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker08", new DateOnly(2026, 6, 10), ShiftKind.Morning);

        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "עדנה", new DateOnly(2026, 6, 3), ShiftKind.Afternoon);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "עדנה", new DateOnly(2026, 6, 6), ShiftKind.Night);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "עדנה", new DateOnly(2026, 6, 7), ShiftKind.Afternoon);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "עדנה", new DateOnly(2026, 6, 9), ShiftKind.Morning);

        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker16 אלדר", new DateOnly(2026, 6, 2), ShiftKind.Afternoon);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker16 אלדר", new DateOnly(2026, 6, 3), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker16 אלדר", new DateOnly(2026, 6, 4), ShiftKind.Afternoon);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker16 אלדר", new DateOnly(2026, 6, 7), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker16 אלדר", new DateOnly(2026, 6, 8), ShiftKind.Afternoon);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker16 אלדר", new DateOnly(2026, 6, 10), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker16 אלדר", new DateOnly(2026, 6, 10), ShiftKind.Afternoon);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker16 אלדר", new DateOnly(2026, 6, 11), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker16 אלדר", new DateOnly(2026, 6, 13), ShiftKind.Night);

        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker19 א", new DateOnly(2026, 5, 31), ShiftKind.Morning);

        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker17 Worker17", new DateOnly(2026, 5, 31), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker17 Worker17", new DateOnly(2026, 6, 1), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker17 Worker17", new DateOnly(2026, 6, 1), ShiftKind.Afternoon);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker17 Worker17", new DateOnly(2026, 6, 5), ShiftKind.Night);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker17 Worker17", new DateOnly(2026, 6, 6), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker17 Worker17", new DateOnly(2026, 6, 13), ShiftKind.Morning);

        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker15י", new DateOnly(2026, 5, 31), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker15י", new DateOnly(2026, 6, 1), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker15י", new DateOnly(2026, 6, 1), ShiftKind.Night);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker15י", new DateOnly(2026, 6, 2), ShiftKind.Afternoon);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker15י", new DateOnly(2026, 6, 13), ShiftKind.Night);

        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker18", new DateOnly(2026, 5, 31), ShiftKind.Afternoon);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker18", new DateOnly(2026, 6, 2), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker18", new DateOnly(2026, 6, 5), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker18", new DateOnly(2026, 6, 7), ShiftKind.Morning);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker18", new DateOnly(2026, 6, 9), ShiftKind.Afternoon);
        AddSubmission(resourcesByName, shifts, availabilityWindows, preferences, "Worker18", new DateOnly(2026, 6, 11), ShiftKind.Morning);
    }

    private static void AddSubmission(
        IReadOnlyDictionary<string, Resource> resourcesByName,
        IReadOnlyList<Shift> shifts,
        ICollection<AvailabilityWindow> availabilityWindows,
        ICollection<ResourcePreference> preferences,
        string resourceName,
        DateOnly date,
        ShiftKind kind)
    {
        var resource = resourcesByName[resourceName];
        var shift = GetShift(shifts, date, kind);

        availabilityWindows.Add(new AvailabilityWindow(
            resource.Id,
            shift.StartUtc,
            shift.EndUtc));

        preferences.Add(new ResourcePreference(
            resource.Id,
            shift.StartUtc,
            shift.EndUtc,
            ResourcePreferenceType.Prefer,
            ResourcePreferencePriority.High));
    }

    private static Shift GetShift(
        IReadOnlyList<Shift> shifts,
        DateOnly date,
        ShiftKind kind)
    {
        return shifts.Single(shift =>
            DateOnly.FromDateTime(shift.StartUtc) == date &&
            shift.Kind == kind);
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
            ShiftKind.Morning => (4, 6),
            ShiftKind.Afternoon => (2, 4),
            ShiftKind.Night => (0, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private static DateTime GetStartUtc(
        DateOnly date,
        ShiftKind kind)
    {
        return kind switch
        {
            ShiftKind.Morning => date.ToDateTime(
                new TimeOnly(6, 30),
                DateTimeKind.Utc),

            ShiftKind.Afternoon => date.ToDateTime(
                new TimeOnly(14, 30),
                DateTimeKind.Utc),

            ShiftKind.Night => date.ToDateTime(
                new TimeOnly(22, 30),
                DateTimeKind.Utc),

            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private static DateTime GetEndUtc(
        DateOnly date,
        ShiftKind kind)
    {
        return kind switch
        {
            ShiftKind.Morning => date.ToDateTime(
                new TimeOnly(14, 30),
                DateTimeKind.Utc),

            ShiftKind.Afternoon => date.ToDateTime(
                new TimeOnly(22, 30),
                DateTimeKind.Utc),

            ShiftKind.Night => date.AddDays(1).ToDateTime(
                new TimeOnly(6, 30),
                DateTimeKind.Utc),

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


    private static void AssertValidActualScenarioRun(
        ExperimentScenario scenario,
        ExperimentRun run,
        string reportContext)
    {
        AssertCandidateReferencesKnownProblemEntities(
            scenario,
            run.Result.Candidate);

        AssertOnlyExpectedRealStructuralViolations(
            scenario,
            run.Result.Evaluation);

        AssertNoMoreThanOneRegularNightAssignmentPerResource(
            scenario,
            run.Result.Candidate);

        AssertMonthlyNightQuotaPerCategory(
            scenario,
            run.Result.Candidate,
            reportContext);
    }

    private static string FormatActualRealWorldMultiSeedReport(
        ExperimentScenario scenario,
        IReadOnlyCollection<MultiSeedComparison> comparisons)
    {
        var orderedComparisons = comparisons
            .OrderBy(comparison => comparison.Seed)
            .ToArray();

        var cleanWinCount = orderedComparisons.Count(comparison => comparison.CleanRankedBetterThanRepair);
        var repairWinCount = orderedComparisons.Count(comparison => comparison.RepairRankedBetterThanClean);
        var tieCount = orderedComparisons.Length - cleanWinCount - repairWinCount;

        var averagePenaltyDeltaRepairMinusClean = orderedComparisons.Average(comparison =>
            comparison.RepairTotalPenalty - comparison.CleanTotalPenalty);

        var averageCleanTargetGapHours = orderedComparisons.Average(comparison =>
            comparison.CleanTotalAbsoluteTargetGapHours);

        var averageRepairTargetGapHours = orderedComparisons.Average(comparison =>
            comparison.RepairTotalAbsoluteTargetGapHours);

        var builder = new StringBuilder();

        builder.AppendLine("Actual Real World Biweekly Multi-Seed Clean vs RepairAssisted GA Comparison");
        builder.AppendLine("Period: 2026-05-31 through 2026-06-13");
        builder.AppendLine($"Resources: {scenario.Resources.Count}");
        builder.AppendLine($"Shifts: {scenario.Shifts.Count}");
        builder.AppendLine($"Submissions: {scenario.ResourcePreferences.Count}");
        builder.AppendLine($"PopulationSize: {GeneticPopulationSize}");
        builder.AppendLine($"GenerationCount: {CleanGenerationCount}");
        builder.AppendLine($"SeedCount: {orderedComparisons.Length}");
        builder.AppendLine();

        builder.AppendLine("KnownRealShortages:");
        builder.AppendLine("- 2026-06-03 Morning: submitted=3, requiredMin=4");
        builder.AppendLine("- 2026-06-10 Morning: submitted=3, requiredMin=4");
        builder.AppendLine();

        builder.AppendLine("PerSeedResults:");

        foreach (var comparison in orderedComparisons)
        {
            builder.AppendLine(
                $"- Seed {comparison.Seed}: " +
                $"Winner={GetMultiSeedWinner(comparison)}, " +
                $"CleanTotalPenalty={comparison.CleanTotalPenalty}, " +
                $"RepairTotalPenalty={comparison.RepairTotalPenalty}, " +
                $"PenaltyDeltaRepairMinusClean={comparison.RepairTotalPenalty - comparison.CleanTotalPenalty}, " +
                $"CleanSoftViolationCount={comparison.CleanSoftViolationCount}, " +
                $"RepairSoftViolationCount={comparison.RepairSoftViolationCount}, " +
                $"CleanAssignments={comparison.CleanAssignmentCount}, " +
                $"RepairAssignments={comparison.RepairAssignmentCount}, " +
                $"CleanTargetGapHours={comparison.CleanTotalAbsoluteTargetGapHours:0.##}, " +
                $"RepairTargetGapHours={comparison.RepairTotalAbsoluteTargetGapHours:0.##}, " +
                $"CleanRuntimeMs={comparison.CleanElapsed.TotalMilliseconds:0.00}, " +
                $"RepairRuntimeMs={comparison.RepairElapsed.TotalMilliseconds:0.00}");
        }

        builder.AppendLine();
        builder.AppendLine("Summary:");
        builder.AppendLine($"CleanWinCount: {cleanWinCount}");
        builder.AppendLine($"RepairWinCount: {repairWinCount}");
        builder.AppendLine($"TieCount: {tieCount}");
        builder.AppendLine($"CleanWinRate: {100.0 * cleanWinCount / orderedComparisons.Length:0.##}%");
        builder.AppendLine($"RepairWinRate: {100.0 * repairWinCount / orderedComparisons.Length:0.##}%");
        builder.AppendLine($"AveragePenaltyDeltaRepairMinusClean: {averagePenaltyDeltaRepairMinusClean:0.##}");
        builder.AppendLine($"AverageCleanTargetGapHours: {averageCleanTargetGapHours:0.##}");
        builder.AppendLine($"AverageRepairTargetGapHours: {averageRepairTargetGapHours:0.##}");

        return builder.ToString();
    }

    private static string GetMultiSeedWinner(MultiSeedComparison comparison)
    {
        if (comparison.CleanRankedBetterThanRepair)
        {
            return "Clean";
        }

        if (comparison.RepairRankedBetterThanClean)
        {
            return "RepairAssisted";
        }

        return "Tie";
    }


    private static string FormatCleanGaEngineDiagnosticsReport(
        IReadOnlyCollection<CleanGaEngineRunDiagnostic> diagnostics,
        IReadOnlyDictionary<int, IReadOnlyList<GeneticGenerationDiagnostic>> generationTraceBySeed)
    {
        var orderedDiagnostics = diagnostics
            .OrderBy(diagnostic => diagnostic.Seed)
            .ToArray();

        var improvedSeedCount = orderedDiagnostics.Count(diagnostic =>
            diagnostic.PenaltyImprovement > 0);

        var distinctFinalPenaltyCount = orderedDiagnostics
            .Select(diagnostic => diagnostic.FinalBestTotalPenalty)
            .Distinct()
            .Count();

        var averageInitialPenalty = orderedDiagnostics.Average(diagnostic =>
            diagnostic.InitialBestTotalPenalty);

        var averageFinalPenalty = orderedDiagnostics.Average(diagnostic =>
            diagnostic.FinalBestTotalPenalty);

        var averagePenaltyImprovement = orderedDiagnostics.Average(diagnostic =>
            diagnostic.PenaltyImprovement);

        var averageFirstBestGeneration = orderedDiagnostics.Average(diagnostic =>
            diagnostic.FirstBestGeneration);

        var averageTargetGapHours = orderedDiagnostics.Average(diagnostic =>
            diagnostic.TotalAbsoluteTargetGapHours);

        var averageAssignmentCount = orderedDiagnostics.Average(diagnostic =>
            diagnostic.AssignmentCount);

        var builder = new StringBuilder();

        builder.AppendLine("Clean GA Engine Progress Diagnostics");
        builder.AppendLine("Period: 2026-05-31 through 2026-06-13");
        builder.AppendLine($"PopulationSize: {GeneticPopulationSize}");
        builder.AppendLine($"GenerationCount: {CleanGenerationCount}");
        builder.AppendLine($"SeedCount: {orderedDiagnostics.Length}");
        builder.AppendLine();

        builder.AppendLine("KnownRealHardViolations:");
        builder.AppendLine("- 2026-06-03 Morning: submitted=3, requiredMin=4");
        builder.AppendLine("- 2026-06-10 Morning: submitted=3, requiredMin=4");
        builder.AppendLine();

        builder.AppendLine("PerSeedProgress:");

        foreach (var diagnostic in orderedDiagnostics)
        {
            builder.AppendLine(
                $"- Seed {diagnostic.Seed}: " +
                $"InitialPenalty={diagnostic.InitialBestTotalPenalty}, " +
                $"FinalPenalty={diagnostic.FinalBestTotalPenalty}, " +
                $"Improvement={diagnostic.PenaltyImprovement}, " +
                $"InitialHard={diagnostic.InitialBestHardViolationCount}, " +
                $"FinalHard={diagnostic.FinalBestHardViolationCount}, " +
                $"InitialSoft={diagnostic.InitialBestSoftViolationCount}, " +
                $"FinalSoft={diagnostic.FinalBestSoftViolationCount}, " +
                $"FirstBestGeneration={diagnostic.FirstBestGeneration}, " +
                $"Assignments={diagnostic.AssignmentCount}, " +
                $"TargetGapHours={diagnostic.TotalAbsoluteTargetGapHours:0.##}, " +
                $"RuntimeMs={diagnostic.Elapsed.TotalMilliseconds:0.00}");
        }

        builder.AppendLine();
        builder.AppendLine("Summary:");
        builder.AppendLine($"ImprovedSeedCount: {improvedSeedCount}");
        builder.AppendLine($"NonImprovedSeedCount: {orderedDiagnostics.Length - improvedSeedCount}");
        builder.AppendLine($"DistinctFinalPenaltyCount: {distinctFinalPenaltyCount}");
        builder.AppendLine($"AverageInitialPenalty: {averageInitialPenalty:0.##}");
        builder.AppendLine($"AverageFinalPenalty: {averageFinalPenalty:0.##}");
        builder.AppendLine($"AveragePenaltyImprovement: {averagePenaltyImprovement:0.##}");
        builder.AppendLine($"AverageFirstBestGeneration: {averageFirstBestGeneration:0.##}");
        builder.AppendLine($"AverageAssignmentCount: {averageAssignmentCount:0.##}");
        builder.AppendLine($"AverageTargetGapHours: {averageTargetGapHours:0.##}");
        builder.AppendLine();

        var traceSeed = orderedDiagnostics[0].Seed;
        builder.AppendLine($"GenerationTraceForSeed: {traceSeed}");

        foreach (var diagnostic in generationTraceBySeed[traceSeed])
        {
            if (diagnostic.GenerationIndex != 0 &&
                diagnostic.GenerationIndex % 5 != 0 &&
                diagnostic.GenerationIndex != CleanGenerationCount)
            {
                continue;
            }

            builder.AppendLine(
                $"- Generation {diagnostic.GenerationIndex}: " +
                $"GenerationBestPenalty={diagnostic.BestTotalPenalty}, " +
                $"BestSoFarPenalty={diagnostic.BestSoFarTotalPenalty}, " +
                $"GenerationBestHard={diagnostic.BestHardViolationCount}, " +
                $"BestSoFarHard={diagnostic.BestSoFarHardViolationCount}, " +
                $"GenerationBestSoft={diagnostic.BestSoftViolationCount}, " +
                $"BestSoFarSoft={diagnostic.BestSoFarSoftViolationCount}, " +
                $"FeasibleCandidates={diagnostic.FeasibleCandidateCount}");
        }

        builder.AppendLine();
        builder.AppendLine("DiagnosticLimitations:");
        builder.AppendLine("- This diagnostic uses public generation-level optimizer metrics only.");
        builder.AppendLine("- It does not yet attribute improvement separately to crossover or mutation.");
        builder.AppendLine("- It does not yet measure full population diversity.");
        builder.AppendLine("- If those questions become important, the optimizer should expose additional diagnostics intentionally.");

        return builder.ToString();
    }


    private static CleanGaComponentHarnessRun RunCleanGaComponentHarness(
        ExperimentScenario scenario,
        int seed,
        int generationCount,
        CleanGaComponentMode mode)
    {
        var stopwatch = Stopwatch.StartNew();

        var evaluator = new ScheduleEvaluator();
        var ranker = new ScheduleEvaluationResultRanker();
        var selector = new SchedulePopulationSelector(
            tournamentSize: 3,
            seed: seed);

        var crossover = new ScheduleCrossoverOperator(seed);
        var mutation = new CleanScheduleMutationOperator(seed);
        var random = new Random(seed);

        var population = CreateComponentInitialPopulation(
            scenario.Problem,
            GeneticPopulationSize,
            random,
            evaluator);

        var bestSoFar = selector.SelectElites(
            population,
            eliteCount: 1)[0];

        var generationDiagnostics = new List<CleanGaComponentGenerationDiagnostic>
        {
            CreateComponentGenerationDiagnostic(
                generationIndex: 0,
                population,
                bestSoFar)
        };

        for (var generation = 0; generation < generationCount; generation++)
        {
            population = CreateComponentNextGeneration(
                scenario.Problem,
                population,
                selector,
                crossover,
                mutation,
                evaluator,
                mode);

            var generationBest = selector.SelectElites(
                population,
                eliteCount: 1)[0];

            if (ranker.IsBetterThan(
                    generationBest.Evaluation,
                    bestSoFar.Evaluation))
            {
                bestSoFar = generationBest;
            }

            generationDiagnostics.Add(CreateComponentGenerationDiagnostic(
                generationIndex: generation + 1,
                population,
                bestSoFar));
        }

        stopwatch.Stop();

        var firstBestGeneration = generationDiagnostics
            .First(diagnostic => diagnostic.BestSoFarPenalty ==
                                 generationDiagnostics[^1].BestSoFarPenalty)
            .GenerationIndex;

        var summary = CreateComponentResultSummary(
            scenario,
            bestSoFar,
            generationDiagnostics[0].BestSoFarPenalty,
            generationDiagnostics[^1].BestSoFarPenalty,
            firstBestGeneration);

        return new CleanGaComponentHarnessRun(
            Mode: mode,
            Summary: summary with
            {
                RuntimeMs = stopwatch.Elapsed.TotalMilliseconds
            },
            GenerationDiagnostics: generationDiagnostics);
    }

    private static IReadOnlyList<ScheduleOptimizationResult> CreateComponentInitialPopulation(
        SchedulingProblem problem,
        int populationSize,
        Random random,
        ScheduleEvaluator evaluator)
    {
        var population = new List<ScheduleOptimizationResult>(populationSize);

        for (var i = 0; i < populationSize; i++)
        {
            var candidate = CreateComponentRandomCandidate(
                problem,
                random);

            population.Add(new ScheduleOptimizationResult(
                candidate,
                evaluator.Evaluate(problem, candidate)));
        }

        return population;
    }

    private static IReadOnlyList<ScheduleOptimizationResult> CreateComponentNextGeneration(
        SchedulingProblem problem,
        IReadOnlyCollection<ScheduleOptimizationResult> currentPopulation,
        SchedulePopulationSelector selector,
        ScheduleCrossoverOperator crossover,
        CleanScheduleMutationOperator mutation,
        ScheduleEvaluator evaluator,
        CleanGaComponentMode mode)
    {
        var nextGeneration = new List<ScheduleOptimizationResult>(GeneticPopulationSize);

        foreach (var elite in selector.SelectElites(currentPopulation, eliteCount: 1))
        {
            nextGeneration.Add(elite);
        }

        while (nextGeneration.Count < GeneticPopulationSize)
        {
            var childCandidate = CreateComponentChildCandidate(
                problem,
                currentPopulation,
                selector,
                crossover,
                mutation,
                mode);

            nextGeneration.Add(new ScheduleOptimizationResult(
                childCandidate,
                evaluator.Evaluate(problem, childCandidate)));
        }

        return nextGeneration;
    }

    private static ScheduleCandidate CreateComponentChildCandidate(
        SchedulingProblem problem,
        IReadOnlyCollection<ScheduleOptimizationResult> currentPopulation,
        SchedulePopulationSelector selector,
        ScheduleCrossoverOperator crossover,
        CleanScheduleMutationOperator mutation,
        CleanGaComponentMode mode)
    {
        if (mode == CleanGaComponentMode.MutationOnly)
        {
            var parent = selector.SelectTournamentParent(currentPopulation);

            return mutation.Mutate(
                problem,
                parent.Candidate);
        }

        var firstParent = selector.SelectTournamentParent(currentPopulation);
        var secondParent = selector.SelectTournamentParent(currentPopulation);

        var crossoverCandidate = crossover.Crossover(
            problem,
            firstParent.Candidate,
            secondParent.Candidate);

        if (mode == CleanGaComponentMode.CrossoverOnly)
        {
            return crossoverCandidate;
        }

        return mutation.Mutate(
            problem,
            crossoverCandidate);
    }

    private static ScheduleCandidate CreateComponentRandomCandidate(
        SchedulingProblem problem,
        Random random)
    {
        var assignments = new List<Assignment>();

        var shiftsById = problem.Shifts
            .ToDictionary(shift => shift.Id);

        foreach (var shift in problem.Shifts.OrderBy(shift => shift.StartUtc))
        {
            var targetAssignmentCount = GetComponentTargetAssignmentCount(
                problem,
                shift,
                random);

            if (targetAssignmentCount <= 0)
            {
                continue;
            }

            var assignableResources = problem.Resources
                .Where(resource => CanComponentAssignResource(
                    problem,
                    resource,
                    shift,
                    assignments,
                    shiftsById))
                .ToList();

            ShuffleComponentItems(
                assignableResources,
                random);

            foreach (var resource in assignableResources.Take(targetAssignmentCount))
            {
                assignments.Add(new Assignment(
                    resource.Id,
                    shift.Id));
            }
        }

        return new ScheduleCandidate(assignments);
    }

    private static int GetComponentTargetAssignmentCount(
        SchedulingProblem problem,
        Shift shift,
        Random random)
    {
        var effectiveMinResourceCount = GetComponentEffectiveMinResourceCount(
            problem,
            shift);

        if (effectiveMinResourceCount >= shift.MaxResourceCount)
        {
            return shift.MaxResourceCount;
        }

        return random.Next(
            effectiveMinResourceCount,
            shift.MaxResourceCount + 1);
    }

    private static int GetComponentEffectiveMinResourceCount(
        SchedulingProblem problem,
        Shift shift)
    {
        var effectiveMinResourceCount = shift.MinResourceCount;
        var hasPreferDemand = problem.ResourcePreferences.Any(preference =>
            preference.Type == ResourcePreferenceType.Prefer &&
            Overlaps(
                preference.StartUtc,
                preference.EndUtc,
                shift.StartUtc,
                shift.EndUtc));

        if (hasPreferDemand &&
            shift.MinResourceCount == 0 &&
            shift.MaxResourceCount > 0 &&
            shift.RequiresPreferenceToAssign)
        {
            effectiveMinResourceCount = Math.Max(effectiveMinResourceCount, 1);
        }

        return Math.Min(effectiveMinResourceCount, shift.MaxResourceCount);
    }

    private static bool CanComponentAssignResource(
        SchedulingProblem problem,
        Resource resource,
        Shift shift,
        IReadOnlyCollection<Assignment> existingAssignments,
        IReadOnlyDictionary<Guid, Shift> shiftsById)
    {
        if (!problem.AvailabilityWindows.Any(window =>
                window.ResourceId == resource.Id &&
                window.Covers(shift)))
        {
            return false;
        }

        if (shift.RequiresPreferenceToAssign &&
            !problem.ResourcePreferences.Any(preference =>
                preference.ResourceId == resource.Id &&
                preference.Type == ResourcePreferenceType.Prefer &&
                Overlaps(
                    preference.StartUtc,
                    preference.EndUtc,
                    shift.StartUtc,
                    shift.EndUtc)))
        {
            return false;
        }

        return !existingAssignments
            .Where(assignment => assignment.ResourceId == resource.Id)
            .Select(assignment => shiftsById[assignment.ShiftId])
            .Any(existingShift => Overlaps(
                existingShift.StartUtc,
                existingShift.EndUtc,
                shift.StartUtc,
                shift.EndUtc));
    }

    private static void ShuffleComponentItems<T>(
        IList<T> items,
        Random random)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var randomIndex = random.Next(i + 1);

            (items[i], items[randomIndex]) = (items[randomIndex], items[i]);
        }
    }

    private static CleanGaComponentGenerationDiagnostic CreateComponentGenerationDiagnostic(
        int generationIndex,
        IReadOnlyCollection<ScheduleOptimizationResult> population,
        ScheduleOptimizationResult bestSoFar)
    {
        var selector = new SchedulePopulationSelector(
            tournamentSize: 3,
            seed: 1);

        var generationBest = selector.SelectElites(
            population,
            eliteCount: 1)[0];

        return new CleanGaComponentGenerationDiagnostic(
            GenerationIndex: generationIndex,
            GenerationBestPenalty: generationBest.Evaluation.Score.TotalPenalty,
            BestSoFarPenalty: bestSoFar.Evaluation.Score.TotalPenalty,
            GenerationBestHardViolationCount: generationBest.Evaluation.Score.HardViolationCount,
            BestSoFarHardViolationCount: bestSoFar.Evaluation.Score.HardViolationCount,
            GenerationBestSoftViolationCount: generationBest.Evaluation.Score.SoftViolationCount,
            BestSoFarSoftViolationCount: bestSoFar.Evaluation.Score.SoftViolationCount,
            DistinctCandidateCount: population
                .Select(result => ToComponentCandidateKey(result.Candidate))
                .Distinct()
                .Count(),
            AverageAssignmentCount: population.Average(result =>
                result.Candidate.Assignments.Count));
    }

    private static string ToComponentCandidateKey(ScheduleCandidate candidate)
    {
        return string.Join(
            "|",
            candidate.Assignments
                .OrderBy(assignment => assignment.ShiftId)
                .ThenBy(assignment => assignment.ResourceId)
                .Select(assignment => $"{assignment.ResourceId}:{assignment.ShiftId}"));
    }

    private static CleanGaComponentResultSummary CreateComponentResultSummary(
        ExperimentScenario scenario,
        ScheduleOptimizationResult result,
        int initialPenalty,
        int finalPenalty,
        int firstBestGeneration)
    {
        var metrics = CalculateTargetGapMetrics(
            scenario,
            result.Candidate);

        return new CleanGaComponentResultSummary(
            InitialPenalty: initialPenalty,
            FinalPenalty: finalPenalty,
            PenaltyImprovement: initialPenalty - finalPenalty,
            HardViolationCount: result.Evaluation.Score.HardViolationCount,
            SoftViolationCount: result.Evaluation.Score.SoftViolationCount,
            AssignmentCount: result.Candidate.Assignments.Count,
            TotalAbsoluteTargetGapHours: metrics.TotalAbsoluteTargetGapHours,
            FirstBestGeneration: firstBestGeneration,
            RuntimeMs: 0);
    }

    private static string FormatCleanGaComponentDiagnosticsReport(
        IReadOnlyCollection<CleanGaComponentSeedDiagnostic> diagnostics,
        IReadOnlyDictionary<(int Seed, CleanGaComponentMode Mode), IReadOnlyList<CleanGaComponentGenerationDiagnostic>> tracesBySeedAndMode)
    {
        var orderedDiagnostics = diagnostics
            .OrderBy(diagnostic => diagnostic.Seed)
            .ToArray();

        var builder = new StringBuilder();

        builder.AppendLine("Stage 7.5C Clean GA Component Diagnostics");
        builder.AppendLine("Period: 2026-05-31 through 2026-06-13");
        builder.AppendLine($"PopulationSize: {GeneticPopulationSize}");
        builder.AppendLine("GenerationCount: 10");
        builder.AppendLine($"SeedCount: {orderedDiagnostics.Length}");
        builder.AppendLine();

        builder.AppendLine("ComponentDefinitions:");
        builder.AppendLine("- ProductionClean: actual GeneticScheduleOptimizer in Clean mode.");
        builder.AppendLine("- CrossoverOnly: diagnostic harness using selection and crossover without mutation.");
        builder.AppendLine("- MutationOnly: diagnostic harness using selection and CleanMutation without crossover.");
        builder.AppendLine("- CrossoverThenMutation: diagnostic harness using selection, crossover, and CleanMutation.");
        builder.AppendLine();

        builder.AppendLine("PerSeedResults:");

        foreach (var diagnostic in orderedDiagnostics)
        {
            builder.AppendLine(
                $"- Seed {diagnostic.Seed}: " +
                $"ProductionPenalty={diagnostic.ProductionClean.FinalPenalty}, " +
                $"CrossoverOnlyPenalty={diagnostic.CrossoverOnly.FinalPenalty}, " +
                $"MutationOnlyPenalty={diagnostic.MutationOnly.FinalPenalty}, " +
                $"CrossoverThenMutationPenalty={diagnostic.CrossoverThenMutation.FinalPenalty}, " +
                $"ProductionAssignments={diagnostic.ProductionClean.AssignmentCount}, " +
                $"CrossoverOnlyAssignments={diagnostic.CrossoverOnly.AssignmentCount}, " +
                $"MutationOnlyAssignments={diagnostic.MutationOnly.AssignmentCount}, " +
                $"CrossoverThenMutationAssignments={diagnostic.CrossoverThenMutation.AssignmentCount}, " +
                $"ProductionTargetGap={diagnostic.ProductionClean.TotalAbsoluteTargetGapHours:0.##}, " +
                $"CrossoverOnlyTargetGap={diagnostic.CrossoverOnly.TotalAbsoluteTargetGapHours:0.##}, " +
                $"MutationOnlyTargetGap={diagnostic.MutationOnly.TotalAbsoluteTargetGapHours:0.##}, " +
                $"CrossoverThenMutationTargetGap={diagnostic.CrossoverThenMutation.TotalAbsoluteTargetGapHours:0.##}");
        }

        builder.AppendLine();
        builder.AppendLine("Summary:");
        AppendComponentSummary(builder, "ProductionClean", orderedDiagnostics.Select(item => item.ProductionClean));
        AppendComponentSummary(builder, "CrossoverOnly", orderedDiagnostics.Select(item => item.CrossoverOnly));
        AppendComponentSummary(builder, "MutationOnly", orderedDiagnostics.Select(item => item.MutationOnly));
        AppendComponentSummary(builder, "CrossoverThenMutation", orderedDiagnostics.Select(item => item.CrossoverThenMutation));

        var traceSeed = orderedDiagnostics[0].Seed;
        builder.AppendLine();
        builder.AppendLine($"GenerationTraceForSeed: {traceSeed}");

        foreach (var mode in new[]
                 {
                     CleanGaComponentMode.CrossoverOnly,
                     CleanGaComponentMode.MutationOnly,
                     CleanGaComponentMode.CrossoverThenMutation
                 })
        {
            builder.AppendLine($"Mode: {mode}");

            foreach (var generation in tracesBySeedAndMode[(traceSeed, mode)])
            {
                if (generation.GenerationIndex != 0 &&
                    generation.GenerationIndex % 5 != 0 &&
                    generation.GenerationIndex != 10)
                {
                    continue;
                }

                builder.AppendLine(
                    $"- Generation {generation.GenerationIndex}: " +
                    $"GenerationBestPenalty={generation.GenerationBestPenalty}, " +
                    $"BestSoFarPenalty={generation.BestSoFarPenalty}, " +
                    $"GenerationBestHard={generation.GenerationBestHardViolationCount}, " +
                    $"BestSoFarHard={generation.BestSoFarHardViolationCount}, " +
                    $"GenerationBestSoft={generation.GenerationBestSoftViolationCount}, " +
                    $"BestSoFarSoft={generation.BestSoFarSoftViolationCount}, " +
                    $"DistinctCandidates={generation.DistinctCandidateCount}, " +
                    $"AverageAssignments={generation.AverageAssignmentCount:0.##}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("DiagnosticLimitations:");
        builder.AppendLine("- Component modes are diagnostic harnesses, not production optimizer modes.");
        builder.AppendLine("- The production Clean optimizer remains the source of truth.");
        builder.AppendLine("- This diagnostic is intended to identify whether crossover, mutation, or their combination deserves deeper instrumentation.");

        return builder.ToString();
    }

    private static void AppendComponentSummary(
        StringBuilder builder,
        string name,
        IEnumerable<CleanGaComponentResultSummary> summaries)
    {
        var items = summaries.ToArray();

        builder.AppendLine(
            $"- {name}: " +
            $"AverageInitialPenalty={items.Average(item => item.InitialPenalty):0.##}, " +
            $"AverageFinalPenalty={items.Average(item => item.FinalPenalty):0.##}, " +
            $"AverageImprovement={items.Average(item => item.PenaltyImprovement):0.##}, " +
            $"AverageAssignments={items.Average(item => item.AssignmentCount):0.##}, " +
            $"AverageTargetGap={items.Average(item => item.TotalAbsoluteTargetGapHours):0.##}, " +
            $"AverageFirstBestGeneration={items.Average(item => item.FirstBestGeneration):0.##}");
    }

    private static ExperimentRun RunOptimizer(
        string name,
        IScheduleOptimizer optimizer,
        SchedulingProblem problem,
        IReadOnlyList<GeneticGenerationDiagnostic>? diagnostics = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = optimizer.Optimize(problem);
        stopwatch.Stop();

        return new ExperimentRun(
            Name: name,
            Result: result,
            Elapsed: stopwatch.Elapsed,
            Diagnostics: diagnostics ?? []);
    }

    private static string FormatActualRealWorldReport(
        ExperimentScenario scenario,
        ExperimentRun deterministic,
        ExperimentRun clean,
        ExperimentRun repair)
    {
        var builder = new StringBuilder();
        var ranker = new ScheduleEvaluationResultRanker();

        builder.AppendLine("Actual Real World Biweekly Clean vs RepairAssisted GA Comparison");
        builder.AppendLine("Period: 2026-05-31 through 2026-06-13");
        builder.AppendLine($"Resources: {scenario.Resources.Count}");
        builder.AppendLine($"Shifts: {scenario.Shifts.Count}");
        builder.AppendLine($"Submissions: {scenario.ResourcePreferences.Count}");
        builder.AppendLine($"TotalSubmittedPreferredHours: {ExpectedTotalSubmittedPreferredHours:0.##}");
        builder.AppendLine($"TotalEffectiveTargetHours: {ExpectedTotalEffectiveTargetHours:0.##}");
        builder.AppendLine();

        builder.AppendLine("KnownRealShortages:");
        builder.AppendLine("- 2026-06-03 Morning: submitted=3, requiredMin=4");
        builder.AppendLine("- 2026-06-10 Morning: submitted=3, requiredMin=4");
        builder.AppendLine();

        AppendRunSummary(builder, scenario, deterministic);
        builder.AppendLine();

        AppendRunSummary(builder, scenario, clean);
        builder.AppendLine();

        AppendRunSummary(builder, scenario, repair);
        builder.AppendLine();

        builder.AppendLine("Comparison:");
        builder.AppendLine(
            $"CleanRankedBetterThanDeterministic: {ranker.IsBetterThan(clean.Result.Evaluation, deterministic.Result.Evaluation)}");
        builder.AppendLine(
            $"RepairRankedBetterThanDeterministic: {ranker.IsBetterThan(repair.Result.Evaluation, deterministic.Result.Evaluation)}");
        builder.AppendLine(
            $"RepairRankedBetterThanClean: {ranker.IsBetterThan(repair.Result.Evaluation, clean.Result.Evaluation)}");
        builder.AppendLine(
            $"CleanRankedBetterThanRepair: {ranker.IsBetterThan(clean.Result.Evaluation, repair.Result.Evaluation)}");
        builder.AppendLine(
            $"CleanPenaltyDeltaAgainstDeterministic: {deterministic.Result.Evaluation.Score.TotalPenalty - clean.Result.Evaluation.Score.TotalPenalty}");
        builder.AppendLine(
            $"RepairPenaltyDeltaAgainstDeterministic: {deterministic.Result.Evaluation.Score.TotalPenalty - repair.Result.Evaluation.Score.TotalPenalty}");
        builder.AppendLine(
            $"RepairPenaltyDeltaAgainstClean: {clean.Result.Evaluation.Score.TotalPenalty - repair.Result.Evaluation.Score.TotalPenalty}");

        return builder.ToString();
    }

    private static void AppendRunSummary(
        StringBuilder builder,
        ExperimentScenario scenario,
        ExperimentRun run)
    {
        var evaluation = run.Result.Evaluation;

        builder.AppendLine(run.Name);
        builder.AppendLine($"RuntimeMs: {run.Elapsed.TotalMilliseconds:0.00}");
        builder.AppendLine($"IsFeasible: {evaluation.IsFeasible}");
        builder.AppendLine($"Score.Value: {evaluation.Score.Value}");
        builder.AppendLine($"TotalPenalty: {evaluation.Score.TotalPenalty}");
        builder.AppendLine($"HardViolationCount: {evaluation.Score.HardViolationCount}");
        builder.AppendLine($"SoftViolationCount: {evaluation.Score.SoftViolationCount}");
        builder.AppendLine($"Assignments.Count: {run.Result.Candidate.Assignments.Count}");

        AppendViolations(builder, evaluation);
        AppendTargetGapSummary(builder, scenario, run);
        AppendMonthlyNightQuotaSummary(builder, scenario, run);
        AppendResourceTargetSummary(builder, scenario, run);
        AppendAssignmentsByShift(builder, scenario, run.Result.Candidate);
        AppendGenerationDiagnostics(builder, run);
    }

    private static void AppendViolations(
        StringBuilder builder,
        ScheduleEvaluationResult evaluation)
    {
        builder.AppendLine("ViolationsByType:");

        var groups = evaluation.Violations
            .GroupBy(violation => violation.Type)
            .OrderBy(group => group.Key.ToString())
            .ToArray();

        if (groups.Length == 0)
        {
            builder.AppendLine("- none");
            return;
        }

        foreach (var group in groups)
        {
            builder.AppendLine($"- {group.Key}: {group.Count()}");
        }
    }

    private static void AppendTargetGapSummary(
        StringBuilder builder,
        ExperimentScenario scenario,
        ExperimentRun run)
    {
        var metrics = CalculateTargetGapMetrics(
            scenario,
            run.Result.Candidate);

        builder.AppendLine("TargetGapSummary:");
        builder.AppendLine($"TotalAbsoluteTargetGapHours: {metrics.TotalAbsoluteTargetGapHours:0.##}");
        builder.AppendLine($"TotalOverTargetHours: {metrics.TotalOverTargetHours:0.##}");
        builder.AppendLine($"TotalUnderTargetHours: {metrics.TotalUnderTargetHours:0.##}");
        builder.AppendLine($"AverageOverTargetHours: {metrics.AverageOverTargetHours:0.##}");
        builder.AppendLine($"MaxOverTargetHours: {metrics.MaxOverTargetHours:0.##}");
        builder.AppendLine($"MaxUnderTargetHours: {metrics.MaxUnderTargetHours:0.##}");
    }

    private static void AppendMonthlyNightQuotaSummary(
        StringBuilder builder,
        ExperimentScenario scenario,
        ExperimentRun run)
    {
        var groups = GetMonthlyNightQuotaGroups(
                scenario,
                run.Result.Candidate)
            .ToArray();

        builder.AppendLine("MonthlyNightQuotaSummary:");

        if (groups.Length == 0)
        {
            builder.AppendLine("- none");
            return;
        }

        foreach (var group in groups)
        {
            builder.AppendLine(
                $"- {GetResourceName(scenario, group.ResourceId)}: " +
                $"Year={group.Year}, " +
                $"Month={group.Month}, " +
                $"Category={group.Category}, " +
                $"Count={group.Count}");
        }
    }

    private static void AppendResourceTargetSummary(
        StringBuilder builder,
        ExperimentScenario scenario,
        ExperimentRun run)
    {
        var shiftsById = scenario.Shifts.ToDictionary(shift => shift.Id);

        var demandsByResourceId = scenario.ResourceWorkloadDemands
            .ToDictionary(demand => demand.ResourceId);

        var assignmentsByResourceId = run.Result.Candidate.Assignments
            .GroupBy(assignment => assignment.ResourceId)
            .ToDictionary(
                group => group.Key,
                group => group.ToArray());

        builder.AppendLine("ResourceTargetSummary:");
        builder.AppendLine(
            "- Resource: EffectiveTargetHours, AssignedHours, GapToTarget, RegularNightAssignments, FridayNightAssignments, MotzeiShabbatNightAssignments");

        foreach (var resource in scenario.Resources)
        {
            var resourceAssignments = assignmentsByResourceId.TryGetValue(
                resource.Id,
                out var assignments)
                ? assignments
                : Array.Empty<Assignment>();

            var assignedHours = resourceAssignments.Sum(assignment =>
                GetShiftHours(shiftsById[assignment.ShiftId]));

            var regularNightAssignments = resourceAssignments.Count(assignment =>
                shiftsById[assignment.ShiftId].NightShiftCategory == NightShiftCategory.Regular);

            var fridayNightAssignments = resourceAssignments.Count(assignment =>
                shiftsById[assignment.ShiftId].NightShiftCategory == NightShiftCategory.FridayNight);

            var motzeiShabbatNightAssignments = resourceAssignments.Count(assignment =>
                shiftsById[assignment.ShiftId].NightShiftCategory == NightShiftCategory.MotzeiShabbatNight);

            var demand = demandsByResourceId[resource.Id];
            var gapToTarget = demand.EffectiveTargetHours - assignedHours;

            builder.AppendLine(
                $"- {resource.Name}: " +
                $"EffectiveTargetHours={demand.EffectiveTargetHours:0.##}, " +
                $"AssignedHours={assignedHours:0.##}, " +
                $"GapToTarget={gapToTarget:0.##}, " +
                $"RegularNightAssignments={regularNightAssignments}, " +
                $"FridayNightAssignments={fridayNightAssignments}, " +
                $"MotzeiShabbatNightAssignments={motzeiShabbatNightAssignments}");
        }
    }

    private static void AppendAssignmentsByShift(
        StringBuilder builder,
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        builder.AppendLine("AssignmentsByShift:");

        foreach (var shift in scenario.Shifts.OrderBy(shift => shift.StartUtc))
        {
            var assignments = candidate.Assignments
                .Where(assignment => assignment.ShiftId == shift.Id)
                .Select(assignment => GetResourceName(scenario, assignment.ResourceId))
                .OrderBy(name => name)
                .ToArray();

            var assignedText = assignments.Length == 0
                ? "unassigned"
                : string.Join(", ", assignments);

            builder.AppendLine(
                $"- {FormatShift(shift)} min={shift.MinResourceCount}, max={shift.MaxResourceCount}, submitted={CountSubmittedResourcesForShift(scenario, shift)} -> {assignedText}");
        }
    }

    private static void AppendGenerationDiagnostics(
        StringBuilder builder,
        ExperimentRun run)
    {
        if (run.Diagnostics.Count == 0)
        {
            return;
        }

        builder.AppendLine("GenerationDiagnostics:");

        var first = run.Diagnostics[0];
        var last = run.Diagnostics[^1];

        AppendDiagnostic(builder, first);

        if (run.Diagnostics.Count > 2)
        {
            var middle = run.Diagnostics[run.Diagnostics.Count / 2];

            if (middle.GenerationIndex != first.GenerationIndex &&
                middle.GenerationIndex != last.GenerationIndex)
            {
                AppendDiagnostic(builder, middle);
            }
        }

        if (last.GenerationIndex != first.GenerationIndex)
        {
            AppendDiagnostic(builder, last);
        }
    }

    private static void AppendDiagnostic(
        StringBuilder builder,
        GeneticGenerationDiagnostic diagnostic)
    {
        builder.AppendLine(
            $"- Generation {diagnostic.GenerationIndex}: " +
            $"PopulationSize={diagnostic.PopulationSize}, " +
            $"FeasibleCandidates={diagnostic.FeasibleCandidateCount}, " +
            $"BestScoreValue={diagnostic.BestScoreValue}, " +
            $"BestTotalPenalty={diagnostic.BestTotalPenalty}, " +
            $"BestHardViolationCount={diagnostic.BestHardViolationCount}, " +
            $"BestSoftViolationCount={diagnostic.BestSoftViolationCount}, " +
            $"BestSoFarScoreValue={diagnostic.BestSoFarScoreValue}, " +
            $"BestSoFarTotalPenalty={diagnostic.BestSoFarTotalPenalty}");
    }

    private static TargetGapMetrics CalculateTargetGapMetrics(
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        var shiftsById = scenario.Shifts.ToDictionary(shift => shift.Id);

        var assignedHoursByResourceId = candidate.Assignments
            .GroupBy(assignment => assignment.ResourceId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(assignment =>
                    GetShiftHours(shiftsById[assignment.ShiftId])));

        var totalAbsoluteTargetGapHours = 0.0;
        var totalOverTargetHours = 0.0;
        var totalUnderTargetHours = 0.0;
        var overTargetResourceCount = 0;
        var maxOverTargetHours = 0.0;
        var maxUnderTargetHours = 0.0;

        foreach (var demand in scenario.ResourceWorkloadDemands)
        {
            var assignedHours = assignedHoursByResourceId.GetValueOrDefault(
                demand.ResourceId,
                0.0);

            var gapToTarget = demand.EffectiveTargetHours - assignedHours;

            totalAbsoluteTargetGapHours += Math.Abs(gapToTarget);

            if (gapToTarget < 0)
            {
                var overTargetHours = -gapToTarget;

                totalOverTargetHours += overTargetHours;
                overTargetResourceCount++;
                maxOverTargetHours = Math.Max(
                    maxOverTargetHours,
                    overTargetHours);

                continue;
            }

            totalUnderTargetHours += gapToTarget;
            maxUnderTargetHours = Math.Max(
                maxUnderTargetHours,
                gapToTarget);
        }

        var averageOverTargetHours = overTargetResourceCount == 0
            ? 0.0
            : totalOverTargetHours / overTargetResourceCount;

        return new TargetGapMetrics(
            totalAbsoluteTargetGapHours,
            totalOverTargetHours,
            totalUnderTargetHours,
            averageOverTargetHours,
            maxOverTargetHours,
            maxUnderTargetHours);
    }

    private static IReadOnlyCollection<MonthlyNightQuotaGroup> GetMonthlyNightQuotaGroups(
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        var shiftsById = scenario.Shifts
            .ToDictionary(shift => shift.Id);

        return candidate.Assignments
            .Select(assignment => new
            {
                assignment.ResourceId,
                Shift = shiftsById[assignment.ShiftId]
            })
            .Where(item => HasMonthlyNightQuota(item.Shift.NightShiftCategory))
            .GroupBy(item => new
            {
                item.ResourceId,
                Year = item.Shift.StartUtc.Year,
                Month = item.Shift.StartUtc.Month,
                Category = item.Shift.NightShiftCategory!.Value
            })
            .Select(group => new MonthlyNightQuotaGroup(
                group.Key.ResourceId,
                group.Key.Year,
                group.Key.Month,
                group.Key.Category,
                group.Count()))
            .OrderBy(group => GetResourceName(scenario, group.ResourceId))
            .ThenBy(group => group.Year)
            .ThenBy(group => group.Month)
            .ThenBy(group => group.Category)
            .ToArray();
    }

    private static void AssertCandidateReferencesKnownProblemEntities(
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        var resourceIds = scenario.Resources
            .Select(resource => resource.Id)
            .ToHashSet();

        var shiftIds = scenario.Shifts
            .Select(shift => shift.Id)
            .ToHashSet();

        Assert.All(candidate.Assignments, assignment =>
        {
            Assert.Contains(assignment.ResourceId, resourceIds);
            Assert.Contains(assignment.ShiftId, shiftIds);
        });

        Assert.Equal(
            candidate.Assignments.Count,
            candidate.Assignments
                .Select(assignment => $"{assignment.ResourceId}:{assignment.ShiftId}")
                .Distinct()
                .Count());
    }

    private static void AssertOnlyExpectedRealStructuralViolations(
        ExperimentScenario scenario,
        ScheduleEvaluationResult evaluation)
    {
        Assert.Equal(
            0,
            CountViolations(
                evaluation,
                ConstraintViolationType.ResourceUnavailable));

        Assert.Equal(
            0,
            CountViolations(
                evaluation,
                ConstraintViolationType.ResourceAssignedToOverlappingShifts));

        Assert.Equal(
            0,
            CountViolations(
                evaluation,
                ConstraintViolationType.AssignedWithoutRequiredPreference));

        Assert.Equal(
            0,
            CountViolations(
                evaluation,
                ConstraintViolationType.ShiftOverstaffed));

        Assert.Equal(
            0,
            CountViolations(
                evaluation,
                ConstraintViolationType.ShiftSequenceQuotaExceeded));

        Assert.Equal(
            0,
            CountViolations(
                evaluation,
                ConstraintViolationType.ResourceMonthlyNightShiftQuotaExceeded));

        AssertOnlyKnownRealUnderstaffedViolations(
            scenario,
            evaluation);
    }

    private static void AssertOnlyKnownRealUnderstaffedViolations(
        ExperimentScenario scenario,
        ScheduleEvaluationResult evaluation)
    {
        var shiftsById = scenario.Shifts.ToDictionary(shift => shift.Id);

        var understaffedViolations = evaluation.Violations
            .Where(violation => violation.Type == ConstraintViolationType.ShiftUnderstaffed)
            .ToArray();

        Assert.Equal(2, understaffedViolations.Length);

        Assert.All(understaffedViolations, violation =>
        {
            Assert.NotNull(violation.ShiftId);
            var shift = shiftsById[violation.ShiftId!.Value];

            Assert.True(
                IsKnownRealUnderstaffedShift(shift),
                $"Unexpected ShiftUnderstaffed violation on {FormatShift(shift)}.");
        });
    }

    private static bool IsKnownRealUnderstaffedShift(Shift shift)
    {
        if (shift.Kind != ShiftKind.Morning)
        {
            return false;
        }

        var date = DateOnly.FromDateTime(shift.StartUtc);

        return date == new DateOnly(2026, 6, 3) ||
               date == new DateOnly(2026, 6, 10);
    }

    private static void AssertNoMoreThanOneRegularNightAssignmentPerResource(
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        var shiftsById = scenario.Shifts.ToDictionary(shift => shift.Id);

        var offenders = candidate.Assignments
            .Select(assignment => new
            {
                assignment.ResourceId,
                Shift = shiftsById[assignment.ShiftId]
            })
            .Where(item => item.Shift.NightShiftCategory == NightShiftCategory.Regular)
            .GroupBy(item => item.ResourceId)
            .Where(group => group.Count() > 1)
            .Select(group => $"{GetResourceName(scenario, group.Key)}={group.Count()}")
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "A resource was assigned to more than one regular weekday night shift: " +
            string.Join(", ", offenders));
    }

    private static void AssertMonthlyNightQuotaPerCategory(
        ExperimentScenario scenario,
        ScheduleCandidate candidate,
        string report)
    {
        var quotaViolations = GetMonthlyNightQuotaGroups(
                scenario,
                candidate)
            .Where(item => item.Count > 1)
            .ToArray();

        var details = string.Join(
            Environment.NewLine,
            quotaViolations.Select(item =>
                $"- {GetResourceName(scenario, item.ResourceId)}: " +
                $"Year={item.Year}, Month={item.Month}, " +
                $"Category={item.Category}, Count={item.Count}"));

        Assert.True(
            quotaViolations.Length == 0,
            "Monthly night quota per category was exceeded." +
            Environment.NewLine +
            details +
            Environment.NewLine +
            Environment.NewLine +
            report);
    }

    private static bool HasMonthlyNightQuota(NightShiftCategory? category)
    {
        return category is NightShiftCategory.Regular or NightShiftCategory.MotzeiShabbatNight;
    }

    private static void AssertSubmittedResourceNamesForShift(
        ExperimentScenario scenario,
        DateOnly date,
        ShiftKind kind,
        IReadOnlyCollection<string> expectedNames)
    {
        var shift = GetShift(scenario.Shifts, date, kind);

        var actualNames = GetSubmittedResourceIdsForShift(scenario, shift)
            .Select(resourceId => GetResourceName(scenario, resourceId))
            .OrderBy(name => name)
            .ToArray();

        Assert.Equal(
            expectedNames.OrderBy(name => name).ToArray(),
            actualNames);
    }

    private static int CountSubmittedResourcesForShift(
        ExperimentScenario scenario,
        Shift shift)
    {
        return scenario.ResourcePreferences
            .Where(preference => preference.Type == ResourcePreferenceType.Prefer)
            .Where(preference => Overlaps(
                preference.StartUtc,
                preference.EndUtc,
                shift.StartUtc,
                shift.EndUtc))
            .Select(preference => preference.ResourceId)
            .Distinct()
            .Count();
    }

    private static IReadOnlyCollection<Guid> GetSubmittedResourceIdsForShift(
        ExperimentScenario scenario,
        Shift shift)
    {
        return scenario.ResourcePreferences
            .Where(preference => preference.Type == ResourcePreferenceType.Prefer)
            .Where(preference => Overlaps(
                preference.StartUtc,
                preference.EndUtc,
                shift.StartUtc,
                shift.EndUtc))
            .Select(preference => preference.ResourceId)
            .Distinct()
            .ToArray();
    }

    private static string GetResourceName(
        ExperimentScenario scenario,
        Guid resourceId)
    {
        return scenario.Resources
            .FirstOrDefault(resource => resource.Id == resourceId)
            ?.Name ?? resourceId.ToString();
    }

    private static int CountViolations(
        ScheduleEvaluationResult evaluation,
        ConstraintViolationType type)
    {
        return evaluation.Violations.Count(violation => violation.Type == type);
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

    private static bool IsWeekday(Shift shift)
    {
        return !IsFridayOrSaturday(shift);
    }

    private static bool IsFridayOrSaturday(Shift shift)
    {
        var dayOfWeek = DateOnly.FromDateTime(shift.StartUtc).DayOfWeek;

        return dayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday;
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

    private static string ToWindowKey(
        DateTime startUtc,
        DateTime endUtc)
    {
        return $"{startUtc:o}:{endUtc:o}";
    }

    private static string ToSubmissionKey(
        Guid resourceId,
        DateTime startUtc,
        DateTime endUtc)
    {
        return $"{resourceId}:{startUtc:o}:{endUtc:o}";
    }

    private static string FormatShift(Shift shift)
    {
        var category = shift.NightShiftCategory is null
            ? string.Empty
            : $" {shift.NightShiftCategory}";

        return $"{shift.Kind}{category} {shift.StartUtc:yyyy-MM-dd HH:mm}-{shift.EndUtc:yyyy-MM-dd HH:mm} UTC";
    }


    private sealed record MultiSeedComparison(
        int Seed,
        bool CleanRankedBetterThanRepair,
        bool RepairRankedBetterThanClean,
        int CleanTotalPenalty,
        int RepairTotalPenalty,
        int CleanHardViolationCount,
        int RepairHardViolationCount,
        int CleanSoftViolationCount,
        int RepairSoftViolationCount,
        int CleanAssignmentCount,
        int RepairAssignmentCount,
        double CleanTotalAbsoluteTargetGapHours,
        double RepairTotalAbsoluteTargetGapHours,
        TimeSpan CleanElapsed,
        TimeSpan RepairElapsed);


    private enum CleanGaComponentMode
    {
        CrossoverOnly = 1,
        MutationOnly = 2,
        CrossoverThenMutation = 3
    }

    private sealed record CleanGaComponentSeedDiagnostic(
        int Seed,
        CleanGaComponentResultSummary ProductionClean,
        CleanGaComponentResultSummary CrossoverOnly,
        CleanGaComponentResultSummary MutationOnly,
        CleanGaComponentResultSummary CrossoverThenMutation);

    private sealed record CleanGaComponentHarnessRun(
        CleanGaComponentMode Mode,
        CleanGaComponentResultSummary Summary,
        IReadOnlyList<CleanGaComponentGenerationDiagnostic> GenerationDiagnostics);

    private sealed record CleanGaComponentResultSummary(
        int InitialPenalty,
        int FinalPenalty,
        int PenaltyImprovement,
        int HardViolationCount,
        int SoftViolationCount,
        int AssignmentCount,
        double TotalAbsoluteTargetGapHours,
        int FirstBestGeneration,
        double RuntimeMs);

    private sealed record CleanGaComponentGenerationDiagnostic(
        int GenerationIndex,
        int GenerationBestPenalty,
        int BestSoFarPenalty,
        int GenerationBestHardViolationCount,
        int BestSoFarHardViolationCount,
        int GenerationBestSoftViolationCount,
        int BestSoFarSoftViolationCount,
        int DistinctCandidateCount,
        double AverageAssignmentCount);

    private sealed record TargetGapMetrics(
        double TotalAbsoluteTargetGapHours,
        double TotalOverTargetHours,
        double TotalUnderTargetHours,
        double AverageOverTargetHours,
        double MaxOverTargetHours,
        double MaxUnderTargetHours);

    private sealed record MonthlyNightQuotaGroup(
        Guid ResourceId,
        int Year,
        int Month,
        NightShiftCategory Category,
        int Count);

    private sealed record ExperimentRun(
        string Name,
        ScheduleOptimizationResult Result,
        TimeSpan Elapsed,
        IReadOnlyList<GeneticGenerationDiagnostic> Diagnostics);


    private sealed record CleanGaEngineRunDiagnostic(
        int Seed,
        int InitialBestTotalPenalty,
        int FinalBestTotalPenalty,
        int PenaltyImprovement,
        int InitialBestHardViolationCount,
        int FinalBestHardViolationCount,
        int InitialBestSoftViolationCount,
        int FinalBestSoftViolationCount,
        int FirstBestGeneration,
        int AssignmentCount,
        double TotalAbsoluteTargetGapHours,
        TimeSpan Elapsed);

    private sealed record ExperimentScenario(
        SchedulingProblem Problem,
        IReadOnlyList<Resource> Resources,
        IReadOnlyList<Shift> Shifts,
        IReadOnlyList<AvailabilityWindow> AvailabilityWindows,
        IReadOnlyList<ResourcePreference> ResourcePreferences,
        IReadOnlyList<ResourceWorkloadDemand> ResourceWorkloadDemands);
}
