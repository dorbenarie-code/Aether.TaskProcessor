using System.Diagnostics;
using System.Text;
using Aether.Application.Scheduling.Contracts;
using Aether.Application.Scheduling.Interfaces;
using Aether.Application.Scheduling.Optimization;
using Aether.Domain.Optimization;
using Xunit.Abstractions;

namespace Aether.Tests.Application.Scheduling;

public sealed class GeneticOptimizerSequenceQuotaControlledScenarioTests
{
    private readonly ITestOutputHelper _output;

    public GeneticOptimizerSequenceQuotaControlledScenarioTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Run_ShouldReduceNightToAfternoonSequenceQuotaViolations_WhenDeterministicCreatesThreeSequencesForFirstResource()
    {
        var scenario = CreateScenario();

        var deterministic = RunOptimizer(
            "Deterministic",
            new DeterministicScheduleOptimizer(),
            scenario.Problem);

        var initialOnlyDiagnosticsSink = new CollectingDiagnosticsSink();

        var initialOnly = RunOptimizer(
            "Genetic initial only",
            new GeneticScheduleOptimizer(
                populationSize: 120,
                seed: 20260609,
                generationCount: 0,
                eliteCount: 1,
                tournamentSize: 3,
                diagnosticsSink: initialOnlyDiagnosticsSink),
            scenario.Problem,
            initialOnlyDiagnosticsSink.Diagnostics);

        var evolvedDiagnosticsSink = new CollectingDiagnosticsSink();

        var evolved = RunOptimizer(
            "Genetic evolved",
            new GeneticScheduleOptimizer(
                populationSize: 120,
                seed: 20260609,
                generationCount: 80,
                eliteCount: 1,
                tournamentSize: 3,
                diagnosticsSink: evolvedDiagnosticsSink),
            scenario.Problem,
            evolvedDiagnosticsSink.Diagnostics);

        var report = FormatReport(
            scenario,
            deterministic,
            initialOnly,
            evolved);

        _output.WriteLine(report);
        System.Console.WriteLine(report);

        var deterministicSequenceQuotaViolations = CountViolations(
            deterministic,
            ConstraintViolationType.ShiftSequenceQuotaExceeded);

        var evolvedSequenceQuotaViolations = CountViolations(
            evolved,
            ConstraintViolationType.ShiftSequenceQuotaExceeded);

        Assert.Equal(
            3,
            CountNightToAfternoonSequencesForResource(
                scenario,
                deterministic.Result.Candidate,
                scenario.Amit.Id));

        Assert.Equal(1, deterministicSequenceQuotaViolations);
        Assert.Equal(0, evolvedSequenceQuotaViolations);

        Assert.True(
            GetMaxNightToAfternoonSequenceCount(scenario, evolved.Result.Candidate) <= 2,
            report);

        var ranker = new ScheduleEvaluationResultRanker();

        Assert.True(
            ranker.IsBetterThan(
                evolved.Result.Evaluation,
                deterministic.Result.Evaluation),
            report);

        Assert.False(
            ranker.IsBetterThan(
                initialOnly.Result.Evaluation,
                evolved.Result.Evaluation),
            report);

        Assert.Equal(0, CountViolations(evolved, ConstraintViolationType.ResourceUnavailable));
        Assert.Equal(0, CountViolations(evolved, ConstraintViolationType.ShiftUnderstaffed));

        Assert.Contains("GenerationDiagnostics:", report);
        Assert.Contains("- Generation 0:", report);
        Assert.Contains("- Generation 80:", report);
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

    private static int CountViolations(
        ExperimentRun run,
        ConstraintViolationType type)
    {
        return run.Result.Evaluation.Violations.Count(
            violation => violation.Type == type);
    }

    private static ExperimentScenario CreateScenario()
    {
        var period = new SchedulePeriod(
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

        var amit = CreateResource("Amit");
        var dana = CreateResource("Dana");
        var noa = CreateResource("Noa");
        var gal = CreateResource("Gal");
        var ron = CreateResource("Ron");

        var resources = new[]
        {
            amit,
            dana,
            noa,
            gal,
            ron
        };

        var shifts = new List<Shift>();

        shifts.AddRange(CreateNightToAfternoonSequencePair(firstDay: 1));
        shifts.AddRange(CreateNightToAfternoonSequencePair(firstDay: 4));
        shifts.AddRange(CreateNightToAfternoonSequencePair(firstDay: 8));

        var orderedShifts = shifts
            .OrderBy(shift => shift.StartUtc)
            .ToArray();

        var availabilityWindows = new List<AvailabilityWindow>();

        foreach (var resource in resources)
        {
            foreach (var shift in orderedShifts)
            {
                availabilityWindows.Add(CreateAvailability(resource, shift));
            }
        }

        var problem = new SchedulingProblem(
            period: period,
            resources: resources,
            shifts: orderedShifts,
            availabilityWindows: availabilityWindows,
            resourcePreferences: []);

        var resourceNames = resources.ToDictionary(
            resource => resource.Id,
            resource => resource.Name);

        return new ExperimentScenario(
            Problem: problem,
            Resources: resources,
            Shifts: orderedShifts,
            ResourceNames: resourceNames,
            Amit: amit);
    }

    private static IReadOnlyCollection<Shift> CreateNightToAfternoonSequencePair(
        int firstDay)
    {
        var nightStartUtc = new DateTime(
            2026,
            6,
            firstDay,
            22,
            30,
            0,
            DateTimeKind.Utc);

        var nightEndUtc = nightStartUtc
            .AddHours(8)
            .AddMinutes(10);

        var afternoonStartUtc = new DateTime(
            nightEndUtc.Year,
            nightEndUtc.Month,
            nightEndUtc.Day,
            14,
            30,
            0,
            DateTimeKind.Utc);

        var afternoonEndUtc = afternoonStartUtc.AddHours(8);

        return
        [
            CreateShift(
                startUtc: nightStartUtc,
                endUtc: nightEndUtc,
                kind: ShiftKind.Night),

            CreateShift(
                startUtc: afternoonStartUtc,
                endUtc: afternoonEndUtc,
                kind: ShiftKind.Afternoon)
        ];
    }

    private static Shift CreateShift(
        DateTime startUtc,
        DateTime endUtc,
        ShiftKind kind)
    {
        return new Shift(
            Guid.NewGuid(),
            startUtc,
            endUtc,
            kind,
            minResourceCount: 1,
            maxResourceCount: 1);
    }

    private static Resource CreateResource(string name)
    {
        return new Resource(
            Guid.NewGuid(),
            name,
            hourlyCost: 50);
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

    private static string FormatReport(
        ExperimentScenario scenario,
        params ExperimentRun[] runs)
    {
        var builder = new StringBuilder();

        builder.AppendLine("Genetic Optimizer Sequence Quota Controlled Scenario");
        builder.AppendLine("Scenario: Deterministic creates 3 NightToAfternoon sequences for Amit in the same month.");
        builder.AppendLine($"Resources: {scenario.Resources.Count}");
        builder.AppendLine($"Shifts: {scenario.Shifts.Count}");
        builder.AppendLine("Sequence rest gap: 7h50m");
        builder.AppendLine("Shift capacity: min=1, max=1");
        builder.AppendLine();

        foreach (var run in runs)
        {
            AppendRun(builder, scenario, run);
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static void AppendRun(
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
        AppendAssignmentsByShift(builder, scenario, run.Result.Candidate);
        AppendNightToAfternoonSequenceCounts(builder, scenario, run.Result.Candidate);
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

    private static void AppendAssignmentsByShift(
        StringBuilder builder,
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        builder.AppendLine("AssignmentsByShift:");

        foreach (var shift in scenario.Shifts.OrderBy(shift => shift.StartUtc))
        {
            var assignment = candidate.Assignments
                .FirstOrDefault(item => item.ShiftId == shift.Id);

            if (assignment is null)
            {
                builder.AppendLine($"- {FormatShift(shift)} -> unassigned");
                continue;
            }

            builder.AppendLine(
                $"- {FormatShift(shift)} -> {scenario.ResourceNames[assignment.ResourceId]}");
        }
    }

    private static void AppendNightToAfternoonSequenceCounts(
        StringBuilder builder,
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        var counts = CountNightToAfternoonSequencesByResource(
            scenario,
            candidate);

        builder.AppendLine("NightToAfternoonSequencesByResource:");

        foreach (var resource in scenario.Resources)
        {
            builder.AppendLine(
                $"- {scenario.ResourceNames[resource.Id]}: {counts.GetValueOrDefault(resource.Id, 0)}");
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

        foreach (var diagnostic in run.Diagnostics)
        {
            builder.AppendLine(
                $"- Generation {diagnostic.GenerationIndex}: " +
                $"PopulationSize={diagnostic.PopulationSize}, " +
                $"FeasibleCandidates={diagnostic.FeasibleCandidateCount}, " +
                $"BestScoreValue={diagnostic.BestScoreValue}, " +
                $"BestTotalPenalty={diagnostic.BestTotalPenalty}, " +
                $"BestHardViolationCount={diagnostic.BestHardViolationCount}, " +
                $"BestSoftViolationCount={diagnostic.BestSoftViolationCount}");
        }
    }

    private static string FormatShift(Shift shift)
    {
        return $"{shift.Kind} {shift.StartUtc:yyyy-MM-dd HH:mm}-{shift.EndUtc:yyyy-MM-dd HH:mm}";
    }

    private static int CountNightToAfternoonSequencesForResource(
        ExperimentScenario scenario,
        ScheduleCandidate candidate,
        Guid resourceId)
    {
        return CountNightToAfternoonSequencesByResource(
                scenario,
                candidate)
            .GetValueOrDefault(resourceId, 0);
    }

    private static int GetMaxNightToAfternoonSequenceCount(
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        var counts = CountNightToAfternoonSequencesByResource(
            scenario,
            candidate);

        if (counts.Count == 0)
        {
            return 0;
        }

        return counts.Values.Max();
    }

    private static IReadOnlyDictionary<Guid, int> CountNightToAfternoonSequencesByResource(
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        var shiftsById = scenario.Shifts.ToDictionary(shift => shift.Id);
        var classifier = new ShiftSequenceClassifier();

        return candidate.Assignments
            .GroupBy(assignment => assignment.ResourceId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var assignedShifts = group
                        .Select(assignment => shiftsById[assignment.ShiftId])
                        .OrderBy(shift => shift.StartUtc)
                        .ToArray();

                    var count = 0;

                    for (var i = 1; i < assignedShifts.Length; i++)
                    {
                        var previousShift = assignedShifts[i - 1];
                        var nextShift = assignedShifts[i];

                        if (classifier.Classify(previousShift, nextShift) ==
                            ShiftSequenceType.NightToAfternoon)
                        {
                            count++;
                        }
                    }

                    return count;
                });
    }

    private sealed record ExperimentScenario(
        SchedulingProblem Problem,
        IReadOnlyCollection<Resource> Resources,
        IReadOnlyCollection<Shift> Shifts,
        IReadOnlyDictionary<Guid, string> ResourceNames,
        Resource Amit);

    private sealed record ExperimentRun(
        string Name,
        ScheduleOptimizationResult Result,
        TimeSpan Elapsed,
        IReadOnlyList<GeneticGenerationDiagnostic> Diagnostics);
}
