using Aether.Application.Scheduling.Contracts;
using Aether.Application.Scheduling.Optimization;
using Aether.Application.Scheduling.Reports;
using Aether.Domain.Optimization;
using Xunit.Abstractions;

namespace Aether.Tests.Application.Scheduling;

public sealed class ProfessionalDomainSchedulingInputScenarioTests
{
    private readonly ITestOutputHelper _output;

    public ProfessionalDomainSchedulingInputScenarioTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Run_ShouldPrintProfessionalDomainInputScenarioReport()
    {
        var scenario = CreateProfessionalScenario();

        AssertEveryResourceSubmittedMinimumRequiredShiftMix(scenario.Problem);
        AssertAtLeastOneNightShiftHasCompetingRequests(scenario.Problem);

        var deterministicOptimizer = new DeterministicScheduleOptimizer();

        var deterministicOptimizationResult = deterministicOptimizer.Optimize(
            scenario.Problem);

        var diagnosticsSink = new CollectingGeneticOptimizerDiagnosticsSink();

        var geneticOptimizer = new GeneticScheduleOptimizer(
            populationSize: 80,
            seed: 20260601,
            generationCount: 30,
            eliteCount: 1,
            tournamentSize: 3,
            diagnosticsSink: diagnosticsSink);

        var geneticOptimizationResult = geneticOptimizer.Optimize(
            scenario.Problem);

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

        var formatter = new SchedulingRunReportFormatter();
        var report = formatter.Format(runResult);

        _output.WriteLine(report);
        System.Console.WriteLine(report);

        var ranker = new ScheduleEvaluationResultRanker();

        Assert.False(ranker.IsBetterThan(
            deterministicResult.Evaluation,
            geneticResult.Evaluation));

        Assert.Equal(8, scenario.Problem.Resources.Count);
        Assert.Equal(16, scenario.Problem.Shifts.Count);
        Assert.Equal(24, scenario.Problem.MinimumAssignedHoursPerResource);

        Assert.Equal(31, geneticResult.GenerationDiagnostics.Count);

        Assert.Contains("Scheduling Run Report", report);
        Assert.Contains("Input Summary", report);
        Assert.Contains("Resources: 8", report);
        Assert.Contains("Shifts: 16", report);
        Assert.Contains("MinimumAssignedHoursPerResource: 24", report);
        Assert.Contains("Comparison", report);
        Assert.Contains("Best Result", report);
        Assert.Contains("LoadByResource", report);
        Assert.Contains("LoadBalance", report);
        Assert.Contains("ViolationsByType", report);
        Assert.Contains("AssignmentsByShift", report);
        Assert.Contains("GenerationDiagnostics", report);
    }

    private static ProfessionalScenario CreateProfessionalScenario()
    {
        var period = new SchedulePeriod(
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));

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

        var morning1 = CreateRegularShift(new DateOnly(2026, 6, 1), ShiftKind.Morning);
        var afternoon1 = CreateRegularShift(new DateOnly(2026, 6, 1), ShiftKind.Afternoon);
        var morning2 = CreateRegularShift(new DateOnly(2026, 6, 2), ShiftKind.Morning);
        var afternoon2 = CreateRegularShift(new DateOnly(2026, 6, 2), ShiftKind.Afternoon);
        var morning3 = CreateRegularShift(new DateOnly(2026, 6, 3), ShiftKind.Morning);
        var afternoon3 = CreateRegularShift(new DateOnly(2026, 6, 3), ShiftKind.Afternoon);
        var morning4 = CreateRegularShift(new DateOnly(2026, 6, 4), ShiftKind.Morning);
        var afternoon4 = CreateRegularShift(new DateOnly(2026, 6, 4), ShiftKind.Afternoon);
        var morning5 = CreateRegularShift(new DateOnly(2026, 6, 8), ShiftKind.Morning);
        var afternoon5 = CreateRegularShift(new DateOnly(2026, 6, 8), ShiftKind.Afternoon);
        var morning6 = CreateRegularShift(new DateOnly(2026, 6, 9), ShiftKind.Morning);
        var afternoon6 = CreateRegularShift(new DateOnly(2026, 6, 9), ShiftKind.Afternoon);

        var night1 = CreateOptionalNightShift(new DateOnly(2026, 6, 5));
        var night2 = CreateOptionalNightShift(new DateOnly(2026, 6, 6));
        var night3 = CreateOptionalNightShift(new DateOnly(2026, 6, 10));
        var night4 = CreateOptionalNightShift(new DateOnly(2026, 6, 11));

        var regularShifts = new[]
        {
            morning1,
            afternoon1,
            morning2,
            afternoon2,
            morning3,
            afternoon3,
            morning4,
            afternoon4,
            morning5,
            afternoon5,
            morning6,
            afternoon6
        };

        var nightShifts = new[]
        {
            night1,
            night2,
            night3,
            night4
        };

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
                if (resource.Id == amit.Id &&
                    (shift.Id == morning1.Id || shift.Id == afternoon1.Id))
                {
                    AddAvoid(resource, shift, availabilityWindows, preferences);
                    continue;
                }

                AddSubmission(resource, shift, availabilityWindows, preferences);
            }
        }

        AddSubmission(amit, night1, availabilityWindows, preferences);
        AddSubmission(dana, night1, availabilityWindows, preferences);

        AddSubmission(noa, night2, availabilityWindows, preferences);
        AddSubmission(gal, night2, availabilityWindows, preferences);

        AddSubmission(ron, night3, availabilityWindows, preferences);
        AddSubmission(lior, night3, availabilityWindows, preferences);

        AddSubmission(maya, night4, availabilityWindows, preferences);
        AddSubmission(yossi, night4, availabilityWindows, preferences);

        AddAvoid(yossi, afternoon3, availabilityWindows, preferences);
        AddAvoid(maya, morning5, availabilityWindows, preferences);

        var problem = new SchedulingProblem(
            period: period,
            resources: resources,
            shifts: shifts,
            availabilityWindows: availabilityWindows,
            resourcePreferences: preferences,
            minimumAssignedHoursPerResource: 24);

        return new ProfessionalScenario(problem);
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

    private static void AssertEveryResourceSubmittedMinimumRequiredShiftMix(
        SchedulingProblem problem)
    {
        foreach (var resource in problem.Resources)
        {
            var morningSubmissionCount = CountSubmittedPreferShifts(
                problem,
                resource,
                ShiftKind.Morning);

            var afternoonSubmissionCount = CountSubmittedPreferShifts(
                problem,
                resource,
                ShiftKind.Afternoon);

            var nightSubmissionCount = CountSubmittedPreferShifts(
                problem,
                resource,
                ShiftKind.Night);

            Assert.True(
                morningSubmissionCount >= 2,
                $"{resource.Name} must submit at least 2 morning shifts.");

            Assert.True(
                afternoonSubmissionCount >= 1,
                $"{resource.Name} must submit at least 1 afternoon shift.");

            Assert.True(
                nightSubmissionCount >= 1,
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
        var startUtc = GetStartUtc(date, kind);
        var endUtc = GetEndUtc(date, kind);

        return new Shift(
            Guid.NewGuid(),
            startUtc,
            endUtc,
            kind,
            minResourceCount: 1,
            maxResourceCount: 3);
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

    private sealed record ProfessionalScenario(
        SchedulingProblem Problem);
}
