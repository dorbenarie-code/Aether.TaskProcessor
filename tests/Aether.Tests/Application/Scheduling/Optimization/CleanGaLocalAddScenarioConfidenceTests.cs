using Aether.Application.Scheduling.Diagnostics;
using Aether.Application.Scheduling.Optimization;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling.Optimization;

public sealed class CleanGaLocalAddScenarioConfidenceTests
{
    private const double HoursTolerance = 0.000001;

    [Fact]
    public void CurrentDemoRegression_ShouldApplyLocalAddAndExhaustScoreImprovingAddMoves()
    {
        var underTargetResource = CreateResource(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "Ziv");

        var assignedResource = CreateResource(
            "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
            "Rafael");

        var shift = CreateShift(
            "cccccccc-cccc-cccc-cccc-cccccccccccc",
            new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc),
            maxResourceCount: 2);

        var problem = CreateProblem(
            resources: [underTargetResource, assignedResource],
            shifts: [shift],
            availabilityWindows:
            [
                CreateAvailability(underTargetResource, shift),
                CreateAvailability(assignedResource, shift)
            ],
            resourcePreferences:
            [
                CreatePrefer(underTargetResource, shift)
            ],
            resourceWorkloadDemands:
            [
                new ResourceWorkloadDemand(
                    underTargetResource.Id,
                    requestedPreferredHours: 8,
                    minimumRequiredHours: 0)
            ]);

        var candidate = new ScheduleCandidate(
        [
            new Assignment(assignedResource.Id, shift.Id)
        ]);

        var weights = ScheduleScoringWeights.CreateDefault();
        var evaluation = Evaluate(problem, candidate, weights);

        AssertFeasible(evaluation);
        Assert.True(evaluation.Score.TotalPenalty > 0);

        var result = Improve(problem, candidate, evaluation, weights);

        AssertFeasible(result.Evaluation);
        Assert.True(result.AcceptedAddMoveCount > 0);
        Assert.True(result.FinalTotalPenalty < result.InitialTotalPenalty);
        Assert.Equal(candidate.Assignments.Count + 1, result.Candidate.Assignments.Count);

        Assert.Contains(
            result.Candidate.Assignments,
            assignment =>
                assignment.ResourceId == underTargetResource.Id &&
                assignment.ShiftId == shift.Id);

        var diagnostic = Analyze(problem, result.Candidate, result.Evaluation, weights);

        Assert.Equal(0, diagnostic.ScoreImprovingAddMoveCount);
    }

    [Fact]
    public void NoOpPolish_ShouldNotChangeCandidate_WhenNoScoreImprovingAddMoveExists()
    {
        var resource = CreateResource(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "Ziv");

        var shift = CreateShift(
            "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
            new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc),
            maxResourceCount: 1);

        var problem = CreateProblem(
            resources: [resource],
            shifts: [shift],
            availabilityWindows:
            [
                CreateAvailability(resource, shift)
            ],
            resourcePreferences:
            [
                CreateAvoid(resource, shift)
            ],
            resourceWorkloadDemands:
            [
                new ResourceWorkloadDemand(
                    resource.Id,
                    requestedPreferredHours: 8,
                    minimumRequiredHours: 0)
            ]);

        var candidate = new ScheduleCandidate([]);

        var weights = ScheduleScoringWeights.CreateDefault();
        var evaluation = Evaluate(problem, candidate, weights);

        AssertFeasible(evaluation);
        Assert.True(evaluation.Score.TotalPenalty > 0);

        var result = Improve(problem, candidate, evaluation, weights);

        AssertFeasible(result.Evaluation);
        Assert.Equal(0, result.AcceptedAddMoveCount);
        Assert.Equal(0, result.IterationCount);
        Assert.Equal(evaluation.Score.TotalPenalty, result.InitialTotalPenalty);
        Assert.Equal(evaluation.Score.TotalPenalty, result.FinalTotalPenalty);
        Assert.Equal(candidate.Assignments.Count, result.Candidate.Assignments.Count);
        Assert.Empty(result.AcceptedMoves);

        var diagnostic = Analyze(problem, result.Candidate, result.Evaluation, weights);

        Assert.Equal(0, diagnostic.ScoreImprovingAddMoveCount);
    }

    [Fact]
    public void ManagerConstraints_ShouldRemainPreserved_AfterLocalAddImprovement()
    {
        var forbiddenResource = CreateResource(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "Niv");

        var legalUnderTargetResource = CreateResource(
            "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
            "Ziv");

        var assignedResource = CreateResource(
            "cccccccc-cccc-cccc-cccc-cccccccccccc",
            "Rafael");

        var forbiddenShift = CreateShift(
            "dddddddd-dddd-dddd-dddd-dddddddddddd",
            new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc),
            maxResourceCount: 2);

        var capacityLimitedShift = CreateShift(
            "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee",
            new DateTime(2026, 6, 2, 6, 30, 0, DateTimeKind.Utc),
            maxResourceCount: 1);

        var legalShift = CreateShift(
            "ffffffff-ffff-ffff-ffff-ffffffffffff",
            new DateTime(2026, 6, 3, 6, 30, 0, DateTimeKind.Utc),
            maxResourceCount: 2);

        var problem = CreateProblem(
            resources: [forbiddenResource, legalUnderTargetResource, assignedResource],
            shifts: [forbiddenShift, capacityLimitedShift, legalShift],
            availabilityWindows:
            [
                CreateAvailability(legalUnderTargetResource, capacityLimitedShift),
                CreateAvailability(legalUnderTargetResource, legalShift),
                CreateAvailability(assignedResource, forbiddenShift),
                CreateAvailability(assignedResource, capacityLimitedShift),
                CreateAvailability(assignedResource, legalShift)
            ],
            resourcePreferences:
            [
                CreatePrefer(forbiddenResource, forbiddenShift),
                CreatePrefer(legalUnderTargetResource, capacityLimitedShift),
                CreatePrefer(legalUnderTargetResource, legalShift)
            ],
            resourceWorkloadDemands:
            [
                new ResourceWorkloadDemand(
                    forbiddenResource.Id,
                    requestedPreferredHours: 8,
                    minimumRequiredHours: 0),
                new ResourceWorkloadDemand(
                    legalUnderTargetResource.Id,
                    requestedPreferredHours: 16,
                    minimumRequiredHours: 0)
            ]);

        var candidate = new ScheduleCandidate(
        [
            new Assignment(assignedResource.Id, capacityLimitedShift.Id),
            new Assignment(assignedResource.Id, legalShift.Id)
        ]);

        var weights = ScheduleScoringWeights.CreateDefault();
        var evaluation = Evaluate(problem, candidate, weights);

        AssertFeasible(evaluation);

        var result = Improve(problem, candidate, evaluation, weights);

        AssertFeasible(result.Evaluation);
        Assert.Equal(1, result.AcceptedAddMoveCount);

        Assert.Contains(
            result.Candidate.Assignments,
            assignment =>
                assignment.ResourceId == legalUnderTargetResource.Id &&
                assignment.ShiftId == legalShift.Id);

        Assert.DoesNotContain(
            result.Candidate.Assignments,
            assignment =>
                assignment.ResourceId == forbiddenResource.Id &&
                assignment.ShiftId == forbiddenShift.Id);

        Assert.Equal(1, CountAssignments(result.Candidate, capacityLimitedShift));

        Assert.All(problem.Shifts, shift =>
        {
            Assert.True(
                CountAssignments(result.Candidate, shift) <= shift.MaxResourceCount,
                $"Shift {shift.Id} exceeded max capacity.");
        });

        Assert.DoesNotContain(
            result.Evaluation.Violations,
            violation =>
                violation.Type == ConstraintViolationType.ResourceUnavailable ||
                violation.Type == ConstraintViolationType.ShiftOverstaffed);
    }

    [Fact]
    public void RemainingPressure_ShouldShowUnderAndOverTargetWorkers_WhenAddMovesAreExhausted()
    {
        var underTargetResource = CreateResource(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "Under");

        var overTargetResource = CreateResource(
            "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
            "Over");

        var alreadyAssignedShift = CreateShift(
            "cccccccc-cccc-cccc-cccc-cccccccccccc",
            new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc),
            maxResourceCount: 1);

        var fullShift = CreateShift(
            "dddddddd-dddd-dddd-dddd-dddddddddddd",
            new DateTime(2026, 6, 2, 6, 30, 0, DateTimeKind.Utc),
            maxResourceCount: 1);

        var problem = CreateProblem(
            resources: [underTargetResource, overTargetResource],
            shifts: [alreadyAssignedShift, fullShift],
            availabilityWindows:
            [
                CreateAvailability(underTargetResource, alreadyAssignedShift),
                CreateAvailability(underTargetResource, fullShift),
                CreateAvailability(overTargetResource, fullShift)
            ],
            resourcePreferences:
            [
                CreatePrefer(underTargetResource, alreadyAssignedShift),
                CreatePrefer(underTargetResource, fullShift)
            ],
            resourceWorkloadDemands:
            [
                new ResourceWorkloadDemand(
                    underTargetResource.Id,
                    requestedPreferredHours: 16,
                    minimumRequiredHours: 0)
            ]);

        var candidate = new ScheduleCandidate(
        [
            new Assignment(underTargetResource.Id, alreadyAssignedShift.Id),
            new Assignment(overTargetResource.Id, fullShift.Id)
        ]);

        var weights = ScheduleScoringWeights.CreateDefault();
        var evaluation = Evaluate(problem, candidate, weights);

        AssertFeasible(evaluation);

        var result = Improve(problem, candidate, evaluation, weights);

        AssertFeasible(result.Evaluation);
        Assert.Equal(0, result.AcceptedAddMoveCount);
        Assert.Equal(evaluation.Score.TotalPenalty, result.FinalTotalPenalty);

        var diagnostic = Analyze(problem, result.Candidate, result.Evaluation, weights);

        Assert.Equal(0, diagnostic.ScoreImprovingAddMoveCount);
        Assert.True(diagnostic.CandidateAddMoveCount > 0);

        Assert.Contains(
            diagnostic.WorkerDiagnostics,
            worker =>
                worker.ResourceId == underTargetResource.Id &&
                worker.GapToTarget < -HoursTolerance);

        Assert.Contains(
            diagnostic.WorkerDiagnostics,
            worker =>
                worker.ResourceId == overTargetResource.Id &&
                worker.GapToTarget > HoursTolerance);
    }

    private static PostRunLocalAddImprovementResult Improve(
        SchedulingProblem problem,
        ScheduleCandidate candidate,
        ScheduleEvaluationResult evaluation,
        ScheduleScoringWeights weights)
    {
        return new PostRunLocalAddImprovementOptimizer()
            .Improve(
                problem,
                candidate,
                evaluation,
                weights);
    }

    private static TargetGapExplainabilityDiagnostic Analyze(
        SchedulingProblem problem,
        ScheduleCandidate candidate,
        ScheduleEvaluationResult evaluation,
        ScheduleScoringWeights weights)
    {
        return new CleanGaTargetGapExplainabilityAnalyzer()
            .Analyze(
                problem,
                candidate,
                evaluation,
                weights);
    }

    private static ScheduleEvaluationResult Evaluate(
        SchedulingProblem problem,
        ScheduleCandidate candidate,
        ScheduleScoringWeights weights)
    {
        return new ScheduleEvaluator(weights)
            .Evaluate(problem, candidate);
    }

    private static SchedulingProblem CreateProblem(
        IReadOnlyCollection<Resource> resources,
        IReadOnlyCollection<Shift> shifts,
        IReadOnlyCollection<AvailabilityWindow> availabilityWindows,
        IReadOnlyCollection<ResourcePreference> resourcePreferences,
        IReadOnlyCollection<ResourceWorkloadDemand> resourceWorkloadDemands)
    {
        return new SchedulingProblem(
            CreatePeriod(),
            resources,
            shifts,
            availabilityWindows,
            resourcePreferences,
            resourceWorkloadDemands: resourceWorkloadDemands);
    }

    private static SchedulePeriod CreatePeriod()
    {
        return new SchedulePeriod(
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));
    }

    private static Resource CreateResource(
        string id,
        string name)
    {
        return new Resource(
            Guid.Parse(id),
            name,
            hourlyCost: 100m);
    }

    private static Shift CreateShift(
        string id,
        DateTime startUtc,
        int maxResourceCount,
        int minResourceCount = 0)
    {
        return new Shift(
            Guid.Parse(id),
            startUtc,
            startUtc.AddHours(8),
            ShiftKind.Morning,
            minResourceCount,
            maxResourceCount,
            requiresPreferenceToAssign: false);
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

    private static ResourcePreference CreatePrefer(
        Resource resource,
        Shift shift)
    {
        return new ResourcePreference(
            resource.Id,
            shift.StartUtc,
            shift.EndUtc,
            ResourcePreferenceType.Prefer,
            ResourcePreferencePriority.High);
    }

    private static ResourcePreference CreateAvoid(
        Resource resource,
        Shift shift)
    {
        return new ResourcePreference(
            resource.Id,
            shift.StartUtc,
            shift.EndUtc,
            ResourcePreferenceType.Avoid,
            ResourcePreferencePriority.High);
    }

    private static int CountAssignments(
        ScheduleCandidate candidate,
        Shift shift)
    {
        return candidate.Assignments.Count(
            assignment => assignment.ShiftId == shift.Id);
    }

    private static void AssertFeasible(
        ScheduleEvaluationResult evaluation)
    {
        Assert.True(
            evaluation.IsFeasible,
            $"Expected feasible candidate, but got {evaluation.Score.HardViolationCount} hard violations.");

        Assert.Equal(0, evaluation.Score.HardViolationCount);
    }
}
