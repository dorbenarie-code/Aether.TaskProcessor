using Aether.Application.Scheduling.Contracts;
using Aether.Application.Scheduling.Optimization;
using Aether.Application.Scheduling.Reports;
using Aether.Domain.Optimization;
using Xunit.Abstractions;

namespace Aether.Tests.Application.Scheduling;

public sealed class RealWorldBiWeeklyGeneticPressureScenarioTests
{
    private const int MinimumAssignedHoursPerResource = 45;
    private const int GenerationCount = 80;

    private readonly ITestOutputHelper _output;

    public RealWorldBiWeeklyGeneticPressureScenarioTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Ranker_ShouldPreferIgnoredAvoidPreference_OverHardUnderstaffedViolation()
    {
        var resource = CreateResource("Guard01");

        var shift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 6, 3, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 3, 14, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning,
            minResourceCount: 1,
            maxResourceCount: 1);

        var problem = new SchedulingProblem(
            period: CreateBiWeeklyPeriod(),
            resources: [resource],
            shifts: [shift],
            availabilityWindows: [CreateAvailability(resource, shift)],
            resourcePreferences:
            [
                CreatePreference(
                    resource,
                    shift,
                    ResourcePreferenceType.Avoid)
            ]);

        var evaluator = new ScheduleEvaluator();

        var softSacrificeResult = evaluator.Evaluate(
            problem,
            new ScheduleCandidate(
            [
                new Assignment(resource.Id, shift.Id)
            ]));

        var hardFailureResult = evaluator.Evaluate(
            problem,
            new ScheduleCandidate([]));

        Assert.True(softSacrificeResult.IsFeasible);
        Assert.Equal(0, softSacrificeResult.Score.HardViolationCount);
        Assert.Contains(
            softSacrificeResult.Violations,
            violation => violation.Type == ConstraintViolationType.IgnoredAvoidPreference);

        Assert.False(hardFailureResult.IsFeasible);
        Assert.Contains(
            hardFailureResult.Violations,
            violation => violation.Type == ConstraintViolationType.ShiftUnderstaffed);

        var ranker = new ScheduleEvaluationResultRanker();

        Assert.True(ranker.IsBetterThan(
            softSacrificeResult,
            hardFailureResult));
    }

    [Fact]
    public void Evaluate_ShouldDetectExpectedHardAndSoftViolations_InBadManualCandidate()
    {
        var scenario = CreateBadManualConstraintScenario();
        var evaluator = new ScheduleEvaluator();

        var result = evaluator.Evaluate(
            scenario.Problem,
            scenario.BadCandidate);

        Assert.False(result.IsFeasible);
        Assert.True(result.Score.HardViolationCount > 0);
        Assert.True(result.Score.SoftViolationCount > 0);

        AssertContainsViolation(result, ConstraintViolationType.ResourceUnavailable);
        AssertContainsViolation(result, ConstraintViolationType.ResourceAssignedToOverlappingShifts);
        AssertContainsViolation(result, ConstraintViolationType.ResourceMinimumAssignedHoursNotMet);
        AssertContainsViolation(result, ConstraintViolationType.ResourceWeeklyMinimumShiftMixNotMet);
        AssertContainsViolation(result, ConstraintViolationType.ShiftUnderstaffed);
        AssertContainsViolation(result, ConstraintViolationType.ShiftOverstaffed);
        AssertContainsViolation(result, ConstraintViolationType.IgnoredAvoidPreference);
        AssertContainsViolation(result, ConstraintViolationType.ShiftSequenceQuotaExceeded);
    }

    [Fact]
    [Trait("Category", "SchedulingExperiment")]
    public void Run_ShouldImproveOrNotWorsenComparedToDeterministicBaseline_InRealWorldBiWeeklyPressureScenario()
    {
        var scenario = CreateRealWorldBiWeeklyScenario();

        Assert.Equal(MinimumAssignedHoursPerResource, scenario.Problem.MinimumAssignedHoursPerResource);
        AssertTotalCapacityCanSatisfyBiWeeklyMinimumHours(scenario.Problem);
        AssertEveryResourceSubmittedEnoughRegularShifts(scenario.Problem);
        AssertEveryMandatoryShiftHasEnoughAvailableResources(scenario.Problem);
        AssertAtLeastOneMotzeiShabbatNightExists(scenario.Problem);
        AssertAtLeastOneMotzeiShabbatNightHasPreferDemand(scenario.Problem);

        var deterministicOptimizationResult = new DeterministicScheduleOptimizer()
            .Optimize(scenario.Problem);

        var diagnosticsSink = new CollectingGeneticOptimizerDiagnosticsSink();

        var geneticOptimizationResult = new GeneticScheduleOptimizer(
                populationSize: 120,
                seed: 20260601,
                generationCount: GenerationCount,
                eliteCount: 1,
                tournamentSize: 3,
                diagnosticsSink: diagnosticsSink)
            .Optimize(scenario.Problem);

        var deterministicResult = CreateRunOptimizationResult(
            scenario.Problem,
            deterministicOptimizationResult,
            generationDiagnostics: []);

        var geneticResult = CreateRunOptimizationResult(
            scenario.Problem,
            geneticOptimizationResult,
            diagnosticsSink.Diagnostics);

        var runResult = CreateRunResult(
            scenario.Problem,
            deterministicResult,
            geneticResult);

        var report = new SchedulingRunReportFormatter().Format(runResult);

        _output.WriteLine(report);
        System.Console.WriteLine(report);

        Assert.Contains("Scheduling Run Report", report);
        Assert.Contains("Resources: 14", report);
        Assert.Contains("MinimumAssignedHoursPerResource: 45", report);
        Assert.Contains("Comparison", report);
        Assert.Contains("Best Result", report);
        Assert.Contains("LoadByResource", report);
        Assert.Contains("LoadBalance", report);
        Assert.Contains("ViolationsByType", report);
        Assert.Contains("AssignmentsByShift", report);
        Assert.Contains("GenerationDiagnostics", report);

        var ranker = new ScheduleEvaluationResultRanker();

        Assert.True(
            ranker.IsBetterThan(
                geneticResult.Evaluation,
                deterministicResult.Evaluation),
            report);

        Assert.True(
            geneticResult.Evaluation.Score.HardViolationCount <=
            deterministicResult.Evaluation.Score.HardViolationCount,
            report);

        Assert.True(
            geneticResult.Evaluation.Score.TotalPenalty <=
            deterministicResult.Evaluation.Score.TotalPenalty,
            report);

        Assert.True(
            CountViolations(geneticResult, ConstraintViolationType.ResourceMinimumAssignedHoursNotMet) <=
            CountViolations(deterministicResult, ConstraintViolationType.ResourceMinimumAssignedHoursNotMet),
            report);

        Assert.Equal(
            GenerationCount + 1,
            geneticResult.GenerationDiagnostics.Count);

        Assert.Equal(0, CountViolations(geneticResult, ConstraintViolationType.ResourceUnavailable));
        Assert.Equal(0, CountViolations(geneticResult, ConstraintViolationType.ResourceAssignedToOverlappingShifts));
    }

    private static BadManualConstraintScenario CreateBadManualConstraintScenario()
    {
        var amit = CreateResource("Amit");
        var dana = CreateResource("Dana");
        var noa = CreateResource("Noa");
        var gal = CreateResource("Gal");

        var resources = new[]
        {
            amit,
            dana,
            noa,
            gal
        };

        var overstaffedMorning = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 5, 31, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 5, 31, 14, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning,
            minResourceCount: 1,
            maxResourceCount: 1);

        var overlappingShift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 5, 31, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 5, 31, 16, 0, 0, DateTimeKind.Utc),
            ShiftKind.Afternoon,
            minResourceCount: 0,
            maxResourceCount: 1);

        var understaffedAfternoon = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 22, 40, 0, DateTimeKind.Utc),
            ShiftKind.Afternoon,
            minResourceCount: 2,
            maxResourceCount: 2);

        var unavailableMorning = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 6, 2, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 2, 14, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning,
            minResourceCount: 0,
            maxResourceCount: 1);

        var sequenceShifts = new[]
        {
            CreateSequenceNightShift(new DateOnly(2026, 6, 3)),
            CreateSequenceAfternoonShift(new DateOnly(2026, 6, 4)),
            CreateSequenceNightShift(new DateOnly(2026, 6, 6)),
            CreateSequenceAfternoonShift(new DateOnly(2026, 6, 7)),
            CreateSequenceNightShift(new DateOnly(2026, 6, 9)),
            CreateSequenceAfternoonShift(new DateOnly(2026, 6, 10))
        };

        var shifts = new[]
        {
            overstaffedMorning,
            overlappingShift,
            understaffedAfternoon,
            unavailableMorning
        }
        .Concat(sequenceShifts)
        .OrderBy(shift => shift.StartUtc)
        .ToArray();

        var availabilityWindows = new List<AvailabilityWindow>
        {
            CreateAvailability(amit, overstaffedMorning),
            CreateAvailability(dana, overstaffedMorning),
            CreateAvailability(amit, overlappingShift),
            CreateAvailability(noa, understaffedAfternoon)
        };

        foreach (var shift in sequenceShifts)
        {
            availabilityWindows.Add(CreateAvailability(amit, shift));
        }

        var preferences = new List<ResourcePreference>
        {
            CreatePreference(
                amit,
                overstaffedMorning,
                ResourcePreferenceType.Avoid)
        };

        var problem = new SchedulingProblem(
            period: CreateBiWeeklyPeriod(),
            resources: resources,
            shifts: shifts,
            availabilityWindows: availabilityWindows,
            resourcePreferences: preferences,
            minimumAssignedHoursPerResource: MinimumAssignedHoursPerResource,
            minimumMorningShiftsPerResourcePerFullWeek: 2,
            minimumAfternoonShiftsPerResourcePerFullWeek: 1);

        var badCandidate = new ScheduleCandidate(
        [
            new Assignment(amit.Id, overstaffedMorning.Id),
            new Assignment(dana.Id, overstaffedMorning.Id),
            new Assignment(amit.Id, overlappingShift.Id),
            new Assignment(noa.Id, understaffedAfternoon.Id),
            new Assignment(gal.Id, unavailableMorning.Id),
            .. sequenceShifts.Select(shift => new Assignment(amit.Id, shift.Id))
        ]);

        return new BadManualConstraintScenario(
            problem,
            badCandidate);
    }

    private static RealWorldBiWeeklyScenario CreateRealWorldBiWeeklyScenario()
    {
        var resources = Enumerable
            .Range(1, 14)
            .Select(index => CreateResource($"Guard{index:00}"))
            .ToArray();

        var regularShifts = CreateRegularBiWeeklyShifts();
        var optionalNightShifts = CreateOptionalNightShifts();

        var shifts = regularShifts
            .Concat(optionalNightShifts)
            .OrderBy(shift => shift.StartUtc)
            .ToArray();

        var availabilityWindows = new List<AvailabilityWindow>();
        var preferences = new List<ResourcePreference>();

        for (var resourceIndex = 0; resourceIndex < resources.Length; resourceIndex++)
        {
            foreach (var shift in regularShifts)
            {
                if (!ShouldSubmitRegularShift(resourceIndex, shift))
                {
                    continue;
                }

                if (ShouldCreateAvoidTrap(resourceIndex, shift))
                {
                    AddAvoid(
                        resources[resourceIndex],
                        shift,
                        availabilityWindows,
                        preferences);
                    continue;
                }

                AddSubmission(
                    resources[resourceIndex],
                    shift,
                    availabilityWindows,
                    preferences);
            }
        }

        AddOptionalNightSubmissions(
            resources,
            optionalNightShifts,
            availabilityWindows,
            preferences);

        var problem = new SchedulingProblem(
            period: CreateBiWeeklyPeriod(),
            resources: resources,
            shifts: shifts,
            availabilityWindows: availabilityWindows,
            resourcePreferences: preferences,
            minimumAssignedHoursPerResource: MinimumAssignedHoursPerResource,
            minimumMorningShiftsPerResourcePerFullWeek: 2,
            minimumAfternoonShiftsPerResourcePerFullWeek: 1);

        return new RealWorldBiWeeklyScenario(problem);
    }

    private static IReadOnlyList<Shift> CreateRegularBiWeeklyShifts()
    {
        var shifts = new List<Shift>();

        for (var dayOffset = 0; dayOffset < 14; dayOffset++)
        {
            var date = new DateOnly(2026, 5, 31).AddDays(dayOffset);

            shifts.Add(CreateRegularShift(date, ShiftKind.Morning));
            shifts.Add(CreateRegularShift(date, ShiftKind.Afternoon));
        }

        return shifts;
    }

    private static IReadOnlyList<Shift> CreateOptionalNightShifts()
    {
        return
        [
            CreateOptionalNightShift(
                new DateOnly(2026, 6, 2),
                NightShiftCategory.Regular),

            CreateOptionalNightShift(
                new DateOnly(2026, 6, 5),
                NightShiftCategory.FridayNight),

            CreateOptionalNightShift(
                new DateOnly(2026, 6, 6),
                NightShiftCategory.MotzeiShabbatNight),

            CreateOptionalNightShift(
                new DateOnly(2026, 6, 9),
                NightShiftCategory.Regular),

            CreateOptionalNightShift(
                new DateOnly(2026, 6, 12),
                NightShiftCategory.FridayNight),

            CreateOptionalNightShift(
                new DateOnly(2026, 6, 13),
                NightShiftCategory.MotzeiShabbatNight)
        ];
    }

    private static bool ShouldSubmitRegularShift(
        int resourceIndex,
        Shift shift)
    {
        if (IsFridayOrSaturday(shift))
        {
            return IsWeekendBottleneckSubmission(resourceIndex, shift);
        }

        var dateNumber = shift.StartUtc.Day;
        var kindOffset = shift.Kind == ShiftKind.Morning ? 0 : 3;

        return (resourceIndex + dateNumber + kindOffset) % 7 != 0;
    }

    private static bool IsWeekendBottleneckSubmission(
        int resourceIndex,
        Shift shift)
    {
        var baseIndex = (shift.StartUtc.Day + (shift.Kind == ShiftKind.Morning ? 0 : 2)) % 14;

        return resourceIndex == baseIndex ||
               resourceIndex == (baseIndex + 1) % 14 ||
               resourceIndex == (baseIndex + 2) % 14 ||
               resourceIndex == (baseIndex + 5) % 14 ||
               resourceIndex == (baseIndex + 8) % 14;
    }

    private static bool ShouldCreateAvoidTrap(
        int resourceIndex,
        Shift shift)
    {
        return resourceIndex == 0 &&
               shift.StartUtc.Date == new DateTime(2026, 6, 3).Date &&
               shift.Kind == ShiftKind.Morning
            || resourceIndex == 3 &&
               shift.StartUtc.Date == new DateTime(2026, 6, 5).Date &&
               shift.Kind == ShiftKind.Morning
            || resourceIndex == 7 &&
               shift.StartUtc.Date == new DateTime(2026, 6, 10).Date &&
               shift.Kind == ShiftKind.Afternoon;
    }

    private static void AddOptionalNightSubmissions(
        IReadOnlyList<Resource> resources,
        IReadOnlyList<Shift> nightShifts,
        ICollection<AvailabilityWindow> availabilityWindows,
        ICollection<ResourcePreference> preferences)
    {
        for (var i = 0; i < nightShifts.Count; i++)
        {
            var firstResource = resources[(i * 2) % resources.Count];
            var secondResource = resources[((i * 2) + 5) % resources.Count];
            var thirdResource = resources[((i * 2) + 9) % resources.Count];

            AddSubmission(firstResource, nightShifts[i], availabilityWindows, preferences);
            AddSubmission(secondResource, nightShifts[i], availabilityWindows, preferences);
            AddSubmission(thirdResource, nightShifts[i], availabilityWindows, preferences);
        }
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
            problem,
            Warnings: [],
            deterministicResult,
            geneticResult,
            comparison);
    }

    private static SchedulingRunOptimizationResult CreateRunOptimizationResult(
        SchedulingProblem problem,
        ScheduleOptimizationResult optimizationResult,
        IReadOnlyCollection<GeneticGenerationDiagnostic> generationDiagnostics)
    {
        var shiftsById = problem.Shifts.ToDictionary(shift => shift.Id);

        var loadByResource = problem.Resources
            .Select(resource =>
            {
                var resourceAssignments = optimizationResult.Candidate.Assignments
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

        var violationsByType = optimizationResult.Evaluation.Violations
            .GroupBy(violation => violation.Type)
            .ToDictionary(
                group => group.Key,
                group => group.Count());

        return new SchedulingRunOptimizationResult(
            optimizationResult.Candidate,
            optimizationResult.Evaluation,
            loadByResource,
            violationsByType,
            generationDiagnostics);
    }

    private static void AssertTotalCapacityCanSatisfyBiWeeklyMinimumHours(
        SchedulingProblem problem)
    {
        var totalCapacityHours = problem.Shifts.Sum(shift =>
            (shift.EndUtc - shift.StartUtc).TotalHours * shift.MaxResourceCount);

        var requiredMinimumHours = problem.Resources.Count *
                                   problem.MinimumAssignedHoursPerResource;

        Assert.True(
            totalCapacityHours >= requiredMinimumHours,
            $"Total capacity {totalCapacityHours:0.##}h is below required minimum {requiredMinimumHours:0.##}h.");
    }

    private static void AssertEveryResourceSubmittedEnoughRegularShifts(
        SchedulingProblem problem)
    {
        foreach (var resource in problem.Resources)
        {
            var regularPreferCount = problem.ResourcePreferences
                .Where(preference => preference.ResourceId == resource.Id)
                .Where(preference => preference.Type == ResourcePreferenceType.Prefer)
                .Count(preference => problem.Shifts
                    .Where(shift => shift.Kind is ShiftKind.Morning or ShiftKind.Afternoon)
                    .Any(shift => Overlaps(shift, preference)));

            Assert.True(
                regularPreferCount >= 6,
                $"{resource.Name} must submit at least 6 regular shifts for the bi-weekly 45h minimum scenario.");
        }
    }

    private static void AssertEveryMandatoryShiftHasEnoughAvailableResources(
        SchedulingProblem problem)
    {
        foreach (var shift in problem.Shifts.Where(shift => shift.MinResourceCount > 0))
        {
            var availableResourceCount = problem.Resources.Count(resource =>
                problem.AvailabilityWindows.Any(window =>
                    window.ResourceId == resource.Id &&
                    window.Covers(shift)));

            Assert.True(
                availableResourceCount >= shift.MinResourceCount,
                $"Shift {shift.StartUtc:yyyy-MM-dd HH:mm} {shift.Kind} has only {availableResourceCount} available resources for min {shift.MinResourceCount}.");
        }
    }

    private static void AssertAtLeastOneMotzeiShabbatNightExists(
        SchedulingProblem problem)
    {
        Assert.Contains(
            problem.Shifts,
            shift => shift.Kind == ShiftKind.Night &&
                     shift.NightShiftCategory == NightShiftCategory.MotzeiShabbatNight);
    }

    private static void AssertAtLeastOneMotzeiShabbatNightHasPreferDemand(
        SchedulingProblem problem)
    {
        var hasMotzeiShabbatPreferDemand = problem.Shifts
            .Where(shift => shift.Kind == ShiftKind.Night)
            .Where(shift => shift.NightShiftCategory == NightShiftCategory.MotzeiShabbatNight)
            .Any(shift => problem.ResourcePreferences
                .Where(preference => preference.Type == ResourcePreferenceType.Prefer)
                .Any(preference => Overlaps(shift, preference)));

        Assert.True(
            hasMotzeiShabbatPreferDemand,
            "At least one Motzei Shabbat night shift should have prefer demand.");
    }

    private static void AssertContainsViolation(
        ScheduleEvaluationResult result,
        ConstraintViolationType type)
    {
        Assert.Contains(
            result.Violations,
            violation => violation.Type == type);
    }

    private static int CountViolations(
        SchedulingRunOptimizationResult result,
        ConstraintViolationType type)
    {
        return result.ViolationsByType.TryGetValue(type, out var count)
            ? count
            : 0;
    }

    private static void AddSubmission(
        Resource resource,
        Shift shift,
        ICollection<AvailabilityWindow> availabilityWindows,
        ICollection<ResourcePreference> preferences)
    {
        availabilityWindows.Add(CreateAvailability(resource, shift));
        preferences.Add(CreatePreference(resource, shift, ResourcePreferenceType.Prefer));
    }

    private static void AddAvoid(
        Resource resource,
        Shift shift,
        ICollection<AvailabilityWindow> availabilityWindows,
        ICollection<ResourcePreference> preferences)
    {
        availabilityWindows.Add(CreateAvailability(resource, shift));
        preferences.Add(CreatePreference(resource, shift, ResourcePreferenceType.Avoid));
    }

    private static AvailabilityWindow CreateAvailability(
        Resource resource,
        Shift shift)
    {
        return new AvailabilityWindow(
            resource.Id,
            shift.StartUtc,
            shift.EndUtc);
    }

    private static ResourcePreference CreatePreference(
        Resource resource,
        Shift shift,
        ResourcePreferenceType type)
    {
        return new ResourcePreference(
            resource.Id,
            shift.StartUtc,
            shift.EndUtc,
            type,
            ResourcePreferencePriority.High);
    }

    private static Resource CreateResource(string name)
    {
        return new Resource(
            Guid.NewGuid(),
            name,
            hourlyCost: 100m);
    }

    private static SchedulePeriod CreateBiWeeklyPeriod()
    {
        return new SchedulePeriod(
            new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc));
    }

    private static Shift CreateRegularShift(
        DateOnly date,
        ShiftKind kind)
    {
        return new Shift(
            Guid.NewGuid(),
            GetStartUtc(date, kind),
            GetEndUtc(date, kind),
            kind,
            minResourceCount: 3,
            maxResourceCount: 4);
    }

    private static Shift CreateOptionalNightShift(
        DateOnly date,
        NightShiftCategory nightShiftCategory)
    {
        return new Shift(
            Guid.NewGuid(),
            date.ToDateTime(
                new TimeOnly(22, 30),
                DateTimeKind.Utc),
            date.AddDays(1).ToDateTime(
                new TimeOnly(6, 40),
                DateTimeKind.Utc),
            ShiftKind.Night,
            minResourceCount: 0,
            maxResourceCount: 1,
            requiresPreferenceToAssign: true,
            requiresMinimumWhenPreferenceExists: true,
            nightShiftCategory: nightShiftCategory);
    }

    private static Shift CreateSequenceNightShift(DateOnly date)
    {
        return new Shift(
            Guid.NewGuid(),
            date.ToDateTime(
                new TimeOnly(22, 30),
                DateTimeKind.Utc),
            date.AddDays(1).ToDateTime(
                new TimeOnly(6, 40),
                DateTimeKind.Utc),
            ShiftKind.Night,
            minResourceCount: 0,
            maxResourceCount: 1);
    }

    private static Shift CreateSequenceAfternoonShift(DateOnly date)
    {
        return new Shift(
            Guid.NewGuid(),
            date.ToDateTime(
                new TimeOnly(14, 0),
                DateTimeKind.Utc),
            date.ToDateTime(
                new TimeOnly(22, 0),
                DateTimeKind.Utc),
            ShiftKind.Afternoon,
            minResourceCount: 0,
            maxResourceCount: 1);
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
                new TimeOnly(22, 40),
                DateTimeKind.Utc),
            ShiftKind.Night => date.AddDays(1).ToDateTime(
                new TimeOnly(6, 40),
                DateTimeKind.Utc),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private static bool IsFridayOrSaturday(Shift shift)
    {
        return shift.StartUtc.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday;
    }

    private static bool Overlaps(
        Shift shift,
        ResourcePreference preference)
    {
        return preference.StartUtc < shift.EndUtc &&
               shift.StartUtc < preference.EndUtc;
    }

    private sealed record BadManualConstraintScenario(
        SchedulingProblem Problem,
        ScheduleCandidate BadCandidate);

    private sealed record RealWorldBiWeeklyScenario(
        SchedulingProblem Problem);
}
