using Aether.Application.Scheduling.Contracts;
using Aether.Application.Scheduling.Optimization;
using Aether.Application.Scheduling.Reports;
using Aether.Domain.Optimization;
using Xunit.Abstractions;

namespace Aether.Tests.Application.Scheduling;

public sealed class MonthlyProfessional14ResourcesScenarioTests
{
    private const int MinimumMonthlyHoursPerResource = 90;
    private const int PopulationSize = 180;
    private const int GenerationCount = 50;
    private const int Seed = 20260614;

    private readonly ITestOutputHelper _output;

    public MonthlyProfessional14ResourcesScenarioTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    [Trait("Category", "SchedulingExperiment")]
    public void Run_ShouldPrintMonthlyProfessional14ResourcesScenarioReport()
    {
        var scenario = CreateMonthlyScenario();

        Assert.Equal(14, scenario.Problem.Resources.Count);
        Assert.Equal(68, scenario.Problem.Shifts.Count);
        Assert.Equal(MinimumMonthlyHoursPerResource, scenario.Problem.MinimumAssignedHoursPerResource);

        AssertTotalCapacityCanSatisfyMonthlyMinimumHours(scenario.Problem);
        AssertEveryResourceSubmittedAtLeastMinimumMonthlyHours(scenario.Problem);
        AssertEveryResourceHasAtLeastOnePersonalConstraint(scenario.Problem);
        AssertEveryResourceSubmittedWeeklyShiftMixOptions(scenario.Problem);
        AssertAtLeastOneNightShiftHasCompetingRequests(scenario.Problem);

        var deterministicOptimizationResult = new DeterministicScheduleOptimizer()
            .Optimize(scenario.Problem);

        var diagnosticsSink = new CollectingGeneticOptimizerDiagnosticsSink();

        var geneticOptimizationResult = new GeneticScheduleOptimizer(
                populationSize: PopulationSize,
                seed: Seed,
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
        Assert.Contains("Shifts: 68", report);
        Assert.Contains("MinimumAssignedHoursPerResource: 90", report);
        Assert.Contains("Comparison", report);
        Assert.Contains("Best Result", report);
        Assert.Contains("LoadByResource", report);
        Assert.Contains("LoadBalance", report);
        Assert.Contains("ViolationsByType", report);
        Assert.Contains("AssignmentsByShift", report);
        Assert.Contains("GenerationDiagnostics", report);

        Assert.Equal(GenerationCount + 1, geneticResult.GenerationDiagnostics.Count);

        Assert.True(
            geneticResult.Evaluation.Score.HardViolationCount <= deterministicResult.Evaluation.Score.HardViolationCount,
            "Genetic result is expected to have no more hard violations than the deterministic baseline.");

        Assert.True(
            geneticResult.Evaluation.Score.TotalPenalty <= deterministicResult.Evaluation.Score.TotalPenalty,
            "Genetic result is expected to have no higher total penalty than the deterministic baseline.");
    }

    private static MonthlyScenario CreateMonthlyScenario()
    {
        var period = new SchedulePeriod(
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

        var resources = new[]
        {
            CreateResource("Amit"),
            CreateResource("Dana"),
            CreateResource("Gal"),
            CreateResource("Lior"),
            CreateResource("Maya"),
            CreateResource("Noa"),
            CreateResource("Ron"),
            CreateResource("Yossi"),
            CreateResource("Eyal"),
            CreateResource("Shira"),
            CreateResource("Tomer"),
            CreateResource("Yael"),
            CreateResource("Omer"),
            CreateResource("Roni")
        };

        var regularShifts = CreateRegularMonthlyShifts();
        var nightShifts = CreateOptionalNightShifts();

        var shifts = regularShifts
            .Concat(nightShifts)
            .OrderBy(shift => shift.StartUtc)
            .ToArray();

        var availabilityWindows = new List<AvailabilityWindow>();
        var preferences = new List<ResourcePreference>();

        foreach (var resource in resources)
        {
            foreach (var shift in regularShifts)
            {
                if (ShouldCreatePersonalAvoidConstraint(resource, shift, resources))
                {
                    AddAvoid(resource, shift, availabilityWindows, preferences);
                    continue;
                }

                AddSubmission(resource, shift, availabilityWindows, preferences);
            }
        }

        AddNightSubmissions(
            resources,
            nightShifts,
            availabilityWindows,
            preferences);

        var problem = new SchedulingProblem(
            period: period,
            resources: resources,
            shifts: shifts,
            availabilityWindows: availabilityWindows,
            resourcePreferences: preferences,
            minimumAssignedHoursPerResource: MinimumMonthlyHoursPerResource,
            minimumMorningShiftsPerResourcePerFullWeek: 1,
            minimumAfternoonShiftsPerResourcePerFullWeek: 1);

        return new MonthlyScenario(problem);
    }

    private static IReadOnlyList<Shift> CreateRegularMonthlyShifts()
    {
        var shifts = new List<Shift>();

        for (var day = 1; day <= 30; day++)
        {
            var date = new DateOnly(2026, 6, day);

            shifts.Add(CreateRegularShift(date, ShiftKind.Morning));
            shifts.Add(CreateRegularShift(date, ShiftKind.Afternoon));
        }

        return shifts;
    }

    private static IReadOnlyList<Shift> CreateOptionalNightShifts()
    {
        var nightDefinitions = new[]
        {
            (Date: new DateOnly(2026, 6, 5), Category: NightShiftCategory.FridayNight),
            (Date: new DateOnly(2026, 6, 6), Category: NightShiftCategory.MotzeiShabbatNight),
            (Date: new DateOnly(2026, 6, 12), Category: NightShiftCategory.FridayNight),
            (Date: new DateOnly(2026, 6, 13), Category: NightShiftCategory.MotzeiShabbatNight),
            (Date: new DateOnly(2026, 6, 19), Category: NightShiftCategory.FridayNight),
            (Date: new DateOnly(2026, 6, 20), Category: NightShiftCategory.MotzeiShabbatNight),
            (Date: new DateOnly(2026, 6, 26), Category: NightShiftCategory.FridayNight),
            (Date: new DateOnly(2026, 6, 27), Category: NightShiftCategory.MotzeiShabbatNight)
        };

        return nightDefinitions
            .Select(definition => CreateOptionalNightShift(definition.Date, definition.Category))
            .ToArray();
    }

    private static bool ShouldCreatePersonalAvoidConstraint(
        Resource resource,
        Shift shift,
        IReadOnlyList<Resource> resources)
    {
        var resourceIndex = Array.FindIndex(
            resources.ToArray(),
            candidate => candidate.Id == resource.Id);

        if (resourceIndex < 0)
        {
            return false;
        }

        var avoidDay = 1 + (resourceIndex * 2 % 28);
        var avoidKind = resourceIndex % 2 == 0
            ? ShiftKind.Morning
            : ShiftKind.Afternoon;

        return shift.StartUtc.Date == new DateTime(2026, 6, avoidDay).Date &&
               shift.Kind == avoidKind;
    }

    private static void AddNightSubmissions(
        IReadOnlyList<Resource> resources,
        IReadOnlyList<Shift> nightShifts,
        ICollection<AvailabilityWindow> availabilityWindows,
        ICollection<ResourcePreference> preferences)
    {
        for (var i = 0; i < nightShifts.Count; i++)
        {
            var firstResource = resources[i % resources.Count];
            var secondResource = resources[(i + 1) % resources.Count];
            var thirdResource = resources[(i + 7) % resources.Count];
            var fourthResource = resources[(i + 8) % resources.Count];

            AddSubmission(firstResource, nightShifts[i], availabilityWindows, preferences);
            AddSubmission(secondResource, nightShifts[i], availabilityWindows, preferences);
            AddSubmission(thirdResource, nightShifts[i], availabilityWindows, preferences);
            AddSubmission(fourthResource, nightShifts[i], availabilityWindows, preferences);
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

    private static int CountViolations(
        SchedulingRunOptimizationResult result,
        ConstraintViolationType type)
    {
        return result.ViolationsByType.TryGetValue(type, out var count)
            ? count
            : 0;
    }

    private static void AssertTotalCapacityCanSatisfyMonthlyMinimumHours(
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

    private static void AssertEveryResourceSubmittedAtLeastMinimumMonthlyHours(
        SchedulingProblem problem)
    {
        foreach (var resource in problem.Resources)
        {
            var submittedHours = problem.AvailabilityWindows
                .Where(window => window.ResourceId == resource.Id)
                .Sum(window => (window.EndUtc - window.StartUtc).TotalHours);

            Assert.True(
                submittedHours >= problem.MinimumAssignedHoursPerResource,
                $"{resource.Name} submitted only {submittedHours:0.##}h, below the required {problem.MinimumAssignedHoursPerResource}h.");
        }
    }

    private static void AssertEveryResourceHasAtLeastOnePersonalConstraint(
        SchedulingProblem problem)
    {
        foreach (var resource in problem.Resources)
        {
            var hasPersonalConstraint = problem.ResourcePreferences
                .Any(preference => preference.ResourceId == resource.Id);

            Assert.True(
                hasPersonalConstraint,
                $"{resource.Name} must have at least one personal preference or constraint.");
        }
    }

    private static void AssertEveryResourceSubmittedWeeklyShiftMixOptions(
        SchedulingProblem problem)
    {
        for (var weekStartUtc = problem.Period.StartUtc;
             weekStartUtc.AddDays(7) <= problem.Period.EndUtc;
             weekStartUtc = weekStartUtc.AddDays(7))
        {
            var weekEndUtc = weekStartUtc.AddDays(7);

            foreach (var resource in problem.Resources)
            {
                var hasMorningOption = HasSubmittedShiftOption(
                    problem,
                    resource,
                    ShiftKind.Morning,
                    weekStartUtc,
                    weekEndUtc);

                var hasAfternoonOption = HasSubmittedShiftOption(
                    problem,
                    resource,
                    ShiftKind.Afternoon,
                    weekStartUtc,
                    weekEndUtc);

                Assert.True(
                    hasMorningOption,
                    $"{resource.Name} must submit at least one morning option for week starting {weekStartUtc:yyyy-MM-dd}.");

                Assert.True(
                    hasAfternoonOption,
                    $"{resource.Name} must submit at least one afternoon option for week starting {weekStartUtc:yyyy-MM-dd}.");
            }
        }
    }

    private static bool HasSubmittedShiftOption(
        SchedulingProblem problem,
        Resource resource,
        ShiftKind shiftKind,
        DateTime weekStartUtc,
        DateTime weekEndUtc)
    {
        return problem.AvailabilityWindows
            .Where(window => window.ResourceId == resource.Id)
            .Any(window => problem.Shifts
                .Where(shift => shift.Kind == shiftKind)
                .Where(shift => shift.StartUtc >= weekStartUtc)
                .Where(shift => shift.StartUtc < weekEndUtc)
                .Any(shift => window.StartUtc <= shift.StartUtc &&
                              window.EndUtc >= shift.EndUtc));
    }

    private static void AssertAtLeastOneNightShiftHasCompetingRequests(
        SchedulingProblem problem)
    {
        var hasCompetingNightRequests = problem.Shifts
            .Where(shift => shift.Kind == ShiftKind.Night)
            .Any(shift => problem.ResourcePreferences
                .Where(preference => preference.Type == ResourcePreferenceType.Prefer)
                .Count(preference => Overlaps(shift, preference)) >= 2);

        Assert.True(
            hasCompetingNightRequests,
            "At least one night shift should have competing prefer requests.");
    }

    private static void AddSubmission(
        Resource resource,
        Shift shift,
        ICollection<AvailabilityWindow> availabilityWindows,
        ICollection<ResourcePreference> preferences)
    {
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

    private static void AddAvoid(
        Resource resource,
        Shift shift,
        ICollection<AvailabilityWindow> availabilityWindows,
        ICollection<ResourcePreference> preferences)
    {
        availabilityWindows.Add(new AvailabilityWindow(
            resource.Id,
            shift.StartUtc,
            shift.EndUtc));

        preferences.Add(new ResourcePreference(
            resource.Id,
            shift.StartUtc,
            shift.EndUtc,
            ResourcePreferenceType.Avoid,
            ResourcePreferencePriority.High));
    }

    private static Resource CreateResource(string name)
    {
        return new Resource(
            Guid.NewGuid(),
            name,
            hourlyCost: 100m);
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
            maxResourceCount: 3);
    }

    private static Shift CreateOptionalNightShift(
        DateOnly date,
        NightShiftCategory category)
    {
        return new Shift(
            Guid.NewGuid(),
            date.ToDateTime(
                new TimeOnly(22, 30),
                DateTimeKind.Utc),
            date.AddDays(1).ToDateTime(
                new TimeOnly(6, 30),
                DateTimeKind.Utc),
            ShiftKind.Night,
            minResourceCount: 0,
            maxResourceCount: 1,
            requiresPreferenceToAssign: true,
            requiresMinimumWhenPreferenceExists: true,
            nightShiftCategory: category);
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

    private static bool Overlaps(
        Shift shift,
        ResourcePreference preference)
    {
        return preference.StartUtc < shift.EndUtc &&
               shift.StartUtc < preference.EndUtc;
    }

    private sealed record MonthlyScenario(
        SchedulingProblem Problem);
}
