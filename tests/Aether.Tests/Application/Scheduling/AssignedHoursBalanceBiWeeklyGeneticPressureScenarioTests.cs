using Aether.Application.Scheduling.Optimization;
using Aether.Domain.Optimization;
using Xunit.Abstractions;

namespace Aether.Tests.Application.Scheduling;

public sealed class AssignedHoursBalanceBiWeeklyGeneticPressureScenarioTests
{
    private const double MaximumAssignedHoursDeviationFromAverageHours = 5.0;

    private readonly ITestOutputHelper _output;

    public AssignedHoursBalanceBiWeeklyGeneticPressureScenarioTests(
        ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    [Trait("Category", "SchedulingExperiment")]
    public void Run_ShouldImproveAssignedHoursBalancePressure_InBiWeeklyScenario()
    {
        var problem = CreateProblem();

        Assert.Equal(
            MaximumAssignedHoursDeviationFromAverageHours,
            problem.MaximumAssignedHoursDeviationFromAverageHours);

        var deterministicResult = new DeterministicScheduleOptimizer()
            .Optimize(problem);

        var diagnosticsSink = new CollectingGeneticOptimizerDiagnosticsSink();

        var geneticResult = new GeneticScheduleOptimizer(
                populationSize: 120,
                seed: 20260601,
                generationCount: 80,
                eliteCount: 1,
                tournamentSize: 3,
                diagnosticsSink: diagnosticsSink)
            .Optimize(problem);

        var deterministicBalanceViolations = CountViolations(
            deterministicResult.Evaluation.Violations,
            ConstraintViolationType.ResourceAssignedHoursBalanceExceeded);

        var geneticBalanceViolations = CountViolations(
            geneticResult.Evaluation.Violations,
            ConstraintViolationType.ResourceAssignedHoursBalanceExceeded);

        var deterministicSpread = GetLoadSpreadHours(problem, deterministicResult.Candidate);
        var geneticSpread = GetLoadSpreadHours(problem, geneticResult.Candidate);

        _output.WriteLine($"Deterministic IsFeasible: {deterministicResult.Evaluation.IsFeasible}");
        _output.WriteLine($"Deterministic TotalPenalty: {deterministicResult.Evaluation.Score.TotalPenalty}");
        _output.WriteLine($"Deterministic BalanceViolations: {deterministicBalanceViolations}");
        _output.WriteLine($"Deterministic LoadSpreadHours: {deterministicSpread:0.##}");

        _output.WriteLine($"Genetic IsFeasible: {geneticResult.Evaluation.IsFeasible}");
        _output.WriteLine($"Genetic TotalPenalty: {geneticResult.Evaluation.Score.TotalPenalty}");
        _output.WriteLine($"Genetic BalanceViolations: {geneticBalanceViolations}");
        _output.WriteLine($"Genetic LoadSpreadHours: {geneticSpread:0.##}");
        _output.WriteLine($"GenerationDiagnostics: {diagnosticsSink.Diagnostics.Count}");

        Assert.True(
            deterministicBalanceViolations > 0,
            "The deterministic baseline must expose assigned-hours balance pressure.");

        Assert.True(
            geneticResult.Evaluation.Score.HardViolationCount <=
            deterministicResult.Evaluation.Score.HardViolationCount);

        Assert.True(
            geneticBalanceViolations <= deterministicBalanceViolations);

        Assert.True(
            geneticSpread <= deterministicSpread);

        Assert.Equal(
            81,
            diagnosticsSink.Diagnostics.Count);
    }

    private static SchedulingProblem CreateProblem()
    {
        var resources = Enumerable
            .Range(1, 8)
            .Select(index => new Resource(
                Guid.NewGuid(),
                $"Guard{index:00}",
                hourlyCost: 100m))
            .ToArray();

        var shifts = Enumerable
            .Range(0, 12)
            .Select(index => new Shift(
                Guid.NewGuid(),
                new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc).AddDays(index),
                new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Utc).AddDays(index),
                ShiftKind.Morning,
                minResourceCount: 1,
                maxResourceCount: 2))
            .ToArray();

        var availabilityWindows = new List<AvailabilityWindow>();
        var resourcePreferences = new List<ResourcePreference>();

        foreach (var resource in resources)
        {
            foreach (var shift in shifts)
            {
                availabilityWindows.Add(new AvailabilityWindow(
                    resource.Id,
                    shift.StartUtc,
                    shift.EndUtc));

                resourcePreferences.Add(new ResourcePreference(
                    resource.Id,
                    shift.StartUtc,
                    shift.EndUtc,
                    ResourcePreferenceType.Prefer,
                    ResourcePreferencePriority.High));
            }
        }

        return new SchedulingProblem(
            period: new SchedulePeriod(
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc)),
            resources: resources,
            shifts: shifts,
            availabilityWindows: availabilityWindows,
            resourcePreferences: resourcePreferences,
            minimumAssignedHoursPerResource: 8,
            maximumAssignedHoursDeviationFromAverageHours: MaximumAssignedHoursDeviationFromAverageHours);
    }

    private static int CountViolations(
        IReadOnlyCollection<ConstraintViolation> violations,
        ConstraintViolationType type)
    {
        return violations.Count(violation => violation.Type == type);
    }

    private static double GetLoadSpreadHours(
        SchedulingProblem problem,
        ScheduleCandidate candidate)
    {
        var shiftsById = problem.Shifts
            .ToDictionary(shift => shift.Id);

        var assignedHoursByResourceId = candidate.Assignments
            .GroupBy(assignment => assignment.ResourceId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(assignment =>
                {
                    var shift = shiftsById[assignment.ShiftId];

                    return (shift.EndUtc - shift.StartUtc).TotalHours;
                }));

        var assignedHours = problem.Resources
            .Select(resource =>
            {
                assignedHoursByResourceId.TryGetValue(resource.Id, out var hours);

                return hours;
            })
            .ToArray();

        return assignedHours.Max() - assignedHours.Min();
    }
}
