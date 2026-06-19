using Aether.Application.Scheduling.Optimization;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class ScheduleMutationOperatorTests
{
    [Fact]
    public void Mutate_ShouldThrow_WhenProblemIsNull()
    {
        var candidate = new ScheduleCandidate([]);
        var mutation = new ScheduleMutationOperator(seed: 1);

        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            mutation.Mutate(null!, candidate);
        });

        Assert.Equal("problem", exception.ParamName);
    }

    [Fact]
    public void Mutate_ShouldThrow_WhenCandidateIsNull()
    {
        var resource = CreateResource("Dana");
        var shift = CreateMorningShift();

        var problem = CreateProblem(
            resources: [resource],
            shifts: [shift],
            availabilityWindows: [CreateAvailability(resource, shift)],
            resourcePreferences: []);

        var mutation = new ScheduleMutationOperator(seed: 1);

        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            mutation.Mutate(problem, null!);
        });

        Assert.Equal("candidate", exception.ParamName);
    }

    [Fact]
    public void Mutate_ShouldReturnNewCandidate_WithoutChangingOriginalCandidate()
    {
        var dana = CreateResource("Dana");
        var yossi = CreateResource("Yossi");
        var shift = CreateMorningShift();

        var problem = CreateProblem(
            resources: [dana, yossi],
            shifts: [shift],
            availabilityWindows:
            [
                CreateAvailability(dana, shift),
                CreateAvailability(yossi, shift)
            ],
            resourcePreferences: []);

        var originalCandidate = new ScheduleCandidate(
        [
            new Assignment(dana.Id, shift.Id)
        ]);

        var mutation = new ScheduleMutationOperator(seed: 1);

        var mutatedCandidate = mutation.Mutate(problem, originalCandidate);

        Assert.NotSame(originalCandidate, mutatedCandidate);

        var originalAssignment = Assert.Single(originalCandidate.Assignments);
        Assert.Equal(dana.Id, originalAssignment.ResourceId);
        Assert.Equal(shift.Id, originalAssignment.ShiftId);

        var mutatedAssignment = Assert.Single(mutatedCandidate.Assignments);
        Assert.Equal(yossi.Id, mutatedAssignment.ResourceId);
        Assert.Equal(shift.Id, mutatedAssignment.ShiftId);
    }

    [Fact]
    public void Mutate_ShouldReassignOneExistingShiftAssignment_WhenReplacementIsAvailable()
    {
        var dana = CreateResource("Dana");
        var yossi = CreateResource("Yossi");
        var shift = CreateMorningShift();

        var problem = CreateProblem(
            resources: [dana, yossi],
            shifts: [shift],
            availabilityWindows:
            [
                CreateAvailability(dana, shift),
                CreateAvailability(yossi, shift)
            ],
            resourcePreferences: []);

        var originalCandidate = new ScheduleCandidate(
        [
            new Assignment(dana.Id, shift.Id)
        ]);

        var mutation = new ScheduleMutationOperator(seed: 1);

        var mutatedCandidate = mutation.Mutate(problem, originalCandidate);

        var assignment = Assert.Single(mutatedCandidate.Assignments);

        Assert.Equal(yossi.Id, assignment.ResourceId);
        Assert.Equal(shift.Id, assignment.ShiftId);
    }

    [Fact]
    public void Mutate_ShouldNotCreateDuplicateAssignmentForSameResourceAndShift()
    {
        var dana = CreateResource("Dana");
        var yossi = CreateResource("Yossi");
        var noa = CreateResource("Noa");
        var shift = CreateMorningShift(maxResourceCount: 3);

        var problem = CreateProblem(
            resources: [dana, yossi, noa],
            shifts: [shift],
            availabilityWindows:
            [
                CreateAvailability(dana, shift),
                CreateAvailability(yossi, shift),
                CreateAvailability(noa, shift)
            ],
            resourcePreferences: []);

        var originalCandidate = new ScheduleCandidate(
        [
            new Assignment(dana.Id, shift.Id),
            new Assignment(yossi.Id, shift.Id)
        ]);

        var mutation = new ScheduleMutationOperator(seed: 1);

        var mutatedCandidate = mutation.Mutate(problem, originalCandidate);

        Assert.Equal(2, mutatedCandidate.Assignments.Count);

        var distinctAssignmentKeys = mutatedCandidate.Assignments
            .Select(assignment => $"{assignment.ResourceId}:{assignment.ShiftId}")
            .Distinct()
            .Count();

        Assert.Equal(mutatedCandidate.Assignments.Count, distinctAssignmentKeys);
    }

    [Fact]
    public void Mutate_ShouldReturnSameAssignments_WhenNoReplacementHasAvailability()
    {
        var dana = CreateResource("Dana");
        var yossi = CreateResource("Yossi");
        var shift = CreateMorningShift();

        var problem = CreateProblem(
            resources: [dana, yossi],
            shifts: [shift],
            availabilityWindows:
            [
                CreateAvailability(dana, shift)
            ],
            resourcePreferences: []);

        var originalCandidate = new ScheduleCandidate(
        [
            new Assignment(dana.Id, shift.Id)
        ]);

        var mutation = new ScheduleMutationOperator(seed: 1);

        var mutatedCandidate = mutation.Mutate(problem, originalCandidate);

        Assert.NotSame(originalCandidate, mutatedCandidate);
        Assert.Equal(ToAssignmentKeys(originalCandidate), ToAssignmentKeys(mutatedCandidate));
    }

    [Fact]
    public void Mutate_ShouldRespectRequiresPreferenceToAssign()
    {
        var dana = CreateResource("Dana");
        var yossi = CreateResource("Yossi");

        var shift = CreateNightShift(
            requiresPreferenceToAssign: true);

        var problem = CreateProblem(
            resources: [dana, yossi],
            shifts: [shift],
            availabilityWindows:
            [
                CreateAvailability(dana, shift),
                CreateAvailability(yossi, shift)
            ],
            resourcePreferences:
            [
                CreatePreferPreference(dana, shift)
            ]);

        var originalCandidate = new ScheduleCandidate(
        [
            new Assignment(dana.Id, shift.Id)
        ]);

        var mutation = new ScheduleMutationOperator(seed: 1);

        var mutatedCandidate = mutation.Mutate(problem, originalCandidate);

        Assert.NotSame(originalCandidate, mutatedCandidate);
        Assert.Equal(ToAssignmentKeys(originalCandidate), ToAssignmentKeys(mutatedCandidate));
    }

    [Fact]
    public void Mutate_ShouldAvoidOverlappingAssignmentsForReplacementResource()
    {
        var dana = CreateResource("Dana");
        var yossi = CreateResource("Yossi");

        var targetShift = CreateShift(
            new DateTime(2026, 6, 3, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 3, 16, 0, 0, DateTimeKind.Utc),
            ShiftKind.Morning);

        var overlappingShift = CreateShift(
            new DateTime(2026, 6, 3, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 3, 18, 0, 0, DateTimeKind.Utc),
            ShiftKind.Afternoon);

        var problem = CreateProblem(
            resources: [dana, yossi],
            shifts: [targetShift, overlappingShift],
            availabilityWindows:
            [
                CreateAvailability(dana, targetShift),
                CreateAvailability(yossi, targetShift),
                CreateAvailability(yossi, overlappingShift)
            ],
            resourcePreferences: []);

        var originalCandidate = new ScheduleCandidate(
        [
            new Assignment(dana.Id, targetShift.Id),
            new Assignment(yossi.Id, overlappingShift.Id)
        ]);

        var mutation = new ScheduleMutationOperator(seed: 1);

        var mutatedCandidate = mutation.Mutate(problem, originalCandidate);

        Assert.DoesNotContain(
            mutatedCandidate.Assignments,
            assignment => assignment.ResourceId == yossi.Id &&
                          assignment.ShiftId == targetShift.Id);

        var evaluation = new ScheduleEvaluator()
            .Evaluate(problem, mutatedCandidate);

        Assert.DoesNotContain(
            evaluation.Violations,
            violation => violation.Type == ConstraintViolationType.ResourceAssignedToOverlappingShifts);
    }


    [Fact]
    public void Mutate_ShouldPerformChainReassign_WhenParentEvaluationContainsIgnoredAvoidPreference()
    {
        var dana = CreateResource("Dana");
        var yossi = CreateResource("Yossi");

        var avoidedShift = CreateShift(
            new DateTime(2026, 6, 2, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 2, 14, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning);

        var cleanShift = CreateShift(
            new DateTime(2026, 6, 3, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 3, 14, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning);

        var problem = CreateProblem(
            resources: [dana, yossi],
            shifts: [avoidedShift, cleanShift],
            availabilityWindows:
            [
                CreateAvailability(dana, avoidedShift),
                CreateAvailability(dana, cleanShift),
                CreateAvailability(yossi, avoidedShift),
                CreateAvailability(yossi, cleanShift)
            ],
            resourcePreferences:
            [
                CreateAvoidPreference(dana, avoidedShift)
            ],
            minimumAssignedHoursPerResource: 8);

        var originalCandidate = new ScheduleCandidate(
        [
            new Assignment(dana.Id, avoidedShift.Id),
            new Assignment(yossi.Id, cleanShift.Id)
        ]);

        var parentEvaluation = new ScheduleEvaluator()
            .Evaluate(problem, originalCandidate);

        Assert.True(parentEvaluation.IsFeasible);
        Assert.Contains(
            parentEvaluation.Violations,
            violation => violation.Type == ConstraintViolationType.IgnoredAvoidPreference);

        var mutation = new ScheduleMutationOperator(seed: 1);

        var mutatedCandidate = mutation.Mutate(
            problem,
            originalCandidate,
            parentEvaluation);

        Assert.Contains(
            mutatedCandidate.Assignments,
            assignment => assignment.ResourceId == yossi.Id &&
                          assignment.ShiftId == avoidedShift.Id);

        Assert.Contains(
            mutatedCandidate.Assignments,
            assignment => assignment.ResourceId == dana.Id &&
                          assignment.ShiftId == cleanShift.Id);

        Assert.DoesNotContain(
            mutatedCandidate.Assignments,
            assignment => assignment.ResourceId == dana.Id &&
                          assignment.ShiftId == avoidedShift.Id);

        var childEvaluation = new ScheduleEvaluator()
            .Evaluate(problem, mutatedCandidate);

        Assert.True(childEvaluation.IsFeasible);

        Assert.DoesNotContain(
            childEvaluation.Violations,
            violation => violation.Type == ConstraintViolationType.IgnoredAvoidPreference);

        Assert.DoesNotContain(
            childEvaluation.Violations,
            violation => violation.Type == ConstraintViolationType.ResourceMinimumAssignedHoursNotMet);
    }

    [Fact]
    public void Mutate_ShouldRemoveOverTargetAssignment_WhenShiftRemainsAboveMinimumAndRankingImproves()
    {
        var dana = CreateResource("Dana");
        var yossi = CreateResource("Yossi");

        var removableShift = CreateShift(
            new DateTime(2026, 6, 2, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 2, 14, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning,
            maxResourceCount: 2);

        var requiredShift = CreateShift(
            new DateTime(2026, 6, 3, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 3, 14, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning);

        var problem = CreateProblem(
            resources: [dana, yossi],
            shifts: [removableShift, requiredShift],
            availabilityWindows:
            [
                CreateAvailability(dana, removableShift),
                CreateAvailability(dana, requiredShift),
                CreateAvailability(yossi, removableShift)
            ],
            resourcePreferences: [],
            resourceWorkloadDemands:
            [
                new ResourceWorkloadDemand(
                    dana.Id,
                    requestedPreferredHours: 8,
                    minimumRequiredHours: 0)
            ]);

        var originalCandidate = new ScheduleCandidate(
        [
            new Assignment(dana.Id, removableShift.Id),
            new Assignment(yossi.Id, removableShift.Id),
            new Assignment(dana.Id, requiredShift.Id)
        ]);

        var evaluator = new ScheduleEvaluator();
        var parentEvaluation = evaluator.Evaluate(problem, originalCandidate);

        Assert.True(parentEvaluation.IsFeasible);
        Assert.Contains(
            parentEvaluation.Violations,
            violation => violation.Type == ConstraintViolationType.ResourceEffectiveTargetAssignedHoursAboveTarget);

        var mutation = new ScheduleMutationOperator(seed: 1);

        var mutatedCandidate = mutation.Mutate(
            problem,
            originalCandidate,
            parentEvaluation);

        Assert.Equal(2, mutatedCandidate.Assignments.Count);

        Assert.DoesNotContain(
            mutatedCandidate.Assignments,
            assignment => assignment.ResourceId == dana.Id &&
                          assignment.ShiftId == removableShift.Id);

        Assert.Contains(
            mutatedCandidate.Assignments,
            assignment => assignment.ResourceId == yossi.Id &&
                          assignment.ShiftId == removableShift.Id);

        Assert.Contains(
            mutatedCandidate.Assignments,
            assignment => assignment.ResourceId == dana.Id &&
                          assignment.ShiftId == requiredShift.Id);

        var childEvaluation = evaluator.Evaluate(problem, mutatedCandidate);

        Assert.True(childEvaluation.IsFeasible);
        Assert.Equal(0, childEvaluation.Score.HardViolationCount);

        var ranker = new ScheduleEvaluationResultRanker();

        Assert.True(ranker.IsBetterThan(childEvaluation, parentEvaluation));
    }

    [Fact]
    public void Mutate_ShouldAddAssignmentToUnderTargetResource_WhenRankingImproves()
    {
        var dana = CreateResource("Dana");
        var yossi = CreateResource("Yossi");

        var shift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 6, 2, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 2, 14, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning,
            minResourceCount: 1,
            maxResourceCount: 2,
            requiresPreferenceToAssign: true);

        var problem = CreateProblem(
            resources: [dana, yossi],
            shifts: [shift],
            availabilityWindows:
            [
                CreateAvailability(dana, shift),
                CreateAvailability(yossi, shift)
            ],
            resourcePreferences:
            [
                CreatePreferPreference(dana, shift),
                CreatePreferPreference(yossi, shift)
            ],
            resourceWorkloadDemands:
            [
                new ResourceWorkloadDemand(
                    dana.Id,
                    requestedPreferredHours: 8,
                    minimumRequiredHours: 0),

                new ResourceWorkloadDemand(
                    yossi.Id,
                    requestedPreferredHours: 8,
                    minimumRequiredHours: 0)
            ]);

        var originalCandidate = new ScheduleCandidate(
        [
            new Assignment(dana.Id, shift.Id)
        ]);

        var evaluator = new ScheduleEvaluator();
        var parentEvaluation = evaluator.Evaluate(problem, originalCandidate);

        Assert.True(parentEvaluation.IsFeasible);
        Assert.Contains(
            parentEvaluation.Violations,
            violation => violation.Type == ConstraintViolationType.ResourceEffectiveTargetAssignedHoursBelowTarget &&
                         violation.ResourceId == yossi.Id);

        var mutation = new ScheduleMutationOperator(seed: 1);

        var mutatedCandidate = mutation.Mutate(
            problem,
            originalCandidate,
            parentEvaluation);

        Assert.Equal(
            originalCandidate.Assignments.Count + 1,
            mutatedCandidate.Assignments.Count);

        Assert.Contains(
            mutatedCandidate.Assignments,
            assignment => assignment.ResourceId == dana.Id &&
                          assignment.ShiftId == shift.Id);

        Assert.Contains(
            mutatedCandidate.Assignments,
            assignment => assignment.ResourceId == yossi.Id &&
                          assignment.ShiftId == shift.Id);

        var childEvaluation = evaluator.Evaluate(problem, mutatedCandidate);

        Assert.True(childEvaluation.IsFeasible);
        Assert.Equal(0, childEvaluation.Score.HardViolationCount);

        var ranker = new ScheduleEvaluationResultRanker();

        Assert.True(ranker.IsBetterThan(childEvaluation, parentEvaluation));
    }

    [Fact]
    public void Mutate_ShouldRejectUnderTargetAdd_WhenFullEvaluatorReportsHardViolation()
    {
        var dana = CreateResource("Dana");

        var motzeiShabbatNight = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 6, 6, 22, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 7, 6, 30, 0, DateTimeKind.Utc),
            ShiftKind.Night,
            minResourceCount: 0,
            maxResourceCount: 1,
            nightShiftCategory: NightShiftCategory.MotzeiShabbatNight);

        var problem = CreateProblem(
            resources: [dana],
            shifts: [motzeiShabbatNight],
            availabilityWindows:
            [
                CreateAvailability(dana, motzeiShabbatNight)
            ],
            resourcePreferences: [],
            resourceWorkloadDemands:
            [
                new ResourceWorkloadDemand(
                    dana.Id,
                    requestedPreferredHours: 8,
                    minimumRequiredHours: 0)
            ],
            resourceMonthlyNightShiftHistories:
            [
                new ResourceMonthlyNightShiftHistory(
                    dana.Id,
                    year: 2026,
                    month: 6,
                    nightShiftCategory: NightShiftCategory.MotzeiShabbatNight,
                    assignedCount: 1)
            ]);

        var originalCandidate = new ScheduleCandidate([]);

        var evaluator = new ScheduleEvaluator();
        var parentEvaluation = evaluator.Evaluate(problem, originalCandidate);

        Assert.True(parentEvaluation.IsFeasible);
        Assert.Contains(
            parentEvaluation.Violations,
            violation => violation.Type == ConstraintViolationType.ResourceEffectiveTargetAssignedHoursBelowTarget);

        var mutation = new ScheduleMutationOperator(seed: 1);

        var mutatedCandidate = mutation.Mutate(
            problem,
            originalCandidate,
            parentEvaluation);

        Assert.Empty(mutatedCandidate.Assignments);

        var childEvaluation = evaluator.Evaluate(problem, mutatedCandidate);

        Assert.True(childEvaluation.IsFeasible);
        Assert.DoesNotContain(
            childEvaluation.Violations,
            violation => violation.Type == ConstraintViolationType.ResourceMonthlyNightShiftQuotaExceeded);
    }

    [Fact]
    public void Mutate_ShouldReturnEvaluatableCandidate()
    {
        var dana = CreateResource("Dana");
        var yossi = CreateResource("Yossi");
        var shift = CreateMorningShift();

        var problem = CreateProblem(
            resources: [dana, yossi],
            shifts: [shift],
            availabilityWindows:
            [
                CreateAvailability(dana, shift),
                CreateAvailability(yossi, shift)
            ],
            resourcePreferences: []);

        var originalCandidate = new ScheduleCandidate(
        [
            new Assignment(dana.Id, shift.Id)
        ]);

        var mutation = new ScheduleMutationOperator(seed: 1);

        var mutatedCandidate = mutation.Mutate(problem, originalCandidate);

        var evaluation = new ScheduleEvaluator()
            .Evaluate(problem, mutatedCandidate);

        Assert.NotNull(evaluation);
        Assert.True(evaluation.IsFeasible);
    }

    private static SchedulingProblem CreateProblem(
        IReadOnlyCollection<Resource> resources,
        IReadOnlyCollection<Shift> shifts,
        IReadOnlyCollection<AvailabilityWindow> availabilityWindows,
        IReadOnlyCollection<ResourcePreference> resourcePreferences,
        int minimumAssignedHoursPerResource = 0,
        IReadOnlyCollection<ResourceWorkloadDemand>? resourceWorkloadDemands = null,
        IReadOnlyCollection<ResourceMonthlyNightShiftHistory>? resourceMonthlyNightShiftHistories = null)
    {
        return new SchedulingProblem(
            period: new SchedulePeriod(
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc)),
            resources: resources,
            shifts: shifts,
            availabilityWindows: availabilityWindows,
            resourcePreferences: resourcePreferences,
            minimumAssignedHoursPerResource: minimumAssignedHoursPerResource,
            resourceMonthlyNightShiftHistories: resourceMonthlyNightShiftHistories,
            resourceWorkloadDemands: resourceWorkloadDemands);
    }

    private static Resource CreateResource(string name)
    {
        return new Resource(
            Guid.NewGuid(),
            name,
            hourlyCost: 100m);
    }

    private static Shift CreateMorningShift(
        int maxResourceCount = 1)
    {
        return CreateShift(
            new DateTime(2026, 6, 2, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 2, 14, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning,
            maxResourceCount: maxResourceCount);
    }

    private static Shift CreateNightShift(
        bool requiresPreferenceToAssign = false)
    {
        return new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 6, 2, 22, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 3, 6, 30, 0, DateTimeKind.Utc),
            ShiftKind.Night,
            minResourceCount: 1,
            maxResourceCount: 1,
            requiresPreferenceToAssign: requiresPreferenceToAssign);
    }

    private static Shift CreateShift(
        DateTime startUtc,
        DateTime endUtc,
        ShiftKind kind,
        int maxResourceCount = 1)
    {
        return new Shift(
            Guid.NewGuid(),
            startUtc,
            endUtc,
            kind,
            minResourceCount: 1,
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

    private static ResourcePreference CreatePreferPreference(
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

    private static ResourcePreference CreateAvoidPreference(
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

    private static string[] ToAssignmentKeys(ScheduleCandidate candidate)
    {
        return candidate.Assignments
            .OrderBy(assignment => assignment.ShiftId)
            .ThenBy(assignment => assignment.ResourceId)
            .Select(assignment => $"{assignment.ResourceId}:{assignment.ShiftId}")
            .ToArray();
    }
}
