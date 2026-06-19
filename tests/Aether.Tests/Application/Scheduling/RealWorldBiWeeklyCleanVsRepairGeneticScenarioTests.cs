using Aether.Application.Scheduling.Contracts;
using Aether.Application.Scheduling.Interfaces;
using System.Diagnostics;
using System.Text;
using Aether.Application.Scheduling.Optimization;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class RealWorldBiWeeklyCleanVsRepairGeneticScenarioTests
{
    private const int ResourceCount = 16;
    private const int DaysInSchedule = 14;
    private const int ExpectedShiftCount = DaysInSchedule * 3;
    private const double ExpectedTotalEffectiveTargetHours = 736.0;
    private const double HoursTolerance = 0.000001;
    private const int GeneticPopulationSize = 120;
    private const int CleanGenerationCount = 30;
    private const int GeneticSeed = 20260603;

    [Fact]
    public void CreateScenario_ShouldCreateExpectedGuardResources()
    {
        var scenario = CreateScenario();

        Assert.Equal(ResourceCount, scenario.Resources.Count);
        Assert.Equal("Guard01", scenario.Resources[0].Name);
        Assert.Equal("Guard16", scenario.Resources[^1].Name);

        Assert.Equal(
            ResourceCount,
            scenario.Resources
                .Select(resource => resource.Id)
                .Distinct()
                .Count());

        Assert.All(scenario.Resources, resource =>
        {
            Assert.False(resource.Id == Guid.Empty);
            Assert.StartsWith("Guard", resource.Name);
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
    public void CreateScenario_ShouldApplyBusinessCapacityRules()
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
    public void CreateScenario_ShouldCreateSubmissionDrivenAvailabilityAndPreferPreferences()
    {
        var scenario = CreateScenario();

        Assert.NotEmpty(scenario.AvailabilityWindows);
        Assert.NotEmpty(scenario.ResourcePreferences);

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
    public void CreateScenario_ShouldProvideEnoughSubmittedResourcesForMandatoryWeekdayDaytimeShifts()
    {
        var scenario = CreateScenario();

        var mandatoryWeekdayDaytimeShifts = scenario.Shifts
            .Where(shift => IsWeekday(shift))
            .Where(shift => shift.Kind is ShiftKind.Morning or ShiftKind.Afternoon)
            .ToArray();

        Assert.NotEmpty(mandatoryWeekdayDaytimeShifts);

        Assert.All(mandatoryWeekdayDaytimeShifts, shift =>
        {
            var submittedResourceCount = CountSubmittedResourcesForShift(
                scenario,
                shift);

            Assert.True(
                submittedResourceCount >= shift.MinResourceCount,
                $"Shift {FormatShift(shift)} has only {submittedResourceCount} submitted resources, but minimum is {shift.MinResourceCount}.");

            Assert.True(
                submittedResourceCount >= shift.MaxResourceCount,
                $"Shift {FormatShift(shift)} has only {submittedResourceCount} submitted resources, but maximum is {shift.MaxResourceCount}.");
        });
    }

    [Fact]
    public void CreateScenario_ShouldCreateOptionalWeekendDaytimeDemand_WithoutMakingItMandatory()
    {
        var scenario = CreateScenario();

        var optionalWeekendDaytimeShifts = scenario.Shifts
            .Where(shift => IsFridayOrSaturday(shift))
            .Where(shift => shift.Kind is ShiftKind.Morning or ShiftKind.Afternoon)
            .ToArray();

        Assert.NotEmpty(optionalWeekendDaytimeShifts);

        Assert.All(optionalWeekendDaytimeShifts, shift =>
        {
            Assert.Equal(0, shift.MinResourceCount);

            var submittedResourceCount = CountSubmittedResourcesForShift(
                scenario,
                shift);

            Assert.True(
                submittedResourceCount > 0,
                $"Optional weekend shift {FormatShift(shift)} should still have preferred demand.");
        });
    }

    [Fact]
    public void CreateScenario_ShouldCreateNightPoolDemand()
    {
        var scenario = CreateScenario();

        var regularNightShifts = scenario.Shifts
            .Where(shift => shift.NightShiftCategory == NightShiftCategory.Regular)
            .ToArray();

        var fridayNightShifts = scenario.Shifts
            .Where(shift => shift.NightShiftCategory == NightShiftCategory.FridayNight)
            .ToArray();

        var motzeiShabbatNightShifts = scenario.Shifts
            .Where(shift => shift.NightShiftCategory == NightShiftCategory.MotzeiShabbatNight)
            .ToArray();

        Assert.Equal(10, regularNightShifts.Length);
        Assert.Equal(2, fridayNightShifts.Length);
        Assert.Equal(2, motzeiShabbatNightShifts.Length);

        Assert.All(regularNightShifts, shift =>
        {
            Assert.Equal(1, shift.MinResourceCount);
            Assert.Equal(1, shift.MaxResourceCount);
            Assert.Equal(4, CountSubmittedResourcesForShift(scenario, shift));
        });

        Assert.All(fridayNightShifts, shift =>
        {
            Assert.Equal(0, shift.MinResourceCount);
            Assert.Equal(1, shift.MaxResourceCount);
            Assert.Equal(2, CountSubmittedResourcesForShift(scenario, shift));
        });

        Assert.All(motzeiShabbatNightShifts, shift =>
        {
            Assert.Equal(3, shift.MinResourceCount);
            Assert.Equal(3, shift.MaxResourceCount);
            Assert.Equal(8, CountSubmittedResourcesForShift(scenario, shift));
        });
    }

    [Fact]
    public void CreateScenario_ShouldProvideEnoughSubmittedResourcesForMandatoryNightShifts()
    {
        var scenario = CreateScenario();

        var mandatoryNightShifts = scenario.Shifts
            .Where(shift => shift.Kind == ShiftKind.Night)
            .Where(shift => shift.MinResourceCount > 0)
            .ToArray();

        Assert.Equal(12, mandatoryNightShifts.Length);

        Assert.All(mandatoryNightShifts, shift =>
        {
            var submittedResourceCount = CountSubmittedResourcesForShift(
                scenario,
                shift);

            Assert.True(
                submittedResourceCount >= shift.MinResourceCount,
                $"Night shift {FormatShift(shift)} has only {submittedResourceCount} submitted resources, but minimum is {shift.MinResourceCount}.");

            Assert.True(
                submittedResourceCount >= shift.MaxResourceCount,
                $"Night shift {FormatShift(shift)} has only {submittedResourceCount} submitted resources, but maximum is {shift.MaxResourceCount}.");
        });
    }

    [Fact]
    public void CreateScenario_ShouldCreateOverlappingMotzeiShabbatPoolsToExerciseFairness()
    {
        var scenario = CreateScenario();

        var motzeiShabbatNightShifts = scenario.Shifts
            .Where(shift => shift.NightShiftCategory == NightShiftCategory.MotzeiShabbatNight)
            .OrderBy(shift => shift.StartUtc)
            .ToArray();

        Assert.Equal(2, motzeiShabbatNightShifts.Length);

        var firstPool = GetSubmittedResourceIdsForShift(
                scenario,
                motzeiShabbatNightShifts[0])
            .ToHashSet();

        var secondPool = GetSubmittedResourceIdsForShift(
                scenario,
                motzeiShabbatNightShifts[1])
            .ToHashSet();

        Assert.Equal(8, firstPool.Count);
        Assert.Equal(8, secondPool.Count);
        Assert.True(firstPool.SetEquals(secondPool));

        Assert.True(
            firstPool.Count > motzeiShabbatNightShifts[0].MaxResourceCount,
            "Motzei Shabbat pool must have more requested guards than capacity to create real competition.");
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

        Assert.True(totalSubmittedPreferredHours > 0);

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

        Assert.True(
            Math.Abs(ExpectedTotalEffectiveTargetHours - totalEffectiveTargetHours) < HoursTolerance,
            $"Expected total effective target {ExpectedTotalEffectiveTargetHours:0.##}h but got {totalEffectiveTargetHours:0.##}h.");

        Assert.True(
            totalMinimumCapacityHours <= totalEffectiveTargetHours,
            $"Minimum capacity {totalMinimumCapacityHours:0.##}h is above target {totalEffectiveTargetHours:0.##}h.");

        Assert.True(
            totalMaximumCapacityHours >= totalEffectiveTargetHours,
            $"Maximum capacity {totalMaximumCapacityHours:0.##}h is below target {totalEffectiveTargetHours:0.##}h.");
    }


    [Fact]
    public void CreateIndividualAvailabilityShortageScenario_ShouldCreateExpectedShortageShape()
    {
        var scenario = CreateIndividualAvailabilityShortageScenario();

        Assert.Equal(ResourceCount, scenario.Resources.Count);
        Assert.Equal(ExpectedShiftCount, scenario.Shifts.Count);
        Assert.Equal(ResourceCount, scenario.ResourceWorkloadDemands.Count);

        Assert.Equal(0, scenario.Problem.MinimumAssignedHoursPerResource);
        Assert.Equal(0, scenario.Problem.MinimumMorningShiftsPerResourcePerFullWeek);
        Assert.Equal(0, scenario.Problem.MinimumAfternoonShiftsPerResourcePerFullWeek);

        var weekdayMorningShifts = scenario.Shifts
            .Where(IsWeekdayMorning)
            .ToArray();

        Assert.Equal(10, weekdayMorningShifts.Length);

        Assert.All(weekdayMorningShifts, shift =>
        {
            var availableCount = CountAvailableResourcesForShift(
                scenario,
                shift);

            var preferCount = CountPreferredResourcesForShift(
                scenario,
                shift);

            var avoidCount = CountAvoidingResourcesForShift(
                scenario,
                shift);

            Assert.False(shift.RequiresPreferenceToAssign);
            Assert.True(availableCount >= shift.MinResourceCount);
            Assert.True(preferCount < shift.MinResourceCount);
            Assert.True(avoidCount >= shift.MinResourceCount);
            Assert.Equal(ResourceCount, availableCount);
            Assert.Equal(1, preferCount);
            Assert.Equal(ResourceCount - 1, avoidCount);
        });

        var mandatoryPreferenceRequiredShifts = scenario.Shifts
            .Where(shift => shift.MinResourceCount > 0)
            .Where(shift => shift.RequiresPreferenceToAssign)
            .ToArray();

        Assert.NotEmpty(mandatoryPreferenceRequiredShifts);

        Assert.All(mandatoryPreferenceRequiredShifts, shift =>
        {
            var preferredResourceCount = CountPreferredResourcesForShift(
                scenario,
                shift);

            Assert.True(
                preferredResourceCount >= shift.MinResourceCount,
                $"Shift {FormatShift(shift)} has only {preferredResourceCount} preferred resources, but minimum is {shift.MinResourceCount}.");
        });

        Assert.Equal(
            scenario.AvailabilityWindows.Count,
            scenario.AvailabilityWindows
                .Select(window => ToSubmissionKey(
                    window.ResourceId,
                    window.StartUtc,
                    window.EndUtc))
                .Distinct()
                .Count());

        Assert.Equal(
            scenario.ResourcePreferences.Count,
            scenario.ResourcePreferences
                .Select(preference => $"{preference.Type}:{ToSubmissionKey(
                    preference.ResourceId,
                    preference.StartUtc,
                    preference.EndUtc)}")
                .Distinct()
                .Count());
    }

    [Fact]
    [Trait("Category", "SchedulingExperiment")]
    public void CleanGeneticOptimizer_ShouldPrintIndividualAvailabilityShortageScenarioReport()
    {
        var scenario = CreateIndividualAvailabilityShortageScenario();
        var diagnosticsSink = new CollectingGeneticOptimizerDiagnosticsSink();
        var weights = CreateVariantCScoringWeights();

        var clean = RunOptimizer(
            "Clean GA Variant C",
            new GeneticScheduleOptimizer(
                populationSize: GeneticPopulationSize,
                seed: GeneticSeed,
                generationCount: CleanGenerationCount,
                eliteCount: 1,
                tournamentSize: 3,
                diagnosticsSink: diagnosticsSink,
                evolutionMode: GeneticEvolutionMode.Clean,
                scoringWeights: weights),
            scenario.Problem,
            diagnosticsSink.Diagnostics);

        var report = FormatIndividualAvailabilityShortageScenarioReport(
            scenario,
            weights,
            clean);

        System.Console.WriteLine(report);

        Assert.Contains("Stage 7.6D.1 Realistic Individual Availability Shortage Scenario", report);
        Assert.Contains("Mode: Clean GA", report);
        Assert.Contains("Variant: Variant C", report);
        Assert.Contains("AvailabilityPressureSummary:", report);
        Assert.Contains("MorningShiftScarcitySummary:", report);
        Assert.Contains("WeekendScarcitySummary:", report);
        Assert.Contains("MorningAssignmentsByResource:", report);
        Assert.Contains("AvoidedMorningAssignmentsByResource:", report);
        Assert.Contains("PenaltyBreakdownByType:", report);
        Assert.Contains(nameof(ConstraintViolationType.IgnoredAvoidPreference), report);

        AssertCandidateReferencesKnownProblemEntities(
            scenario,
            clean.Result.Candidate);

        AssertNoBasicStructuralViolations(clean.Result.Evaluation);

        Assert.Equal(
            CleanGenerationCount + 1,
            clean.Diagnostics.Count);

        Assert.True(
            clean.Diagnostics[^1].BestSoFarTotalPenalty <=
            clean.Diagnostics[0].BestSoFarTotalPenalty,
            report);
    }


    [Fact]
    [Trait("Category", "SchedulingExperiment")]
    public void CleanGeneticOptimizer_ShouldPrintIndividualAvailabilityShortageMultiSeedStabilityReport()
    {
        var scenario = CreateIndividualAvailabilityShortageScenario();
        var weights = CreateVariantCScoringWeights();

        var seeds = new[]
        {
            20260603,
            20260604,
            20260605
        };

        var runs = new List<MultiSeedSensitivityRun>();

        foreach (var seed in seeds)
        {
            var diagnosticsSink = new CollectingGeneticOptimizerDiagnosticsSink();

            var run = RunOptimizer(
                $"Clean GA Variant C Individual Shortage Seed {seed}",
                new GeneticScheduleOptimizer(
                    populationSize: GeneticPopulationSize,
                    seed: seed,
                    generationCount: CleanGenerationCount,
                    eliteCount: 1,
                    tournamentSize: 3,
                    diagnosticsSink: diagnosticsSink,
                    evolutionMode: GeneticEvolutionMode.Clean,
                    scoringWeights: weights),
                scenario.Problem,
                diagnosticsSink.Diagnostics);

            AssertCandidateReferencesKnownProblemEntities(
                scenario,
                run.Result.Candidate);

            AssertNoBasicStructuralViolations(run.Result.Evaluation);

            Assert.Equal(
                CleanGenerationCount + 1,
                run.Diagnostics.Count);

            Assert.True(
                run.Diagnostics[^1].BestSoFarTotalPenalty <=
                run.Diagnostics[0].BestSoFarTotalPenalty,
                $"Seed {seed} should not return a best-so-far penalty worse than generation 0.");

            runs.Add(new MultiSeedSensitivityRun(
                Seed: seed,
                Run: run));
        }

        var report = FormatIndividualAvailabilityShortageMultiSeedStabilityReport(
            scenario,
            weights,
            runs);

        System.Console.WriteLine(report);

        Assert.Contains("Stage 7.6D.2 Individual Availability Shortage Multi-Seed Stability Diagnostic", report);
        Assert.Contains("Mode: Clean GA", report);
        Assert.Contains("Variant: Variant C", report);
        Assert.Contains("IndividualShortageSeedSummary:", report);
        Assert.Contains("IndividualShortageMultiSeedAggregateSummary:", report);
        Assert.Contains("MinTotalAssignedHours:", report);
        Assert.Contains("MaxTotalAssignedHours:", report);
        Assert.Contains("AverageTotalAssignedHours:", report);
        Assert.Contains("MaxAvoidedMorningAssignmentsForSingleResourceRange:", report);
        Assert.Contains("AnyRunHasHardViolations:", report);
    }


    [Fact]
    public void CreateIndividualAvailabilityShortageWithSequencePressureScenario_ShouldCreateExpectedSequencePressureShape()
    {
        var scenario = CreateIndividualAvailabilityShortageWithSequencePressureScenario();

        Assert.Equal(ResourceCount, scenario.Resources.Count);
        Assert.Equal(ExpectedShiftCount, scenario.Shifts.Count);
        Assert.Equal(ResourceCount, scenario.ResourceWorkloadDemands.Count);

        var weekdayMorningShifts = scenario.Shifts
            .Where(IsWeekdayMorning)
            .ToArray();

        Assert.Equal(10, weekdayMorningShifts.Length);

        Assert.All(weekdayMorningShifts, shift =>
        {
            var availableCount = CountAvailableResourcesForShift(
                scenario,
                shift);

            var preferCount = CountPreferredResourcesForShift(
                scenario,
                shift);

            var avoidCount = CountAvoidingResourcesForShift(
                scenario,
                shift);

            Assert.False(shift.RequiresPreferenceToAssign);
            Assert.True(availableCount >= shift.MinResourceCount);
            Assert.True(preferCount < shift.MinResourceCount);
            Assert.Equal(ResourceCount, availableCount);
            Assert.Equal(1, preferCount);
            Assert.Equal(ResourceCount - 1, avoidCount);
        });

        var firstMorningShift = scenario.Shifts
            .First(shift => shift.Kind == ShiftKind.Morning);

        var firstAfternoonShift = scenario.Shifts
            .First(shift => shift.Kind == ShiftKind.Afternoon);

        var firstNightShift = scenario.Shifts
            .First(shift => shift.Kind == ShiftKind.Night);

        Assert.Equal(new TimeOnly(6, 30), TimeOnly.FromDateTime(firstMorningShift.StartUtc));
        Assert.Equal(new TimeOnly(14, 20), TimeOnly.FromDateTime(firstMorningShift.EndUtc));
        Assert.Equal(new TimeOnly(14, 20), TimeOnly.FromDateTime(firstAfternoonShift.StartUtc));
        Assert.Equal(new TimeOnly(22, 40), TimeOnly.FromDateTime(firstAfternoonShift.EndUtc));
        Assert.Equal(new TimeOnly(22, 40), TimeOnly.FromDateTime(firstNightShift.StartUtc));
        Assert.Equal(new TimeOnly(6, 30), TimeOnly.FromDateTime(firstNightShift.EndUtc));

        Assert.True(
            CountPotentialSequenceTemplatePairs(
                scenario.Shifts,
                ShiftSequenceType.AfternoonToMorning) > 0);

        Assert.True(
            CountPotentialSequenceTemplatePairs(
                scenario.Shifts,
                ShiftSequenceType.NightToAfternoon) > 0);
    }

    [Fact]
    [Trait("Category", "SchedulingExperiment")]
    public void CleanGeneticOptimizer_ShouldPrintIndividualAvailabilityShortageWithSequencePressureReport()
    {
        var scenario = CreateIndividualAvailabilityShortageWithSequencePressureScenario();
        var diagnosticsSink = new CollectingGeneticOptimizerDiagnosticsSink();
        var weights = CreateVariantCScoringWeights();

        var clean = RunOptimizer(
            "Clean GA Variant C Individual Shortage With Sequence Pressure",
            new GeneticScheduleOptimizer(
                populationSize: GeneticPopulationSize,
                seed: GeneticSeed,
                generationCount: CleanGenerationCount,
                eliteCount: 1,
                tournamentSize: 3,
                diagnosticsSink: diagnosticsSink,
                evolutionMode: GeneticEvolutionMode.Clean,
                scoringWeights: weights),
            scenario.Problem,
            diagnosticsSink.Diagnostics);

        var report = FormatIndividualAvailabilityShortageWithSequencePressureReport(
            scenario,
            weights,
            clean);

        System.Console.WriteLine(report);

        Assert.Contains("Stage 7.6D.3 Individual Availability Shortage With Sequence Pressure Diagnostic", report);
        Assert.Contains("Mode: Clean GA", report);
        Assert.Contains("Variant: Variant C", report);
        Assert.Contains("SequenceTemplatePressureSummary:", report);
        Assert.Contains("SequencePressureSummary:", report);
        Assert.Contains("SequenceAssignmentsByResource:", report);
        Assert.Contains("ShiftSequenceQuotaExceededViolationCount:", report);
        Assert.Contains("PenaltyBreakdownByType:", report);

        AssertCandidateReferencesKnownProblemEntities(
            scenario,
            clean.Result.Candidate);

        AssertNoBasicStructuralViolations(clean.Result.Evaluation);

        Assert.Equal(
            CleanGenerationCount + 1,
            clean.Diagnostics.Count);

        Assert.True(
            clean.Diagnostics[^1].BestSoFarTotalPenalty <=
            clean.Diagnostics[0].BestSoFarTotalPenalty,
            report);
    }


    [Fact]
    [Trait("Category", "SchedulingExperiment")]
    public void CleanGeneticOptimizer_ShouldPrintIndividualAvailabilityShortageWithSequencePressureMultiSeedStabilityReport()
    {
        var scenario = CreateIndividualAvailabilityShortageWithSequencePressureScenario();
        var weights = CreateVariantCScoringWeights();

        var seeds = new[]
        {
            20260603,
            20260604,
            20260605
        };

        var runs = new List<MultiSeedSensitivityRun>();

        foreach (var seed in seeds)
        {
            var diagnosticsSink = new CollectingGeneticOptimizerDiagnosticsSink();

            var run = RunOptimizer(
                $"Clean GA Variant C Individual Shortage Sequence Pressure Seed {seed}",
                new GeneticScheduleOptimizer(
                    populationSize: GeneticPopulationSize,
                    seed: seed,
                    generationCount: CleanGenerationCount,
                    eliteCount: 1,
                    tournamentSize: 3,
                    diagnosticsSink: diagnosticsSink,
                    evolutionMode: GeneticEvolutionMode.Clean,
                    scoringWeights: weights),
                scenario.Problem,
                diagnosticsSink.Diagnostics);

            AssertCandidateReferencesKnownProblemEntities(
                scenario,
                run.Result.Candidate);

            AssertNoBasicStructuralViolations(run.Result.Evaluation);

            Assert.Equal(
                CleanGenerationCount + 1,
                run.Diagnostics.Count);

            Assert.True(
                run.Diagnostics[^1].BestSoFarTotalPenalty <=
                run.Diagnostics[0].BestSoFarTotalPenalty,
                $"Seed {seed} should not return a best-so-far penalty worse than generation 0.");

            runs.Add(new MultiSeedSensitivityRun(
                Seed: seed,
                Run: run));
        }

        var report = FormatIndividualAvailabilityShortageWithSequencePressureMultiSeedStabilityReport(
            scenario,
            weights,
            runs);

        System.Console.WriteLine(report);

        Assert.Contains("Stage 7.6D.4 Individual Availability Shortage With Sequence Pressure Multi-Seed Stability Diagnostic", report);
        Assert.Contains("Mode: Clean GA", report);
        Assert.Contains("Variant: Variant C", report);
        Assert.Contains("SequencePressureSeedSummary:", report);
        Assert.Contains("SequencePressureMultiSeedAggregateSummary:", report);
        Assert.Contains("ShiftSequenceQuotaExceededViolationCount:", report);
        Assert.Contains("AnyRunHasSequenceQuotaViolations:", report);
        Assert.Contains("MaxMonthlyTotalSequencesForSingleResourceRange:", report);
        Assert.Contains("AllRunsImprovedBestSoFar:", report);
    }



    [Fact]
    [Trait("Category", "SchedulingExperiment")]
    public void CleanGeneticOptimizer_ShouldPrintUnwantedAssignmentFairnessDiagnostic()
    {
        var scenario = CreateIndividualAvailabilityShortageWithSequencePressureScenario();
        var weights = ScheduleScoringWeights.CreateDefault();

        var seeds = new[]
        {
            20260603,
            20260604,
            20260605
        };

        var runs = new List<MultiSeedSensitivityRun>();

        foreach (var seed in seeds)
        {
            var diagnosticsSink = new CollectingGeneticOptimizerDiagnosticsSink();

            var run = RunOptimizer(
                $"Clean GA Default Policy Unwanted Assignment Fairness Seed {seed}",
                new GeneticScheduleOptimizer(
                    populationSize: GeneticPopulationSize,
                    seed: seed,
                    generationCount: CleanGenerationCount,
                    eliteCount: 1,
                    tournamentSize: 3,
                    diagnosticsSink: diagnosticsSink,
                    evolutionMode: GeneticEvolutionMode.Clean,
                    scoringWeights: weights),
                scenario.Problem,
                diagnosticsSink.Diagnostics);

            AssertCandidateReferencesKnownProblemEntities(
                scenario,
                run.Result.Candidate);

            AssertNoBasicStructuralViolations(run.Result.Evaluation);

            Assert.Equal(
                CleanGenerationCount + 1,
                run.Diagnostics.Count);

            Assert.True(
                run.Diagnostics[^1].BestSoFarTotalPenalty <=
                run.Diagnostics[0].BestSoFarTotalPenalty,
                $"Seed {seed} should not return a best-so-far penalty worse than generation 0.");

            Assert.Equal(
                CalculateIgnoredAvoidAssignmentCountsByResource(
                    scenario,
                    run.Result.Candidate).Sum(),
                CountViolations(
                    run.Result.Evaluation,
                    ConstraintViolationType.IgnoredAvoidPreference));

            runs.Add(new MultiSeedSensitivityRun(
                Seed: seed,
                Run: run));
        }

        var report = FormatUnwantedAssignmentFairnessDiagnosticReport(
            scenario,
            weights,
            runs);

        System.Console.WriteLine(report);

        Assert.Contains("Stage 7.7A Unwanted Assignment Fairness Diagnostic", report);
        Assert.Contains("UnwantedFairnessSeedSummary:", report);
        Assert.Contains("IgnoredAvoidAssignmentsByResource:", report);
        Assert.Contains("UnwantedFairnessAggregateSummary:", report);
        Assert.Contains("TotalIgnoredAvoidAssignmentsRange:", report);
        Assert.Contains("MaxIgnoredAvoidAssignmentsForSingleResourceRange:", report);
        Assert.Contains("MaxAvoidedMorningAssignmentsForSingleResourceRange:", report);
        Assert.Contains("PotentialUnwantedConcentrationSignal:", report);
    }



    [Fact]
    public void CreateScenario_DefaultScoringWeights_ShouldPreserveRealWorldDeterministicBaselineScore()
    {
        var scenario = CreateScenario();

        var result = new DeterministicScheduleOptimizer()
            .Optimize(scenario.Problem);

        var explicitDefaultScore = new ScheduleScoreCalculator(
                ScheduleScoringWeights.CreateDefault())
            .Calculate(result.Evaluation.Violations);

        Assert.Equal(result.Evaluation.Score.Value, explicitDefaultScore.Value);
        Assert.Equal(result.Evaluation.Score.TotalPenalty, explicitDefaultScore.TotalPenalty);
        Assert.Equal(result.Evaluation.Score.HardViolationCount, explicitDefaultScore.HardViolationCount);
        Assert.Equal(result.Evaluation.Score.SoftViolationCount, explicitDefaultScore.SoftViolationCount);
        Assert.Equal(result.Evaluation.Score.IsFeasible, explicitDefaultScore.IsFeasible);
    }

    [Fact]
    public void DeterministicBaseline_ShouldRunOnScenarioProblem()
    {
        var scenario = CreateScenario();

        var result = new DeterministicScheduleOptimizer()
            .Optimize(scenario.Problem);

        Assert.NotNull(result.Candidate);
        Assert.NotNull(result.Evaluation);
        Assert.NotEmpty(result.Candidate.Assignments);

        var resourceIds = scenario.Resources
            .Select(resource => resource.Id)
            .ToHashSet();

        var shiftIds = scenario.Shifts
            .Select(shift => shift.Id)
            .ToHashSet();

        Assert.All(result.Candidate.Assignments, assignment =>
        {
            Assert.Contains(assignment.ResourceId, resourceIds);
            Assert.Contains(assignment.ShiftId, shiftIds);
        });

        Assert.Equal(
            result.Candidate.Assignments.Count,
            result.Candidate.Assignments
                .Select(assignment => $"{assignment.ResourceId}:{assignment.ShiftId}")
                .Distinct()
                .Count());
    }

    [Fact]
    public void DeterministicBaseline_ShouldExposeOptimizationPressureWithoutBasicStructuralFailures()
    {
        var scenario = CreateScenario();

        var result = new DeterministicScheduleOptimizer()
            .Optimize(scenario.Problem);

        Assert.Equal(
            0,
            CountViolations(
                result.Evaluation,
                ConstraintViolationType.ResourceUnavailable));

        Assert.Equal(
            0,
            CountViolations(
                result.Evaluation,
                ConstraintViolationType.ResourceAssignedToOverlappingShifts));

        Assert.Equal(
            0,
            CountViolations(
                result.Evaluation,
                ConstraintViolationType.AssignedWithoutRequiredPreference));

        Assert.Equal(
            0,
            CountViolations(
                result.Evaluation,
                ConstraintViolationType.ShiftUnderstaffed));

        Assert.Equal(
            0,
            CountViolations(
                result.Evaluation,
                ConstraintViolationType.ShiftOverstaffed));

        Assert.True(
            result.Evaluation.Score.TotalPenalty > 0,
            "Deterministic baseline should expose real optimization pressure. If penalty is zero, the scenario is too easy.");
    }

    [Fact]
    public void CleanGeneticOptimizer_ShouldRunOnScenarioProblemAndReportDiagnostics()
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

        AssertNoBasicStructuralViolations(result.Evaluation);
    }

    [Fact]
    public void CleanGeneticOptimizer_ShouldNotReturnWorseThanInitialCleanPopulationBest()
    {
        var scenario = CreateScenario();

        var initialOnly = new GeneticScheduleOptimizer(
                populationSize: GeneticPopulationSize,
                seed: GeneticSeed,
                generationCount: 0,
                eliteCount: 1,
                tournamentSize: 3,
                evolutionMode: GeneticEvolutionMode.Clean)
            .Optimize(scenario.Problem);

        var evolved = new GeneticScheduleOptimizer(
                populationSize: GeneticPopulationSize,
                seed: GeneticSeed,
                generationCount: CleanGenerationCount,
                eliteCount: 1,
                tournamentSize: 3,
                evolutionMode: GeneticEvolutionMode.Clean)
            .Optimize(scenario.Problem);

        var ranker = new ScheduleEvaluationResultRanker();

        Assert.False(
            ranker.IsBetterThan(
                initialOnly.Evaluation,
                evolved.Evaluation),
            "Clean GA should not return a result ranked worse than the initial best candidate when elitism is enabled.");

        AssertNoBasicStructuralViolations(evolved.Evaluation);
    }


    [Fact]
    public void CleanGeneticOptimizer_ShouldPrintRealScenarioScoringBaseline()
    {
        var scenario = CreateScenario();
        var diagnosticsSink = new CollectingGeneticOptimizerDiagnosticsSink();

        var clean = RunOptimizer(
            "Clean GA",
            new GeneticScheduleOptimizer(
                populationSize: GeneticPopulationSize,
                seed: GeneticSeed,
                generationCount: CleanGenerationCount,
                eliteCount: 1,
                tournamentSize: 3,
                diagnosticsSink: diagnosticsSink,
                evolutionMode: GeneticEvolutionMode.Clean),
            scenario.Problem,
            diagnosticsSink.Diagnostics);

        var report = FormatCleanRealScenarioScoringBaselineReport(
            scenario,
            clean);

        System.Console.WriteLine(report);

        Assert.Contains("Stage 7.6A Real Scenario Scoring Baseline", report);
        Assert.Contains("Mode: Clean GA", report);
        Assert.Contains("ScenarioCapacitySummary:", report);
        Assert.Contains("PenaltyBreakdownByType:", report);
        Assert.Contains("RequestedPreferredFulfillmentSummary:", report);
        Assert.Contains("TargetGapSummary:", report);
        Assert.Contains("GenerationDiagnostics:", report);
        Assert.Contains(nameof(ConstraintViolationType.ResourceRequestedPreferredHoursNotSatisfied), report);

        AssertCandidateReferencesKnownProblemEntities(
            scenario,
            clean.Result.Candidate);

        AssertNoBasicStructuralViolations(clean.Result.Evaluation);

        Assert.Equal(
            CleanGenerationCount + 1,
            clean.Diagnostics.Count);

        Assert.True(
            clean.Result.Evaluation.Score.HardViolationCount == 0,
            report);

        Assert.True(
            clean.Diagnostics[^1].BestSoFarTotalPenalty <=
            clean.Diagnostics[0].BestSoFarTotalPenalty,
            report);
    }


    [Fact]
    [Trait("Category", "SchedulingExperiment")]
    public void CleanGeneticOptimizer_ShouldPrintManualScoringWeightSensitivityReport()
    {
        var scenario = CreateScenario();

        var variants = CreateManualScoringWeightSensitivityVariants();
        var runs = new List<ScoringWeightSensitivityRun>();

        foreach (var variant in variants)
        {
            var diagnosticsSink = new CollectingGeneticOptimizerDiagnosticsSink();

            var run = RunOptimizer(
                $"Clean GA {variant.Name}",
                new GeneticScheduleOptimizer(
                    populationSize: GeneticPopulationSize,
                    seed: GeneticSeed,
                    generationCount: CleanGenerationCount,
                    eliteCount: 1,
                    tournamentSize: 3,
                    diagnosticsSink: diagnosticsSink,
                    evolutionMode: GeneticEvolutionMode.Clean,
                    scoringWeights: variant.Weights),
                scenario.Problem,
                diagnosticsSink.Diagnostics);

            AssertCandidateReferencesKnownProblemEntities(
                scenario,
                run.Result.Candidate);

            AssertNoBasicStructuralViolations(run.Result.Evaluation);

            Assert.Equal(
                CleanGenerationCount + 1,
                run.Diagnostics.Count);

            Assert.True(
                run.Diagnostics[^1].BestSoFarTotalPenalty <=
                run.Diagnostics[0].BestSoFarTotalPenalty,
                $"{variant.Name} should not return a best-so-far penalty worse than generation 0.");

            runs.Add(new ScoringWeightSensitivityRun(
                Variant: variant,
                Run: run));
        }

        var report = FormatManualScoringWeightSensitivityReport(
            scenario,
            runs);

        System.Console.WriteLine(report);

        Assert.Contains("Stage 7.6B.2 Manual Scoring Weight Sensitivity Report", report);
        Assert.Contains("Mode: Clean GA", report);
        Assert.Contains("Current", report);
        Assert.Contains("Variant A", report);
        Assert.Contains("Variant B", report);
        Assert.Contains("Variant C", report);
        Assert.Contains("NetTradeoffPerPreferred8HourAssignment:", report);
        Assert.Contains("NetPenaltyImprovement: -40", report);
        Assert.Contains("NetPenaltyImprovement: 0", report);
        Assert.Contains("NetPenaltyImprovement: -40", report);
        Assert.Contains("TotalAssignedHours:", report);
        Assert.Contains("TotalOverTargetHours:", report);
        Assert.Contains("TotalUnderTargetHours:", report);
        Assert.Contains("AssignedRequestedPreferredHours:", report);
        Assert.Contains("UnsatisfiedRequestedPreferredHours:", report);
        Assert.Contains("PenaltyBreakdownByType:", report);
        Assert.Contains(nameof(ConstraintViolationType.ResourceRequestedPreferredHoursNotSatisfied), report);
        Assert.Contains(nameof(ConstraintViolationType.ResourceEffectiveTargetAssignedHoursAboveTarget), report);
    }

    [Fact]
    [Trait("Category", "SchedulingExperiment")]
    public void CleanGeneticOptimizer_ShouldPrintBalanceExcessPenaltyMultiSeedStabilityReport()
    {
        const double balanceToleranceHours = 5.0;

        var scenario = CreateScenarioWithAssignedHoursBalanceTolerance(
            balanceToleranceHours);

        var seeds = new[]
        {
            20260603,
            20260604,
            20260605
        };

        var variants = CreateBalanceExcessPenaltyMultiSeedVariants();
        var runs = new List<BalanceExcessMultiSeedRun>();

        foreach (var seed in seeds)
        {
            foreach (var variant in variants)
            {
                var diagnosticsSink = new CollectingGeneticOptimizerDiagnosticsSink();

                var run = RunOptimizer(
                    $"Clean GA Variant C {variant.Name} Seed {seed}",
                    new GeneticScheduleOptimizer(
                        populationSize: GeneticPopulationSize,
                        seed: seed,
                        generationCount: CleanGenerationCount,
                        eliteCount: 1,
                        tournamentSize: 3,
                        diagnosticsSink: diagnosticsSink,
                        evolutionMode: GeneticEvolutionMode.Clean,
                        scoringWeights: variant.Weights),
                    scenario.Problem,
                    diagnosticsSink.Diagnostics);

                AssertCandidateReferencesKnownProblemEntities(
                    scenario,
                    run.Result.Candidate);

                AssertNoBasicStructuralViolations(run.Result.Evaluation);

                Assert.Equal(
                    CleanGenerationCount + 1,
                    run.Diagnostics.Count);

                Assert.True(
                    run.Diagnostics[^1].BestSoFarTotalPenalty <=
                    run.Diagnostics[0].BestSoFarTotalPenalty,
                    $"{variant.Name} seed {seed} should not return a best-so-far penalty worse than generation 0.");

                runs.Add(new BalanceExcessMultiSeedRun(
                    Seed: seed,
                    SensitivityRun: new ScoringWeightSensitivityRun(
                        Variant: variant,
                        Run: run)));
            }
        }

        var report = FormatBalanceExcessPenaltyMultiSeedStabilityReport(
            scenario,
            balanceToleranceHours,
            seeds,
            runs);

        System.Console.WriteLine(report);

        Assert.Contains("Stage 7.7D Balance Excess Penalty Multi-Seed Stability Report", report);
        Assert.Contains("Mode: Clean GA", report);
        Assert.Contains("Variant: Variant C", report);
        Assert.Contains("BalanceToleranceHours: 5", report);
        Assert.Contains("SeedCount: 3", report);
        Assert.Contains("VariantCount: 2", report);
        Assert.Contains("BalanceExcess0", report);
        Assert.Contains("BalanceExcess100", report);
        Assert.Contains("Seed: 20260603", report);
        Assert.Contains("Seed: 20260604", report);
        Assert.Contains("Seed: 20260605", report);
        Assert.Contains("BalanceExcessMultiSeedAggregateSummary:", report);
        Assert.Contains("VariantAggregateSummary:", report);
        Assert.Contains("AnyRunHasHardViolations:", report);
        Assert.Contains("AllRunsImprovedBestSoFar:", report);
    }


    [Fact]
    [Trait("Category", "SchedulingExperiment")]
    public void CleanGeneticOptimizer_ShouldPrintBalanceExcessPenaltyWeightDiagnostic()
    {
        const double balanceToleranceHours = 5.0;

        var scenario = CreateScenarioWithAssignedHoursBalanceTolerance(
            balanceToleranceHours);

        var variants = CreateBalanceExcessPenaltyWeightVariants();
        var runs = new List<ScoringWeightSensitivityRun>();

        foreach (var variant in variants)
        {
            var diagnosticsSink = new CollectingGeneticOptimizerDiagnosticsSink();

            var run = RunOptimizer(
                $"Clean GA Variant C {variant.Name}",
                new GeneticScheduleOptimizer(
                    populationSize: GeneticPopulationSize,
                    seed: GeneticSeed,
                    generationCount: CleanGenerationCount,
                    eliteCount: 1,
                    tournamentSize: 3,
                    diagnosticsSink: diagnosticsSink,
                    evolutionMode: GeneticEvolutionMode.Clean,
                    scoringWeights: variant.Weights),
                scenario.Problem,
                diagnosticsSink.Diagnostics);

            AssertCandidateReferencesKnownProblemEntities(
                scenario,
                run.Result.Candidate);

            AssertNoBasicStructuralViolations(run.Result.Evaluation);

            Assert.Equal(
                CleanGenerationCount + 1,
                run.Diagnostics.Count);

            Assert.True(
                run.Diagnostics[^1].BestSoFarTotalPenalty <=
                run.Diagnostics[0].BestSoFarTotalPenalty,
                $"{variant.Name} should not return a best-so-far penalty worse than generation 0.");

            runs.Add(new ScoringWeightSensitivityRun(
                Variant: variant,
                Run: run));
        }

        var report = FormatBalanceExcessPenaltyWeightDiagnosticReport(
            scenario,
            balanceToleranceHours,
            runs);

        System.Console.WriteLine(report);

        Assert.Contains("Stage 7.7C Balance Excess Penalty Weight Diagnostic", report);
        Assert.Contains("Mode: Clean GA", report);
        Assert.Contains("Variant: Variant C", report);
        Assert.Contains("BalanceToleranceHours: 5", report);
        Assert.Contains("BalanceExcess0", report);
        Assert.Contains("BalanceExcess25", report);
        Assert.Contains("BalanceExcess50", report);
        Assert.Contains("BalanceExcess100", report);
        Assert.Contains("AssignedHoursBalanceMetrics:", report);
        Assert.Contains("MaxAssignedHoursDeviationFromAverage:", report);
        Assert.Contains("ResourcesOutsideBalanceToleranceCount:", report);
        Assert.Contains("BalanceViolationCountFromEvaluation:", report);
        Assert.Contains("BalanceExcessPenaltyAggregateSummary:", report);
        Assert.Contains(nameof(ConstraintViolationType.ResourceAssignedHoursBalanceExceeded), report);
    }


    [Fact]
    public void CleanGeneticOptimizer_ShouldPrintComparisonAgainstDeterministicBaseline()
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

        var report = FormatDeterministicVsCleanReport(
            deterministic,
            clean);

        System.Console.WriteLine(report);

        Assert.Contains("Real World Biweekly Clean GA Comparison", report);
        Assert.Contains("Deterministic", report);
        Assert.Contains("Clean GA", report);
        Assert.Contains("CleanRankedBetterThanDeterministic", report);
        Assert.Contains("PenaltyDelta", report);
        Assert.Contains("GenerationDiagnostics", report);

        AssertCandidateReferencesKnownProblemEntities(
            scenario,
            deterministic.Result.Candidate);

        AssertCandidateReferencesKnownProblemEntities(
            scenario,
            clean.Result.Candidate);

        AssertNoBasicStructuralViolations(deterministic.Result.Evaluation);
        AssertNoBasicStructuralViolations(clean.Result.Evaluation);

        Assert.Equal(
            CleanGenerationCount + 1,
            clean.Diagnostics.Count);

        Assert.True(
            deterministic.Result.Evaluation.Score.TotalPenalty > 0,
            "Deterministic baseline should still expose optimization pressure.");

        var ranker = new ScheduleEvaluationResultRanker();

        Assert.True(
            ranker.IsBetterThan(
                clean.Result.Evaluation,
                deterministic.Result.Evaluation),
            report);

        Assert.False(
            ranker.IsBetterThan(
                deterministic.Result.Evaluation,
                clean.Result.Evaluation),
            report);

        Assert.True(
            clean.Result.Evaluation.Score.HardViolationCount <=
            deterministic.Result.Evaluation.Score.HardViolationCount,
            report);

        Assert.True(
            clean.Result.Evaluation.Score.TotalPenalty <=
            deterministic.Result.Evaluation.Score.TotalPenalty,
            report);

        Assert.True(
            clean.Diagnostics[^1].BestSoFarTotalPenalty <=
            clean.Diagnostics[0].BestSoFarTotalPenalty,
            report);
    }

    [Fact]
    public void RepairAssistedGeneticOptimizer_ShouldPrintComparisonAgainstCleanGeneticOptimizer()
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

        var report = FormatDeterministicCleanRepairReport(
            scenario,
            deterministic,
            clean,
            repair);

        System.Console.WriteLine(report);

        AssertMonthlyNightQuotaPerCategory(scenario, clean.Result.Candidate, report);
        AssertMonthlyNightQuotaPerCategory(scenario, repair.Result.Candidate, report);

        Assert.Contains("Real World Biweekly Clean vs RepairAssisted GA Comparison", report);
        Assert.Contains("Deterministic", report);
        Assert.Contains("Clean GA", report);
        Assert.Contains("RepairAssisted GA", report);
        Assert.Contains("RepairRankedBetterThanClean", report);
        Assert.Contains("CleanRankedBetterThanRepair", report);
        Assert.Contains("RepairPenaltyDeltaAgainstClean", report);
        Assert.Contains("ResourceTargetSummary:", report);
        Assert.Contains("EffectiveTargetHours", report);
        Assert.Contains("AssignedHours", report);
        Assert.Contains("GapToTarget", report);
        Assert.Contains("RegularNightAssignments", report);
        Assert.Contains("MotzeiShabbatNightAssignments", report);
        Assert.Contains("TargetGapSummary:", report);
        Assert.Contains("TotalAbsoluteTargetGapHours", report);
        Assert.Contains("TotalOverTargetHours", report);
        Assert.Contains("TotalUnderTargetHours", report);
        Assert.Contains("AverageOverTargetHours", report);
        Assert.Contains("MaxOverTargetHours", report);
        Assert.Contains("MaxUnderTargetHours", report);
        Assert.Contains("MonthlyNightQuotaSummary:", report);
        Assert.Contains("UnsatisfiedMotzeiShabbatPreferenceRequestCount", report);

        AssertCandidateReferencesKnownProblemEntities(
            scenario,
            deterministic.Result.Candidate);

        AssertCandidateReferencesKnownProblemEntities(
            scenario,
            clean.Result.Candidate);

        AssertCandidateReferencesKnownProblemEntities(
            scenario,
            repair.Result.Candidate);

        AssertNoBasicStructuralViolations(clean.Result.Evaluation);
        AssertNoBasicStructuralViolations(repair.Result.Evaluation);

        Assert.Equal(
            CleanGenerationCount + 1,
            clean.Diagnostics.Count);

        Assert.Equal(
            CleanGenerationCount + 1,
            repair.Diagnostics.Count);

        var ranker = new ScheduleEvaluationResultRanker();

        Assert.True(
            ranker.IsBetterThan(
                clean.Result.Evaluation,
                deterministic.Result.Evaluation),
            report);

        Assert.True(
            ranker.IsBetterThan(
                repair.Result.Evaluation,
                deterministic.Result.Evaluation),
            report);

        Assert.True(
            clean.Result.Evaluation.Score.TotalPenalty <=
            deterministic.Result.Evaluation.Score.TotalPenalty,
            report);

        Assert.True(
            repair.Result.Evaluation.Score.TotalPenalty <=
            deterministic.Result.Evaluation.Score.TotalPenalty,
            report);

        Assert.False(
            ranker.IsBetterThan(
                clean.Result.Evaluation,
                repair.Result.Evaluation),
            report);

        Assert.True(
            repair.Result.Evaluation.Score.HardViolationCount <=
            clean.Result.Evaluation.Score.HardViolationCount,
            report);

        Assert.True(
            repair.Result.Evaluation.Score.TotalPenalty <=
            clean.Result.Evaluation.Score.TotalPenalty,
            report);

        Assert.True(
            repair.Diagnostics[^1].BestSoFarTotalPenalty <=
            repair.Diagnostics[0].BestSoFarTotalPenalty,
            report);
    }

    [Fact]
    public void CleanAndRepairGeneticOptimizers_ShouldPrintMultiSeedComparison()
    {
        var seeds = new[]
        {
            20260603,
            20260604,
            20260605
        };

        var scenario = CreateScenario();

        var deterministic = RunOptimizer(
            "Deterministic",
            new DeterministicScheduleOptimizer(),
            scenario.Problem);

        var ranker = new ScheduleEvaluationResultRanker();
        var builder = new StringBuilder();

        builder.AppendLine("Real World Biweekly Multi Seed Comparison");
        builder.AppendLine();
        builder.AppendLine(
            $"Deterministic: TotalPenalty={deterministic.Result.Evaluation.Score.TotalPenalty}, " +
            $"HardViolationCount={deterministic.Result.Evaluation.Score.HardViolationCount}");
        builder.AppendLine();

        foreach (var seed in seeds)
        {
            var cleanDiagnosticsSink = new CollectingGeneticOptimizerDiagnosticsSink();

            var clean = RunOptimizer(
                $"Clean GA Seed {seed}",
                new GeneticScheduleOptimizer(
                    populationSize: GeneticPopulationSize,
                    seed: seed,
                    generationCount: CleanGenerationCount,
                    eliteCount: 1,
                    tournamentSize: 3,
                    diagnosticsSink: cleanDiagnosticsSink,
                    evolutionMode: GeneticEvolutionMode.Clean),
                scenario.Problem,
                cleanDiagnosticsSink.Diagnostics);

            var repairDiagnosticsSink = new CollectingGeneticOptimizerDiagnosticsSink();

            var repair = RunOptimizer(
                $"RepairAssisted GA Seed {seed}",
                new GeneticScheduleOptimizer(
                    populationSize: GeneticPopulationSize,
                    seed: seed,
                    generationCount: CleanGenerationCount,
                    eliteCount: 1,
                    tournamentSize: 3,
                    diagnosticsSink: repairDiagnosticsSink,
                    evolutionMode: GeneticEvolutionMode.RepairAssisted),
                scenario.Problem,
                repairDiagnosticsSink.Diagnostics);

            var cleanMetrics = CalculateTargetGapMetrics(
                scenario,
                clean.Result.Candidate);

            var repairMetrics = CalculateTargetGapMetrics(
                scenario,
                repair.Result.Candidate);

            AppendMultiSeedRunSummary(
                builder,
                scenario,
                seed,
                clean,
                repair,
                cleanMetrics,
                repairMetrics);

            AssertCandidateReferencesKnownProblemEntities(
                scenario,
                clean.Result.Candidate);

            AssertCandidateReferencesKnownProblemEntities(
                scenario,
                repair.Result.Candidate);

            AssertNoBasicStructuralViolations(clean.Result.Evaluation);
            AssertNoBasicStructuralViolations(repair.Result.Evaluation);

            Assert.Equal(
                0,
                CountViolations(
                    clean.Result.Evaluation,
                    ConstraintViolationType.ResourceMonthlyNightShiftQuotaExceeded));

            Assert.Equal(
                0,
                CountViolations(
                    repair.Result.Evaluation,
                    ConstraintViolationType.ResourceMonthlyNightShiftQuotaExceeded));

            Assert.True(
                ranker.IsBetterThan(
                    clean.Result.Evaluation,
                    deterministic.Result.Evaluation),
                builder.ToString());

            Assert.True(
                ranker.IsBetterThan(
                    repair.Result.Evaluation,
                    deterministic.Result.Evaluation),
                builder.ToString());

            Assert.True(
                clean.Diagnostics[^1].BestSoFarTotalPenalty <=
                clean.Diagnostics[0].BestSoFarTotalPenalty,
                builder.ToString());

            Assert.True(
                repair.Diagnostics[^1].BestSoFarTotalPenalty <=
                repair.Diagnostics[0].BestSoFarTotalPenalty,
                builder.ToString());
        }

        var report = builder.ToString();

        System.Console.WriteLine(report);

        Assert.Contains("Real World Biweekly Multi Seed Comparison", report);
        Assert.Contains("AverageOverTargetHours", report);
        Assert.Contains("MaxOverTargetHours", report);
        Assert.Contains("MaxUnderTargetHours", report);
        Assert.Contains("UnsatisfiedMotzeiShabbatPreferenceRequestCount", report);
    }

    [Fact]
    [Trait("Category", "SchedulingExperiment")]
    public void RepairAssistedGeneticOptimizer_ShouldPrintWorkloadTransferDiagnostics()
    {
        const int diagnosticSeed = 20260605;

        var scenario = CreateScenario();
        var repairDiagnosticsSink = new CollectingGeneticOptimizerDiagnosticsSink();

        var repair = RunOptimizer(
            $"RepairAssisted GA Seed {diagnosticSeed}",
            new GeneticScheduleOptimizer(
                populationSize: GeneticPopulationSize,
                seed: diagnosticSeed,
                generationCount: CleanGenerationCount,
                eliteCount: 1,
                tournamentSize: 3,
                diagnosticsSink: repairDiagnosticsSink,
                evolutionMode: GeneticEvolutionMode.RepairAssisted),
            scenario.Problem,
            repairDiagnosticsSink.Diagnostics);

        AssertCandidateReferencesKnownProblemEntities(
            scenario,
            repair.Result.Candidate);

        AssertNoBasicStructuralViolations(repair.Result.Evaluation);

        AssertMonthlyNightQuotaPerCategory(
            scenario,
            repair.Result.Candidate,
            "Base RepairAssisted result should respect monthly night quota before transfer diagnostics.");

        var diagnostics = AnalyzeWorkloadTransferMoves(
            scenario,
            repair.Result);

        var report = FormatWorkloadTransferDiagnosticsReport(
            repair,
            diagnostics);

        System.Console.WriteLine(report);

        Assert.Contains("Stage 7.5C Workload Transfer Diagnostics", report);
        Assert.Contains("CandidateTransferMoveCount", report);
        Assert.Contains("ImprovingTransferMoveCount", report);
        Assert.Contains("RejectedHardViolationByType", report);
    }

    [Fact]
    [Trait("Category", "SchedulingExperiment")]
    public void RepairAssistedGeneticOptimizer_ShouldPrintUnderTargetAddDiagnostics()
    {
        const int diagnosticSeed = 20260605;

        var scenario = CreateScenario();
        var repairDiagnosticsSink = new CollectingGeneticOptimizerDiagnosticsSink();

        var repair = RunOptimizer(
            $"RepairAssisted GA Seed {diagnosticSeed}",
            new GeneticScheduleOptimizer(
                populationSize: GeneticPopulationSize,
                seed: diagnosticSeed,
                generationCount: CleanGenerationCount,
                eliteCount: 1,
                tournamentSize: 3,
                diagnosticsSink: repairDiagnosticsSink,
                evolutionMode: GeneticEvolutionMode.RepairAssisted),
            scenario.Problem,
            repairDiagnosticsSink.Diagnostics);

        AssertCandidateReferencesKnownProblemEntities(
            scenario,
            repair.Result.Candidate);

        AssertNoBasicStructuralViolations(repair.Result.Evaluation);

        AssertMonthlyNightQuotaPerCategory(
            scenario,
            repair.Result.Candidate,
            "Base RepairAssisted result should respect monthly night quota before add diagnostics.");

        var diagnostics = AnalyzeUnderTargetAddMoves(
            scenario,
            repair.Result);

        var report = FormatUnderTargetAddDiagnosticsReport(
            repair,
            diagnostics);

        System.Console.WriteLine(report);

        Assert.Contains("Stage 7.5D UnderTarget Add Diagnostics", report);
        Assert.Contains("CandidateAddMoveCount", report);
        Assert.Contains("ImprovingAddMoveCount", report);
        Assert.Contains("RejectedHardViolationByType", report);
    }

    private static UnderTargetAddDiagnostics AnalyzeUnderTargetAddMoves(
        ExperimentScenario scenario,
        ScheduleOptimizationResult baseResult)
    {
        var evaluator = new ScheduleEvaluator();
        var ranker = new ScheduleEvaluationResultRanker();

        var shiftsById = scenario.Shifts
            .ToDictionary(shift => shift.Id);

        var resourcesById = scenario.Resources
            .ToDictionary(resource => resource.Id);

        var assignments = baseResult.Candidate.Assignments.ToArray();

        var assignmentCountByShiftId = assignments
            .GroupBy(assignment => assignment.ShiftId)
            .ToDictionary(
                group => group.Key,
                group => group.Count());

        var assignedHoursByResourceId = assignments
            .GroupBy(assignment => assignment.ResourceId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(assignment =>
                    GetShiftHours(shiftsById[assignment.ShiftId])));

        var underTargetResourceIds = scenario.ResourceWorkloadDemands
            .Where(demand =>
            {
                var assignedHours = assignedHoursByResourceId.GetValueOrDefault(
                    demand.ResourceId,
                    0.0);

                return assignedHours + HoursTolerance < demand.EffectiveTargetHours;
            })
            .Select(demand => demand.ResourceId)
            .ToArray();

        var candidateAddMoveCount = 0;
        var rejectedShiftAtMaxCapacity = 0;
        var rejectedAlreadyAssignedToShift = 0;
        var rejectedUnavailable = 0;
        var rejectedMissingRequiredPreference = 0;
        var rejectedOverlap = 0;
        var rejectedHardViolation = 0;
        var rejectedNotImproving = 0;
        var improvingAddMoveCount = 0;

        var rejectedHardViolationByType = new Dictionary<ConstraintViolationType, int>();

        ScheduleEvaluationResult? bestAddEvaluation = null;
        UnderTargetAddMove? bestAddMove = null;

        foreach (var targetResourceId in underTargetResourceIds)
        {
            foreach (var shift in scenario.Shifts)
            {
                candidateAddMoveCount++;

                assignmentCountByShiftId.TryGetValue(
                    shift.Id,
                    out var assignedResourceCount);

                if (assignedResourceCount >= shift.MaxResourceCount)
                {
                    rejectedShiftAtMaxCapacity++;
                    continue;
                }

                if (assignments.Any(existingAssignment =>
                        existingAssignment.ResourceId == targetResourceId &&
                        existingAssignment.ShiftId == shift.Id))
                {
                    rejectedAlreadyAssignedToShift++;
                    continue;
                }

                if (!IsAvailableForShift(
                        scenario,
                        targetResourceId,
                        shift))
                {
                    rejectedUnavailable++;
                    continue;
                }

                if (shift.RequiresPreferenceToAssign &&
                    !HasPreferPreferenceForShift(
                        scenario,
                        targetResourceId,
                        shift))
                {
                    rejectedMissingRequiredPreference++;
                    continue;
                }

                if (HasOverlappingAssignment(
                        assignments,
                        shiftsById,
                        targetResourceId,
                        shift))
                {
                    rejectedOverlap++;
                    continue;
                }

                var addCandidate = CreateAddCandidate(
                    assignments,
                    targetResourceId,
                    shift.Id);

                var addEvaluation = evaluator.Evaluate(
                    scenario.Problem,
                    addCandidate);

                if (addEvaluation.Score.HardViolationCount > 0)
                {
                    rejectedHardViolation++;

                    foreach (var violation in addEvaluation.Violations
                                 .Where(violation => violation.Severity == ConstraintViolationSeverity.Hard))
                    {
                        rejectedHardViolationByType.TryGetValue(
                            violation.Type,
                            out var currentCount);

                        rejectedHardViolationByType[violation.Type] = currentCount + 1;
                    }

                    continue;
                }

                if (!ranker.IsBetterThan(
                        addEvaluation,
                        baseResult.Evaluation))
                {
                    rejectedNotImproving++;
                    continue;
                }

                improvingAddMoveCount++;

                if (bestAddEvaluation is not null &&
                    !ranker.IsBetterThan(
                        addEvaluation,
                        bestAddEvaluation))
                {
                    continue;
                }

                bestAddEvaluation = addEvaluation;
                bestAddMove = new UnderTargetAddMove(
                    ResourceName: resourcesById[targetResourceId].Name,
                    ShiftStartUtc: shift.StartUtc,
                    ShiftEndUtc: shift.EndUtc,
                    ShiftKind: shift.Kind,
                    NightShiftCategory: shift.NightShiftCategory,
                    TotalPenalty: addEvaluation.Score.TotalPenalty,
                    PenaltyDelta: baseResult.Evaluation.Score.TotalPenalty -
                                  addEvaluation.Score.TotalPenalty);
            }
        }

        var baseMetrics = CalculateTargetGapMetrics(
            scenario,
            baseResult.Candidate);

        return new UnderTargetAddDiagnostics(
            BaseMetrics: baseMetrics,
            UnderTargetResourceCount: underTargetResourceIds.Length,
            CandidateAddMoveCount: candidateAddMoveCount,
            RejectedShiftAtMaxCapacity: rejectedShiftAtMaxCapacity,
            RejectedAlreadyAssignedToShift: rejectedAlreadyAssignedToShift,
            RejectedUnavailable: rejectedUnavailable,
            RejectedMissingRequiredPreference: rejectedMissingRequiredPreference,
            RejectedOverlap: rejectedOverlap,
            RejectedHardViolation: rejectedHardViolation,
            RejectedNotImproving: rejectedNotImproving,
            ImprovingAddMoveCount: improvingAddMoveCount,
            RejectedHardViolationByType: rejectedHardViolationByType
                .OrderBy(item => item.Key.ToString())
                .ToDictionary(
                    item => item.Key,
                    item => item.Value),
            BestAddMove: bestAddMove);
    }

    private static string FormatUnderTargetAddDiagnosticsReport(
        ExperimentRun repair,
        UnderTargetAddDiagnostics diagnostics)
    {
        var builder = new StringBuilder();

        builder.AppendLine("Stage 7.5D UnderTarget Add Diagnostics");
        builder.AppendLine();

        builder.AppendLine("Scope:");
        builder.AppendLine("- Mode=RepairAssisted");
        builder.AppendLine("- Seed=20260605");
        builder.AppendLine("- DiagnosticOnly=True");
        builder.AppendLine("- MutationAdded=True");
        builder.AppendLine("- MutationName=TryCreateUnderTargetAddCandidate");
        builder.AppendLine();

        builder.AppendLine("Base RepairAssisted:");
        builder.AppendLine($"TotalPenalty={repair.Result.Evaluation.Score.TotalPenalty}");
        builder.AppendLine($"HardViolationCount={repair.Result.Evaluation.Score.HardViolationCount}");
        builder.AppendLine($"SoftViolationCount={repair.Result.Evaluation.Score.SoftViolationCount}");
        builder.AppendLine($"Assignments={repair.Result.Candidate.Assignments.Count}");
        builder.AppendLine($"TotalAbsoluteTargetGapHours={diagnostics.BaseMetrics.TotalAbsoluteTargetGapHours:0.##}");
        builder.AppendLine($"TotalOverTargetHours={diagnostics.BaseMetrics.TotalOverTargetHours:0.##}");
        builder.AppendLine($"TotalUnderTargetHours={diagnostics.BaseMetrics.TotalUnderTargetHours:0.##}");
        builder.AppendLine($"AverageOverTargetHours={diagnostics.BaseMetrics.AverageOverTargetHours:0.##}");
        builder.AppendLine($"MaxOverTargetHours={diagnostics.BaseMetrics.MaxOverTargetHours:0.##}");
        builder.AppendLine($"MaxUnderTargetHours={diagnostics.BaseMetrics.MaxUnderTargetHours:0.##}");
        builder.AppendLine($"UnderTargetResourceCount={diagnostics.UnderTargetResourceCount}");
        builder.AppendLine();

        builder.AppendLine("Add Diagnostics:");
        builder.AppendLine($"CandidateAddMoveCount={diagnostics.CandidateAddMoveCount}");
        builder.AppendLine($"RejectedShiftAtMaxCapacity={diagnostics.RejectedShiftAtMaxCapacity}");
        builder.AppendLine($"RejectedAlreadyAssignedToShift={diagnostics.RejectedAlreadyAssignedToShift}");
        builder.AppendLine($"RejectedUnavailable={diagnostics.RejectedUnavailable}");
        builder.AppendLine($"RejectedMissingRequiredPreference={diagnostics.RejectedMissingRequiredPreference}");
        builder.AppendLine($"RejectedOverlap={diagnostics.RejectedOverlap}");
        builder.AppendLine($"RejectedHardViolation={diagnostics.RejectedHardViolation}");
        builder.AppendLine($"RejectedNotImproving={diagnostics.RejectedNotImproving}");
        builder.AppendLine($"ImprovingAddMoveCount={diagnostics.ImprovingAddMoveCount}");

        AppendUnderTargetAddRejectedHardViolationBreakdown(
            builder,
            diagnostics);

        AppendBestUnderTargetAddMove(
            builder,
            diagnostics);

        return builder.ToString();
    }

    private static void AppendUnderTargetAddRejectedHardViolationBreakdown(
        StringBuilder builder,
        UnderTargetAddDiagnostics diagnostics)
    {
        builder.AppendLine("RejectedHardViolationByType:");

        if (diagnostics.RejectedHardViolationByType.Count == 0)
        {
            builder.AppendLine("- none");
            return;
        }

        foreach (var item in diagnostics.RejectedHardViolationByType)
        {
            builder.AppendLine($"- {item.Key}: {item.Value}");
        }
    }

    private static void AppendBestUnderTargetAddMove(
        StringBuilder builder,
        UnderTargetAddDiagnostics diagnostics)
    {
        builder.AppendLine("BestAddMove:");

        if (diagnostics.BestAddMove is null)
        {
            builder.AppendLine("- none");
            return;
        }

        var move = diagnostics.BestAddMove;

        builder.AppendLine($"BestAddDelta={move.PenaltyDelta}");
        builder.AppendLine($"BestAddTotalPenalty={move.TotalPenalty}");
        builder.AppendLine($"BestAddResource={move.ResourceName}");
        builder.AppendLine($"BestAddShiftStartUtc={move.ShiftStartUtc:o}");
        builder.AppendLine($"BestAddShiftEndUtc={move.ShiftEndUtc:o}");
        builder.AppendLine($"BestAddShiftKind={move.ShiftKind}");
        builder.AppendLine($"BestAddNightShiftCategory={move.NightShiftCategory?.ToString() ?? "None"}");
    }

    private static ScheduleCandidate CreateAddCandidate(
        IReadOnlyList<Assignment> assignments,
        Guid resourceId,
        Guid shiftId)
    {
        var mutatedAssignments = assignments.ToList();

        mutatedAssignments.Add(new Assignment(
            resourceId,
            shiftId));

        return new ScheduleCandidate(mutatedAssignments);
    }

    private sealed record UnderTargetAddDiagnostics(
        TargetGapMetrics BaseMetrics,
        int UnderTargetResourceCount,
        int CandidateAddMoveCount,
        int RejectedShiftAtMaxCapacity,
        int RejectedAlreadyAssignedToShift,
        int RejectedUnavailable,
        int RejectedMissingRequiredPreference,
        int RejectedOverlap,
        int RejectedHardViolation,
        int RejectedNotImproving,
        int ImprovingAddMoveCount,
        IReadOnlyDictionary<ConstraintViolationType, int> RejectedHardViolationByType,
        UnderTargetAddMove? BestAddMove);

    private sealed record UnderTargetAddMove(
        string ResourceName,
        DateTime ShiftStartUtc,
        DateTime ShiftEndUtc,
        ShiftKind ShiftKind,
        NightShiftCategory? NightShiftCategory,
        int TotalPenalty,
        int PenaltyDelta);

    private static WorkloadTransferDiagnostics AnalyzeWorkloadTransferMoves(
        ExperimentScenario scenario,
        ScheduleOptimizationResult baseResult)
    {
        var evaluator = new ScheduleEvaluator();
        var ranker = new ScheduleEvaluationResultRanker();

        var shiftsById = scenario.Shifts
            .ToDictionary(shift => shift.Id);

        var resourcesById = scenario.Resources
            .ToDictionary(resource => resource.Id);

        var assignments = baseResult.Candidate.Assignments.ToArray();

        var assignedHoursByResourceId = assignments
            .GroupBy(assignment => assignment.ResourceId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(assignment =>
                    GetShiftHours(shiftsById[assignment.ShiftId])));

        var overTargetResourceIds = scenario.ResourceWorkloadDemands
            .Where(demand =>
            {
                var assignedHours = assignedHoursByResourceId.GetValueOrDefault(
                    demand.ResourceId,
                    0.0);

                return assignedHours > demand.EffectiveTargetHours + HoursTolerance;
            })
            .Select(demand => demand.ResourceId)
            .ToHashSet();

        var underTargetResourceIds = scenario.ResourceWorkloadDemands
            .Where(demand =>
            {
                var assignedHours = assignedHoursByResourceId.GetValueOrDefault(
                    demand.ResourceId,
                    0.0);

                return assignedHours + HoursTolerance < demand.EffectiveTargetHours;
            })
            .Select(demand => demand.ResourceId)
            .ToArray();

        var candidateTransferMoveCount = 0;
        var rejectedAlreadyAssignedToShift = 0;
        var rejectedUnavailable = 0;
        var rejectedMissingRequiredPreference = 0;
        var rejectedOverlap = 0;
        var rejectedHardViolation = 0;
        var rejectedNotImproving = 0;
        var improvingTransferMoveCount = 0;

        var rejectedHardViolationByType = new Dictionary<ConstraintViolationType, int>();

        ScheduleEvaluationResult? bestTransferEvaluation = null;
        WorkloadTransferMove? bestTransferMove = null;

        for (var assignmentIndex = 0; assignmentIndex < assignments.Length; assignmentIndex++)
        {
            var assignment = assignments[assignmentIndex];

            if (!overTargetResourceIds.Contains(assignment.ResourceId))
            {
                continue;
            }

            var shift = shiftsById[assignment.ShiftId];

            foreach (var targetResourceId in underTargetResourceIds)
            {
                if (targetResourceId == assignment.ResourceId)
                {
                    continue;
                }

                candidateTransferMoveCount++;

                if (assignments.Any(existingAssignment =>
                        existingAssignment.ResourceId == targetResourceId &&
                        existingAssignment.ShiftId == assignment.ShiftId))
                {
                    rejectedAlreadyAssignedToShift++;
                    continue;
                }

                if (!IsAvailableForShift(
                        scenario,
                        targetResourceId,
                        shift))
                {
                    rejectedUnavailable++;
                    continue;
                }

                if (shift.RequiresPreferenceToAssign &&
                    !HasPreferPreferenceForShift(
                        scenario,
                        targetResourceId,
                        shift))
                {
                    rejectedMissingRequiredPreference++;
                    continue;
                }

                if (HasOverlappingAssignment(
                        assignments,
                        shiftsById,
                        targetResourceId,
                        shift))
                {
                    rejectedOverlap++;
                    continue;
                }

                var transferCandidate = CreateTransferCandidate(
                    assignments,
                    assignmentIndex,
                    targetResourceId);

                var transferEvaluation = evaluator.Evaluate(
                    scenario.Problem,
                    transferCandidate);

                if (transferEvaluation.Score.HardViolationCount > 0)
                {
                    rejectedHardViolation++;

                    foreach (var violation in transferEvaluation.Violations
                                 .Where(violation => violation.Severity == ConstraintViolationSeverity.Hard))
                    {
                        rejectedHardViolationByType.TryGetValue(
                            violation.Type,
                            out var currentCount);

                        rejectedHardViolationByType[violation.Type] = currentCount + 1;
                    }

                    continue;
                }

                if (!ranker.IsBetterThan(
                        transferEvaluation,
                        baseResult.Evaluation))
                {
                    rejectedNotImproving++;
                    continue;
                }

                improvingTransferMoveCount++;

                if (bestTransferEvaluation is not null &&
                    !ranker.IsBetterThan(
                        transferEvaluation,
                        bestTransferEvaluation))
                {
                    continue;
                }

                bestTransferEvaluation = transferEvaluation;
                bestTransferMove = new WorkloadTransferMove(
                    FromResourceName: GetResourceName(scenario, assignment.ResourceId),
                    ToResourceName: resourcesById[targetResourceId].Name,
                    ShiftStartUtc: shift.StartUtc,
                    ShiftEndUtc: shift.EndUtc,
                    ShiftKind: shift.Kind,
                    NightShiftCategory: shift.NightShiftCategory,
                    TotalPenalty: transferEvaluation.Score.TotalPenalty,
                    PenaltyDelta: baseResult.Evaluation.Score.TotalPenalty -
                                  transferEvaluation.Score.TotalPenalty);
            }
        }

        var baseMetrics = CalculateTargetGapMetrics(
            scenario,
            baseResult.Candidate);

        return new WorkloadTransferDiagnostics(
            BaseMetrics: baseMetrics,
            OverTargetResourceCount: overTargetResourceIds.Count,
            UnderTargetResourceCount: underTargetResourceIds.Length,
            CandidateTransferMoveCount: candidateTransferMoveCount,
            RejectedAlreadyAssignedToShift: rejectedAlreadyAssignedToShift,
            RejectedUnavailable: rejectedUnavailable,
            RejectedMissingRequiredPreference: rejectedMissingRequiredPreference,
            RejectedOverlap: rejectedOverlap,
            RejectedHardViolation: rejectedHardViolation,
            RejectedNotImproving: rejectedNotImproving,
            ImprovingTransferMoveCount: improvingTransferMoveCount,
            RejectedHardViolationByType: rejectedHardViolationByType
                .OrderBy(item => item.Key.ToString())
                .ToDictionary(
                    item => item.Key,
                    item => item.Value),
            BestTransferMove: bestTransferMove);
    }

    private static string FormatWorkloadTransferDiagnosticsReport(
        ExperimentRun repair,
        WorkloadTransferDiagnostics diagnostics)
    {
        var builder = new StringBuilder();

        builder.AppendLine("Stage 7.5C Workload Transfer Diagnostics");
        builder.AppendLine();
        builder.AppendLine("Scope:");
        builder.AppendLine("- Mode=RepairAssisted");
        builder.AppendLine("- Seed=20260605");
        builder.AppendLine("- DiagnosticOnly=True");
        builder.AppendLine("- MutationAdded=True");
        builder.AppendLine("- MutationName=TryCreateUnderTargetAddCandidate");
        builder.AppendLine();

        builder.AppendLine("Base RepairAssisted:");
        builder.AppendLine($"TotalPenalty={repair.Result.Evaluation.Score.TotalPenalty}");
        builder.AppendLine($"HardViolationCount={repair.Result.Evaluation.Score.HardViolationCount}");
        builder.AppendLine($"SoftViolationCount={repair.Result.Evaluation.Score.SoftViolationCount}");
        builder.AppendLine($"Assignments={repair.Result.Candidate.Assignments.Count}");
        builder.AppendLine($"TotalAbsoluteTargetGapHours={diagnostics.BaseMetrics.TotalAbsoluteTargetGapHours:0.##}");
        builder.AppendLine($"TotalOverTargetHours={diagnostics.BaseMetrics.TotalOverTargetHours:0.##}");
        builder.AppendLine($"TotalUnderTargetHours={diagnostics.BaseMetrics.TotalUnderTargetHours:0.##}");
        builder.AppendLine($"AverageOverTargetHours={diagnostics.BaseMetrics.AverageOverTargetHours:0.##}");
        builder.AppendLine($"MaxOverTargetHours={diagnostics.BaseMetrics.MaxOverTargetHours:0.##}");
        builder.AppendLine($"MaxUnderTargetHours={diagnostics.BaseMetrics.MaxUnderTargetHours:0.##}");
        builder.AppendLine($"OverTargetResourceCount={diagnostics.OverTargetResourceCount}");
        builder.AppendLine($"UnderTargetResourceCount={diagnostics.UnderTargetResourceCount}");
        builder.AppendLine();

        builder.AppendLine("Transfer Diagnostics:");
        builder.AppendLine($"CandidateTransferMoveCount={diagnostics.CandidateTransferMoveCount}");
        builder.AppendLine($"RejectedAlreadyAssignedToShift={diagnostics.RejectedAlreadyAssignedToShift}");
        builder.AppendLine($"RejectedUnavailable={diagnostics.RejectedUnavailable}");
        builder.AppendLine($"RejectedMissingRequiredPreference={diagnostics.RejectedMissingRequiredPreference}");
        builder.AppendLine($"RejectedOverlap={diagnostics.RejectedOverlap}");
        builder.AppendLine($"RejectedHardViolation={diagnostics.RejectedHardViolation}");
        builder.AppendLine($"RejectedNotImproving={diagnostics.RejectedNotImproving}");
        builder.AppendLine($"ImprovingTransferMoveCount={diagnostics.ImprovingTransferMoveCount}");

        AppendRejectedHardViolationBreakdown(
            builder,
            diagnostics);

        AppendBestTransferMove(
            builder,
            diagnostics);

        return builder.ToString();
    }

    private static void AppendRejectedHardViolationBreakdown(
        StringBuilder builder,
        WorkloadTransferDiagnostics diagnostics)
    {
        builder.AppendLine("RejectedHardViolationByType:");

        if (diagnostics.RejectedHardViolationByType.Count == 0)
        {
            builder.AppendLine("- none");
            return;
        }

        foreach (var item in diagnostics.RejectedHardViolationByType)
        {
            builder.AppendLine($"- {item.Key}: {item.Value}");
        }
    }

    private static void AppendBestTransferMove(
        StringBuilder builder,
        WorkloadTransferDiagnostics diagnostics)
    {
        builder.AppendLine("BestTransferMove:");

        if (diagnostics.BestTransferMove is null)
        {
            builder.AppendLine("- none");
            return;
        }

        var move = diagnostics.BestTransferMove;

        builder.AppendLine($"BestTransferDelta={move.PenaltyDelta}");
        builder.AppendLine($"BestTransferTotalPenalty={move.TotalPenalty}");
        builder.AppendLine($"BestTransferFromResource={move.FromResourceName}");
        builder.AppendLine($"BestTransferToResource={move.ToResourceName}");
        builder.AppendLine($"BestTransferShiftStartUtc={move.ShiftStartUtc:o}");
        builder.AppendLine($"BestTransferShiftEndUtc={move.ShiftEndUtc:o}");
        builder.AppendLine($"BestTransferShiftKind={move.ShiftKind}");
        builder.AppendLine($"BestTransferNightShiftCategory={move.NightShiftCategory?.ToString() ?? "None"}");
    }

    private static ScheduleCandidate CreateTransferCandidate(
        IReadOnlyList<Assignment> assignments,
        int assignmentIndex,
        Guid targetResourceId)
    {
        var mutatedAssignments = assignments.ToList();
        var sourceAssignment = mutatedAssignments[assignmentIndex];

        mutatedAssignments[assignmentIndex] = new Assignment(
            targetResourceId,
            sourceAssignment.ShiftId);

        return new ScheduleCandidate(mutatedAssignments);
    }

    private static bool IsAvailableForShift(
        ExperimentScenario scenario,
        Guid resourceId,
        Shift shift)
    {
        return scenario.AvailabilityWindows.Any(window =>
            window.ResourceId == resourceId &&
            window.Covers(shift));
    }

    private static bool HasPreferPreferenceForShift(
        ExperimentScenario scenario,
        Guid resourceId,
        Shift shift)
    {
        return scenario.ResourcePreferences
            .Where(preference => preference.ResourceId == resourceId)
            .Where(preference => preference.Type == ResourcePreferenceType.Prefer)
            .Any(preference => Overlaps(
                preference.StartUtc,
                preference.EndUtc,
                shift.StartUtc,
                shift.EndUtc));
    }

    private static bool HasOverlappingAssignment(
        IReadOnlyCollection<Assignment> assignments,
        IReadOnlyDictionary<Guid, Shift> shiftsById,
        Guid resourceId,
        Shift shift)
    {
        return assignments
            .Where(assignment => assignment.ResourceId == resourceId)
            .Where(assignment => shiftsById.ContainsKey(assignment.ShiftId))
            .Select(assignment => shiftsById[assignment.ShiftId])
            .Any(existingShift => Overlaps(
                existingShift.StartUtc,
                existingShift.EndUtc,
                shift.StartUtc,
                shift.EndUtc));
    }

    private sealed record WorkloadTransferDiagnostics(
        TargetGapMetrics BaseMetrics,
        int OverTargetResourceCount,
        int UnderTargetResourceCount,
        int CandidateTransferMoveCount,
        int RejectedAlreadyAssignedToShift,
        int RejectedUnavailable,
        int RejectedMissingRequiredPreference,
        int RejectedOverlap,
        int RejectedHardViolation,
        int RejectedNotImproving,
        int ImprovingTransferMoveCount,
        IReadOnlyDictionary<ConstraintViolationType, int> RejectedHardViolationByType,
        WorkloadTransferMove? BestTransferMove);

    private sealed record WorkloadTransferMove(
        string FromResourceName,
        string ToResourceName,
        DateTime ShiftStartUtc,
        DateTime ShiftEndUtc,
        ShiftKind ShiftKind,
        NightShiftCategory? NightShiftCategory,
        int TotalPenalty,
        int PenaltyDelta);

    private static void AssertWeekdayCapacity(Shift shift)
    {
        if (shift.Kind == ShiftKind.Morning)
        {
            Assert.Equal(3, shift.MinResourceCount);
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
        Assert.Equal(1, shift.MinResourceCount);
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
        var resources = CreateResources(ResourceCount);
        var shifts = CreateBiWeeklyShifts();

        var availabilityWindows = new List<AvailabilityWindow>();
        var preferences = new List<ResourcePreference>();

        AddDaytimeSubmissions(
            resources,
            shifts,
            availabilityWindows,
            preferences);

        AddNightSubmissions(
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

    private static ExperimentScenario CreateScenarioWithAssignedHoursBalanceTolerance(
        double balanceToleranceHours)
    {
        var scenario = CreateScenario();

        var problem = new SchedulingProblem(
            period: scenario.Problem.Period,
            resources: scenario.Resources,
            shifts: scenario.Shifts,
            availabilityWindows: scenario.AvailabilityWindows,
            resourcePreferences: scenario.ResourcePreferences,
            minimumAssignedHoursPerResource: scenario.Problem.MinimumAssignedHoursPerResource,
            minimumMorningShiftsPerResourcePerFullWeek: scenario.Problem.MinimumMorningShiftsPerResourcePerFullWeek,
            minimumAfternoonShiftsPerResourcePerFullWeek: scenario.Problem.MinimumAfternoonShiftsPerResourcePerFullWeek,
            resourceMonthlyNightShiftHistories: scenario.Problem.ResourceMonthlyNightShiftHistories,
            maximumAssignedHoursDeviationFromAverageHours: balanceToleranceHours,
            resourceWorkloadDemands: scenario.ResourceWorkloadDemands);

        return new ExperimentScenario(
            Problem: problem,
            Resources: scenario.Resources,
            Shifts: scenario.Shifts,
            AvailabilityWindows: scenario.AvailabilityWindows,
            ResourcePreferences: scenario.ResourcePreferences,
            ResourceWorkloadDemands: scenario.ResourceWorkloadDemands);
    }


    private static ExperimentScenario CreateIndividualAvailabilityShortageScenario()
    {
        var resources = CreateResources(ResourceCount);
        var shifts = CreateBiWeeklyShifts(RequiresPreferenceForIndividualShortageShift);

        var availabilityWindows = new List<AvailabilityWindow>();
        var preferences = new List<ResourcePreference>();

        AddIndividualAvailabilityShortageProfiles(
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

    private static ExperimentScenario CreateIndividualAvailabilityShortageWithSequencePressureScenario()
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
        AddIndividualShortageProfile(
            resources[0],
            shifts,
            availabilityWindows,
            preferences,
            prefer: shift =>
                IsWeekdayMorningOn(shift, DayOfWeek.Sunday, DayOfWeek.Thursday) ||
                IsWeekdayAfternoonOn(shift, DayOfWeek.Monday) ||
                IsRegularNightOn(shift, DayOfWeek.Sunday) ||
                IsMotzeiShabbatNight(shift),
            avoid: IsWeekdayMorning);

        AddIndividualShortageProfile(
            resources[1],
            shifts,
            availabilityWindows,
            preferences,
            prefer: shift =>
                IsWeekdayAfternoonOn(shift, DayOfWeek.Sunday, DayOfWeek.Tuesday) ||
                IsRegularNightOn(shift, DayOfWeek.Monday) ||
                IsMotzeiShabbatNight(shift),
            avoid: IsWeekdayMorning);

        AddIndividualShortageProfile(
            resources[2],
            shifts,
            availabilityWindows,
            preferences,
            prefer: shift =>
                IsWeekdayAfternoonOn(shift, DayOfWeek.Sunday, DayOfWeek.Wednesday) ||
                IsRegularNightOn(shift, DayOfWeek.Sunday, DayOfWeek.Tuesday) ||
                IsMotzeiShabbatNight(shift),
            avoid: IsWeekdayMorning);

        AddIndividualShortageProfile(
            resources[3],
            shifts,
            availabilityWindows,
            preferences,
            prefer: shift =>
                IsWeekdayAfternoonOn(shift, DayOfWeek.Monday, DayOfWeek.Thursday) ||
                IsRegularNightOn(shift, DayOfWeek.Monday, DayOfWeek.Wednesday) ||
                IsFridayMorning(shift) ||
                IsMotzeiShabbatNight(shift),
            avoid: IsWeekdayMorning);

        AddIndividualShortageProfile(
            resources[4],
            shifts,
            availabilityWindows,
            preferences,
            prefer: shift =>
                IsWeekdayMorningOn(shift, DayOfWeek.Monday) ||
                IsWeekdayAfternoonOn(shift, DayOfWeek.Tuesday) ||
                IsRegularNightOn(shift, DayOfWeek.Tuesday, DayOfWeek.Thursday) ||
                IsFridayAfternoon(shift) ||
                IsMotzeiShabbatNight(shift),
            avoid: IsWeekdayMorning);

        AddIndividualShortageProfile(
            resources[5],
            shifts,
            availabilityWindows,
            preferences,
            prefer: shift =>
                IsWeekdayAfternoonOn(shift, DayOfWeek.Wednesday) ||
                IsRegularNightOn(shift, DayOfWeek.Sunday, DayOfWeek.Wednesday) ||
                IsSaturdayMorning(shift) ||
                IsMotzeiShabbatNight(shift),
            avoid: IsWeekdayMorning);

        AddIndividualShortageProfile(
            resources[6],
            shifts,
            availabilityWindows,
            preferences,
            prefer: shift =>
                IsWeekdayAfternoonOn(shift, DayOfWeek.Thursday) ||
                IsRegularNightOn(shift, DayOfWeek.Monday, DayOfWeek.Thursday) ||
                IsSaturdayAfternoon(shift) ||
                IsMotzeiShabbatNight(shift),
            avoid: IsWeekdayMorning);

        AddIndividualShortageProfile(
            resources[7],
            shifts,
            availabilityWindows,
            preferences,
            prefer: shift =>
                IsWeekdayAfternoonOn(shift, DayOfWeek.Sunday) ||
                IsRegularNightOn(shift, DayOfWeek.Tuesday) ||
                IsMotzeiShabbatNight(shift),
            avoid: IsWeekdayMorning);

        AddIndividualShortageProfile(
            resources[8],
            shifts,
            availabilityWindows,
            preferences,
            prefer: shift =>
                IsWeekdayMorningOn(shift, DayOfWeek.Tuesday) ||
                IsWeekdayAfternoonOn(shift, DayOfWeek.Monday) ||
                IsRegularNightOn(shift, DayOfWeek.Wednesday),
            avoid: IsWeekdayMorning);

        AddIndividualShortageProfile(
            resources[9],
            shifts,
            availabilityWindows,
            preferences,
            prefer: shift =>
                IsWeekdayAfternoonOn(shift, DayOfWeek.Tuesday) ||
                IsRegularNightOn(shift, DayOfWeek.Sunday, DayOfWeek.Thursday) ||
                IsFridayMorning(shift),
            avoid: IsWeekdayMorning);

        AddIndividualShortageProfile(
            resources[10],
            shifts,
            availabilityWindows,
            preferences,
            prefer: shift =>
                IsWeekdayAfternoonOn(shift, DayOfWeek.Wednesday) ||
                IsRegularNightOn(shift, DayOfWeek.Monday) ||
                IsFridayNight(shift),
            avoid: IsWeekdayMorning);

        AddIndividualShortageProfile(
            resources[11],
            shifts,
            availabilityWindows,
            preferences,
            prefer: shift =>
                IsWeekdayAfternoonOn(shift, DayOfWeek.Sunday, DayOfWeek.Thursday) ||
                IsRegularNightOn(shift, DayOfWeek.Tuesday),
            avoid: IsWeekdayMorning);

        AddIndividualShortageProfile(
            resources[12],
            shifts,
            availabilityWindows,
            preferences,
            prefer: shift =>
                IsWeekdayMorningOn(shift, DayOfWeek.Wednesday) ||
                IsWeekdayAfternoonOn(shift, DayOfWeek.Monday) ||
                IsRegularNightOn(shift, DayOfWeek.Wednesday),
            avoid: IsWeekdayMorning);

        AddIndividualShortageProfile(
            resources[13],
            shifts,
            availabilityWindows,
            preferences,
            prefer: shift =>
                IsWeekdayAfternoonOn(shift, DayOfWeek.Tuesday) ||
                IsRegularNightOn(shift, DayOfWeek.Thursday),
            avoid: IsWeekdayMorning);

        AddIndividualShortageProfile(
            resources[14],
            shifts,
            availabilityWindows,
            preferences,
            prefer: shift =>
                IsWeekdayAfternoonOn(shift, DayOfWeek.Wednesday) ||
                IsFridayNight(shift),
            avoid: IsWeekdayMorning);

        AddIndividualShortageProfile(
            resources[15],
            shifts,
            availabilityWindows,
            preferences,
            prefer: shift =>
                IsWeekdayAfternoonOn(shift, DayOfWeek.Thursday),
            avoid: IsWeekdayMorning);
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
            AddPrefer(
                resource,
                shift,
                availabilityWindows,
                preferences);
        }

        foreach (var shift in shifts.Where(avoid))
        {
            if (HasAnyPreferenceForShift(
                    resource,
                    shift,
                    preferences))
            {
                continue;
            }

            AddAvoid(
                resource,
                shift,
                availabilityWindows,
                preferences);
        }
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

    private static IReadOnlyList<Resource> CreateResources(int count)
    {
        return Enumerable
            .Range(1, count)
            .Select(index => CreateResource($"Guard{index:00}"))
            .ToArray();
    }

    private static Resource CreateResource(string name)
    {
        return new Resource(
            Guid.NewGuid(),
            name,
            hourlyCost: 100m);
    }

    private static IReadOnlyList<Shift> CreateBiWeeklyShifts(
        Func<DateOnly, ShiftKind, bool>? requiresPreferenceToAssign = null)
    {
        requiresPreferenceToAssign ??= (_, _) => true;

        var shifts = new List<Shift>();

        for (var dayOffset = 0; dayOffset < DaysInSchedule; dayOffset++)
        {
            var date = new DateOnly(2026, 5, 31).AddDays(dayOffset);

            shifts.Add(CreateShift(
                date,
                ShiftKind.Morning,
                requiresPreferenceToAssign(date, ShiftKind.Morning)));

            shifts.Add(CreateShift(
                date,
                ShiftKind.Afternoon,
                requiresPreferenceToAssign(date, ShiftKind.Afternoon)));

            shifts.Add(CreateShift(
                date,
                ShiftKind.Night,
                requiresPreferenceToAssign(date, ShiftKind.Night)));
        }

        return shifts
            .OrderBy(shift => shift.StartUtc)
            .ToArray();
    }

    private static IReadOnlyList<Shift> CreateBiWeeklySequencePressureShifts(
        Func<DateOnly, ShiftKind, bool>? requiresPreferenceToAssign = null)
    {
        requiresPreferenceToAssign ??= (_, _) => true;

        var shifts = new List<Shift>();

        for (var dayOffset = 0; dayOffset < DaysInSchedule; dayOffset++)
        {
            var date = new DateOnly(2026, 5, 31).AddDays(dayOffset);

            shifts.Add(CreateSequencePressureShift(
                date,
                ShiftKind.Morning,
                requiresPreferenceToAssign(date, ShiftKind.Morning)));

            shifts.Add(CreateSequencePressureShift(
                date,
                ShiftKind.Afternoon,
                requiresPreferenceToAssign(date, ShiftKind.Afternoon)));

            shifts.Add(CreateSequencePressureShift(
                date,
                ShiftKind.Night,
                requiresPreferenceToAssign(date, ShiftKind.Night)));
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


    private static Shift CreateShift(
        DateOnly date,
        ShiftKind kind,
        bool requiresPreferenceToAssign)
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
            requiresPreferenceToAssign: requiresPreferenceToAssign,
            requiresMinimumWhenPreferenceExists: false,
            nightShiftCategory: GetNightShiftCategory(date, kind));
    }

    private static void AddDaytimeSubmissions(
        IReadOnlyList<Resource> resources,
        IReadOnlyList<Shift> shifts,
        ICollection<AvailabilityWindow> availabilityWindows,
        ICollection<ResourcePreference> preferences)
    {
        for (var resourceIndex = 0; resourceIndex < resources.Count; resourceIndex++)
        {
            foreach (var shift in shifts)
            {
                if (!ShouldSubmitDaytimeShift(resourceIndex, shift))
                {
                    continue;
                }

                AddSubmission(
                    resources[resourceIndex],
                    shift,
                    availabilityWindows,
                    preferences);
            }
        }
    }

    private static void AddNightSubmissions(
        IReadOnlyList<Resource> resources,
        IReadOnlyList<Shift> shifts,
        ICollection<AvailabilityWindow> availabilityWindows,
        ICollection<ResourcePreference> preferences)
    {
        var regularNightIndex = 0;
        var fridayNightIndex = 0;

        foreach (var shift in shifts
                     .Where(shift => shift.Kind == ShiftKind.Night)
                     .OrderBy(shift => shift.StartUtc))
        {
            if (shift.NightShiftCategory == NightShiftCategory.Regular)
            {
                AddRegularNightSubmissionPool(
                    resources,
                    shift,
                    regularNightIndex,
                    availabilityWindows,
                    preferences);

                regularNightIndex++;
                continue;
            }

            if (shift.NightShiftCategory == NightShiftCategory.FridayNight)
            {
                AddFridayNightSubmissionPool(
                    resources,
                    shift,
                    fridayNightIndex,
                    availabilityWindows,
                    preferences);

                fridayNightIndex++;
                continue;
            }

            if (shift.NightShiftCategory == NightShiftCategory.MotzeiShabbatNight)
            {
                AddMotzeiShabbatNightSubmissionPool(
                    resources,
                    shift,
                    availabilityWindows,
                    preferences);
            }
        }
    }

    private static void AddRegularNightSubmissionPool(
        IReadOnlyList<Resource> resources,
        Shift shift,
        int regularNightIndex,
        ICollection<AvailabilityWindow> availabilityWindows,
        ICollection<ResourcePreference> preferences)
    {
        AddSubmissionPool(
            resources,
            shift,
            availabilityWindows,
            preferences,
            new[]
            {
                regularNightIndex % resources.Count,
                (regularNightIndex + 1) % resources.Count,
                (regularNightIndex + 5) % resources.Count,
                (regularNightIndex + 10) % resources.Count
            });
    }

    private static void AddFridayNightSubmissionPool(
        IReadOnlyList<Resource> resources,
        Shift shift,
        int fridayNightIndex,
        ICollection<AvailabilityWindow> availabilityWindows,
        ICollection<ResourcePreference> preferences)
    {
        var baseIndex = 10 + fridayNightIndex;

        AddSubmissionPool(
            resources,
            shift,
            availabilityWindows,
            preferences,
            new[]
            {
                baseIndex % resources.Count,
                (baseIndex + 2) % resources.Count
            });
    }

    private static void AddMotzeiShabbatNightSubmissionPool(
        IReadOnlyList<Resource> resources,
        Shift shift,
        ICollection<AvailabilityWindow> availabilityWindows,
        ICollection<ResourcePreference> preferences)
    {
        AddSubmissionPool(
            resources,
            shift,
            availabilityWindows,
            preferences,
            Enumerable.Range(0, 8));
    }

    private static void AddSubmissionPool(
        IReadOnlyList<Resource> resources,
        Shift shift,
        ICollection<AvailabilityWindow> availabilityWindows,
        ICollection<ResourcePreference> preferences,
        IEnumerable<int> zeroBasedResourceIndexes)
    {
        foreach (var resourceIndex in zeroBasedResourceIndexes.Distinct())
        {
            AddSubmission(
                resources[resourceIndex],
                shift,
                availabilityWindows,
                preferences);
        }
    }

    private static bool ShouldSubmitDaytimeShift(
        int resourceIndex,
        Shift shift)
    {
        if (shift.Kind is not ShiftKind.Morning and not ShiftKind.Afternoon)
        {
            return false;
        }

        var resourceNumber = resourceIndex + 1;
        var date = DateOnly.FromDateTime(shift.StartUtc);

        if (date.DayOfWeek == DayOfWeek.Friday)
        {
            return ShouldSubmitFridayDaytime(resourceNumber, shift);
        }

        if (date.DayOfWeek == DayOfWeek.Saturday)
        {
            return ShouldSubmitSaturdayDaytime(resourceNumber, shift);
        }

        return ShouldSubmitWeekdayDaytime(resourceNumber, shift);
    }

    private static bool ShouldSubmitWeekdayDaytime(
        int resourceNumber,
        Shift shift)
    {
        var day = shift.StartUtc.Day;

        if (shift.Kind == ShiftKind.Morning)
        {
            return (resourceNumber + day) % 4 != 0;
        }

        return (resourceNumber + day + 2) % 5 != 0;
    }

    private static bool ShouldSubmitFridayDaytime(
        int resourceNumber,
        Shift shift)
    {
        var day = shift.StartUtc.Day;

        if (shift.Kind == ShiftKind.Morning)
        {
            return (resourceNumber + day) % 4 == 0 ||
                   (resourceNumber + day) % 4 == 1;
        }

        return (resourceNumber + day) % 5 == 0 ||
               (resourceNumber + day) % 5 == 1;
    }

    private static bool ShouldSubmitSaturdayDaytime(
        int resourceNumber,
        Shift shift)
    {
        var day = shift.StartUtc.Day;

        if (shift.Kind == ShiftKind.Morning)
        {
            return (resourceNumber + day) % 6 == 0 ||
                   (resourceNumber + day) % 6 == 1;
        }

        return (resourceNumber + day) % 7 == 0 ||
               (resourceNumber + day) % 7 == 1;
    }

    private static void AddSubmission(
        Resource resource,
        Shift shift,
        ICollection<AvailabilityWindow> availabilityWindows,
        ICollection<ResourcePreference> preferences)
    {
        AddPrefer(
            resource,
            shift,
            availabilityWindows,
            preferences);
    }

    private static void AddPrefer(
        Resource resource,
        Shift shift,
        ICollection<AvailabilityWindow> availabilityWindows,
        ICollection<ResourcePreference> preferences)
    {
        AddAvailabilityIfMissing(
            resource,
            shift,
            availabilityWindows);

        AddPreferenceIfMissing(
            resource,
            shift,
            ResourcePreferenceType.Prefer,
            preferences);
    }

    private static void AddAvoid(
        Resource resource,
        Shift shift,
        ICollection<AvailabilityWindow> availabilityWindows,
        ICollection<ResourcePreference> preferences)
    {
        AddAvailabilityIfMissing(
            resource,
            shift,
            availabilityWindows);

        AddPreferenceIfMissing(
            resource,
            shift,
            ResourcePreferenceType.Avoid,
            preferences);
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

    private static DateTime GetSequencePressureStartUtc(
        DateOnly date,
        ShiftKind kind)
    {
        return kind switch
        {
            ShiftKind.Morning => date.ToDateTime(
                new TimeOnly(6, 30),
                DateTimeKind.Utc),

            ShiftKind.Afternoon => date.ToDateTime(
                new TimeOnly(14, 20),
                DateTimeKind.Utc),

            ShiftKind.Night => date.ToDateTime(
                new TimeOnly(22, 40),
                DateTimeKind.Utc),

            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private static DateTime GetSequencePressureEndUtc(
        DateOnly date,
        ShiftKind kind)
    {
        return kind switch
        {
            ShiftKind.Morning => date.ToDateTime(
                new TimeOnly(14, 20),
                DateTimeKind.Utc),

            ShiftKind.Afternoon => date.ToDateTime(
                new TimeOnly(22, 40),
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
        return CountResourcesWithPreferenceForShift(
            scenario,
            shift,
            ResourcePreferenceType.Prefer);
    }

    private static int CountAvoidingResourcesForShift(
        ExperimentScenario scenario,
        Shift shift)
    {
        return CountResourcesWithPreferenceForShift(
            scenario,
            shift,
            ResourcePreferenceType.Avoid);
    }

    private static int CountResourcesWithPreferenceForShift(
        ExperimentScenario scenario,
        Shift shift,
        ResourcePreferenceType preferenceType)
    {
        return scenario.ResourcePreferences
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

    private static void AppendMultiSeedRunSummary(
        StringBuilder builder,
        ExperimentScenario scenario,
        int seed,
        ExperimentRun clean,
        ExperimentRun repair,
        TargetGapMetrics cleanMetrics,
        TargetGapMetrics repairMetrics)
    {
        builder.AppendLine($"Seed {seed}:");

        AppendCompactOptimizerSummary(
            builder,
            scenario,
            clean,
            cleanMetrics);

        AppendCompactOptimizerSummary(
            builder,
            scenario,
            repair,
            repairMetrics);

        builder.AppendLine(
            $"RepairPenaltyDeltaAgainstClean: {clean.Result.Evaluation.Score.TotalPenalty - repair.Result.Evaluation.Score.TotalPenalty}");
        builder.AppendLine();
    }

    private static void AppendCompactOptimizerSummary(
        StringBuilder builder,
        ExperimentScenario scenario,
        ExperimentRun run,
        TargetGapMetrics metrics)
    {
        builder.AppendLine(
            $"- {run.Name}: " +
            $"TotalPenalty={run.Result.Evaluation.Score.TotalPenalty}, " +
            $"HardViolationCount={run.Result.Evaluation.Score.HardViolationCount}, " +
            $"SoftViolationCount={run.Result.Evaluation.Score.SoftViolationCount}, " +
            $"Assignments={run.Result.Candidate.Assignments.Count}, " +
            $"TotalAbsoluteTargetGapHours={metrics.TotalAbsoluteTargetGapHours:0.##}, " +
            $"TotalOverTargetHours={metrics.TotalOverTargetHours:0.##}, " +
            $"TotalUnderTargetHours={metrics.TotalUnderTargetHours:0.##}, " +
            $"AverageOverTargetHours={metrics.AverageOverTargetHours:0.##}, " +
            $"MaxOverTargetHours={metrics.MaxOverTargetHours:0.##}, " +
            $"MaxUnderTargetHours={metrics.MaxUnderTargetHours:0.##}, " +
            $"UnsatisfiedMotzeiShabbatPreferenceRequestCount={CountUnsatisfiedMotzeiShabbatPreferenceRequests(scenario, run.Result.Candidate)}, " +
            $"FirstPenalty={run.Diagnostics[0].BestSoFarTotalPenalty}, " +
            $"LastPenalty={run.Diagnostics[^1].BestSoFarTotalPenalty}");
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


    private static string FormatCleanRealScenarioScoringBaselineReport(
        ExperimentScenario scenario,
        ExperimentRun clean)
    {
        var builder = new StringBuilder();

        builder.AppendLine("Stage 7.6A Real Scenario Scoring Baseline");
        builder.AppendLine("Mode: Clean GA");
        builder.AppendLine($"Seed: {GeneticSeed}");
        builder.AppendLine($"PopulationSize: {GeneticPopulationSize}");
        builder.AppendLine($"GenerationCount: {CleanGenerationCount}");
        builder.AppendLine();

        AppendScenarioCapacitySummary(builder, scenario);
        builder.AppendLine();

        AppendRunSummary(builder, scenario, clean);
        builder.AppendLine();

        AppendPenaltyBreakdownByType(builder, clean.Result.Evaluation);
        builder.AppendLine();

        AppendRequestedPreferredFulfillmentSummary(builder, scenario, clean.Result.Candidate);
        builder.AppendLine();

        AppendGenerationImprovementSummary(builder, clean);

        return builder.ToString();
    }


    private static string FormatIndividualAvailabilityShortageScenarioReport(
        ExperimentScenario scenario,
        ScheduleScoringWeights weights,
        ExperimentRun clean)
    {
        var builder = new StringBuilder();

        builder.AppendLine("Stage 7.6D.1 Realistic Individual Availability Shortage Scenario");
        builder.AppendLine("Mode: Clean GA");
        builder.AppendLine("Variant: Variant C");
        builder.AppendLine($"Seed: {GeneticSeed}");
        builder.AppendLine($"PopulationSize: {GeneticPopulationSize}");
        builder.AppendLine($"GenerationCount: {CleanGenerationCount}");
        builder.AppendLine("Purpose: Manual diagnostic only. Not part of regular verification.");
        builder.AppendLine();

        AppendScenarioCapacitySummary(builder, scenario);
        builder.AppendLine();

        AppendScoringWeightSummary(builder, weights);
        AppendNetTradeoffPerPreferred8HourAssignment(builder, weights);
        builder.AppendLine();

        AppendAvailabilityPressureSummary(builder, scenario);
        builder.AppendLine();

        AppendMorningShiftScarcitySummary(builder, scenario);
        builder.AppendLine();

        AppendWeekendScarcitySummary(builder, scenario);
        builder.AppendLine();

        AppendCompactScoringSensitivityMetrics(builder, scenario, clean);
        builder.AppendLine();

        AppendPenaltyBreakdownByType(
            builder,
            clean.Result.Evaluation,
            weights);
        builder.AppendLine();

        AppendRequestedPreferredFulfillmentSummary(
            builder,
            scenario,
            clean.Result.Candidate);
        builder.AppendLine();

        AppendTargetGapSummary(builder, scenario, clean);
        builder.AppendLine();

        AppendMorningAssignmentsByResource(
            builder,
            scenario,
            clean.Result.Candidate);
        builder.AppendLine();

        AppendAvoidedMorningAssignmentsByResource(
            builder,
            scenario,
            clean.Result.Candidate);
        builder.AppendLine();

        AppendGenerationImprovementSummary(builder, clean);
        AppendGenerationDiagnostics(builder, clean);

        return builder.ToString();
    }

    private static string FormatIndividualAvailabilityShortageWithSequencePressureReport(
        ExperimentScenario scenario,
        ScheduleScoringWeights weights,
        ExperimentRun clean)
    {
        var builder = new StringBuilder();

        builder.AppendLine("Stage 7.6D.3 Individual Availability Shortage With Sequence Pressure Diagnostic");
        builder.AppendLine("Mode: Clean GA");
        builder.AppendLine("Variant: Variant C");
        builder.AppendLine($"Seed: {GeneticSeed}");
        builder.AppendLine($"PopulationSize: {GeneticPopulationSize}");
        builder.AppendLine($"GenerationCount: {CleanGenerationCount}");
        builder.AppendLine("Purpose: Manual diagnostic only. Not part of regular verification.");
        builder.AppendLine();

        AppendScenarioCapacitySummary(builder, scenario);
        builder.AppendLine();

        AppendScoringWeightSummary(builder, weights);
        AppendNetTradeoffPerPreferred8HourAssignment(builder, weights);
        builder.AppendLine();

        AppendMorningShiftScarcitySummary(builder, scenario);
        builder.AppendLine();

        AppendSequenceTemplatePressureSummary(builder, scenario);
        builder.AppendLine();

        AppendCompactScoringSensitivityMetrics(builder, scenario, clean);
        builder.AppendLine($"ShiftSequenceQuotaExceededViolationCount: {CountViolations(clean.Result.Evaluation, ConstraintViolationType.ShiftSequenceQuotaExceeded)}");
        builder.AppendLine();

        AppendPenaltyBreakdownByType(
            builder,
            clean.Result.Evaluation,
            weights);
        builder.AppendLine();

        AppendRequestedPreferredFulfillmentSummary(
            builder,
            scenario,
            clean.Result.Candidate);
        builder.AppendLine();

        AppendTargetGapSummary(builder, scenario, clean);
        builder.AppendLine();

        AppendMorningAssignmentsByResource(
            builder,
            scenario,
            clean.Result.Candidate);
        builder.AppendLine();

        AppendAvoidedMorningAssignmentsByResource(
            builder,
            scenario,
            clean.Result.Candidate);
        builder.AppendLine();

        AppendSequencePressureSummary(
            builder,
            scenario,
            clean.Result.Candidate,
            clean.Result.Evaluation);
        builder.AppendLine();

        AppendSequenceAssignmentsByResource(
            builder,
            scenario,
            clean.Result.Candidate);
        builder.AppendLine();

        AppendGenerationImprovementSummary(builder, clean);
        AppendGenerationDiagnostics(builder, clean);

        return builder.ToString();
    }




    private static string FormatUnwantedAssignmentFairnessDiagnosticReport(
        ExperimentScenario scenario,
        ScheduleScoringWeights weights,
        IReadOnlyCollection<MultiSeedSensitivityRun> runs)
    {
        var builder = new StringBuilder();

        builder.AppendLine("Stage 7.7A Unwanted Assignment Fairness Diagnostic");
        builder.AppendLine("Mode: Clean GA");
        builder.AppendLine("ScoringPolicy: Default Domain policy after Variant C adoption");
        builder.AppendLine($"PopulationSize: {GeneticPopulationSize}");
        builder.AppendLine($"GenerationCount: {CleanGenerationCount}");
        builder.AppendLine("Purpose: Manual diagnostic only. Not part of regular verification.");
        builder.AppendLine();

        AppendScenarioCapacitySummary(builder, scenario);
        builder.AppendLine();

        AppendScoringWeightSummary(builder, weights);
        builder.AppendLine($"IgnoredAvoidPreferencePenalty: {weights.IgnoredAvoidPreferencePenalty}");
        builder.AppendLine();

        AppendMorningShiftScarcitySummary(builder, scenario);
        builder.AppendLine();

        foreach (var run in runs)
        {
            AppendUnwantedFairnessSeedSummary(
                builder,
                scenario,
                weights,
                run);

            builder.AppendLine();

            AppendIgnoredAvoidAssignmentsByResource(
                builder,
                scenario,
                run.Run.Result.Candidate);

            builder.AppendLine();
        }

        AppendUnwantedFairnessAggregateSummary(
            builder,
            scenario,
            weights,
            runs);

        return builder.ToString();
    }

    private static void AppendUnwantedFairnessSeedSummary(
        StringBuilder builder,
        ExperimentScenario scenario,
        ScheduleScoringWeights weights,
        MultiSeedSensitivityRun sensitivityRun)
    {
        var run = sensitivityRun.Run;
        var candidate = run.Result.Candidate;
        var evaluation = run.Result.Evaluation;

        var ignoredAvoidCounts = CalculateIgnoredAvoidAssignmentCountsByResource(
            scenario,
            candidate);

        var avoidedMorningCounts = CalculateAvoidedMorningAssignmentCountsByResource(
            scenario,
            candidate);

        var totalIgnoredAvoidAssignments = ignoredAvoidCounts.Sum();
        var maxIgnoredAvoidAssignments = ignoredAvoidCounts.Max();

        var topResourceIgnoredAvoidSharePercent =
            totalIgnoredAvoidAssignments == 0
                ? 0
                : 100.0 * maxIgnoredAvoidAssignments / totalIgnoredAvoidAssignments;

        var ignoredAvoidViolationCount = CountViolations(
            evaluation,
            ConstraintViolationType.IgnoredAvoidPreference);

        builder.AppendLine("UnwantedFairnessSeedSummary:");
        builder.AppendLine($"Seed: {sensitivityRun.Seed}");
        builder.AppendLine($"RuntimeMs: {run.Elapsed.TotalMilliseconds:0.00}");
        builder.AppendLine($"IsFeasible: {evaluation.IsFeasible}");
        builder.AppendLine($"TotalPenalty: {evaluation.Score.TotalPenalty}");
        builder.AppendLine($"HardViolationCount: {evaluation.Score.HardViolationCount}");
        builder.AppendLine($"SoftViolationCount: {evaluation.Score.SoftViolationCount}");
        builder.AppendLine($"IgnoredAvoidPreferenceViolationCount: {ignoredAvoidViolationCount}");
        builder.AppendLine($"EstimatedIgnoredAvoidPenalty: {ignoredAvoidViolationCount * weights.IgnoredAvoidPreferencePenalty}");
        builder.AppendLine($"TotalIgnoredAvoidAssignments: {totalIgnoredAvoidAssignments}");
        builder.AppendLine($"MaxIgnoredAvoidAssignmentsForSingleResource: {maxIgnoredAvoidAssignments}");
        builder.AppendLine($"AverageIgnoredAvoidAssignmentsPerResource: {ignoredAvoidCounts.Average():0.##}");
        builder.AppendLine($"IgnoredAvoidAssignmentRange: {maxIgnoredAvoidAssignments - ignoredAvoidCounts.Min()}");
        builder.AppendLine($"ResourcesWithAnyIgnoredAvoidAssignment: {ignoredAvoidCounts.Count(count => count > 0)}");
        builder.AppendLine($"TopResourceIgnoredAvoidSharePercent: {topResourceIgnoredAvoidSharePercent:0.##}");
        builder.AppendLine($"TotalAvoidedMorningAssignments: {avoidedMorningCounts.Sum()}");
        builder.AppendLine($"MaxAvoidedMorningAssignmentsForSingleResource: {avoidedMorningCounts.Max()}");
        builder.AppendLine($"AverageAvoidedMorningAssignmentsPerResource: {avoidedMorningCounts.Average():0.##}");
        builder.AppendLine($"FirstBestSoFarTotalPenalty: {run.Diagnostics[0].BestSoFarTotalPenalty}");
        builder.AppendLine($"LastBestSoFarTotalPenalty: {run.Diagnostics[^1].BestSoFarTotalPenalty}");
        builder.AppendLine($"Improvement: {run.Diagnostics[0].BestSoFarTotalPenalty - run.Diagnostics[^1].BestSoFarTotalPenalty}");
    }

    private static void AppendUnwantedFairnessAggregateSummary(
        StringBuilder builder,
        ExperimentScenario scenario,
        ScheduleScoringWeights weights,
        IReadOnlyCollection<MultiSeedSensitivityRun> runs)
    {
        var ignoredAvoidCountsByRun = runs
            .Select(run => CalculateIgnoredAvoidAssignmentCountsByResource(
                scenario,
                run.Run.Result.Candidate))
            .ToArray();

        var avoidedMorningCountsByRun = runs
            .Select(run => CalculateAvoidedMorningAssignmentCountsByResource(
                scenario,
                run.Run.Result.Candidate))
            .ToArray();

        var totalIgnoredAvoidAssignments = ignoredAvoidCountsByRun
            .Select(counts => counts.Sum())
            .ToArray();

        var maxIgnoredAvoidAssignments = ignoredAvoidCountsByRun
            .Select(counts => counts.Max())
            .ToArray();

        var ignoredAvoidAssignmentRanges = ignoredAvoidCountsByRun
            .Select(counts => counts.Max() - counts.Min())
            .ToArray();

        var resourcesWithAnyIgnoredAvoidAssignment = ignoredAvoidCountsByRun
            .Select(counts => counts.Count(count => count > 0))
            .ToArray();

        var topResourceIgnoredAvoidSharePercent = ignoredAvoidCountsByRun
            .Select(counts => counts.Sum() == 0
                ? 0
                : 100.0 * counts.Max() / counts.Sum())
            .ToArray();

        var totalAvoidedMorningAssignments = avoidedMorningCountsByRun
            .Select(counts => counts.Sum())
            .ToArray();

        var maxAvoidedMorningAssignments = avoidedMorningCountsByRun
            .Select(counts => counts.Max())
            .ToArray();

        var sameTotalDifferentMaxGroupCount = totalIgnoredAvoidAssignments
            .Select((total, index) => new
            {
                Total = total,
                Max = maxIgnoredAvoidAssignments[index]
            })
            .GroupBy(item => item.Total)
            .Count(group => group
                .Select(item => item.Max)
                .Distinct()
                .Count() > 1);

        var totalIgnoredAvoidRange =
            totalIgnoredAvoidAssignments.Max() -
            totalIgnoredAvoidAssignments.Min();

        var maxIgnoredAvoidRange =
            maxIgnoredAvoidAssignments.Max() -
            maxIgnoredAvoidAssignments.Min();

        var potentialUnwantedConcentrationSignal =
            totalIgnoredAvoidRange <= 2 &&
            maxIgnoredAvoidRange >= 2;

        builder.AppendLine("UnwantedFairnessAggregateSummary:");
        builder.AppendLine($"SeedCount: {runs.Count}");
        builder.AppendLine($"TotalIgnoredAvoidAssignmentsRange: {totalIgnoredAvoidAssignments.Min()}-{totalIgnoredAvoidAssignments.Max()}");
        builder.AppendLine($"AverageTotalIgnoredAvoidAssignments: {totalIgnoredAvoidAssignments.Average():0.##}");
        builder.AppendLine($"MaxIgnoredAvoidAssignmentsForSingleResourceRange: {maxIgnoredAvoidAssignments.Min()}-{maxIgnoredAvoidAssignments.Max()}");
        builder.AppendLine($"AverageMaxIgnoredAvoidAssignmentsForSingleResource: {maxIgnoredAvoidAssignments.Average():0.##}");
        builder.AppendLine($"IgnoredAvoidAssignmentRangeByRunRange: {ignoredAvoidAssignmentRanges.Min()}-{ignoredAvoidAssignmentRanges.Max()}");
        builder.AppendLine($"ResourcesWithAnyIgnoredAvoidAssignmentRange: {resourcesWithAnyIgnoredAvoidAssignment.Min()}-{resourcesWithAnyIgnoredAvoidAssignment.Max()}");
        builder.AppendLine($"TopResourceIgnoredAvoidSharePercentRange: {topResourceIgnoredAvoidSharePercent.Min():0.##}-{topResourceIgnoredAvoidSharePercent.Max():0.##}");
        builder.AppendLine($"EstimatedIgnoredAvoidPenaltyRange: {totalIgnoredAvoidAssignments.Min() * weights.IgnoredAvoidPreferencePenalty}-{totalIgnoredAvoidAssignments.Max() * weights.IgnoredAvoidPreferencePenalty}");
        builder.AppendLine($"TotalAvoidedMorningAssignmentsRange: {totalAvoidedMorningAssignments.Min()}-{totalAvoidedMorningAssignments.Max()}");
        builder.AppendLine($"AverageTotalAvoidedMorningAssignments: {totalAvoidedMorningAssignments.Average():0.##}");
        builder.AppendLine($"MaxAvoidedMorningAssignmentsForSingleResourceRange: {maxAvoidedMorningAssignments.Min()}-{maxAvoidedMorningAssignments.Max()}");
        builder.AppendLine($"AverageMaxAvoidedMorningAssignmentsForSingleResource: {maxAvoidedMorningAssignments.Average():0.##}");
        builder.AppendLine($"SameTotalIgnoredAvoidButDifferentMaxGroupCount: {sameTotalDifferentMaxGroupCount}");
        builder.AppendLine($"PotentialUnwantedConcentrationSignal: {potentialUnwantedConcentrationSignal}");
        builder.AppendLine("CurrentDomainInterpretation: IgnoredAvoidPreference penalizes total ignored Avoid assignments, but this diagnostic does not add a dedicated concentration penalty.");
    }

    private static string FormatIndividualAvailabilityShortageWithSequencePressureMultiSeedStabilityReport(
        ExperimentScenario scenario,
        ScheduleScoringWeights weights,
        IReadOnlyCollection<MultiSeedSensitivityRun> runs)
    {
        var builder = new StringBuilder();

        builder.AppendLine("Stage 7.6D.4 Individual Availability Shortage With Sequence Pressure Multi-Seed Stability Diagnostic");
        builder.AppendLine("Mode: Clean GA");
        builder.AppendLine("Variant: Variant C");
        builder.AppendLine($"PopulationSize: {GeneticPopulationSize}");
        builder.AppendLine($"GenerationCount: {CleanGenerationCount}");
        builder.AppendLine("Purpose: Manual diagnostic only. Not part of regular verification.");
        builder.AppendLine();

        AppendScenarioCapacitySummary(builder, scenario);
        builder.AppendLine();

        AppendScoringWeightSummary(builder, weights);
        AppendNetTradeoffPerPreferred8HourAssignment(builder, weights);
        builder.AppendLine();

        AppendMorningShiftScarcitySummary(builder, scenario);
        builder.AppendLine();

        AppendSequenceTemplatePressureSummary(builder, scenario);
        builder.AppendLine();

        foreach (var run in runs)
        {
            AppendSequencePressureSeedSummary(builder, scenario, run);
            builder.AppendLine();
        }

        AppendSequencePressureMultiSeedAggregateSummary(builder, scenario, runs);

        return builder.ToString();
    }

    private static void AppendSequencePressureSeedSummary(
        StringBuilder builder,
        ExperimentScenario scenario,
        MultiSeedSensitivityRun sensitivityRun)
    {
        var run = sensitivityRun.Run;
        var candidate = run.Result.Candidate;
        var evaluation = run.Result.Evaluation;

        var targetGap = CalculateTargetGapMetrics(scenario, candidate);
        var avoidedMorningCounts = CalculateAvoidedMorningAssignmentCountsByResource(scenario, candidate);
        var morningCounts = CalculateWeekdayMorningAssignmentCountsByResource(scenario, candidate);
        var sequenceMetrics = CalculateSequencePressureMetrics(scenario, candidate);

        builder.AppendLine("SequencePressureSeedSummary:");
        builder.AppendLine($"Seed: {sensitivityRun.Seed}");
        builder.AppendLine($"RuntimeMs: {run.Elapsed.TotalMilliseconds:0.00}");
        builder.AppendLine($"IsFeasible: {evaluation.IsFeasible}");
        builder.AppendLine($"Score.Value: {evaluation.Score.Value}");
        builder.AppendLine($"TotalPenalty: {evaluation.Score.TotalPenalty}");
        builder.AppendLine($"HardViolationCount: {evaluation.Score.HardViolationCount}");
        builder.AppendLine($"SoftViolationCount: {evaluation.Score.SoftViolationCount}");
        builder.AppendLine($"Assignments.Count: {candidate.Assignments.Count}");
        builder.AppendLine($"TotalAssignedHours: {CalculateTotalAssignedHours(scenario, candidate):0.##}");
        builder.AppendLine($"TotalOverTargetHours: {targetGap.TotalOverTargetHours:0.##}");
        builder.AppendLine($"TotalUnderTargetHours: {targetGap.TotalUnderTargetHours:0.##}");
        builder.AppendLine($"IgnoredAvoidPreferenceCount: {CountViolations(evaluation, ConstraintViolationType.IgnoredAvoidPreference)}");
        builder.AppendLine($"RequestedPreferredHoursNotSatisfiedCount: {CountViolations(evaluation, ConstraintViolationType.ResourceRequestedPreferredHoursNotSatisfied)}");
        builder.AppendLine($"TotalMorningAssignments: {morningCounts.Sum()}");
        builder.AppendLine($"MaxMorningAssignmentsForSingleResource: {morningCounts.Max()}");
        builder.AppendLine($"TotalAvoidedMorningAssignments: {avoidedMorningCounts.Sum()}");
        builder.AppendLine($"MaxAvoidedMorningAssignmentsForSingleResource: {avoidedMorningCounts.Max()}");
        builder.AppendLine($"AverageAvoidedMorningAssignmentsPerResource: {avoidedMorningCounts.Average():0.##}");
        builder.AppendLine($"TotalAfternoonToMorningSequences: {sequenceMetrics.TotalAfternoonToMorningSequences}");
        builder.AppendLine($"TotalNightToAfternoonSequences: {sequenceMetrics.TotalNightToAfternoonSequences}");
        builder.AppendLine($"TotalSupportedSequences: {sequenceMetrics.TotalSupportedSequences}");
        builder.AppendLine($"MaxMonthlyAfternoonToMorningSequencesForSingleResource: {sequenceMetrics.MaxMonthlyAfternoonToMorningSequencesForSingleResource}");
        builder.AppendLine($"MaxMonthlyNightToAfternoonSequencesForSingleResource: {sequenceMetrics.MaxMonthlyNightToAfternoonSequencesForSingleResource}");
        builder.AppendLine($"MaxMonthlyTotalSequencesForSingleResource: {sequenceMetrics.MaxMonthlyTotalSequencesForSingleResource}");
        builder.AppendLine($"ResourcesWithAnySequenceCount: {sequenceMetrics.ResourcesWithAnySequenceCount}");
        builder.AppendLine($"ResourcesExceedingSequenceQuotaCount: {sequenceMetrics.ResourcesExceedingSequenceQuotaCount}");
        builder.AppendLine($"ShiftSequenceQuotaExceededViolationCount: {CountViolations(evaluation, ConstraintViolationType.ShiftSequenceQuotaExceeded)}");
        builder.AppendLine($"FirstBestSoFarTotalPenalty: {run.Diagnostics[0].BestSoFarTotalPenalty}");
        builder.AppendLine($"LastBestSoFarTotalPenalty: {run.Diagnostics[^1].BestSoFarTotalPenalty}");
        builder.AppendLine($"Improvement: {run.Diagnostics[0].BestSoFarTotalPenalty - run.Diagnostics[^1].BestSoFarTotalPenalty}");
        builder.AppendLine($"FirstFeasibleCandidateCount: {run.Diagnostics[0].FeasibleCandidateCount}");
        builder.AppendLine($"LastFeasibleCandidateCount: {run.Diagnostics[^1].FeasibleCandidateCount}");
    }

    private static void AppendSequencePressureMultiSeedAggregateSummary(
        StringBuilder builder,
        ExperimentScenario scenario,
        IReadOnlyCollection<MultiSeedSensitivityRun> runs)
    {
        var totalPenalties = runs
            .Select(run => run.Run.Result.Evaluation.Score.TotalPenalty)
            .ToArray();

        var totalAssignedHours = runs
            .Select(run => CalculateTotalAssignedHours(scenario, run.Run.Result.Candidate))
            .ToArray();

        var targetGaps = runs
            .Select(run => CalculateTargetGapMetrics(scenario, run.Run.Result.Candidate))
            .ToArray();

        var sequenceMetrics = runs
            .Select(run => CalculateSequencePressureMetrics(scenario, run.Run.Result.Candidate))
            .ToArray();

        var avoidedMorningCounts = runs
            .Select(run => CalculateAvoidedMorningAssignmentCountsByResource(scenario, run.Run.Result.Candidate))
            .ToArray();

        var morningCounts = runs
            .Select(run => CalculateWeekdayMorningAssignmentCountsByResource(scenario, run.Run.Result.Candidate))
            .ToArray();

        var totalAvoidedMorningAssignments = avoidedMorningCounts
            .Select(counts => counts.Sum())
            .ToArray();

        var maxAvoidedMorningAssignments = avoidedMorningCounts
            .Select(counts => counts.Max())
            .ToArray();

        var maxMorningAssignments = morningCounts
            .Select(counts => counts.Max())
            .ToArray();

        var sequenceQuotaExceededCounts = runs
            .Select(run => CountViolations(run.Run.Result.Evaluation, ConstraintViolationType.ShiftSequenceQuotaExceeded))
            .ToArray();

        var totalAfternoonToMorningSequences = sequenceMetrics
            .Select(metrics => metrics.TotalAfternoonToMorningSequences)
            .ToArray();

        var totalNightToAfternoonSequences = sequenceMetrics
            .Select(metrics => metrics.TotalNightToAfternoonSequences)
            .ToArray();

        var totalSupportedSequences = sequenceMetrics
            .Select(metrics => metrics.TotalSupportedSequences)
            .ToArray();

        var maxMonthlyAfternoonToMorningSequences = sequenceMetrics
            .Select(metrics => metrics.MaxMonthlyAfternoonToMorningSequencesForSingleResource)
            .ToArray();

        var maxMonthlyNightToAfternoonSequences = sequenceMetrics
            .Select(metrics => metrics.MaxMonthlyNightToAfternoonSequencesForSingleResource)
            .ToArray();

        var maxMonthlyTotalSequences = sequenceMetrics
            .Select(metrics => metrics.MaxMonthlyTotalSequencesForSingleResource)
            .ToArray();

        var resourcesWithAnySequence = sequenceMetrics
            .Select(metrics => metrics.ResourcesWithAnySequenceCount)
            .ToArray();

        var resourcesExceedingSequenceQuota = sequenceMetrics
            .Select(metrics => metrics.ResourcesExceedingSequenceQuotaCount)
            .ToArray();

        builder.AppendLine("SequencePressureMultiSeedAggregateSummary:");
        builder.AppendLine($"SeedCount: {runs.Count}");
        builder.AppendLine($"MinTotalPenalty: {totalPenalties.Min()}");
        builder.AppendLine($"MaxTotalPenalty: {totalPenalties.Max()}");
        builder.AppendLine($"AverageTotalPenalty: {totalPenalties.Average():0.##}");
        builder.AppendLine($"TotalPenaltyRange: {totalPenalties.Max() - totalPenalties.Min()}");
        builder.AppendLine($"MinTotalAssignedHours: {totalAssignedHours.Min():0.##}");
        builder.AppendLine($"MaxTotalAssignedHours: {totalAssignedHours.Max():0.##}");
        builder.AppendLine($"AverageTotalAssignedHours: {totalAssignedHours.Average():0.##}");
        builder.AppendLine($"TotalAssignedHoursRange: {totalAssignedHours.Max() - totalAssignedHours.Min():0.##}");
        builder.AppendLine($"MinOverTargetHours: {targetGaps.Min(gap => gap.TotalOverTargetHours):0.##}");
        builder.AppendLine($"MaxOverTargetHours: {targetGaps.Max(gap => gap.TotalOverTargetHours):0.##}");
        builder.AppendLine($"AverageOverTargetHours: {targetGaps.Average(gap => gap.TotalOverTargetHours):0.##}");
        builder.AppendLine($"MinUnderTargetHours: {targetGaps.Min(gap => gap.TotalUnderTargetHours):0.##}");
        builder.AppendLine($"MaxUnderTargetHours: {targetGaps.Max(gap => gap.TotalUnderTargetHours):0.##}");
        builder.AppendLine($"AverageUnderTargetHours: {targetGaps.Average(gap => gap.TotalUnderTargetHours):0.##}");
        builder.AppendLine($"TotalAvoidedMorningAssignmentsRange: {totalAvoidedMorningAssignments.Min()}-{totalAvoidedMorningAssignments.Max()}");
        builder.AppendLine($"AverageTotalAvoidedMorningAssignments: {totalAvoidedMorningAssignments.Average():0.##}");
        builder.AppendLine($"MaxMorningAssignmentsForSingleResourceRange: {maxMorningAssignments.Min()}-{maxMorningAssignments.Max()}");
        builder.AppendLine($"MaxAvoidedMorningAssignmentsForSingleResourceRange: {maxAvoidedMorningAssignments.Min()}-{maxAvoidedMorningAssignments.Max()}");
        builder.AppendLine($"AverageMaxAvoidedMorningAssignmentsForSingleResource: {maxAvoidedMorningAssignments.Average():0.##}");
        builder.AppendLine($"TotalAfternoonToMorningSequencesRange: {totalAfternoonToMorningSequences.Min()}-{totalAfternoonToMorningSequences.Max()}");
        builder.AppendLine($"AverageTotalAfternoonToMorningSequences: {totalAfternoonToMorningSequences.Average():0.##}");
        builder.AppendLine($"TotalNightToAfternoonSequencesRange: {totalNightToAfternoonSequences.Min()}-{totalNightToAfternoonSequences.Max()}");
        builder.AppendLine($"AverageTotalNightToAfternoonSequences: {totalNightToAfternoonSequences.Average():0.##}");
        builder.AppendLine($"TotalSupportedSequencesRange: {totalSupportedSequences.Min()}-{totalSupportedSequences.Max()}");
        builder.AppendLine($"AverageTotalSupportedSequences: {totalSupportedSequences.Average():0.##}");
        builder.AppendLine($"MaxMonthlyAfternoonToMorningSequencesForSingleResourceRange: {maxMonthlyAfternoonToMorningSequences.Min()}-{maxMonthlyAfternoonToMorningSequences.Max()}");
        builder.AppendLine($"MaxMonthlyNightToAfternoonSequencesForSingleResourceRange: {maxMonthlyNightToAfternoonSequences.Min()}-{maxMonthlyNightToAfternoonSequences.Max()}");
        builder.AppendLine($"MaxMonthlyTotalSequencesForSingleResourceRange: {maxMonthlyTotalSequences.Min()}-{maxMonthlyTotalSequences.Max()}");
        builder.AppendLine($"ResourcesWithAnySequenceCountRange: {resourcesWithAnySequence.Min()}-{resourcesWithAnySequence.Max()}");
        builder.AppendLine($"ResourcesExceedingSequenceQuotaCountRange: {resourcesExceedingSequenceQuota.Min()}-{resourcesExceedingSequenceQuota.Max()}");
        builder.AppendLine($"ShiftSequenceQuotaExceededViolationCountRange: {sequenceQuotaExceededCounts.Min()}-{sequenceQuotaExceededCounts.Max()}");
        builder.AppendLine($"AnyRunHasSequenceQuotaViolations: {sequenceQuotaExceededCounts.Any(count => count > 0)}");
        builder.AppendLine($"AnyRunHasHardViolations: {runs.Any(run => run.Run.Result.Evaluation.Score.HardViolationCount > 0)}");
        builder.AppendLine($"AllRunsImprovedBestSoFar: {runs.All(run => run.Run.Diagnostics[^1].BestSoFarTotalPenalty <= run.Run.Diagnostics[0].BestSoFarTotalPenalty)}");
    }


    private static string FormatIndividualAvailabilityShortageMultiSeedStabilityReport(
        ExperimentScenario scenario,
        ScheduleScoringWeights weights,
        IReadOnlyCollection<MultiSeedSensitivityRun> runs)
    {
        var builder = new StringBuilder();

        builder.AppendLine("Stage 7.6D.2 Individual Availability Shortage Multi-Seed Stability Diagnostic");
        builder.AppendLine("Mode: Clean GA");
        builder.AppendLine("Variant: Variant C");
        builder.AppendLine($"PopulationSize: {GeneticPopulationSize}");
        builder.AppendLine($"GenerationCount: {CleanGenerationCount}");
        builder.AppendLine("Purpose: Manual diagnostic only. Not part of regular verification.");
        builder.AppendLine();

        AppendScenarioCapacitySummary(builder, scenario);
        builder.AppendLine();

        AppendScoringWeightSummary(builder, weights);
        AppendNetTradeoffPerPreferred8HourAssignment(builder, weights);
        builder.AppendLine();

        AppendMorningShiftScarcitySummary(builder, scenario);
        builder.AppendLine();

        foreach (var run in runs)
        {
            AppendIndividualShortageMultiSeedRunSummary(
                builder,
                scenario,
                run);

            builder.AppendLine();
        }

        AppendIndividualShortageMultiSeedAggregateSummary(
            builder,
            scenario,
            runs);

        return builder.ToString();
    }

    private static void AppendIndividualShortageMultiSeedRunSummary(
        StringBuilder builder,
        ExperimentScenario scenario,
        MultiSeedSensitivityRun sensitivityRun)
    {
        var run = sensitivityRun.Run;
        var candidate = run.Result.Candidate;
        var evaluation = run.Result.Evaluation;
        var targetGap = CalculateTargetGapMetrics(
            scenario,
            candidate);

        var avoidedMorningCounts = CalculateAvoidedMorningAssignmentCountsByResource(
            scenario,
            candidate);

        var morningCounts = CalculateWeekdayMorningAssignmentCountsByResource(
            scenario,
            candidate);

        builder.AppendLine("IndividualShortageSeedSummary:");
        builder.AppendLine($"Seed: {sensitivityRun.Seed}");
        builder.AppendLine($"RuntimeMs: {run.Elapsed.TotalMilliseconds:0.00}");
        builder.AppendLine($"IsFeasible: {evaluation.IsFeasible}");
        builder.AppendLine($"Score.Value: {evaluation.Score.Value}");
        builder.AppendLine($"TotalPenalty: {evaluation.Score.TotalPenalty}");
        builder.AppendLine($"HardViolationCount: {evaluation.Score.HardViolationCount}");
        builder.AppendLine($"SoftViolationCount: {evaluation.Score.SoftViolationCount}");
        builder.AppendLine($"Assignments.Count: {candidate.Assignments.Count}");
        builder.AppendLine($"TotalAssignedHours: {CalculateTotalAssignedHours(scenario, candidate):0.##}");
        builder.AppendLine($"TotalOverTargetHours: {targetGap.TotalOverTargetHours:0.##}");
        builder.AppendLine($"TotalUnderTargetHours: {targetGap.TotalUnderTargetHours:0.##}");
        builder.AppendLine($"IgnoredAvoidPreferenceCount: {CountViolations(evaluation, ConstraintViolationType.IgnoredAvoidPreference)}");
        builder.AppendLine($"RequestedPreferredHoursNotSatisfiedCount: {CountViolations(evaluation, ConstraintViolationType.ResourceRequestedPreferredHoursNotSatisfied)}");
        builder.AppendLine($"TotalMorningAssignments: {morningCounts.Sum()}");
        builder.AppendLine($"MaxMorningAssignmentsForSingleResource: {morningCounts.Max()}");
        builder.AppendLine($"TotalAvoidedMorningAssignments: {avoidedMorningCounts.Sum()}");
        builder.AppendLine($"MaxAvoidedMorningAssignmentsForSingleResource: {avoidedMorningCounts.Max()}");
        builder.AppendLine($"AverageAvoidedMorningAssignmentsPerResource: {avoidedMorningCounts.Average():0.##}");
        builder.AppendLine($"FirstBestSoFarTotalPenalty: {run.Diagnostics[0].BestSoFarTotalPenalty}");
        builder.AppendLine($"LastBestSoFarTotalPenalty: {run.Diagnostics[^1].BestSoFarTotalPenalty}");
        builder.AppendLine($"Improvement: {run.Diagnostics[0].BestSoFarTotalPenalty - run.Diagnostics[^1].BestSoFarTotalPenalty}");
        builder.AppendLine($"FirstFeasibleCandidateCount: {run.Diagnostics[0].FeasibleCandidateCount}");
        builder.AppendLine($"LastFeasibleCandidateCount: {run.Diagnostics[^1].FeasibleCandidateCount}");
    }

    private static void AppendIndividualShortageMultiSeedAggregateSummary(
        StringBuilder builder,
        ExperimentScenario scenario,
        IReadOnlyCollection<MultiSeedSensitivityRun> runs)
    {
        var totalPenalties = runs
            .Select(run => run.Run.Result.Evaluation.Score.TotalPenalty)
            .ToArray();

        var totalAssignedHours = runs
            .Select(run => CalculateTotalAssignedHours(
                scenario,
                run.Run.Result.Candidate))
            .ToArray();

        var targetGaps = runs
            .Select(run => CalculateTargetGapMetrics(
                scenario,
                run.Run.Result.Candidate))
            .ToArray();

        var avoidedMorningCountsByRun = runs
            .Select(run => CalculateAvoidedMorningAssignmentCountsByResource(
                scenario,
                run.Run.Result.Candidate))
            .ToArray();

        var morningCountsByRun = runs
            .Select(run => CalculateWeekdayMorningAssignmentCountsByResource(
                scenario,
                run.Run.Result.Candidate))
            .ToArray();

        var totalAvoidedMorningAssignments = avoidedMorningCountsByRun
            .Select(counts => counts.Sum())
            .ToArray();

        var maxAvoidedMorningAssignments = avoidedMorningCountsByRun
            .Select(counts => counts.Max())
            .ToArray();

        var maxMorningAssignments = morningCountsByRun
            .Select(counts => counts.Max())
            .ToArray();

        builder.AppendLine("IndividualShortageMultiSeedAggregateSummary:");
        builder.AppendLine($"SeedCount: {runs.Count}");
        builder.AppendLine($"MinTotalPenalty: {totalPenalties.Min()}");
        builder.AppendLine($"MaxTotalPenalty: {totalPenalties.Max()}");
        builder.AppendLine($"AverageTotalPenalty: {totalPenalties.Average():0.##}");
        builder.AppendLine($"TotalPenaltyRange: {totalPenalties.Max() - totalPenalties.Min()}");
        builder.AppendLine($"MinTotalAssignedHours: {totalAssignedHours.Min():0.##}");
        builder.AppendLine($"MaxTotalAssignedHours: {totalAssignedHours.Max():0.##}");
        builder.AppendLine($"AverageTotalAssignedHours: {totalAssignedHours.Average():0.##}");
        builder.AppendLine($"TotalAssignedHoursRange: {totalAssignedHours.Max() - totalAssignedHours.Min():0.##}");
        builder.AppendLine($"MinOverTargetHours: {targetGaps.Min(gap => gap.TotalOverTargetHours):0.##}");
        builder.AppendLine($"MaxOverTargetHours: {targetGaps.Max(gap => gap.TotalOverTargetHours):0.##}");
        builder.AppendLine($"AverageOverTargetHours: {targetGaps.Average(gap => gap.TotalOverTargetHours):0.##}");
        builder.AppendLine($"MinUnderTargetHours: {targetGaps.Min(gap => gap.TotalUnderTargetHours):0.##}");
        builder.AppendLine($"MaxUnderTargetHours: {targetGaps.Max(gap => gap.TotalUnderTargetHours):0.##}");
        builder.AppendLine($"AverageUnderTargetHours: {targetGaps.Average(gap => gap.TotalUnderTargetHours):0.##}");
        builder.AppendLine($"TotalAvoidedMorningAssignmentsRange: {totalAvoidedMorningAssignments.Min()}-{totalAvoidedMorningAssignments.Max()}");
        builder.AppendLine($"AverageTotalAvoidedMorningAssignments: {totalAvoidedMorningAssignments.Average():0.##}");
        builder.AppendLine($"MaxMorningAssignmentsForSingleResourceRange: {maxMorningAssignments.Min()}-{maxMorningAssignments.Max()}");
        builder.AppendLine($"MaxAvoidedMorningAssignmentsForSingleResourceRange: {maxAvoidedMorningAssignments.Min()}-{maxAvoidedMorningAssignments.Max()}");
        builder.AppendLine($"AverageMaxAvoidedMorningAssignmentsForSingleResource: {maxAvoidedMorningAssignments.Average():0.##}");
        builder.AppendLine($"AnyRunHasHardViolations: {runs.Any(run => run.Run.Result.Evaluation.Score.HardViolationCount > 0)}");
        builder.AppendLine($"AllRunsImprovedBestSoFar: {runs.All(run => run.Run.Diagnostics[^1].BestSoFarTotalPenalty <= run.Run.Diagnostics[0].BestSoFarTotalPenalty)}");
    }

    private static IReadOnlyList<int> CalculateWeekdayMorningAssignmentCountsByResource(
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        var shiftsById = scenario.Shifts
            .ToDictionary(shift => shift.Id);

        return scenario.Resources
            .Select(resource => candidate.Assignments
                .Where(assignment => assignment.ResourceId == resource.Id)
                .Select(assignment => shiftsById[assignment.ShiftId])
                .Count(IsWeekdayMorning))
            .ToArray();
    }

    private static IReadOnlyList<int> CalculateAvoidedMorningAssignmentCountsByResource(
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        var shiftsById = scenario.Shifts
            .ToDictionary(shift => shift.Id);

        return scenario.Resources
            .Select(resource => candidate.Assignments
                .Where(assignment => assignment.ResourceId == resource.Id)
                .Select(assignment => shiftsById[assignment.ShiftId])
                .Where(IsWeekdayMorning)
                .Count(shift => HasPreferenceForShift(
                    scenario,
                    resource.Id,
                    shift,
                    ResourcePreferenceType.Avoid)))
            .ToArray();
    }

    private static void AppendAvailabilityPressureSummary(
        StringBuilder builder,
        ExperimentScenario scenario)
    {
        builder.AppendLine("AvailabilityPressureSummary:");

        foreach (var kind in Enum.GetValues<ShiftKind>())
        {
            var shifts = scenario.Shifts
                .Where(shift => shift.Kind == kind)
                .ToArray();

            var availableCounts = shifts
                .Select(shift => CountAvailableResourcesForShift(scenario, shift))
                .ToArray();

            var preferCounts = shifts
                .Select(shift => CountPreferredResourcesForShift(scenario, shift))
                .ToArray();

            var avoidCounts = shifts
                .Select(shift => CountAvoidingResourcesForShift(scenario, shift))
                .ToArray();

            builder.AppendLine(
                $"- {kind}: " +
                $"ShiftCount={shifts.Length}, " +
                $"MinAvailableResources={availableCounts.Min()}, " +
                $"MaxAvailableResources={availableCounts.Max()}, " +
                $"AverageAvailableResources={availableCounts.Average():0.##}, " +
                $"MinPreferredResources={preferCounts.Min()}, " +
                $"MaxPreferredResources={preferCounts.Max()}, " +
                $"AveragePreferredResources={preferCounts.Average():0.##}, " +
                $"MinAvoidResources={avoidCounts.Min()}, " +
                $"MaxAvoidResources={avoidCounts.Max()}, " +
                $"AverageAvoidResources={avoidCounts.Average():0.##}");
        }
    }

    private static void AppendMorningShiftScarcitySummary(
        StringBuilder builder,
        ExperimentScenario scenario)
    {
        var weekdayMorningShifts = scenario.Shifts
            .Where(IsWeekdayMorning)
            .ToArray();

        var availableCounts = weekdayMorningShifts
            .Select(shift => CountAvailableResourcesForShift(scenario, shift))
            .ToArray();

        var preferCounts = weekdayMorningShifts
            .Select(shift => CountPreferredResourcesForShift(scenario, shift))
            .ToArray();

        var avoidCounts = weekdayMorningShifts
            .Select(shift => CountAvoidingResourcesForShift(scenario, shift))
            .ToArray();

        builder.AppendLine("MorningShiftScarcitySummary:");
        builder.AppendLine($"WeekdayMorningShiftCount: {weekdayMorningShifts.Length}");
        builder.AppendLine($"TotalMinimumMorningAssignments: {weekdayMorningShifts.Sum(shift => shift.MinResourceCount)}");
        builder.AppendLine($"TotalMaximumMorningAssignments: {weekdayMorningShifts.Sum(shift => shift.MaxResourceCount)}");
        builder.AppendLine($"MinAvailableResources: {availableCounts.Min()}");
        builder.AppendLine($"MaxAvailableResources: {availableCounts.Max()}");
        builder.AppendLine($"MinPreferredResources: {preferCounts.Min()}");
        builder.AppendLine($"MaxPreferredResources: {preferCounts.Max()}");
        builder.AppendLine($"MinAvoidResources: {avoidCounts.Min()}");
        builder.AppendLine($"MaxAvoidResources: {avoidCounts.Max()}");
        builder.AppendLine($"ShiftsWithPreferredResourcesBelowMinimum: {weekdayMorningShifts.Count(shift => CountPreferredResourcesForShift(scenario, shift) < shift.MinResourceCount)}");
        builder.AppendLine($"ShiftsRequiringPreferenceToAssign: {weekdayMorningShifts.Count(shift => shift.RequiresPreferenceToAssign)}");
    }

    private static void AppendWeekendScarcitySummary(
        StringBuilder builder,
        ExperimentScenario scenario)
    {
        var weekendShifts = scenario.Shifts
            .Where(IsFridayOrSaturday)
            .ToArray();

        var availableCounts = weekendShifts
            .Select(shift => CountAvailableResourcesForShift(scenario, shift))
            .ToArray();

        var preferCounts = weekendShifts
            .Select(shift => CountPreferredResourcesForShift(scenario, shift))
            .ToArray();

        builder.AppendLine("WeekendScarcitySummary:");
        builder.AppendLine($"WeekendShiftCount: {weekendShifts.Length}");
        builder.AppendLine($"TotalMinimumWeekendAssignments: {weekendShifts.Sum(shift => shift.MinResourceCount)}");
        builder.AppendLine($"TotalMaximumWeekendAssignments: {weekendShifts.Sum(shift => shift.MaxResourceCount)}");
        builder.AppendLine($"MinAvailableResources: {availableCounts.Min()}");
        builder.AppendLine($"MaxAvailableResources: {availableCounts.Max()}");
        builder.AppendLine($"AverageAvailableResources: {availableCounts.Average():0.##}");
        builder.AppendLine($"MinPreferredResources: {preferCounts.Min()}");
        builder.AppendLine($"MaxPreferredResources: {preferCounts.Max()}");
        builder.AppendLine($"AveragePreferredResources: {preferCounts.Average():0.##}");
    }

    private static void AppendMorningAssignmentsByResource(
        StringBuilder builder,
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        var shiftsById = scenario.Shifts
            .ToDictionary(shift => shift.Id);

        builder.AppendLine("MorningAssignmentsByResource:");

        foreach (var resource in scenario.Resources.OrderBy(resource => resource.Name))
        {
            var morningAssignments = candidate.Assignments
                .Where(assignment => assignment.ResourceId == resource.Id)
                .Select(assignment => shiftsById[assignment.ShiftId])
                .Where(IsWeekdayMorning)
                .ToArray();

            var preferredMorningAssignments = morningAssignments
                .Count(shift => HasPreferenceForShift(
                    scenario,
                    resource.Id,
                    shift,
                    ResourcePreferenceType.Prefer));

            var avoidedMorningAssignments = morningAssignments
                .Count(shift => HasPreferenceForShift(
                    scenario,
                    resource.Id,
                    shift,
                    ResourcePreferenceType.Avoid));

            builder.AppendLine(
                $"- {resource.Name}: " +
                $"MorningAssignments={morningAssignments.Length}, " +
                $"PreferredMorningAssignments={preferredMorningAssignments}, " +
                $"AvoidedMorningAssignments={avoidedMorningAssignments}");
        }
    }

    private static void AppendAvoidedMorningAssignmentsByResource(
        StringBuilder builder,
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        var shiftsById = scenario.Shifts
            .ToDictionary(shift => shift.Id);

        var counts = scenario.Resources
            .Select(resource => new
            {
                Resource = resource,
                Count = candidate.Assignments
                    .Where(assignment => assignment.ResourceId == resource.Id)
                    .Select(assignment => shiftsById[assignment.ShiftId])
                    .Where(IsWeekdayMorning)
                    .Count(shift => HasPreferenceForShift(
                        scenario,
                        resource.Id,
                        shift,
                        ResourcePreferenceType.Avoid))
            })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Resource.Name)
            .ToArray();

        builder.AppendLine("AvoidedMorningAssignmentsByResource:");
        builder.AppendLine($"TotalAvoidedMorningAssignments: {counts.Sum(item => item.Count)}");
        builder.AppendLine($"MaxAvoidedMorningAssignmentsForSingleResource: {counts.Max(item => item.Count)}");
        builder.AppendLine($"AverageAvoidedMorningAssignmentsPerResource: {counts.Average(item => item.Count):0.##}");

        foreach (var item in counts)
        {
            builder.AppendLine($"- {item.Resource.Name}: {item.Count}");
        }
    }


    private static IReadOnlyList<int> CalculateIgnoredAvoidAssignmentCountsByResource(
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        var shiftsById = scenario.Shifts
            .ToDictionary(shift => shift.Id);

        return scenario.Resources
            .Select(resource => candidate.Assignments
                .Where(assignment => assignment.ResourceId == resource.Id)
                .Select(assignment => shiftsById[assignment.ShiftId])
                .Count(shift => HasPreferenceForShift(
                    scenario,
                    resource.Id,
                    shift,
                    ResourcePreferenceType.Avoid)))
            .ToArray();
    }

    private static void AppendIgnoredAvoidAssignmentsByResource(
        StringBuilder builder,
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        var shiftsById = scenario.Shifts
            .ToDictionary(shift => shift.Id);

        var counts = scenario.Resources
            .Select(resource =>
            {
                var assignedShifts = candidate.Assignments
                    .Where(assignment => assignment.ResourceId == resource.Id)
                    .Select(assignment => shiftsById[assignment.ShiftId])
                    .ToArray();

                var ignoredAvoidAssignments = assignedShifts
                    .Count(shift => HasPreferenceForShift(
                        scenario,
                        resource.Id,
                        shift,
                        ResourcePreferenceType.Avoid));

                var avoidedMorningAssignments = assignedShifts
                    .Where(IsWeekdayMorning)
                    .Count(shift => HasPreferenceForShift(
                        scenario,
                        resource.Id,
                        shift,
                        ResourcePreferenceType.Avoid));

                return new
                {
                    Resource = resource,
                    IgnoredAvoidAssignments = ignoredAvoidAssignments,
                    AvoidedMorningAssignments = avoidedMorningAssignments
                };
            })
            .OrderByDescending(item => item.IgnoredAvoidAssignments)
            .ThenByDescending(item => item.AvoidedMorningAssignments)
            .ThenBy(item => item.Resource.Name)
            .ToArray();

        builder.AppendLine("IgnoredAvoidAssignmentsByResource:");
        builder.AppendLine($"TotalIgnoredAvoidAssignments: {counts.Sum(item => item.IgnoredAvoidAssignments)}");
        builder.AppendLine($"MaxIgnoredAvoidAssignmentsForSingleResource: {counts.Max(item => item.IgnoredAvoidAssignments)}");
        builder.AppendLine($"AverageIgnoredAvoidAssignmentsPerResource: {counts.Average(item => item.IgnoredAvoidAssignments):0.##}");

        foreach (var item in counts)
        {
            builder.AppendLine(
                $"- {item.Resource.Name}: " +
                $"IgnoredAvoidAssignments={item.IgnoredAvoidAssignments}, " +
                $"AvoidedMorningAssignments={item.AvoidedMorningAssignments}");
        }
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
            .Any(preference => Overlaps(
                preference.StartUtc,
                preference.EndUtc,
                shift.StartUtc,
                shift.EndUtc));
    }

    private static void AppendSequenceTemplatePressureSummary(
        StringBuilder builder,
        ExperimentScenario scenario)
    {
        builder.AppendLine("SequenceTemplatePressureSummary:");
        builder.AppendLine("MinimumRestHours: 8");
        builder.AppendLine($"PotentialAfternoonToMorningTemplatePairs: {CountPotentialSequenceTemplatePairs(scenario.Shifts, ShiftSequenceType.AfternoonToMorning)}");
        builder.AppendLine($"PotentialNightToAfternoonTemplatePairs: {CountPotentialSequenceTemplatePairs(scenario.Shifts, ShiftSequenceType.NightToAfternoon)}");
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

                if (nextShift.StartUtc < previousShift.EndUtc)
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

    private static void AppendSequencePressureSummary(
        StringBuilder builder,
        ExperimentScenario scenario,
        ScheduleCandidate candidate,
        ScheduleEvaluationResult evaluation)
    {
        var metrics = CalculateSequencePressureMetrics(
            scenario,
            candidate);

        builder.AppendLine("SequencePressureSummary:");
        builder.AppendLine($"TotalAfternoonToMorningSequences: {metrics.TotalAfternoonToMorningSequences}");
        builder.AppendLine($"TotalNightToAfternoonSequences: {metrics.TotalNightToAfternoonSequences}");
        builder.AppendLine($"TotalSupportedSequences: {metrics.TotalSupportedSequences}");
        builder.AppendLine($"MaxMonthlyAfternoonToMorningSequencesForSingleResource: {metrics.MaxMonthlyAfternoonToMorningSequencesForSingleResource}");
        builder.AppendLine($"MaxMonthlyNightToAfternoonSequencesForSingleResource: {metrics.MaxMonthlyNightToAfternoonSequencesForSingleResource}");
        builder.AppendLine($"MaxMonthlyTotalSequencesForSingleResource: {metrics.MaxMonthlyTotalSequencesForSingleResource}");
        builder.AppendLine($"ResourcesWithAnySequenceCount: {metrics.ResourcesWithAnySequenceCount}");
        builder.AppendLine($"ResourcesExceedingSequenceQuotaCount: {metrics.ResourcesExceedingSequenceQuotaCount}");
        builder.AppendLine($"ShiftSequenceQuotaExceededViolationCount: {CountViolations(evaluation, ConstraintViolationType.ShiftSequenceQuotaExceeded)}");
    }

    private static void AppendSequenceAssignmentsByResource(
        StringBuilder builder,
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        var metrics = CalculateSequencePressureMetrics(
            scenario,
            candidate);

        builder.AppendLine("SequenceAssignmentsByResource:");

        foreach (var summary in metrics.ResourceSummaries
                     .OrderByDescending(summary => summary.MaxMonthlyTotalSequences)
                     .ThenByDescending(summary => summary.TotalSupportedSequences)
                     .ThenBy(summary => summary.Resource.Name))
        {
            builder.AppendLine(
                $"- {summary.Resource.Name}: " +
                $"AfternoonToMorning={summary.TotalAfternoonToMorningSequences}, " +
                $"NightToAfternoon={summary.TotalNightToAfternoonSequences}, " +
                $"Total={summary.TotalSupportedSequences}, " +
                $"MaxMonthlyAfternoonToMorning={summary.MaxMonthlyAfternoonToMorningSequences}, " +
                $"MaxMonthlyNightToAfternoon={summary.MaxMonthlyNightToAfternoonSequences}, " +
                $"MaxMonthlyTotal={summary.MaxMonthlyTotalSequences}, " +
                $"ExceedsQuota={summary.ExceedsMonthlyQuota}");
        }
    }

    private static SequencePressureMetrics CalculateSequencePressureMetrics(
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        var shiftsById = scenario.Shifts
            .ToDictionary(shift => shift.Id);

        var classifier = new ShiftSequenceClassifier();
        var summaries = new List<ResourceSequencePressureSummary>();

        foreach (var resource in scenario.Resources)
        {
            var monthlyCounts = new Dictionary<(int Year, int Month), SequenceQuotaCounts>();
            var totalAfternoonToMorning = 0;
            var totalNightToAfternoon = 0;

            var assignedShifts = candidate.Assignments
                .Where(assignment => assignment.ResourceId == resource.Id)
                .Select(assignment => shiftsById[assignment.ShiftId])
                .OrderBy(shift => shift.StartUtc)
                .ToArray();

            for (var index = 1; index < assignedShifts.Length; index++)
            {
                var previousShift = assignedShifts[index - 1];
                var nextShift = assignedShifts[index];
                var sequenceType = classifier.Classify(previousShift, nextShift);

                if (sequenceType is null)
                {
                    continue;
                }

                var key = (nextShift.StartUtc.Year, nextShift.StartUtc.Month);

                if (!monthlyCounts.TryGetValue(key, out var counts))
                {
                    counts = new SequenceQuotaCounts();
                    monthlyCounts[key] = counts;
                }

                counts.Total++;

                if (sequenceType == ShiftSequenceType.AfternoonToMorning)
                {
                    counts.AfternoonToMorning++;
                    totalAfternoonToMorning++;
                }

                if (sequenceType == ShiftSequenceType.NightToAfternoon)
                {
                    counts.NightToAfternoon++;
                    totalNightToAfternoon++;
                }
            }

            var maxMonthlyAfternoonToMorning = monthlyCounts.Count == 0
                ? 0
                : monthlyCounts.Values.Max(counts => counts.AfternoonToMorning);

            var maxMonthlyNightToAfternoon = monthlyCounts.Count == 0
                ? 0
                : monthlyCounts.Values.Max(counts => counts.NightToAfternoon);

            var maxMonthlyTotal = monthlyCounts.Count == 0
                ? 0
                : monthlyCounts.Values.Max(counts => counts.Total);

            var exceedsMonthlyQuota = monthlyCounts.Values.Any(counts =>
                counts.AfternoonToMorning > 2 ||
                counts.NightToAfternoon > 2 ||
                counts.Total > 4);

            summaries.Add(new ResourceSequencePressureSummary(
                Resource: resource,
                TotalAfternoonToMorningSequences: totalAfternoonToMorning,
                TotalNightToAfternoonSequences: totalNightToAfternoon,
                TotalSupportedSequences: totalAfternoonToMorning + totalNightToAfternoon,
                MaxMonthlyAfternoonToMorningSequences: maxMonthlyAfternoonToMorning,
                MaxMonthlyNightToAfternoonSequences: maxMonthlyNightToAfternoon,
                MaxMonthlyTotalSequences: maxMonthlyTotal,
                ExceedsMonthlyQuota: exceedsMonthlyQuota));
        }

        return new SequencePressureMetrics(
            ResourceSummaries: summaries,
            TotalAfternoonToMorningSequences: summaries.Sum(summary => summary.TotalAfternoonToMorningSequences),
            TotalNightToAfternoonSequences: summaries.Sum(summary => summary.TotalNightToAfternoonSequences),
            TotalSupportedSequences: summaries.Sum(summary => summary.TotalSupportedSequences),
            MaxMonthlyAfternoonToMorningSequencesForSingleResource: summaries.Max(summary => summary.MaxMonthlyAfternoonToMorningSequences),
            MaxMonthlyNightToAfternoonSequencesForSingleResource: summaries.Max(summary => summary.MaxMonthlyNightToAfternoonSequences),
            MaxMonthlyTotalSequencesForSingleResource: summaries.Max(summary => summary.MaxMonthlyTotalSequences),
            ResourcesWithAnySequenceCount: summaries.Count(summary => summary.TotalSupportedSequences > 0),
            ResourcesExceedingSequenceQuotaCount: summaries.Count(summary => summary.ExceedsMonthlyQuota));
    }

    private sealed class SequenceQuotaCounts
    {
        public int AfternoonToMorning { get; set; }
        public int NightToAfternoon { get; set; }
        public int Total { get; set; }
    }




    [Fact]
    [Trait("Category", "SchedulingExperiment")]
    public void CleanGeneticOptimizer_ShouldPrintVariantCMultiSeedStabilityReport()
    {
        var scenario = CreateScenario();

        var weights = CreateVariantCScoringWeights();

        var seeds = new[]
        {
            20260603,
            20260604,
            20260605
        };

        var runs = new List<MultiSeedSensitivityRun>();

        foreach (var seed in seeds)
        {
            var diagnosticsSink = new CollectingGeneticOptimizerDiagnosticsSink();

            var run = RunOptimizer(
                $"Clean GA Variant C Seed {seed}",
                new GeneticScheduleOptimizer(
                    populationSize: GeneticPopulationSize,
                    seed: seed,
                    generationCount: CleanGenerationCount,
                    eliteCount: 1,
                    tournamentSize: 3,
                    diagnosticsSink: diagnosticsSink,
                    evolutionMode: GeneticEvolutionMode.Clean,
                    scoringWeights: weights),
                scenario.Problem,
                diagnosticsSink.Diagnostics);

            AssertCandidateReferencesKnownProblemEntities(
                scenario,
                run.Result.Candidate);

            AssertNoBasicStructuralViolations(run.Result.Evaluation);

            Assert.Equal(
                CleanGenerationCount + 1,
                run.Diagnostics.Count);

            Assert.True(
                run.Diagnostics[^1].BestSoFarTotalPenalty <=
                run.Diagnostics[0].BestSoFarTotalPenalty,
                $"Seed {seed} should not return a best-so-far penalty worse than generation 0.");

            runs.Add(new MultiSeedSensitivityRun(
                Seed: seed,
                Run: run));
        }

        var report = FormatVariantCMultiSeedStabilityReport(
            scenario,
            weights,
            runs);

        System.Console.WriteLine(report);

        Assert.Contains("Stage 7.6C Variant C Multi-Seed Stability Diagnostic", report);
        Assert.Contains("Variant C", report);
        Assert.Contains("SeedSummary:", report);
        Assert.Contains("MultiSeedAggregateSummary:", report);
        Assert.Contains("MinTotalAssignedHours:", report);
        Assert.Contains("MaxTotalAssignedHours:", report);
        Assert.Contains("AverageTotalAssignedHours:", report);
        Assert.Contains("AnyRunReachedMaximumCapacity:", report);
        Assert.Contains("AnyRunHasHardViolations:", report);
    }


    private static string FormatVariantCMultiSeedStabilityReport(
        ExperimentScenario scenario,
        ScheduleScoringWeights weights,
        IReadOnlyCollection<MultiSeedSensitivityRun> runs)
    {
        var builder = new StringBuilder();

        builder.AppendLine("Stage 7.6C Variant C Multi-Seed Stability Diagnostic");
        builder.AppendLine("Mode: Clean GA");
        builder.AppendLine("Variant: Variant C");
        builder.AppendLine($"PopulationSize: {GeneticPopulationSize}");
        builder.AppendLine($"GenerationCount: {CleanGenerationCount}");
        builder.AppendLine("Purpose: Manual diagnostic only. Not part of regular verification.");
        builder.AppendLine();

        AppendScenarioCapacitySummary(builder, scenario);
        builder.AppendLine();

        AppendScoringWeightSummary(builder, weights);
        AppendNetTradeoffPerPreferred8HourAssignment(builder, weights);
        builder.AppendLine();

        foreach (var run in runs)
        {
            AppendMultiSeedRunSummary(
                builder,
                scenario,
                run);

            builder.AppendLine();
        }

        AppendMultiSeedAggregateSummary(
            builder,
            scenario,
            runs);

        return builder.ToString();
    }

    private static void AppendMultiSeedRunSummary(
        StringBuilder builder,
        ExperimentScenario scenario,
        MultiSeedSensitivityRun sensitivityRun)
    {
        var run = sensitivityRun.Run;
        var targetGap = CalculateTargetGapMetrics(
            scenario,
            run.Result.Candidate);

        builder.AppendLine("SeedSummary:");
        builder.AppendLine($"Seed: {sensitivityRun.Seed}");
        builder.AppendLine($"RuntimeMs: {run.Elapsed.TotalMilliseconds:0.00}");
        builder.AppendLine($"IsFeasible: {run.Result.Evaluation.IsFeasible}");
        builder.AppendLine($"Score.Value: {run.Result.Evaluation.Score.Value}");
        builder.AppendLine($"TotalPenalty: {run.Result.Evaluation.Score.TotalPenalty}");
        builder.AppendLine($"HardViolationCount: {run.Result.Evaluation.Score.HardViolationCount}");
        builder.AppendLine($"SoftViolationCount: {run.Result.Evaluation.Score.SoftViolationCount}");
        builder.AppendLine($"Assignments.Count: {run.Result.Candidate.Assignments.Count}");
        builder.AppendLine($"TotalAssignedHours: {CalculateTotalAssignedHours(scenario, run.Result.Candidate):0.##}");
        builder.AppendLine($"TotalOverTargetHours: {targetGap.TotalOverTargetHours:0.##}");
        builder.AppendLine($"TotalUnderTargetHours: {targetGap.TotalUnderTargetHours:0.##}");
        builder.AppendLine($"FirstBestSoFarTotalPenalty: {run.Diagnostics[0].BestSoFarTotalPenalty}");
        builder.AppendLine($"LastBestSoFarTotalPenalty: {run.Diagnostics[^1].BestSoFarTotalPenalty}");
        builder.AppendLine($"Improvement: {run.Diagnostics[0].BestSoFarTotalPenalty - run.Diagnostics[^1].BestSoFarTotalPenalty}");
        builder.AppendLine($"FirstFeasibleCandidateCount: {run.Diagnostics[0].FeasibleCandidateCount}");
        builder.AppendLine($"LastFeasibleCandidateCount: {run.Diagnostics[^1].FeasibleCandidateCount}");
    }


    private static void AppendMultiSeedAggregateSummary(
        StringBuilder builder,
        ExperimentScenario scenario,
        IReadOnlyCollection<MultiSeedSensitivityRun> runs)
    {
        var totalAssignedHours = runs
            .Select(run => CalculateTotalAssignedHours(
                scenario,
                run.Run.Result.Candidate))
            .ToArray();

        var targetGaps = runs
            .Select(run => CalculateTargetGapMetrics(
                scenario,
                run.Run.Result.Candidate))
            .ToArray();

        var maximumCapacityHours = scenario.Shifts.Sum(shift => shift.MaxResourceCount * GetShiftHours(shift));

        builder.AppendLine("MultiSeedAggregateSummary:");
        builder.AppendLine($"SeedCount: {runs.Count}");
        builder.AppendLine($"MinTotalAssignedHours: {totalAssignedHours.Min():0.##}");
        builder.AppendLine($"MaxTotalAssignedHours: {totalAssignedHours.Max():0.##}");
        builder.AppendLine($"AverageTotalAssignedHours: {totalAssignedHours.Average():0.##}");
        builder.AppendLine($"MinOverTargetHours: {targetGaps.Min(gap => gap.TotalOverTargetHours):0.##}");
        builder.AppendLine($"MaxOverTargetHours: {targetGaps.Max(gap => gap.TotalOverTargetHours):0.##}");
        builder.AppendLine($"AverageOverTargetHours: {targetGaps.Average(gap => gap.TotalOverTargetHours):0.##}");
        builder.AppendLine($"MinUnderTargetHours: {targetGaps.Min(gap => gap.TotalUnderTargetHours):0.##}");
        builder.AppendLine($"MaxUnderTargetHours: {targetGaps.Max(gap => gap.TotalUnderTargetHours):0.##}");
        builder.AppendLine($"AverageUnderTargetHours: {targetGaps.Average(gap => gap.TotalUnderTargetHours):0.##}");
        builder.AppendLine($"AnyRunReachedMaximumCapacity: {totalAssignedHours.Any(hours => Math.Abs(hours - maximumCapacityHours) < 0.000001)}");
        builder.AppendLine($"AnyRunHasHardViolations: {runs.Any(run => run.Run.Result.Evaluation.Score.HardViolationCount > 0)}");
    }

    private sealed record ResourceSequencePressureSummary(
        Resource Resource,
        int TotalAfternoonToMorningSequences,
        int TotalNightToAfternoonSequences,
        int TotalSupportedSequences,
        int MaxMonthlyAfternoonToMorningSequences,
        int MaxMonthlyNightToAfternoonSequences,
        int MaxMonthlyTotalSequences,
        bool ExceedsMonthlyQuota);

    private sealed record SequencePressureMetrics(
        IReadOnlyCollection<ResourceSequencePressureSummary> ResourceSummaries,
        int TotalAfternoonToMorningSequences,
        int TotalNightToAfternoonSequences,
        int TotalSupportedSequences,
        int MaxMonthlyAfternoonToMorningSequencesForSingleResource,
        int MaxMonthlyNightToAfternoonSequencesForSingleResource,
        int MaxMonthlyTotalSequencesForSingleResource,
        int ResourcesWithAnySequenceCount,
        int ResourcesExceedingSequenceQuotaCount);


    private sealed record MultiSeedSensitivityRun(
        int Seed,
        ExperimentRun Run);

    private static ScheduleScoringWeights CreateVariantCScoringWeights()
    {
        return ScheduleScoringWeights.CreateDefault() with
        {
            ResourceRequestedPreferredHoursNotSatisfiedPenaltyPerHour = 15,
            ResourceEffectiveTargetAssignedHoursAboveTargetPenaltyPerHour = 20
        };
    }


    private static IReadOnlyList<ScoringWeightVariant> CreateBalanceExcessPenaltyMultiSeedVariants()
    {
        var variantC = CreateVariantCScoringWeights();

        return new[]
        {
            new ScoringWeightVariant(
                Name: "BalanceExcess0",
                Weights: variantC with
                {
                    ResourceAssignedHoursBalanceExceededPenaltyPerExcessHour = 0
                }),

            new ScoringWeightVariant(
                Name: "BalanceExcess100",
                Weights: variantC with
                {
                    ResourceAssignedHoursBalanceExceededPenaltyPerExcessHour = 100
                })
        };
    }


    private static IReadOnlyList<ScoringWeightVariant> CreateBalanceExcessPenaltyWeightVariants()
    {
        var variantC = CreateVariantCScoringWeights();

        return new[]
        {
            new ScoringWeightVariant(
                Name: "BalanceExcess0",
                Weights: variantC with
                {
                    ResourceAssignedHoursBalanceExceededPenaltyPerExcessHour = 0
                }),

            new ScoringWeightVariant(
                Name: "BalanceExcess25",
                Weights: variantC with
                {
                    ResourceAssignedHoursBalanceExceededPenaltyPerExcessHour = 25
                }),

            new ScoringWeightVariant(
                Name: "BalanceExcess50",
                Weights: variantC with
                {
                    ResourceAssignedHoursBalanceExceededPenaltyPerExcessHour = 50
                }),

            new ScoringWeightVariant(
                Name: "BalanceExcess100",
                Weights: variantC with
                {
                    ResourceAssignedHoursBalanceExceededPenaltyPerExcessHour = 100
                })
        };
    }


    private static IReadOnlyList<ScoringWeightVariant> CreateManualScoringWeightSensitivityVariants()
    {
        var defaults = ScheduleScoringWeights.CreateDefault();

        return new[]
        {
            new ScoringWeightVariant(
                Name: "Current",
                Weights: defaults),

            new ScoringWeightVariant(
                Name: "Variant A",
                Weights: defaults with
                {
                    ResourceRequestedPreferredHoursNotSatisfiedPenaltyPerHour = 20,
                    ResourceEffectiveTargetAssignedHoursAboveTargetPenaltyPerHour = 20
                }),

            new ScoringWeightVariant(
                Name: "Variant B",
                Weights: defaults with
                {
                    ResourceRequestedPreferredHoursNotSatisfiedPenaltyPerHour = 10,
                    ResourceEffectiveTargetAssignedHoursAboveTargetPenaltyPerHour = 10
                }),

            new ScoringWeightVariant(
                Name: "Variant C",
                Weights: defaults with
                {
                    ResourceRequestedPreferredHoursNotSatisfiedPenaltyPerHour = 15,
                    ResourceEffectiveTargetAssignedHoursAboveTargetPenaltyPerHour = 20
                })
        };
    }

    private static string FormatManualScoringWeightSensitivityReport(
        ExperimentScenario scenario,
        IReadOnlyCollection<ScoringWeightSensitivityRun> runs)
    {
        var builder = new StringBuilder();

        builder.AppendLine("Stage 7.6B.2 Manual Scoring Weight Sensitivity Report");
        builder.AppendLine("Mode: Clean GA");
        builder.AppendLine($"Seed: {GeneticSeed}");
        builder.AppendLine($"PopulationSize: {GeneticPopulationSize}");
        builder.AppendLine($"GenerationCount: {CleanGenerationCount}");
        builder.AppendLine("Purpose: Manual diagnostic only. Not part of regular verification.");
        builder.AppendLine();

        AppendScenarioCapacitySummary(builder, scenario);
        builder.AppendLine();

        foreach (var run in runs)
        {
            AppendScoringWeightSensitivityRunSummary(
                builder,
                scenario,
                run);

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string FormatBalanceExcessPenaltyMultiSeedStabilityReport(
        ExperimentScenario scenario,
        double balanceToleranceHours,
        IReadOnlyCollection<int> seeds,
        IReadOnlyCollection<BalanceExcessMultiSeedRun> runs)
    {
        var builder = new StringBuilder();

        builder.AppendLine("Stage 7.7D Balance Excess Penalty Multi-Seed Stability Report");
        builder.AppendLine("Mode: Clean GA");
        builder.AppendLine("Variant: Variant C");
        builder.AppendLine($"PopulationSize: {GeneticPopulationSize}");
        builder.AppendLine($"GenerationCount: {CleanGenerationCount}");
        builder.AppendLine($"BalanceToleranceHours: {balanceToleranceHours:0.##}");
        builder.AppendLine($"SeedCount: {seeds.Count}");
        builder.AppendLine($"VariantCount: {runs.Select(run => run.SensitivityRun.Variant.Name).Distinct().Count()}");
        builder.AppendLine("Purpose: Manual diagnostic only. Not part of regular verification.");
        builder.AppendLine();

        AppendScenarioCapacitySummary(builder, scenario);
        builder.AppendLine();

        foreach (var run in runs.OrderBy(run => run.Seed).ThenBy(run => run.SensitivityRun.Variant.Name))
        {
            builder.AppendLine("BalanceExcessMultiSeedRunSummary:");
            builder.AppendLine($"Seed: {run.Seed}");

            AppendBalanceExcessPenaltyRunSummary(
                builder,
                scenario,
                balanceToleranceHours,
                run.SensitivityRun);

            builder.AppendLine();
        }

        AppendBalanceExcessMultiSeedAggregateSummary(
            builder,
            scenario,
            runs);

        return builder.ToString();
    }

    private static void AppendBalanceExcessMultiSeedAggregateSummary(
        StringBuilder builder,
        ExperimentScenario scenario,
        IReadOnlyCollection<BalanceExcessMultiSeedRun> runs)
    {
        builder.AppendLine("BalanceExcessMultiSeedAggregateSummary:");
        builder.AppendLine($"RunCount: {runs.Count}");
        builder.AppendLine($"AnyRunHasHardViolations: {runs.Any(run => run.SensitivityRun.Run.Result.Evaluation.Score.HardViolationCount > 0)}");
        builder.AppendLine($"AllRunsImprovedBestSoFar: {runs.All(run => run.SensitivityRun.Run.Diagnostics[^1].BestSoFarTotalPenalty <= run.SensitivityRun.Run.Diagnostics[0].BestSoFarTotalPenalty)}");
        builder.AppendLine();

        foreach (var group in runs.GroupBy(run => run.SensitivityRun.Variant.Name).OrderBy(group => group.Key))
        {
            var balanceMetrics = group
                .Select(run => CalculateAssignedHoursBalanceMetrics(
                    scenario,
                    run.SensitivityRun.Run.Result.Candidate))
                .ToArray();

            var scores = group
                .Select(run => run.SensitivityRun.Run.Result.Evaluation.Score)
                .ToArray();

            var assignedHours = group
                .Select(run => CalculateTotalAssignedHours(
                    scenario,
                    run.SensitivityRun.Run.Result.Candidate))
                .ToArray();

            var targetGaps = group
                .Select(run => CalculateTargetGapMetrics(
                    scenario,
                    run.SensitivityRun.Run.Result.Candidate))
                .ToArray();

            builder.AppendLine("VariantAggregateSummary:");
            builder.AppendLine($"VariantName: {group.Key}");
            builder.AppendLine($"SeedCount: {group.Count()}");
            builder.AppendLine($"MinTotalPenalty: {scores.Min(score => score.TotalPenalty)}");
            builder.AppendLine($"MaxTotalPenalty: {scores.Max(score => score.TotalPenalty)}");
            builder.AppendLine($"AverageTotalPenalty: {scores.Average(score => score.TotalPenalty):0.##}");
            builder.AppendLine($"AnyHardViolations: {scores.Any(score => score.HardViolationCount > 0)}");
            builder.AppendLine($"MinTotalAssignedHours: {assignedHours.Min():0.##}");
            builder.AppendLine($"MaxTotalAssignedHours: {assignedHours.Max():0.##}");
            builder.AppendLine($"AverageTotalAssignedHours: {assignedHours.Average():0.##}");
            builder.AppendLine($"AverageTotalOverTargetHours: {targetGaps.Average(metric => metric.TotalOverTargetHours):0.##}");
            builder.AppendLine($"AverageTotalUnderTargetHours: {targetGaps.Average(metric => metric.TotalUnderTargetHours):0.##}");
            builder.AppendLine($"MinMaxAssignedHoursDeviationFromAverage: {balanceMetrics.Min(metric => metric.MaxAssignedHoursDeviationFromAverage):0.##}");
            builder.AppendLine($"MaxMaxAssignedHoursDeviationFromAverage: {balanceMetrics.Max(metric => metric.MaxAssignedHoursDeviationFromAverage):0.##}");
            builder.AppendLine($"AverageMaxAssignedHoursDeviationFromAverage: {balanceMetrics.Average(metric => metric.MaxAssignedHoursDeviationFromAverage):0.##}");
            builder.AppendLine($"MinResourcesOutsideBalanceToleranceCount: {balanceMetrics.Min(metric => metric.ResourcesOutsideBalanceToleranceCount)}");
            builder.AppendLine($"MaxResourcesOutsideBalanceToleranceCount: {balanceMetrics.Max(metric => metric.ResourcesOutsideBalanceToleranceCount)}");
            builder.AppendLine($"AverageResourcesOutsideBalanceToleranceCount: {balanceMetrics.Average(metric => metric.ResourcesOutsideBalanceToleranceCount):0.##}");
            builder.AppendLine($"MinTotalBalanceExcessMagnitudeHours: {balanceMetrics.Min(metric => metric.TotalExcessDeviationHours):0.##}");
            builder.AppendLine($"MaxTotalBalanceExcessMagnitudeHours: {balanceMetrics.Max(metric => metric.TotalExcessDeviationHours):0.##}");
            builder.AppendLine($"AverageTotalBalanceExcessMagnitudeHours: {balanceMetrics.Average(metric => metric.TotalExcessDeviationHours):0.##}");
            builder.AppendLine();
        }
    }


    private static string FormatBalanceExcessPenaltyWeightDiagnosticReport(
        ExperimentScenario scenario,
        double balanceToleranceHours,
        IReadOnlyCollection<ScoringWeightSensitivityRun> runs)
    {
        var builder = new StringBuilder();

        builder.AppendLine("Stage 7.7C Balance Excess Penalty Weight Diagnostic");
        builder.AppendLine("Mode: Clean GA");
        builder.AppendLine("Variant: Variant C");
        builder.AppendLine($"Seed: {GeneticSeed}");
        builder.AppendLine($"PopulationSize: {GeneticPopulationSize}");
        builder.AppendLine($"GenerationCount: {CleanGenerationCount}");
        builder.AppendLine($"BalanceToleranceHours: {balanceToleranceHours:0.##}");
        builder.AppendLine("Purpose: Manual diagnostic only. Not part of regular verification.");
        builder.AppendLine();

        AppendScenarioCapacitySummary(builder, scenario);
        builder.AppendLine();

        foreach (var run in runs)
        {
            AppendBalanceExcessPenaltyRunSummary(
                builder,
                scenario,
                balanceToleranceHours,
                run);

            builder.AppendLine();
        }

        AppendBalanceExcessPenaltyAggregateSummary(
            builder,
            scenario,
            runs);

        return builder.ToString();
    }

    private static void AppendBalanceExcessPenaltyRunSummary(
        StringBuilder builder,
        ExperimentScenario scenario,
        double balanceToleranceHours,
        ScoringWeightSensitivityRun sensitivityRun)
    {
        var variant = sensitivityRun.Variant;
        var run = sensitivityRun.Run;
        var targetGap = CalculateTargetGapMetrics(
            scenario,
            run.Result.Candidate);

        builder.AppendLine("BalanceExcessVariantSummary:");
        builder.AppendLine($"VariantName: {variant.Name}");
        builder.AppendLine($"BalanceToleranceHours: {balanceToleranceHours:0.##}");
        builder.AppendLine($"BalanceBasePenalty: {variant.Weights.ResourceAssignedHoursBalanceExceededPenalty}");
        builder.AppendLine($"BalanceExcessPenaltyPerHour: {variant.Weights.ResourceAssignedHoursBalanceExceededPenaltyPerExcessHour}");

        AppendScoringWeightSummary(
            builder,
            variant.Weights);

        AppendCompactScoringSensitivityMetrics(
            builder,
            scenario,
            run);

        builder.AppendLine($"TotalOverTargetHours: {targetGap.TotalOverTargetHours:0.##}");
        builder.AppendLine($"TotalUnderTargetHours: {targetGap.TotalUnderTargetHours:0.##}");
        builder.AppendLine();

        AppendAssignedHoursBalanceMetrics(
            builder,
            scenario,
            run.Result.Candidate,
            run.Result.Evaluation);

        builder.AppendLine();

        AppendPenaltyBreakdownByType(
            builder,
            run.Result.Evaluation,
            variant.Weights);

        builder.AppendLine();

        AppendGenerationImprovementSummary(
            builder,
            run);

        AppendGenerationDiagnostics(
            builder,
            run);
    }

    private static void AppendBalanceExcessPenaltyAggregateSummary(
        StringBuilder builder,
        ExperimentScenario scenario,
        IReadOnlyCollection<ScoringWeightSensitivityRun> runs)
    {
        var metrics = runs
            .Select(run => CalculateAssignedHoursBalanceMetrics(
                scenario,
                run.Run.Result.Candidate))
            .ToArray();

        builder.AppendLine("BalanceExcessPenaltyAggregateSummary:");
        builder.AppendLine($"VariantCount: {runs.Count}");
        builder.AppendLine($"MinMaxAssignedHoursDeviationFromAverage: {metrics.Min(metric => metric.MaxAssignedHoursDeviationFromAverage):0.##}");
        builder.AppendLine($"MaxMaxAssignedHoursDeviationFromAverage: {metrics.Max(metric => metric.MaxAssignedHoursDeviationFromAverage):0.##}");
        builder.AppendLine($"MinResourcesOutsideBalanceToleranceCount: {metrics.Min(metric => metric.ResourcesOutsideBalanceToleranceCount)}");
        builder.AppendLine($"MaxResourcesOutsideBalanceToleranceCount: {metrics.Max(metric => metric.ResourcesOutsideBalanceToleranceCount)}");
        builder.AppendLine($"MinTotalBalanceExcessMagnitudeHours: {metrics.Min(metric => metric.TotalExcessDeviationHours):0.##}");
        builder.AppendLine($"MaxTotalBalanceExcessMagnitudeHours: {metrics.Max(metric => metric.TotalExcessDeviationHours):0.##}");
        builder.AppendLine($"AnyRunHasHardViolations: {runs.Any(run => run.Run.Result.Evaluation.Score.HardViolationCount > 0)}");
        builder.AppendLine($"AllRunsImprovedBestSoFar: {runs.All(run => run.Run.Diagnostics[^1].BestSoFarTotalPenalty <= run.Run.Diagnostics[0].BestSoFarTotalPenalty)}");
    }


    private static void AppendScoringWeightSensitivityRunSummary(
        StringBuilder builder,
        ExperimentScenario scenario,
        ScoringWeightSensitivityRun sensitivityRun)
    {
        var variant = sensitivityRun.Variant;
        var run = sensitivityRun.Run;

        builder.AppendLine("VariantSummary:");
        builder.AppendLine($"VariantName: {variant.Name}");

        AppendScoringWeightSummary(
            builder,
            variant.Weights);

        AppendNetTradeoffPerPreferred8HourAssignment(
            builder,
            variant.Weights);

        AppendCompactScoringSensitivityMetrics(
            builder,
            scenario,
            run);

        builder.AppendLine();

        AppendPenaltyBreakdownByType(
            builder,
            run.Result.Evaluation,
            variant.Weights);

        builder.AppendLine();

        AppendRequestedPreferredFulfillmentSummary(
            builder,
            scenario,
            run.Result.Candidate);

        builder.AppendLine();

        AppendTargetGapSummary(
            builder,
            scenario,
            run);

        builder.AppendLine();

        AppendGenerationImprovementSummary(
            builder,
            run);

        AppendGenerationDiagnostics(
            builder,
            run);
    }

    private static void AppendScoringWeightSummary(
        StringBuilder builder,
        ScheduleScoringWeights weights)
    {
        builder.AppendLine("ScoringWeights:");
        builder.AppendLine($"PreferWeight: {weights.ResourceRequestedPreferredHoursNotSatisfiedPenaltyPerHour}");
        builder.AppendLine($"AboveTargetWeight: {weights.ResourceEffectiveTargetAssignedHoursAboveTargetPenaltyPerHour}");
        builder.AppendLine($"BelowTargetWeight: {weights.ResourceEffectiveTargetAssignedHoursBelowTargetPenaltyPerHour}");
        builder.AppendLine($"AssignedHoursBalanceBasePenalty: {weights.ResourceAssignedHoursBalanceExceededPenalty}");
        builder.AppendLine($"AssignedHoursBalanceExcessPenaltyPerHour: {weights.ResourceAssignedHoursBalanceExceededPenaltyPerExcessHour}");
        builder.AppendLine($"IgnoredAvoidPreferencePenalty: {weights.IgnoredAvoidPreferencePenalty}");
        builder.AppendLine($"MotzeiShabbatPreferenceNotSatisfiedPenalty: {weights.ResourceMonthlyNightShiftPreferenceNotSatisfiedPenalty}");
    }

    private static void AppendNetTradeoffPerPreferred8HourAssignment(
        StringBuilder builder,
        ScheduleScoringWeights weights)
    {
        const int preferredShiftHours = 8;

        var avoidedPreferPenalty =
            preferredShiftHours *
            weights.ResourceRequestedPreferredHoursNotSatisfiedPenaltyPerHour;

        var addedAboveTargetPenalty =
            preferredShiftHours *
            weights.ResourceEffectiveTargetAssignedHoursAboveTargetPenaltyPerHour;

        var netPenaltyImprovement =
            avoidedPreferPenalty - addedAboveTargetPenalty;

        builder.AppendLine("NetTradeoffPerPreferred8HourAssignment:");
        builder.AppendLine($"PreferredShiftHours: {preferredShiftHours}");
        builder.AppendLine($"AvoidedPreferPenalty: {avoidedPreferPenalty}");
        builder.AppendLine($"AddedAboveTargetPenalty: {addedAboveTargetPenalty}");
        builder.AppendLine($"NetPenaltyImprovement: {netPenaltyImprovement}");
    }

    private static void AppendCompactScoringSensitivityMetrics(
        StringBuilder builder,
        ExperimentScenario scenario,
        ExperimentRun run)
    {
        var evaluation = run.Result.Evaluation;

        builder.AppendLine("RunMetrics:");
        builder.AppendLine($"RuntimeMs: {run.Elapsed.TotalMilliseconds:0.00}");
        builder.AppendLine($"IsFeasible: {evaluation.IsFeasible}");
        builder.AppendLine($"Score.Value: {evaluation.Score.Value}");
        builder.AppendLine($"TotalPenalty: {evaluation.Score.TotalPenalty}");
        builder.AppendLine($"HardViolationCount: {evaluation.Score.HardViolationCount}");
        builder.AppendLine($"SoftViolationCount: {evaluation.Score.SoftViolationCount}");
        builder.AppendLine($"Assignments.Count: {run.Result.Candidate.Assignments.Count}");
        builder.AppendLine($"TotalAssignedHours: {CalculateTotalAssignedHours(scenario, run.Result.Candidate):0.##}");
    }

    private static double CalculateTotalAssignedHours(
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        var shiftsById = scenario.Shifts
            .ToDictionary(shift => shift.Id);

        return candidate.Assignments.Sum(assignment =>
            GetShiftHours(shiftsById[assignment.ShiftId]));
    }

    private sealed record ScoringWeightVariant(
        string Name,
        ScheduleScoringWeights Weights);

    private sealed record ScoringWeightSensitivityRun(
        ScoringWeightVariant Variant,
        ExperimentRun Run);

    private static void AppendScenarioCapacitySummary(
        StringBuilder builder,
        ExperimentScenario scenario)
    {
        var totalMinimumCapacityHours = scenario.Shifts
            .Sum(shift => GetShiftHours(shift) * shift.MinResourceCount);

        var totalMaximumCapacityHours = scenario.Shifts
            .Sum(shift => GetShiftHours(shift) * shift.MaxResourceCount);

        var totalEffectiveTargetHours = scenario.ResourceWorkloadDemands
            .Sum(demand => demand.EffectiveTargetHours);

        var totalSubmittedPreferredHours = scenario.Resources
            .Sum(resource => GetSubmittedPreferredHours(
                resource,
                scenario.Shifts,
                scenario.ResourcePreferences));

        builder.AppendLine("ScenarioCapacitySummary:");
        builder.AppendLine($"ResourceCount: {scenario.Resources.Count}");
        builder.AppendLine($"ShiftCount: {scenario.Shifts.Count}");
        builder.AppendLine($"TotalMinimumCapacityHours: {totalMinimumCapacityHours:0.##}");
        builder.AppendLine($"TotalMaximumCapacityHours: {totalMaximumCapacityHours:0.##}");
        builder.AppendLine($"TotalEffectiveTargetHours: {totalEffectiveTargetHours:0.##}");
        builder.AppendLine($"TotalSubmittedPreferredHours: {totalSubmittedPreferredHours:0.##}");
    }

    private static void AppendPenaltyBreakdownByType(
        StringBuilder builder,
        ScheduleEvaluationResult evaluation,
        ScheduleScoringWeights? scoringWeights = null)
    {
        builder.AppendLine("PenaltyBreakdownByType:");

        var groups = evaluation.Violations
            .GroupBy(violation => violation.Type)
            .OrderBy(group => group.Key.ToString())
            .ToArray();

        if (groups.Length == 0)
        {
            builder.AppendLine("- none");
            return;
        }

        var calculator = new ScheduleScoreCalculator(
            scoringWeights ?? ScheduleScoringWeights.CreateDefault());

        foreach (var group in groups)
        {
            var violations = group.ToArray();

            var totalMagnitude = violations
                .Where(violation => violation.Magnitude.HasValue)
                .Sum(violation => violation.Magnitude!.Value);

            var estimatedPenalty = calculator
                .Calculate(violations)
                .TotalPenalty;

            builder.AppendLine(
                $"- {group.Key}: " +
                $"Count={violations.Length}, " +
                $"TotalMagnitude={totalMagnitude:0.##}, " +
                $"EstimatedPenalty={estimatedPenalty}");
        }
    }

    private static void AppendRequestedPreferredFulfillmentSummary(
        StringBuilder builder,
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        var requestedKeys = GetRequestedPreferredShiftKeys(scenario);
        var requestedKeySet = requestedKeys.ToHashSet();

        var assignedKeys = candidate.Assignments
            .Select(assignment => (assignment.ResourceId, assignment.ShiftId))
            .Where(requestedKeySet.Contains)
            .Distinct()
            .ToArray();

        var assignedKeySet = assignedKeys.ToHashSet();

        var unsatisfiedKeys = requestedKeys
            .Where(key => !assignedKeySet.Contains(key))
            .ToArray();

        var shiftsById = scenario.Shifts
            .ToDictionary(shift => shift.Id);

        var totalRequestedPreferredHours = requestedKeys
            .Sum(key => GetShiftHours(shiftsById[key.ShiftId]));

        var assignedRequestedPreferredHours = assignedKeys
            .Sum(key => GetShiftHours(shiftsById[key.ShiftId]));

        var unsatisfiedRequestedPreferredHours = unsatisfiedKeys
            .Sum(key => GetShiftHours(shiftsById[key.ShiftId]));

        builder.AppendLine("RequestedPreferredFulfillmentSummary:");
        builder.AppendLine($"RequestedPreferredShiftCount: {requestedKeys.Count}");
        builder.AppendLine($"AssignedRequestedPreferredShiftCount: {assignedKeys.Length}");
        builder.AppendLine($"UnsatisfiedRequestedPreferredShiftCount: {unsatisfiedKeys.Length}");
        builder.AppendLine($"TotalRequestedPreferredHours: {totalRequestedPreferredHours:0.##}");
        builder.AppendLine($"AssignedRequestedPreferredHours: {assignedRequestedPreferredHours:0.##}");
        builder.AppendLine($"UnsatisfiedRequestedPreferredHours: {unsatisfiedRequestedPreferredHours:0.##}");
    }

    private static IReadOnlyList<(Guid ResourceId, Guid ShiftId)> GetRequestedPreferredShiftKeys(
        ExperimentScenario scenario)
    {
        var keys = new HashSet<(Guid ResourceId, Guid ShiftId)>();

        foreach (var preference in scenario.ResourcePreferences
                     .Where(preference => preference.Type == ResourcePreferenceType.Prefer))
        {
            foreach (var shift in scenario.Shifts.Where(shift => Overlaps(
                         preference.StartUtc,
                         preference.EndUtc,
                         shift.StartUtc,
                         shift.EndUtc)))
            {
                keys.Add((preference.ResourceId, shift.Id));
            }
        }

        return keys
            .OrderBy(key => key.ResourceId)
            .ThenBy(key => key.ShiftId)
            .ToArray();
    }

    private static void AppendGenerationImprovementSummary(
        StringBuilder builder,
        ExperimentRun run)
    {
        builder.AppendLine("GenerationImprovementSummary:");

        if (run.Diagnostics.Count == 0)
        {
            builder.AppendLine("- none");
            return;
        }

        var first = run.Diagnostics[0];
        var last = run.Diagnostics[^1];

        builder.AppendLine($"FirstBestSoFarTotalPenalty: {first.BestSoFarTotalPenalty}");
        builder.AppendLine($"LastBestSoFarTotalPenalty: {last.BestSoFarTotalPenalty}");
        builder.AppendLine($"Improvement: {first.BestSoFarTotalPenalty - last.BestSoFarTotalPenalty}");
        builder.AppendLine($"FirstFeasibleCandidateCount: {first.FeasibleCandidateCount}");
        builder.AppendLine($"LastFeasibleCandidateCount: {last.FeasibleCandidateCount}");
    }

    private static string FormatDeterministicCleanRepairReport(
        ExperimentScenario scenario,
        ExperimentRun deterministic,
        ExperimentRun clean,
        ExperimentRun repair)
    {
        var builder = new StringBuilder();
        var ranker = new ScheduleEvaluationResultRanker();

        builder.AppendLine("Real World Biweekly Clean vs RepairAssisted GA Comparison");
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

    private static string FormatDeterministicVsCleanReport(
        ExperimentRun deterministic,
        ExperimentRun clean)
    {
        var builder = new StringBuilder();
        var ranker = new ScheduleEvaluationResultRanker();

        builder.AppendLine("Real World Biweekly Clean GA Comparison");
        builder.AppendLine();

        AppendRunSummary(builder, deterministic);
        builder.AppendLine();

        AppendRunSummary(builder, clean);
        builder.AppendLine();

        builder.AppendLine("Comparison:");
        builder.AppendLine(
            $"CleanRankedBetterThanDeterministic: {ranker.IsBetterThan(clean.Result.Evaluation, deterministic.Result.Evaluation)}");
        builder.AppendLine(
            $"DeterministicRankedBetterThanClean: {ranker.IsBetterThan(deterministic.Result.Evaluation, clean.Result.Evaluation)}");
        builder.AppendLine(
            $"PenaltyDelta: {deterministic.Result.Evaluation.Score.TotalPenalty - clean.Result.Evaluation.Score.TotalPenalty}");

        return builder.ToString();
    }

    private static void AppendRunSummary(
        StringBuilder builder,
        ExperimentScenario scenario,
        ExperimentRun run)
    {
        AppendRunSummary(builder, run);
        AppendTargetGapSummary(builder, scenario, run);
        AppendMonthlyNightQuotaSummary(builder, scenario, run);
        AppendUnsatisfiedMotzeiShabbatPreferenceSummary(builder, scenario, run);
        AppendResourceTargetSummary(builder, scenario, run);
    }

    private static void AppendRunSummary(
        StringBuilder builder,
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
        AppendGenerationDiagnostics(builder, run);
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

    private static void AppendUnsatisfiedMotzeiShabbatPreferenceSummary(
        StringBuilder builder,
        ExperimentScenario scenario,
        ExperimentRun run)
    {
        var count = CountUnsatisfiedMotzeiShabbatPreferenceRequests(
            scenario,
            run.Result.Candidate);

        builder.AppendLine(
            $"UnsatisfiedMotzeiShabbatPreferenceRequestCount: {count}");
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
            "- Resource: EffectiveTargetHours, AssignedHours, GapToTarget, RegularNightAssignments, MotzeiShabbatNightAssignments");

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
                $"MotzeiShabbatNightAssignments={motzeiShabbatNightAssignments}");
        }
    }

    private static void AppendAssignedHoursBalanceMetrics(
        StringBuilder builder,
        ExperimentScenario scenario,
        ScheduleCandidate candidate,
        ScheduleEvaluationResult evaluation)
    {
        var metrics = CalculateAssignedHoursBalanceMetrics(
            scenario,
            candidate);

        var balanceViolations = evaluation.Violations
            .Where(violation => violation.Type == ConstraintViolationType.ResourceAssignedHoursBalanceExceeded)
            .ToArray();

        var violationMagnitude = balanceViolations
            .Where(violation => violation.Magnitude.HasValue)
            .Sum(violation => violation.Magnitude!.Value);

        builder.AppendLine("AssignedHoursBalanceMetrics:");
        builder.AppendLine($"AverageAssignedHours: {metrics.AverageAssignedHours:0.##}");
        builder.AppendLine($"MaxAssignedHoursDeviationFromAverage: {metrics.MaxAssignedHoursDeviationFromAverage:0.##}");
        builder.AppendLine($"AverageAssignedHoursDeviationFromAverage: {metrics.AverageAssignedHoursDeviationFromAverage:0.##}");
        builder.AppendLine($"ResourcesOutsideBalanceToleranceCount: {metrics.ResourcesOutsideBalanceToleranceCount}");
        builder.AppendLine($"TotalBalanceExcessMagnitudeHours: {metrics.TotalExcessDeviationHours:0.##}");
        builder.AppendLine($"MaxBalanceExcessMagnitudeHours: {metrics.MaxExcessDeviationHours:0.##}");
        builder.AppendLine($"BalanceViolationCountFromEvaluation: {balanceViolations.Length}");
        builder.AppendLine($"BalanceViolationTotalMagnitudeFromEvaluation: {violationMagnitude:0.##}");
    }

    private static AssignedHoursBalanceMetrics CalculateAssignedHoursBalanceMetrics(
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        var shiftsById = scenario.Shifts
            .ToDictionary(shift => shift.Id);

        var assignedHoursByResourceId = candidate.Assignments
            .GroupBy(assignment => assignment.ResourceId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(assignment =>
                    GetShiftHours(shiftsById[assignment.ShiftId])));

        var totalAssignedHours = scenario.Resources.Sum(resource =>
            assignedHoursByResourceId.GetValueOrDefault(
                resource.Id,
                0.0));

        var averageAssignedHours =
            totalAssignedHours / scenario.Resources.Count;

        var toleranceHours =
            scenario.Problem.MaximumAssignedHoursDeviationFromAverageHours ?? 0.0;

        var deviations = scenario.Resources
            .Select(resource =>
            {
                var assignedHours = assignedHoursByResourceId.GetValueOrDefault(
                    resource.Id,
                    0.0);

                return Math.Abs(assignedHours - averageAssignedHours);
            })
            .ToArray();

        var excessDeviationHours = deviations
            .Select(deviation => Math.Max(0.0, deviation - toleranceHours))
            .ToArray();

        return new AssignedHoursBalanceMetrics(
            AverageAssignedHours: averageAssignedHours,
            MaxAssignedHoursDeviationFromAverage: deviations.Length == 0 ? 0.0 : deviations.Max(),
            AverageAssignedHoursDeviationFromAverage: deviations.Length == 0 ? 0.0 : deviations.Average(),
            ResourcesOutsideBalanceToleranceCount: excessDeviationHours.Count(excess => excess > HoursTolerance),
            TotalExcessDeviationHours: excessDeviationHours.Sum(),
            MaxExcessDeviationHours: excessDeviationHours.Length == 0 ? 0.0 : excessDeviationHours.Max());
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

    private static int CountUnsatisfiedMotzeiShabbatPreferenceRequests(
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        var motzeiShabbatShifts = scenario.Shifts
            .Where(shift => shift.NightShiftCategory == NightShiftCategory.MotzeiShabbatNight)
            .ToArray();

        if (motzeiShabbatShifts.Length == 0)
        {
            return 0;
        }

        var requestedKeys = new HashSet<(Guid ResourceId, int Year, int Month)>();

        foreach (var preference in scenario.ResourcePreferences
                     .Where(preference => preference.Type == ResourcePreferenceType.Prefer))
        {
            foreach (var shift in motzeiShabbatShifts.Where(shift => Overlaps(
                         preference.StartUtc,
                         preference.EndUtc,
                         shift.StartUtc,
                         shift.EndUtc)))
            {
                requestedKeys.Add((
                    preference.ResourceId,
                    shift.StartUtc.Year,
                    shift.StartUtc.Month));
            }
        }

        var satisfiedKeys = new HashSet<(Guid ResourceId, int Year, int Month)>();

        foreach (var history in scenario.Problem.ResourceMonthlyNightShiftHistories
                     .Where(history => history.NightShiftCategory == NightShiftCategory.MotzeiShabbatNight)
                     .Where(history => history.AssignedCount > 0))
        {
            satisfiedKeys.Add((
                history.ResourceId,
                history.Year,
                history.Month));
        }

        var shiftsById = scenario.Shifts
            .ToDictionary(shift => shift.Id);

        foreach (var assignment in candidate.Assignments)
        {
            var shift = shiftsById[assignment.ShiftId];

            if (shift.NightShiftCategory != NightShiftCategory.MotzeiShabbatNight)
            {
                continue;
            }

            satisfiedKeys.Add((
                assignment.ResourceId,
                shift.StartUtc.Year,
                shift.StartUtc.Month));
        }

        return requestedKeys
            .Count(requestedKey => !satisfiedKeys.Contains(requestedKey));
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

    private static void AssertNoBasicStructuralViolations(
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
                ConstraintViolationType.ShiftUnderstaffed));

        Assert.Equal(
            0,
            CountViolations(
                evaluation,
                ConstraintViolationType.ShiftOverstaffed));
    }

    private static void AssertMonthlyNightQuotaPerCategory(
        ExperimentScenario scenario,
        ScheduleCandidate candidate,
        string report)
    {
        var shiftsById = scenario.Shifts
            .ToDictionary(shift => shift.Id);

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
        return $"{shift.Kind} {shift.StartUtc:yyyy-MM-dd HH:mm}-{shift.EndUtc:yyyy-MM-dd HH:mm} UTC";
    }


    private sealed record IgnoredAvoidBurdenMetrics(
        int ViolationCount,
        double TotalMagnitude,
        double MaxMagnitude,
        int EstimatedPenalty);


    private sealed record BalanceExcessMultiSeedRun(
        int Seed,
        ScoringWeightSensitivityRun SensitivityRun);


    private sealed record AssignedHoursBalanceMetrics(
        double AverageAssignedHours,
        double MaxAssignedHoursDeviationFromAverage,
        double AverageAssignedHoursDeviationFromAverage,
        int ResourcesOutsideBalanceToleranceCount,
        double TotalExcessDeviationHours,
        double MaxExcessDeviationHours);


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

    private sealed record ExperimentScenario(
        SchedulingProblem Problem,
        IReadOnlyList<Resource> Resources,
        IReadOnlyList<Shift> Shifts,
        IReadOnlyList<AvailabilityWindow> AvailabilityWindows,
        IReadOnlyList<ResourcePreference> ResourcePreferences,
        IReadOnlyList<ResourceWorkloadDemand> ResourceWorkloadDemands);
}
