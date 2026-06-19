using System.Diagnostics;
using System.Text;
using Aether.Application.Scheduling.Contracts;
using Aether.Application.Scheduling.Interfaces;
using Aether.Application.Scheduling.Optimization;
using Aether.Domain.Optimization;
using Xunit.Abstractions;

namespace Aether.Tests.Application.Scheduling;

public sealed class GeneticOptimizerControlledExperimentTests
{
    private readonly ITestOutputHelper _output;

    public GeneticOptimizerControlledExperimentTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Run_ShouldProduceControlledGeneticOptimizerExperimentReport()
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
                populationSize: 80,
                seed: 20260601,
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
                populationSize: 80,
                seed: 20260601,
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

        Assert.Contains("GenerationDiagnostics:", report);
        Assert.Contains("- Generation 0:", report);
        Assert.Contains("- Generation 80:", report);

        var ranker = new ScheduleEvaluationResultRanker();

        Assert.False(ranker.IsBetterThan(
            initialOnly.Result.Evaluation,
            evolved.Result.Evaluation));

        Assert.True(ranker.IsBetterThan(
            evolved.Result.Evaluation,
            deterministic.Result.Evaluation));

        Assert.True(
            evolved.Result.Evaluation.Score.HardViolationCount <=
            deterministic.Result.Evaluation.Score.HardViolationCount);

        Assert.True(
            CountViolations(evolved, ConstraintViolationType.IgnoredAvoidPreference) <=
            CountViolations(deterministic, ConstraintViolationType.IgnoredAvoidPreference));
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
            new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));

        var dana = CreateResource("Dana");
        var yossi = CreateResource("Yossi");
        var noa = CreateResource("Noa");
        var amit = CreateResource("Amit");
        var gal = CreateResource("Gal");
        var ron = CreateResource("Ron");
        var lior = CreateResource("Lior");
        var maya = CreateResource("Maya");

        var resources = new[]
        {
            dana,
            yossi,
            noa,
            amit,
            gal,
            ron,
            lior,
            maya
        };

        var resourceNames = resources.ToDictionary(
            resource => resource.Id,
            resource => resource.Name);

        var day1Morning = CreateShift(
            new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning,
            minResourceCount: 2,
            maxResourceCount: 3);

        var day1Afternoon = CreateShift(
            new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 22, 30, 0, DateTimeKind.Utc),
            ShiftKind.Afternoon,
            minResourceCount: 2,
            maxResourceCount: 3);

        var day2Morning = CreateShift(
            new DateTime(2026, 6, 2, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 2, 14, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning,
            minResourceCount: 2,
            maxResourceCount: 3);

        var day2Afternoon = CreateShift(
            new DateTime(2026, 6, 2, 14, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 2, 22, 30, 0, DateTimeKind.Utc),
            ShiftKind.Afternoon,
            minResourceCount: 1,
            maxResourceCount: 2);

        var day2Night = CreateShift(
            new DateTime(2026, 6, 2, 22, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 3, 6, 30, 0, DateTimeKind.Utc),
            ShiftKind.Night,
            minResourceCount: 1,
            maxResourceCount: 1,
            requiresPreferenceToAssign: false);

        var day3Morning = CreateShift(
            new DateTime(2026, 6, 3, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 3, 14, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning,
            minResourceCount: 2,
            maxResourceCount: 3);

        var day3Afternoon = CreateShift(
            new DateTime(2026, 6, 3, 14, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 3, 22, 30, 0, DateTimeKind.Utc),
            ShiftKind.Afternoon,
            minResourceCount: 1,
            maxResourceCount: 2);

        var day4Night = CreateShift(
            new DateTime(2026, 6, 4, 22, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 5, 6, 30, 0, DateTimeKind.Utc),
            ShiftKind.Night,
            minResourceCount: 0,
            maxResourceCount: 1,
            requiresPreferenceToAssign: true,
            requiresMinimumWhenPreferenceExists: true);

        var shifts = new[]
        {
            day1Morning,
            day1Afternoon,
            day2Morning,
            day2Afternoon,
            day2Night,
            day3Morning,
            day3Afternoon,
            day4Night
        }
        .OrderBy(shift => shift.StartUtc)
        .ToArray();

        var availabilityWindows = new List<AvailabilityWindow>();
        var preferences = new List<ResourcePreference>();

        foreach (var resource in resources)
        {
            foreach (var shift in shifts)
            {
                availabilityWindows.Add(CreateAvailability(resource, shift));
            }
        }

        preferences.Add(new ResourcePreference(
            dana.Id,
            day2Night.StartUtc,
            day2Night.EndUtc,
            ResourcePreferenceType.Avoid,
            ResourcePreferencePriority.High));

        preferences.Add(new ResourcePreference(
            amit.Id,
            day2Night.StartUtc,
            day2Night.EndUtc,
            ResourcePreferenceType.Prefer,
            ResourcePreferencePriority.High));

        preferences.Add(new ResourcePreference(
            lior.Id,
            day4Night.StartUtc,
            day4Night.EndUtc,
            ResourcePreferenceType.Prefer,
            ResourcePreferencePriority.High));

        var problem = new SchedulingProblem(
            period: period,
            resources: resources,
            shifts: shifts,
            availabilityWindows: availabilityWindows,
            resourcePreferences: preferences,
            minimumAssignedHoursPerResource: 16);

        return new ExperimentScenario(
            Problem: problem,
            Resources: resources,
            Shifts: shifts,
            ResourceNames: resourceNames);
    }

    private static IReadOnlyList<Shift> CreateRegularShifts()
    {
        var shifts = new List<Shift>();

        for (var day = 1; day <= 6; day++)
        {
            shifts.Add(CreateShift(
                startUtc: new DateTime(2026, 6, day, 6, 30, 0, DateTimeKind.Utc),
                endUtc: new DateTime(2026, 6, day, 14, 30, 0, DateTimeKind.Utc),
                kind: ShiftKind.Morning,
                minResourceCount: 1,
                maxResourceCount: 1));
        }

        for (var day = 1; day <= 5; day++)
        {
            shifts.Add(CreateShift(
                startUtc: new DateTime(2026, 6, day, 14, 30, 0, DateTimeKind.Utc),
                endUtc: new DateTime(2026, 6, day, 22, 30, 0, DateTimeKind.Utc),
                kind: ShiftKind.Afternoon,
                minResourceCount: 1,
                maxResourceCount: 1));
        }

        return shifts;
    }

    private static IReadOnlyList<Shift> CreateOptionalShifts()
    {
        return
        [
            CreateOptionalShift(new DateTime(2026, 6, 1, 22, 30, 0, DateTimeKind.Utc), ShiftKind.Night, true),
            CreateOptionalShift(new DateTime(2026, 6, 3, 22, 30, 0, DateTimeKind.Utc), ShiftKind.Night, true),
            CreateOptionalShift(new DateTime(2026, 6, 5, 22, 30, 0, DateTimeKind.Utc), ShiftKind.Night, true),
            CreateOptionalShift(new DateTime(2026, 6, 6, 14, 30, 0, DateTimeKind.Utc), ShiftKind.Afternoon, false),
            CreateOptionalShift(new DateTime(2026, 6, 7, 14, 30, 0, DateTimeKind.Utc), ShiftKind.Afternoon, false)
        ];
    }

    private static void AddRegularAvailability(
        IReadOnlyCollection<Resource> resources,
        IReadOnlyCollection<Shift> shifts,
        ICollection<AvailabilityWindow> availabilityWindows)
    {
        foreach (var resource in resources)
        {
            foreach (var shift in shifts)
            {
                availabilityWindows.Add(CreateAvailability(resource, shift));
            }
        }
    }

    private static void AddAvoidPreferences(
        Resource resource,
        IEnumerable<Shift> shifts,
        ICollection<ResourcePreference> preferences)
    {
        foreach (var shift in shifts)
        {
            preferences.Add(new ResourcePreference(
                resource.Id,
                shift.StartUtc,
                shift.EndUtc,
                ResourcePreferenceType.Avoid,
                ResourcePreferencePriority.High));
        }
    }

    private static void AddOptionalDemand(
        IReadOnlyList<Resource> resources,
        IReadOnlyList<Shift> optionalShifts,
        ICollection<AvailabilityWindow> availabilityWindows,
        ICollection<ResourcePreference> preferences)
    {
        AddPreferDemand(resources[2], optionalShifts[0], availabilityWindows, preferences);
        AddPreferDemand(resources[3], optionalShifts[0], availabilityWindows, preferences);

        AddPreferDemand(resources[4], optionalShifts[1], availabilityWindows, preferences);
        AddPreferDemand(resources[5], optionalShifts[1], availabilityWindows, preferences);

        AddPreferDemand(resources[6], optionalShifts[2], availabilityWindows, preferences);
        AddPreferDemand(resources[7], optionalShifts[2], availabilityWindows, preferences);

        AddPreferDemand(resources[1], optionalShifts[3], availabilityWindows, preferences);
        AddPreferDemand(resources[2], optionalShifts[3], availabilityWindows, preferences);

        AddPreferDemand(resources[4], optionalShifts[4], availabilityWindows, preferences);
        AddPreferDemand(resources[6], optionalShifts[4], availabilityWindows, preferences);
    }

    private static string FormatReport(
        ExperimentScenario scenario,
        params ExperimentRun[] runs)
    {
        var builder = new StringBuilder();

        builder.AppendLine("Genetic Optimizer Controlled Experiment");
        builder.AppendLine("Resources: 8");
        builder.AppendLine($"Shifts: {scenario.Shifts.Count}");
        builder.AppendLine("MinimumAssignedHoursPerResource: 16");
        builder.AppendLine();

        foreach (var run in runs)
        {
            AppendRun(builder, scenario, run);
            builder.AppendLine();
        }

        if (runs.Length > 0)
        {
            AppendAvoidSwapDiagnostics(builder, scenario, runs[^1]);
            builder.AppendLine();
            AppendChainReassignDiagnostics(builder, scenario, runs[^1]);
            builder.AppendLine();
            AppendCleanLandingDiagnostics(builder, scenario, runs[^1]);
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
        builder.AppendLine($"OptionalPreferredAssignments: {CountOptionalPreferredAssignments(scenario, run.Result.Candidate)}");

        AppendViolations(builder, evaluation);
        AppendResourceLoad(builder, scenario, run.Result.Candidate);
        AppendGenerationDiagnostics(builder, run);
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

    private static void AppendResourceLoad(
        StringBuilder builder,
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        var shiftsById = scenario.Shifts.ToDictionary(shift => shift.Id);

        var hoursByResource = candidate.Assignments
            .GroupBy(assignment => assignment.ResourceId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(assignment =>
                    (shiftsById[assignment.ShiftId].EndUtc -
                     shiftsById[assignment.ShiftId].StartUtc).TotalHours));

        var assignmentsByResource = candidate.Assignments
            .GroupBy(assignment => assignment.ResourceId)
            .ToDictionary(group => group.Key, group => group.Count());

        builder.AppendLine("ResourceLoad:");

        foreach (var resource in scenario.Resources)
        {
            var hours = hoursByResource.GetValueOrDefault(resource.Id, 0);
            var assignments = assignmentsByResource.GetValueOrDefault(resource.Id, 0);

            builder.AppendLine(
                $"- {scenario.ResourceNames[resource.Id]}: {hours:0.0}h, assignments={assignments}");
        }
    }

    private static int CountOptionalPreferredAssignments(
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        var shiftsById = scenario.Shifts.ToDictionary(shift => shift.Id);

        return candidate.Assignments.Count(assignment =>
        {
            var shift = shiftsById[assignment.ShiftId];

            return shift.MinResourceCount == 0 &&
                   scenario.Problem.ResourcePreferences.Any(preference =>
                       preference.ResourceId == assignment.ResourceId &&
                       preference.Type == ResourcePreferenceType.Prefer &&
                       Overlaps(
                           preference.StartUtc,
                           preference.EndUtc,
                           shift.StartUtc,
                           shift.EndUtc));
        });
    }


    private static void AppendAvoidSwapDiagnostics(
        StringBuilder builder,
        ExperimentScenario scenario,
        ExperimentRun run)
    {
        builder.AppendLine("Avoid Swap Diagnostics:");

        var conflicts = GetAvoidConflictAssignments(
                scenario.Problem,
                run.Result.Candidate)
            .ToArray();

        if (conflicts.Length == 0)
        {
            builder.AppendLine("- none");
            return;
        }

        var evaluator = new ScheduleEvaluator();
        var ranker = new ScheduleEvaluationResultRanker();

        foreach (var conflict in conflicts)
        {
            builder.AppendLine(
                $"Conflict: {scenario.ResourceNames[conflict.Assignment.ResourceId]} has Avoid on {FormatShift(conflict.Shift)}.");

            var attempts = new List<SwapAttempt>();

            foreach (var replacement in scenario.Resources.Where(resource =>
                         resource.Id != conflict.Assignment.ResourceId))
            {
                var candidate = ReplaceAssignment(
                    run.Result.Candidate,
                    conflict.Assignment,
                    replacement,
                    conflict.Shift);

                var evaluation = evaluator.Evaluate(
                    scenario.Problem,
                    candidate);

                var reasons = GetSwapBlockReasons(
                    scenario.Problem,
                    run.Result.Candidate,
                    conflict.Assignment,
                    conflict.Shift,
                    replacement,
                    evaluation);

                var improves = evaluation.IsFeasible &&
                               ranker.IsBetterThan(
                                   evaluation,
                                   run.Result.Evaluation);

                attempts.Add(new SwapAttempt(
                    Replacement: replacement,
                    Evaluation: evaluation,
                    BlockReasons: reasons,
                    Improves: improves));

                AppendSwapAttempt(
                    builder,
                    scenario,
                    conflict,
                    replacement,
                    evaluation,
                    reasons,
                    improves);
            }

            AppendSwapConclusion(builder, attempts);
        }
    }



    private static void AppendChainReassignDiagnostics(
        StringBuilder builder,
        ExperimentScenario scenario,
        ExperimentRun run)
    {
        builder.AppendLine("Chain Reassign Diagnostics:");

        var conflicts = GetAvoidConflictAssignments(
                scenario.Problem,
                run.Result.Candidate)
            .ToArray();

        if (conflicts.Length == 0)
        {
            builder.AppendLine("- none");
            return;
        }

        var evaluator = new ScheduleEvaluator();
        var ranker = new ScheduleEvaluationResultRanker();

        foreach (var conflict in conflicts)
        {
            builder.AppendLine(
                $"Conflict: {scenario.ResourceNames[conflict.Assignment.ResourceId]} has Avoid on {FormatShift(conflict.Shift)}.");

            var attempts = new List<ChainReassignAttempt>();

            foreach (var secondAssignment in run.Result.Candidate.Assignments)
            {
                if (IsSameAssignment(secondAssignment, conflict.Assignment))
                {
                    continue;
                }

                if (secondAssignment.ResourceId == conflict.Assignment.ResourceId)
                {
                    continue;
                }

                var replacement = scenario.Resources.Single(resource =>
                    resource.Id == secondAssignment.ResourceId);

                var secondShift = scenario.Shifts.Single(shift =>
                    shift.Id == secondAssignment.ShiftId);

                if (secondShift.Id == conflict.Shift.Id)
                {
                    continue;
                }

                if (!TryCreateChainReassignCandidate(
                        run.Result.Candidate,
                        conflict.Assignment,
                        conflict.Shift,
                        replacement,
                        secondAssignment,
                        secondShift,
                        out var candidate,
                        out var rejectionReason))
                {
                    var rejectedAttempt = new ChainReassignAttempt(
                        Replacement: replacement,
                        SecondShift: secondShift,
                        Evaluation: null,
                        Improves: false,
                        RejectionReason: rejectionReason);

                    attempts.Add(rejectedAttempt);

                    AppendChainReassignAttempt(
                        builder,
                        scenario,
                        conflict,
                        rejectedAttempt);

                    continue;
                }

                var evaluation = evaluator.Evaluate(
                    scenario.Problem,
                    candidate!);

                var improves = evaluation.IsFeasible &&
                               ranker.IsBetterThan(
                                   evaluation,
                                   run.Result.Evaluation);

                var attempt = new ChainReassignAttempt(
                    Replacement: replacement,
                    SecondShift: secondShift,
                    Evaluation: evaluation,
                    Improves: improves,
                    RejectionReason: null);

                attempts.Add(attempt);

                AppendChainReassignAttempt(
                    builder,
                    scenario,
                    conflict,
                    attempt);
            }

            AppendChainReassignConclusion(builder, attempts);
        }
    }

    private static bool TryCreateChainReassignCandidate(
        ScheduleCandidate candidate,
        Assignment avoidedAssignment,
        Shift avoidedShift,
        Resource replacement,
        Assignment secondAssignment,
        Shift secondShift,
        out ScheduleCandidate? chainCandidate,
        out string? rejectionReason)
    {
        var baseAssignments = candidate.Assignments
            .Where(assignment => !IsSameAssignment(assignment, avoidedAssignment))
            .Where(assignment => !IsSameAssignment(assignment, secondAssignment))
            .ToList();

        var replacementToAvoidedShift = new Assignment(
            replacement.Id,
            avoidedShift.Id);

        var avoidedResourceToSecondShift = new Assignment(
            avoidedAssignment.ResourceId,
            secondShift.Id);

        if (baseAssignments.Any(assignment =>
                IsSameAssignment(assignment, replacementToAvoidedShift)))
        {
            chainCandidate = null;
            rejectionReason = "replacement is already assigned to avoided shift";
            return false;
        }

        if (baseAssignments.Any(assignment =>
                IsSameAssignment(assignment, avoidedResourceToSecondShift)))
        {
            chainCandidate = null;
            rejectionReason = "avoided resource is already assigned to second shift";
            return false;
        }

        var assignments = baseAssignments
            .Concat(new[]
            {
                replacementToAvoidedShift,
                avoidedResourceToSecondShift
            })
            .ToArray();

        chainCandidate = new ScheduleCandidate(assignments);
        rejectionReason = null;
        return true;
    }

    private static void AppendChainReassignAttempt(
        StringBuilder builder,
        ExperimentScenario scenario,
        AvoidConflict conflict,
        ChainReassignAttempt attempt)
    {
        var originalName = scenario.ResourceNames[conflict.Assignment.ResourceId];
        var replacementName = scenario.ResourceNames[attempt.Replacement.Id];

        if (attempt.Evaluation is null)
        {
            builder.AppendLine(
                $"- Can we chain {originalName} with {replacementName} from {FormatShift(attempt.SecondShift)}? NO. Reason: {attempt.RejectionReason}.");
            return;
        }

        if (!attempt.Evaluation.IsFeasible)
        {
            builder.AppendLine(
                $"- Can we chain {originalName} with {replacementName} from {FormatShift(attempt.SecondShift)}? NO. Violations: {FormatViolationSummary(attempt.Evaluation)}.");
            return;
        }

        if (!attempt.Improves)
        {
            builder.AppendLine(
                $"- Can we chain {originalName} with {replacementName} from {FormatShift(attempt.SecondShift)}? YES, but it does not improve ranking. TotalPenalty={attempt.Evaluation.Score.TotalPenalty}, SoftViolations={attempt.Evaluation.Score.SoftViolationCount}, Violations: {FormatViolationSummary(attempt.Evaluation)}.");
            return;
        }

        builder.AppendLine(
            $"- Can we chain {originalName} with {replacementName} from {FormatShift(attempt.SecondShift)}? YES, and it improves ranking. TotalPenalty={attempt.Evaluation.Score.TotalPenalty}, SoftViolations={attempt.Evaluation.Score.SoftViolationCount}, Violations: {FormatViolationSummary(attempt.Evaluation)}.");
    }

    private static void AppendChainReassignConclusion(
        StringBuilder builder,
        IReadOnlyCollection<ChainReassignAttempt> attempts)
    {
        if (attempts.Count == 0)
        {
            builder.AppendLine("Conclusion: No second assignment exists for chain reassign.");
            return;
        }

        if (attempts.All(attempt =>
                attempt.Evaluation is null ||
                !attempt.Evaluation.IsFeasible))
        {
            builder.AppendLine("Conclusion: No feasible chain reassign exists for this Avoid violation.");
            return;
        }

        if (attempts.Any(attempt => attempt.Improves))
        {
            builder.AppendLine("Conclusion: At least one improving chain reassign exists. Mutation/evolution did not find it in this run.");
            return;
        }

        builder.AppendLine("Conclusion: Feasible chain reassign moves exist, but none improves the ranked result.");
    }

    private static string FormatViolationSummary(
        ScheduleEvaluationResult evaluation)
    {
        var groups = evaluation.Violations
            .GroupBy(violation => violation.Type)
            .OrderBy(group => group.Key.ToString())
            .Select(group => $"{group.Key} x{group.Count()}")
            .ToArray();

        return groups.Length == 0
            ? "none"
            : string.Join(", ", groups);
    }



    private static void AppendCleanLandingDiagnostics(
        StringBuilder builder,
        ExperimentScenario scenario,
        ExperimentRun run)
    {
        builder.AppendLine("Clean Landing Diagnostics:");

        var conflicts = GetAvoidConflictAssignments(
                scenario.Problem,
                run.Result.Candidate)
            .ToArray();

        if (conflicts.Length == 0)
        {
            builder.AppendLine("- none");
            return;
        }

        var evaluator = new ScheduleEvaluator();

        foreach (var conflict in conflicts)
        {
            var resource = scenario.Resources.Single(candidateResource =>
                candidateResource.Id == conflict.Assignment.ResourceId);

            builder.AppendLine(
                $"Resource: {scenario.ResourceNames[resource.Id]} currently has Avoid on {FormatShift(conflict.Shift)}.");

            var cleanAddCount = 0;
            var cleanMoveCount = 0;

            foreach (var landingShift in scenario.Shifts.OrderBy(shift => shift.StartUtc))
            {
                if (landingShift.Id == conflict.Shift.Id)
                {
                    continue;
                }

                var addReasons = GetCleanLandingBlockReasons(
                    scenario.Problem,
                    run.Result.Candidate,
                    resource,
                    landingShift,
                    ignoredAssignment: null);

                if (addReasons.Count == 0)
                {
                    var addCandidate = AddAssignment(
                        run.Result.Candidate,
                        resource,
                        landingShift);

                    var addEvaluation = evaluator.Evaluate(
                        scenario.Problem,
                        addCandidate);

                    if (addEvaluation.IsFeasible)
                    {
                        cleanAddCount++;

                        builder.AppendLine(
                            $"- Add-only landing on {FormatShift(landingShift)}: YES. TotalPenalty={addEvaluation.Score.TotalPenalty}, Violations={FormatViolationSummary(addEvaluation)}.");
                    }
                    else
                    {
                        builder.AppendLine(
                            $"- Add-only landing on {FormatShift(landingShift)}: NO. Evaluation violations={FormatViolationSummary(addEvaluation)}.");
                    }
                }
                else
                {
                    builder.AppendLine(
                        $"- Add-only landing on {FormatShift(landingShift)}: NO. Reasons: {string.Join("; ", addReasons)}.");
                }

                var moveReasons = GetCleanLandingBlockReasons(
                    scenario.Problem,
                    run.Result.Candidate,
                    resource,
                    landingShift,
                    ignoredAssignment: conflict.Assignment);

                if (moveReasons.Count == 0)
                {
                    var moveCandidate = MoveAssignment(
                        run.Result.Candidate,
                        conflict.Assignment,
                        resource,
                        landingShift);

                    var moveEvaluation = evaluator.Evaluate(
                        scenario.Problem,
                        moveCandidate);

                    if (moveEvaluation.IsFeasible)
                    {
                        cleanMoveCount++;

                        builder.AppendLine(
                            $"  Move-after-removing-avoid landing on {FormatShift(landingShift)}: YES. TotalPenalty={moveEvaluation.Score.TotalPenalty}, Violations={FormatViolationSummary(moveEvaluation)}.");
                    }
                    else
                    {
                        builder.AppendLine(
                            $"  Move-after-removing-avoid landing on {FormatShift(landingShift)}: NO. Evaluation violations={FormatViolationSummary(moveEvaluation)}.");
                    }
                }
            }

            AppendCleanLandingConclusion(
                builder,
                cleanAddCount,
                cleanMoveCount);
        }
    }


    private static bool HasAvoidPreferenceForShift(
        SchedulingProblem problem,
        Resource resource,
        Shift shift)
    {
        return problem.ResourcePreferences.Any(preference =>
            preference.ResourceId == resource.Id &&
            preference.Type == ResourcePreferenceType.Avoid &&
            Overlaps(
                preference.StartUtc,
                preference.EndUtc,
                shift.StartUtc,
                shift.EndUtc));
    }

    private static void AppendCleanLandingConclusion(
        StringBuilder builder,
        int cleanAddCount,
        int cleanMoveCount)
    {
        if (cleanAddCount > 0)
        {
            builder.AppendLine(
                $"Conclusion: Clean add landing exists ({cleanAddCount}). Smart Add may be useful.");
            return;
        }

        if (cleanMoveCount > 0)
        {
            builder.AppendLine(
                $"Conclusion: Clean move landing exists ({cleanMoveCount}), but no add-only landing exists.");
            return;
        }

        builder.AppendLine(
            "Conclusion: No clean landing exists for this resource under the current candidate and scenario facts.");
    }

    private static IReadOnlyCollection<string> GetCleanLandingBlockReasons(
        SchedulingProblem problem,
        ScheduleCandidate candidate,
        Resource resource,
        Shift landingShift,
        Assignment? ignoredAssignment)
    {
        var reasons = new List<string>();

        if (HasAvoidPreferenceForShift(problem, resource, landingShift))
        {
            reasons.Add("resource has Avoid on landing shift");
        }

        if (!IsAvailableForShift(problem, resource, landingShift))
        {
            reasons.Add("resource has no AvailabilityWindow coverage");
        }

        if (landingShift.RequiresPreferenceToAssign &&
            !HasPreferPreferenceForShift(problem, resource, landingShift))
        {
            reasons.Add("resource is missing required Prefer");
        }

        if (IsAlreadyAssignedToShift(
                candidate,
                ignoredAssignment,
                resource,
                landingShift))
        {
            reasons.Add("resource is already assigned to landing shift");
        }

        if (HasOverlappingAssignment(
                candidate,
                ignoredAssignment,
                resource,
                landingShift,
                problem.Shifts))
        {
            reasons.Add("resource has overlapping assignment");
        }

        var assignedCount = candidate.Assignments
            .Where(assignment => ignoredAssignment is null ||
                                 !IsSameAssignment(assignment, ignoredAssignment))
            .Count(assignment => assignment.ShiftId == landingShift.Id);

        if (assignedCount >= landingShift.MaxResourceCount)
        {
            reasons.Add($"landing shift is already at MaxResourceCount={landingShift.MaxResourceCount}");
        }

        return reasons
            .Distinct()
            .ToArray();
    }

    private static IReadOnlyList<AvoidConflict> GetAvoidConflictAssignments(
        SchedulingProblem problem,
        ScheduleCandidate candidate)
    {
        var shiftsById = problem.Shifts.ToDictionary(shift => shift.Id);
        var conflicts = new List<AvoidConflict>();

        foreach (var assignment in candidate.Assignments)
        {
            var shift = shiftsById[assignment.ShiftId];

            var hasAvoid = problem.ResourcePreferences.Any(preference =>
                preference.ResourceId == assignment.ResourceId &&
                preference.Type == ResourcePreferenceType.Avoid &&
                Overlaps(
                    preference.StartUtc,
                    preference.EndUtc,
                    shift.StartUtc,
                    shift.EndUtc));

            if (hasAvoid)
            {
                conflicts.Add(new AvoidConflict(
                    Assignment: assignment,
                    Shift: shift));
            }
        }

        return conflicts;
    }


    private static ScheduleCandidate AddAssignment(
        ScheduleCandidate candidate,
        Resource resource,
        Shift shift)
    {
        var assignments = candidate.Assignments
            .Concat(new[]
            {
                new Assignment(resource.Id, shift.Id)
            })
            .ToArray();

        return new ScheduleCandidate(assignments);
    }

    private static ScheduleCandidate MoveAssignment(
        ScheduleCandidate candidate,
        Assignment originalAssignment,
        Resource resource,
        Shift landingShift)
    {
        var assignments = candidate.Assignments
            .Select(assignment =>
                IsSameAssignment(assignment, originalAssignment)
                    ? new Assignment(resource.Id, landingShift.Id)
                    : assignment)
            .ToArray();

        return new ScheduleCandidate(assignments);
    }

    private static ScheduleCandidate ReplaceAssignment(
        ScheduleCandidate candidate,
        Assignment originalAssignment,
        Resource replacement,
        Shift shift)
    {
        var assignments = candidate.Assignments
            .Select(assignment =>
                IsSameAssignment(assignment, originalAssignment)
                    ? new Assignment(replacement.Id, shift.Id)
                    : assignment)
            .ToArray();

        return new ScheduleCandidate(assignments);
    }

    private static bool IsSameAssignment(
        Assignment first,
        Assignment second)
    {
        return first.ResourceId == second.ResourceId &&
               first.ShiftId == second.ShiftId;
    }


    private static IReadOnlyList<string> GetSwapBlockReasons(
        SchedulingProblem problem,
        ScheduleCandidate candidate,
        Assignment originalAssignment,
        Shift shift,
        Resource replacement,
        ScheduleEvaluationResult evaluation)
    {
        var reasons = new List<string>();

        if (!IsAvailableForShift(problem, replacement, shift))
        {
            reasons.Add("replacement has no AvailabilityWindow coverage");
        }

        if (shift.RequiresPreferenceToAssign &&
            !HasPreferPreferenceForShift(problem, replacement, shift))
        {
            reasons.Add("replacement is missing required Prefer");
        }

        if (IsAlreadyAssignedToShift(candidate, originalAssignment, replacement, shift))
        {
            reasons.Add("replacement is already assigned to this shift");
        }

        if (HasOverlappingAssignment(candidate, originalAssignment, replacement, shift, problem.Shifts))
        {
            reasons.Add("replacement has overlapping assignment");
        }

        var hardViolations = evaluation.Violations
            .Where(violation => violation.Severity == ConstraintViolationSeverity.Hard)
            .GroupBy(violation => violation.Type)
            .OrderBy(group => group.Key.ToString())
            .Select(group => $"{group.Key} x{group.Count()}")
            .ToArray();

        foreach (var hardViolation in hardViolations)
        {
            reasons.Add($"candidate hard violation: {hardViolation}");
        }

        return reasons
            .Distinct()
            .ToArray();
    }

    private static bool IsAvailableForShift(
        SchedulingProblem problem,
        Resource resource,
        Shift shift)
    {
        return problem.AvailabilityWindows.Any(window =>
            window.ResourceId == resource.Id &&
            window.Covers(shift));
    }

    private static bool HasPreferPreferenceForShift(
        SchedulingProblem problem,
        Resource resource,
        Shift shift)
    {
        return problem.ResourcePreferences.Any(preference =>
            preference.ResourceId == resource.Id &&
            preference.Type == ResourcePreferenceType.Prefer &&
            Overlaps(
                preference.StartUtc,
                preference.EndUtc,
                shift.StartUtc,
                shift.EndUtc));
    }


    private static bool IsAlreadyAssignedToShift(
        ScheduleCandidate candidate,
        Assignment? ignoredAssignment,
        Resource resource,
        Shift shift)
    {
        return candidate.Assignments
            .Where(assignment => ignoredAssignment is null ||
                                 !IsSameAssignment(assignment, ignoredAssignment))
            .Any(assignment =>
                assignment.ResourceId == resource.Id &&
                assignment.ShiftId == shift.Id);
    }

    private static bool HasOverlappingAssignment(
        ScheduleCandidate candidate,
        Assignment? ignoredAssignment,
        Resource resource,
        Shift candidateShift,
        IReadOnlyCollection<Shift> shifts)
    {
        var shiftsById = shifts.ToDictionary(shift => shift.Id);

        return candidate.Assignments
            .Where(assignment => ignoredAssignment is null ||
                                 !IsSameAssignment(assignment, ignoredAssignment))
            .Where(assignment => assignment.ResourceId == resource.Id)
            .Select(assignment => shiftsById[assignment.ShiftId])
            .Any(existingShift => Overlaps(
                existingShift.StartUtc,
                existingShift.EndUtc,
                candidateShift.StartUtc,
                candidateShift.EndUtc));
    }


    private static void AppendSwapAttempt(
        StringBuilder builder,
        ExperimentScenario scenario,
        AvoidConflict conflict,
        Resource replacement,
        ScheduleEvaluationResult evaluation,
        IReadOnlyCollection<string> reasons,
        bool improves)
    {
        var originalName = scenario.ResourceNames[conflict.Assignment.ResourceId];
        var replacementName = scenario.ResourceNames[replacement.Id];

        if (!evaluation.IsFeasible)
        {
            builder.AppendLine(
                $"- Can we swap {originalName} with {replacementName}? NO. Reasons: {string.Join("; ", reasons)}.");
            return;
        }

        if (!improves)
        {
            builder.AppendLine(
                $"- Can we swap {originalName} with {replacementName}? YES, but it does not improve ranking. TotalPenalty={evaluation.Score.TotalPenalty}, SoftViolations={evaluation.Score.SoftViolationCount}.");
            return;
        }

        builder.AppendLine(
            $"- Can we swap {originalName} with {replacementName}? YES, and it improves ranking. TotalPenalty={evaluation.Score.TotalPenalty}, SoftViolations={evaluation.Score.SoftViolationCount}.");
    }

    private static void AppendSwapConclusion(
        StringBuilder builder,
        IReadOnlyCollection<SwapAttempt> attempts)
    {
        if (attempts.All(attempt => !attempt.Evaluation.IsFeasible))
        {
            builder.AppendLine("Conclusion: This Avoid violation is locked for the current single-reassign mutation by hard constraints.");
            return;
        }

        if (attempts.Any(attempt => attempt.Improves))
        {
            builder.AppendLine("Conclusion: At least one improving swap exists. Mutation/evolution did not find it in this run.");
            return;
        }

        builder.AppendLine("Conclusion: Feasible swaps exist, but none improves the ranked result.");
    }

    private static string FormatShift(Shift shift)
    {
        return $"{shift.Kind} ({shift.StartUtc:yyyy-MM-dd HH:mm} - {shift.EndUtc:yyyy-MM-dd HH:mm} UTC)";
    }

    private static void AddPreferDemand(
        Resource resource,
        Shift shift,
        ICollection<AvailabilityWindow> availabilityWindows,
        ICollection<ResourcePreference> preferences)
    {
        availabilityWindows.Add(CreateAvailability(resource, shift));

        preferences.Add(new ResourcePreference(
            resource.Id,
            shift.StartUtc,
            shift.EndUtc,
            ResourcePreferenceType.Prefer,
            ResourcePreferencePriority.High));
    }

    private static Resource CreateResource(string name)
    {
        return new Resource(
            Guid.NewGuid(),
            name,
            hourlyCost: 100m);
    }

    private static Shift CreateOptionalShift(
        DateTime startUtc,
        ShiftKind kind,
        bool requiresMinimumWhenPreferenceExists)
    {
        var endUtc = kind == ShiftKind.Night
            ? startUtc.AddHours(8)
            : startUtc.AddHours(8);

        return CreateShift(
            startUtc,
            endUtc,
            kind,
            minResourceCount: 0,
            maxResourceCount: 1,
            requiresPreferenceToAssign: true,
            requiresMinimumWhenPreferenceExists: requiresMinimumWhenPreferenceExists);
    }

    private static Shift CreateShift(
        DateTime startUtc,
        DateTime endUtc,
        ShiftKind kind,
        int minResourceCount,
        int maxResourceCount,
        bool requiresPreferenceToAssign = false,
        bool requiresMinimumWhenPreferenceExists = false)
    {
        return new Shift(
            Guid.NewGuid(),
            startUtc,
            endUtc,
            kind,
            minResourceCount: minResourceCount,
            maxResourceCount: maxResourceCount,
            requiresPreferenceToAssign: requiresPreferenceToAssign,
            requiresMinimumWhenPreferenceExists: requiresMinimumWhenPreferenceExists);
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

    private static bool Overlaps(
        DateTime firstStartUtc,
        DateTime firstEndUtc,
        DateTime secondStartUtc,
        DateTime secondEndUtc)
    {
        return firstStartUtc < secondEndUtc &&
               secondStartUtc < firstEndUtc;
    }

    private sealed record ExperimentScenario(
        SchedulingProblem Problem,
        IReadOnlyList<Resource> Resources,
        IReadOnlyList<Shift> Shifts,
        IReadOnlyDictionary<Guid, string> ResourceNames);

    private sealed record ExperimentRun(
        string Name,
        ScheduleOptimizationResult Result,
        TimeSpan Elapsed,
        IReadOnlyList<GeneticGenerationDiagnostic> Diagnostics);

    private sealed record AvoidConflict(
        Assignment Assignment,
        Shift Shift);

    private sealed record SwapAttempt(
        Resource Replacement,
        ScheduleEvaluationResult Evaluation,
        IReadOnlyCollection<string> BlockReasons,
        bool Improves);

    private sealed record ChainReassignAttempt(
        Resource Replacement,
        Shift SecondShift,
        ScheduleEvaluationResult? Evaluation,
        bool Improves,
        string? RejectionReason);
}
