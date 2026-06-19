using Aether.Application.Scheduling.Optimization;
using Aether.Application.Scheduling.SubmissionForms;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class FormSubmissionCleanGeneticAcceptanceTests
{
    private const int AcceptedPopulationSize = 120;
    private const int AcceptedGenerationCount = 100;
    private const int AcceptedSeed = 20260603;

    [Fact]
    public void CleanGeneticOptimizer_ShouldRunAcceptedConfiguration_FromNewFormProblem()
    {
        var scenario = CreateScenario();

        var buildRequest = new FormSubmissionSchedulingProblemBuildRequest(
            scenario.Period,
            scenario.Resources,
            scenario.Shifts,
            scenario.WorkerSubmissions,
            TotalEffectiveTargetHours: 24);

        var problemBuildResult = new FormSubmissionSchedulingProblemBuilder()
            .Build(buildRequest);

        Assert.Empty(problemBuildResult.Warnings);
        Assert.Equal(3, problemBuildResult.Problem.Shifts.Count);
        Assert.Equal(3, problemBuildResult.Problem.AvailabilityWindows.Count);
        Assert.Equal(3, problemBuildResult.Problem.ResourcePreferences.Count);
        Assert.Equal(2, problemBuildResult.Problem.ResourceWorkloadDemands.Count);

        var diagnosticsSink = new CollectingGeneticOptimizerDiagnosticsSink();

        var optimizer = new GeneticScheduleOptimizer(
            populationSize: AcceptedPopulationSize,
            seed: AcceptedSeed,
            generationCount: AcceptedGenerationCount,
            eliteCount: 1,
            tournamentSize: 3,
            diagnosticsSink: diagnosticsSink,
            evolutionMode: GeneticEvolutionMode.Clean,
            scoringWeights: ScheduleScoringWeights.CreateDefault());

        var result = optimizer.Optimize(problemBuildResult.Problem);

        Assert.NotNull(result.Candidate);
        Assert.NotNull(result.Evaluation);
        Assert.NotEmpty(result.Candidate.Assignments);

        Assert.Equal(
            AcceptedGenerationCount + 1,
            diagnosticsSink.Diagnostics.Count);

        Assert.Equal(
            Enumerable.Range(0, AcceptedGenerationCount + 1).ToArray(),
            diagnosticsSink.Diagnostics
                .Select(diagnostic => diagnostic.GenerationIndex)
                .ToArray());

        Assert.All(diagnosticsSink.Diagnostics, diagnostic =>
        {
            Assert.Equal(AcceptedPopulationSize, diagnostic.PopulationSize);
            Assert.InRange(diagnostic.FeasibleCandidateCount, 0, AcceptedPopulationSize);
        });

        AssertCandidateReferencesKnownProblemEntities(
            problemBuildResult.Problem,
            result.Candidate);

        AssertNoBasicStructuralViolations(result.Evaluation);

        Assert.True(
            result.Evaluation.IsFeasible,
            $"Expected accepted Clean GA run from new form problem to be feasible, but got {result.Evaluation.Score.HardViolationCount} hard violations.");

        Assert.True(
            diagnosticsSink.Diagnostics[^1].BestSoFarTotalPenalty <=
            diagnosticsSink.Diagnostics[0].BestSoFarTotalPenalty,
            "Accepted Clean GA run should not return a best-so-far penalty worse than generation 0.");
    }

    private static TestScenario CreateScenario()
    {
        var firstResource = new Resource(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "Guard01",
            hourlyCost: 0);

        var secondResource = new Resource(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            "Guard02",
            hourlyCost: 0);

        var periodStartUtc = new DateTime(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc);
        var periodEndUtc = periodStartUtc.AddDays(14);

        var firstMorningStartUtc = new DateTime(2026, 6, 14, 6, 30, 0, DateTimeKind.Utc);
        var firstMorningEndUtc = new DateTime(2026, 6, 14, 14, 30, 0, DateTimeKind.Utc);

        var firstAfternoonStartUtc = new DateTime(2026, 6, 15, 14, 30, 0, DateTimeKind.Utc);
        var firstAfternoonEndUtc = new DateTime(2026, 6, 15, 22, 30, 0, DateTimeKind.Utc);

        var firstNightStartUtc = new DateTime(2026, 6, 16, 22, 30, 0, DateTimeKind.Utc);
        var firstNightEndUtc = new DateTime(2026, 6, 17, 6, 30, 0, DateTimeKind.Utc);

        var firstMorningShift = new Shift(
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            firstMorningStartUtc,
            firstMorningEndUtc,
            ShiftKind.Morning,
            minResourceCount: 1,
            maxResourceCount: 1);

        var firstAfternoonShift = new Shift(
            Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            firstAfternoonStartUtc,
            firstAfternoonEndUtc,
            ShiftKind.Afternoon,
            minResourceCount: 1,
            maxResourceCount: 1,
            requiresPreferenceToAssign: true);

        var firstNightShift = new Shift(
            Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
            firstNightStartUtc,
            firstNightEndUtc,
            ShiftKind.Night,
            minResourceCount: 1,
            maxResourceCount: 1,
            requiresPreferenceToAssign: true,
            nightShiftCategory: NightShiftCategory.Regular);

        var submissions = new[]
        {
            new WorkerSubmission(
                firstResource.Id,
                [
                    new WorkerShiftSubmission(
                        DateOnly.FromDateTime(firstMorningStartUtc),
                        ShiftKind.Morning,
                        ShiftSubmissionChoice.StrongAvailable),
                    new WorkerShiftSubmission(
                        DateOnly.FromDateTime(firstAfternoonStartUtc),
                        ShiftKind.Afternoon,
                        ShiftSubmissionChoice.StrongAvailable)
                ]),
            new WorkerSubmission(
                secondResource.Id,
                [
                    new WorkerShiftSubmission(
                        DateOnly.FromDateTime(firstNightStartUtc),
                        ShiftKind.Night,
                        ShiftSubmissionChoice.Available)
                ])
        };

        return new TestScenario(
            Period: new SchedulePeriod(periodStartUtc, periodEndUtc),
            Resources: [firstResource, secondResource],
            Shifts: [firstMorningShift, firstAfternoonShift, firstNightShift],
            WorkerSubmissions: submissions);
    }

    private static void AssertCandidateReferencesKnownProblemEntities(
        SchedulingProblem problem,
        ScheduleCandidate candidate)
    {
        var resourceIds = problem.Resources
            .Select(resource => resource.Id)
            .ToHashSet();

        var shiftIds = problem.Shifts
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
        Assert.DoesNotContain(
            evaluation.Violations,
            violation => violation.Type == ConstraintViolationType.ResourceUnavailable);

        Assert.DoesNotContain(
            evaluation.Violations,
            violation => violation.Type == ConstraintViolationType.ResourceAssignedToOverlappingShifts);

        Assert.DoesNotContain(
            evaluation.Violations,
            violation => violation.Type == ConstraintViolationType.AssignedWithoutRequiredPreference);

        Assert.DoesNotContain(
            evaluation.Violations,
            violation => violation.Type == ConstraintViolationType.ShiftUnderstaffed);

        Assert.DoesNotContain(
            evaluation.Violations,
            violation => violation.Type == ConstraintViolationType.ShiftOverstaffed);
    }

    private sealed record TestScenario(
        SchedulePeriod Period,
        IReadOnlyCollection<Resource> Resources,
        IReadOnlyCollection<Shift> Shifts,
        IReadOnlyCollection<WorkerSubmission> WorkerSubmissions);
}
