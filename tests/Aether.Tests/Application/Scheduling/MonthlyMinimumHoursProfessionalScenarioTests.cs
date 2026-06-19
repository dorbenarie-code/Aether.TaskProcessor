using Aether.Application.Scheduling.Contracts;
using Aether.Application.Scheduling.Optimization;
using Aether.Application.Scheduling.Reports;
using Aether.Domain.Optimization;
using Xunit.Abstractions;

namespace Aether.Tests.Application.Scheduling;

public sealed class MonthlyMinimumHoursProfessionalScenarioTests
{
    private readonly ITestOutputHelper _output;

    public MonthlyMinimumHoursProfessionalScenarioTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Run_ShouldPrintMonthlyMinimumHoursProfessionalScenarioReport()
    {
        var scenario = CreateMonthlyScenario();

        Assert.Equal(90, scenario.Problem.MinimumAssignedHoursPerResource);
        AssertTotalCapacityCanSatisfyMonthlyMinimumHours(scenario.Problem);
        AssertEveryResourceSubmittedMinimumRequiredShiftMix(scenario.Problem);
        AssertAtLeastOneNightShiftHasCompetingRequests(scenario.Problem);

        var deterministicOptimizationResult = new DeterministicScheduleOptimizer()
            .Optimize(scenario.Problem);

        var diagnosticsSink = new CollectingGeneticOptimizerDiagnosticsSink();

        var geneticOptimizationResult = new GeneticScheduleOptimizer(
                populationSize: 120,
                seed: 20260601,
                generationCount: 30,
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
        Assert.Contains("Resources: 8", report);
        Assert.Contains("MinimumAssignedHoursPerResource: 90", report);
        Assert.Contains("Comparison", report);
        Assert.Contains("Best Result", report);
        Assert.Contains("LoadByResource", report);
        Assert.Contains("LoadBalance", report);
        Assert.Contains("ViolationsByType", report);
        Assert.Contains("AssignmentsByShift", report);
        Assert.Contains("GenerationDiagnostics", report);

        Assert.True(
            CountViolations(deterministicResult, ConstraintViolationType.ResourceMinimumAssignedHoursNotMet) > 0,
            "Deterministic is expected to leave some resources below the monthly minimum in this scenario.");

        Assert.Equal(31, geneticResult.GenerationDiagnostics.Count);
    }

    private static MonthlyScenario CreateMonthlyScenario()
    {
        var period = new SchedulePeriod(
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

        var amit = CreateResource("Amit");
        var dana = CreateResource("Dana");
        var noa = CreateResource("Noa");
        var gal = CreateResource("Gal");
        var ron = CreateResource("Ron");
        var lior = CreateResource("Lior");
        var maya = CreateResource("Maya");
        var yossi = CreateResource("Yossi");

        var resources = new[]
        {
            amit,
            dana,
            noa,
            gal,
            ron,
            lior,
            maya,
            yossi
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
                if (ShouldCreateAvoidTrap(resource, shift, amit, dana, yossi))
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
            minimumAssignedHoursPerResource: 90);

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
        var nightDates = new[]
        {
            new DateOnly(2026, 6, 2),
            new DateOnly(2026, 6, 5),
            new DateOnly(2026, 6, 9),
            new DateOnly(2026, 6, 12),
            new DateOnly(2026, 6, 16),
            new DateOnly(2026, 6, 19),
            new DateOnly(2026, 6, 23),
            new DateOnly(2026, 6, 26)
        };

        return nightDates
            .Select(CreateOptionalNightShift)
            .ToArray();
    }

    private static bool ShouldCreateAvoidTrap(
        Resource resource,
        Shift shift,
        Resource amit,
        Resource dana,
        Resource yossi)
    {
        return resource.Id == amit.Id &&
               shift.StartUtc.Date == new DateTime(2026, 6, 3).Date &&
               shift.Kind == ShiftKind.Morning
            || resource.Id == dana.Id &&
               shift.StartUtc.Date == new DateTime(2026, 6, 4).Date &&
               shift.Kind == ShiftKind.Afternoon
            || resource.Id == yossi.Id &&
               shift.StartUtc.Date == new DateTime(2026, 6, 10).Date &&
               shift.Kind == ShiftKind.Afternoon;
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

            AddSubmission(firstResource, nightShifts[i], availabilityWindows, preferences);
            AddSubmission(secondResource, nightShifts[i], availabilityWindows, preferences);
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

    private static void AssertEveryResourceSubmittedMinimumRequiredShiftMix(
        SchedulingProblem problem)
    {
        foreach (var resource in problem.Resources)
        {
            Assert.True(
                CountSubmittedPreferShifts(problem, resource, ShiftKind.Morning) >= 2,
                $"{resource.Name} must submit at least 2 morning shifts.");

            Assert.True(
                CountSubmittedPreferShifts(problem, resource, ShiftKind.Afternoon) >= 1,
                $"{resource.Name} must submit at least 1 afternoon shift.");

            Assert.True(
                CountSubmittedPreferShifts(problem, resource, ShiftKind.Night) >= 1,
                $"{resource.Name} must submit at least 1 night shift.");
        }
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

    private static int CountSubmittedPreferShifts(
        SchedulingProblem problem,
        Resource resource,
        ShiftKind shiftKind)
    {
        return problem.ResourcePreferences
            .Where(preference => preference.ResourceId == resource.Id)
            .Where(preference => preference.Type == ResourcePreferenceType.Prefer)
            .Count(preference => problem.Shifts
                .Where(shift => shift.Kind == shiftKind)
                .Any(shift => Overlaps(shift, preference)));
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
            minResourceCount: 1,
            maxResourceCount: 2);
    }

    private static Shift CreateOptionalNightShift(DateOnly date)
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
            requiresMinimumWhenPreferenceExists: true);
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
