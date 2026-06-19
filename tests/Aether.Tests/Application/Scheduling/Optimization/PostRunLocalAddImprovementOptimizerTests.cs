using Aether.Application.Scheduling.Optimization;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling.Optimization;

public sealed class PostRunLocalAddImprovementOptimizerTests
{
    private const double HoursTolerance = 0.000001;

    [Fact]
    public void Improve_ShouldAcceptScoreImprovingAddMove_AndReturnImprovedCandidate()
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

        var evaluation = new ScheduleEvaluator(weights)
            .Evaluate(problem, candidate);

        Assert.True(evaluation.IsFeasible);
        Assert.Equal(200, evaluation.Score.TotalPenalty);

        var result = new PostRunLocalAddImprovementOptimizer()
            .Improve(
                problem,
                candidate,
                evaluation,
                weights);

        Assert.True(result.Evaluation.IsFeasible);
        Assert.Equal(1, result.AcceptedAddMoveCount);
        Assert.Equal(1, result.IterationCount);
        Assert.Equal(200, result.InitialTotalPenalty);
        Assert.Equal(0, result.FinalTotalPenalty);
        Assert.Equal(0, result.Evaluation.Score.TotalPenalty);

        Assert.Equal(2, result.Candidate.Assignments.Count);

        Assert.Contains(
            result.Candidate.Assignments,
            assignment =>
                assignment.ResourceId == underTargetResource.Id &&
                assignment.ShiftId == shift.Id);

        Assert.Contains(
            result.Candidate.Assignments,
            assignment =>
                assignment.ResourceId == assignedResource.Id &&
                assignment.ShiftId == shift.Id);

        var acceptedMove = Assert.Single(result.AcceptedMoves);

        Assert.Equal(underTargetResource.Id, acceptedMove.ResourceId);
        Assert.Equal("Ziv", acceptedMove.ResourceName);
        Assert.Equal(shift.Id, acceptedMove.ShiftId);
        Assert.Equal(200, acceptedMove.PreviousTotalPenalty);
        Assert.Equal(0, acceptedMove.NewTotalPenalty);
        Assert.Equal(200, acceptedMove.PenaltyDelta);
    }

    [Fact]
    public void Improve_ShouldRejectFairnessImprovingButScoreNotImprovingAddMove()
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

        var evaluation = new ScheduleEvaluator(weights)
            .Evaluate(problem, candidate);

        Assert.True(evaluation.IsFeasible);
        Assert.True(evaluation.Score.TotalPenalty > 0);

        var result = new PostRunLocalAddImprovementOptimizer()
            .Improve(
                problem,
                candidate,
                evaluation,
                weights);

        Assert.True(result.Evaluation.IsFeasible);
        Assert.Equal(0, result.AcceptedAddMoveCount);
        Assert.Equal(0, result.IterationCount);
        Assert.Equal(evaluation.Score.TotalPenalty, result.InitialTotalPenalty);
        Assert.Equal(evaluation.Score.TotalPenalty, result.FinalTotalPenalty);
        Assert.Empty(result.Candidate.Assignments);
        Assert.Empty(result.AcceptedMoves);
    }

    [Fact]
    public void Improve_ShouldRepeatUntilNoScoreImprovingAddMoveRemains()
    {
        var underTargetResource = CreateResource(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "Ziv");

        var assignedResource = CreateResource(
            "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
            "Rafael");

        var firstShift = CreateShift(
            "cccccccc-cccc-cccc-cccc-cccccccccccc",
            new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc),
            maxResourceCount: 2);

        var secondShift = CreateShift(
            "dddddddd-dddd-dddd-dddd-dddddddddddd",
            new DateTime(2026, 6, 2, 6, 30, 0, DateTimeKind.Utc),
            maxResourceCount: 2);

        var problem = CreateProblem(
            resources: [underTargetResource, assignedResource],
            shifts: [firstShift, secondShift],
            availabilityWindows:
            [
                CreateAvailability(underTargetResource, firstShift),
                CreateAvailability(underTargetResource, secondShift),
                CreateAvailability(assignedResource, firstShift),
                CreateAvailability(assignedResource, secondShift)
            ],
            resourcePreferences:
            [
                CreatePrefer(underTargetResource, firstShift),
                CreatePrefer(underTargetResource, secondShift)
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
            new Assignment(assignedResource.Id, firstShift.Id),
            new Assignment(assignedResource.Id, secondShift.Id)
        ]);

        var weights = ScheduleScoringWeights.CreateDefault();

        var evaluation = new ScheduleEvaluator(weights)
            .Evaluate(problem, candidate);

        Assert.True(evaluation.IsFeasible);
        Assert.Equal(400, evaluation.Score.TotalPenalty);

        var result = new PostRunLocalAddImprovementOptimizer()
            .Improve(
                problem,
                candidate,
                evaluation,
                weights);

        Assert.True(result.Evaluation.IsFeasible);
        Assert.Equal(2, result.AcceptedAddMoveCount);
        Assert.Equal(2, result.IterationCount);
        Assert.Equal(400, result.InitialTotalPenalty);
        Assert.Equal(0, result.FinalTotalPenalty);
        Assert.Equal(0, result.Evaluation.Score.TotalPenalty);
        Assert.Equal(4, result.Candidate.Assignments.Count);

        Assert.Contains(
            result.Candidate.Assignments,
            assignment =>
                assignment.ResourceId == underTargetResource.Id &&
                assignment.ShiftId == firstShift.Id);

        Assert.Contains(
            result.Candidate.Assignments,
            assignment =>
                assignment.ResourceId == underTargetResource.Id &&
                assignment.ShiftId == secondShift.Id);

        Assert.Equal(2, result.AcceptedMoves.Count);
        Assert.All(result.AcceptedMoves, move => Assert.True(move.PenaltyDelta > 0));
    }

    [Fact]
    public void Improve_ShouldRejectAddMoveThatCreatesHardViolation()
    {
        var resource = CreateResource(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "Ziv");

        var firstNightShift = CreateNightShift(
            "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
            new DateTime(2026, 6, 1, 22, 40, 0, DateTimeKind.Utc));

        var secondNightShift = CreateNightShift(
            "cccccccc-cccc-cccc-cccc-cccccccccccc",
            new DateTime(2026, 6, 3, 22, 40, 0, DateTimeKind.Utc));

        var problem = CreateProblem(
            resources: [resource],
            shifts: [firstNightShift, secondNightShift],
            availabilityWindows:
            [
                CreateAvailability(resource, firstNightShift),
                CreateAvailability(resource, secondNightShift)
            ],
            resourcePreferences:
            [
                CreatePrefer(resource, firstNightShift),
                CreatePrefer(resource, secondNightShift)
            ],
            resourceWorkloadDemands:
            [
                new ResourceWorkloadDemand(
                    resource.Id,
                    requestedPreferredHours: 16,
                    minimumRequiredHours: 0)
            ]);

        var candidate = new ScheduleCandidate(
        [
            new Assignment(resource.Id, firstNightShift.Id)
        ]);

        var weights = ScheduleScoringWeights.CreateDefault();

        var evaluation = new ScheduleEvaluator(weights)
            .Evaluate(problem, candidate);

        Assert.True(evaluation.IsFeasible);

        var result = new PostRunLocalAddImprovementOptimizer()
            .Improve(
                problem,
                candidate,
                evaluation,
                weights);

        Assert.True(result.Evaluation.IsFeasible);
        Assert.Equal(0, result.AcceptedAddMoveCount);
        Assert.Equal(0, result.IterationCount);
        Assert.Equal(evaluation.Score.TotalPenalty, result.InitialTotalPenalty);
        Assert.Equal(evaluation.Score.TotalPenalty, result.FinalTotalPenalty);

        var assignment = Assert.Single(result.Candidate.Assignments);

        Assert.Equal(resource.Id, assignment.ResourceId);
        Assert.Equal(firstNightShift.Id, assignment.ShiftId);
        Assert.Empty(result.AcceptedMoves);
    }

    [Fact]
    public void Improve_ShouldNotChangeCandidate_WhenNoImprovingAddMoveExists()
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
                CreatePrefer(resource, shift)
            ],
            resourceWorkloadDemands:
            [
                new ResourceWorkloadDemand(
                    resource.Id,
                    requestedPreferredHours: 8,
                    minimumRequiredHours: 0)
            ]);

        var candidate = new ScheduleCandidate(
        [
            new Assignment(resource.Id, shift.Id)
        ]);

        var weights = ScheduleScoringWeights.CreateDefault();

        var evaluation = new ScheduleEvaluator(weights)
            .Evaluate(problem, candidate);

        Assert.True(evaluation.IsFeasible);
        Assert.Equal(0, evaluation.Score.TotalPenalty);

        var result = new PostRunLocalAddImprovementOptimizer()
            .Improve(
                problem,
                candidate,
                evaluation,
                weights);

        Assert.True(result.Evaluation.IsFeasible);
        Assert.Equal(0, result.AcceptedAddMoveCount);
        Assert.Equal(0, result.IterationCount);
        Assert.Equal(0, result.InitialTotalPenalty);
        Assert.Equal(0, result.FinalTotalPenalty);
        Assert.Equal(0, result.Evaluation.Score.TotalPenalty);

        var assignment = Assert.Single(result.Candidate.Assignments);

        Assert.Equal(resource.Id, assignment.ResourceId);
        Assert.Equal(shift.Id, assignment.ShiftId);
        Assert.Empty(result.AcceptedMoves);
    }

    [Fact]
    public void Improve_ShouldUseDeterministicOrder_WhenMultipleEqualMovesExist()
    {
        var resource = CreateResource(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "Ziv");

        var laterShift = CreateShift(
            "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
            new DateTime(2026, 6, 2, 6, 30, 0, DateTimeKind.Utc),
            maxResourceCount: 1);

        var earlierShift = CreateShift(
            "cccccccc-cccc-cccc-cccc-cccccccccccc",
            new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc),
            maxResourceCount: 1);

        var problem = CreateProblem(
            resources: [resource],
            shifts: [laterShift, earlierShift],
            availabilityWindows:
            [
                CreateAvailability(resource, laterShift),
                CreateAvailability(resource, earlierShift)
            ],
            resourcePreferences:
            [
                CreatePrefer(resource, laterShift),
                CreatePrefer(resource, earlierShift)
            ],
            resourceWorkloadDemands:
            [
                new ResourceWorkloadDemand(
                    resource.Id,
                    requestedPreferredHours: 16,
                    minimumRequiredHours: 0)
            ]);

        var candidate = new ScheduleCandidate([]);

        var weights = ScheduleScoringWeights.CreateDefault();

        var evaluation = new ScheduleEvaluator(weights)
            .Evaluate(problem, candidate);

        Assert.True(evaluation.IsFeasible);

        var result = new PostRunLocalAddImprovementOptimizer()
            .Improve(
                problem,
                candidate,
                evaluation,
                weights);

        Assert.True(result.Evaluation.IsFeasible);
        Assert.Equal(2, result.AcceptedAddMoveCount);
        Assert.Equal(2, result.IterationCount);
        Assert.Equal(0, result.FinalTotalPenalty);

        Assert.Equal(
            [earlierShift.Id, laterShift.Id],
            result.AcceptedMoves
                .Select(move => move.ShiftId)
                .ToArray());
    }

    private static Shift CreateNightShift(
        string id,
        DateTime startUtc)
    {
        return new Shift(
            Guid.Parse(id),
            startUtc,
            startUtc.AddHours(7).AddMinutes(50),
            ShiftKind.Night,
            minResourceCount: 0,
            maxResourceCount: 1,
            nightShiftCategory: NightShiftCategory.Regular);
    }

    private static SchedulingProblem CreateProblem(
        IReadOnlyCollection<Resource> resources,
        IReadOnlyCollection<Shift> shifts,
        IReadOnlyCollection<AvailabilityWindow> availabilityWindows,
        IReadOnlyCollection<ResourcePreference> resourcePreferences,
        IReadOnlyCollection<ResourceWorkloadDemand> resourceWorkloadDemands)
    {
        var periodStartUtc = shifts
            .Min(shift => shift.StartUtc)
            .Date;

        var periodEndUtc = shifts
            .Max(shift => shift.EndUtc)
            .Date
            .AddDays(1);

        return new SchedulingProblem(
            new SchedulePeriod(periodStartUtc, periodEndUtc),
            resources,
            shifts,
            availabilityWindows,
            resourcePreferences,
            resourceWorkloadDemands: resourceWorkloadDemands);
    }

    private static Resource CreateResource(
        string id,
        string name)
    {
        return new Resource(
            Guid.Parse(id),
            name,
            hourlyCost: 0);
    }

    private static Shift CreateShift(
        string id,
        DateTime startUtc,
        int maxResourceCount)
    {
        return new Shift(
            Guid.Parse(id),
            startUtc,
            startUtc.AddHours(8),
            ShiftKind.Morning,
            minResourceCount: 0,
            maxResourceCount: maxResourceCount);
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
}
