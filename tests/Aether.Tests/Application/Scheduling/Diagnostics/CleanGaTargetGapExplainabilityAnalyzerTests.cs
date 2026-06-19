using Aether.Application.Scheduling.Diagnostics;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling.Diagnostics;

public sealed class CleanGaTargetGapExplainabilityAnalyzerTests
{
    private const double HoursTolerance = 0.000001;

    [Fact]
    public void Analyze_ShouldReportWorkerTargetGapAndPreferredFulfillmentMetrics()
    {
        var resource = CreateResource("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "Ziv");

        var firstShift = CreateShift(
            "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
            new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc));

        var secondShift = CreateShift(
            "cccccccc-cccc-cccc-cccc-cccccccccccc",
            new DateTime(2026, 6, 2, 6, 30, 0, DateTimeKind.Utc));

        var problem = new SchedulingProblem(
            new SchedulePeriod(
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc)),
            resources: [resource],
            shifts: [firstShift, secondShift],
            availabilityWindows:
            [
                CreateAvailability(resource, firstShift),
                CreateAvailability(resource, secondShift)
            ],
            resourcePreferences:
            [
                CreatePrefer(resource, firstShift),
                CreatePrefer(resource, secondShift)
            ],
            resourceWorkloadDemands:
            [
                new ResourceWorkloadDemand(
                    resource.Id,
                    requestedPreferredHours: 24,
                    minimumRequiredHours: 0)
            ]);

        var candidate = new ScheduleCandidate(
        [
            new Assignment(resource.Id, firstShift.Id)
        ]);

        var weights = ScheduleScoringWeights.CreateDefault();

        var evaluation = new ScheduleEvaluator(weights)
            .Evaluate(problem, candidate);

        Assert.True(evaluation.IsFeasible);

        var diagnostic = new CleanGaTargetGapExplainabilityAnalyzer()
            .Analyze(
                problem,
                candidate,
                evaluation,
                weights);

        var workerDiagnostic = Assert.Single(diagnostic.WorkerDiagnostics);

        Assert.Equal(resource.Id, workerDiagnostic.ResourceId);
        Assert.Equal("Ziv", workerDiagnostic.ResourceName);
        Assert.Equal(24.0, workerDiagnostic.TargetHours, HoursTolerance);
        Assert.Equal(8.0, workerDiagnostic.AssignedHours, HoursTolerance);
        Assert.Equal(-16.0, workerDiagnostic.GapToTarget, HoursTolerance);
        Assert.Equal(16.0, workerDiagnostic.SubmittedPreferredHours, HoursTolerance);
        Assert.Equal(8.0, workerDiagnostic.AssignedPreferredHours, HoursTolerance);
        Assert.Equal(8.0, workerDiagnostic.UnsatisfiedPreferredHours, HoursTolerance);

        Assert.Equal(1, diagnostic.UnderTargetWorkerCount);
    }


    [Fact]
    public void Analyze_ShouldReportScoreImprovingAddMove_WhenUnderTargetWorkerCanBeAddedLegally()
    {
        var underTargetResource = CreateResource(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "Ziv");

        var assignedResource = CreateResource(
            "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
            "Rafael");

        var shift = new Shift(
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning,
            minResourceCount: 0,
            maxResourceCount: 2);

        var problem = new SchedulingProblem(
            new SchedulePeriod(
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc)),
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
        Assert.True(evaluation.Score.TotalPenalty > 0);

        var diagnostic = new CleanGaTargetGapExplainabilityAnalyzer()
            .Analyze(
                problem,
                candidate,
                evaluation,
                weights);

        Assert.Equal(1, diagnostic.UnderTargetWorkerCount);
        Assert.Equal(1, diagnostic.CandidateAddMoveCount);
        Assert.Equal(1, diagnostic.ScoreImprovingAddMoveCount);
        Assert.Equal(0, diagnostic.FairnessImprovingButScoreNotImprovingAddMoveCount);
        Assert.Equal(0, diagnostic.RejectedShiftAtMaxCapacity);
        Assert.Equal(0, diagnostic.RejectedAlreadyAssignedToShift);
        Assert.Equal(0, diagnostic.RejectedUnavailable);
        Assert.Equal(0, diagnostic.RejectedMissingRequiredPreference);
        Assert.Equal(0, diagnostic.RejectedOverlap);
        Assert.Equal(0, diagnostic.RejectedHardViolation);
        Assert.Equal(0, diagnostic.RejectedNotImproving);
    }

    [Fact]
    public void Analyze_ShouldReportFairnessImprovingButScoreNotImprovingAddMove_WhenAddMoveReducesTargetGapButIncreasesPenalty()
    {
        var resource = CreateResource(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "Ziv");

        var shift = new Shift(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning,
            minResourceCount: 0,
            maxResourceCount: 1,
            requiresPreferenceToAssign: false);

        var problem = new SchedulingProblem(
            new SchedulePeriod(
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc)),
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
        Assert.Contains(
            evaluation.Violations,
            violation =>
                violation.Type == ConstraintViolationType.ResourceEffectiveTargetAssignedHoursBelowTarget &&
                violation.ResourceId == resource.Id &&
                violation.Magnitude == 8);

        var diagnostic = new CleanGaTargetGapExplainabilityAnalyzer()
            .Analyze(
                problem,
                candidate,
                evaluation,
                weights);

        Assert.Equal(1, diagnostic.UnderTargetWorkerCount);
        Assert.Equal(1, diagnostic.CandidateAddMoveCount);
        Assert.Equal(0, diagnostic.ScoreImprovingAddMoveCount);
        Assert.Equal(1, diagnostic.FairnessImprovingButScoreNotImprovingAddMoveCount);
        Assert.Equal(0, diagnostic.RejectedShiftAtMaxCapacity);
        Assert.Equal(0, diagnostic.RejectedAlreadyAssignedToShift);
        Assert.Equal(0, diagnostic.RejectedUnavailable);
        Assert.Equal(0, diagnostic.RejectedMissingRequiredPreference);
        Assert.Equal(0, diagnostic.RejectedOverlap);
        Assert.Equal(0, diagnostic.RejectedHardViolation);
        Assert.Equal(0, diagnostic.RejectedNotImproving);
    }


    [Fact]
    public void Analyze_ShouldReportRejectedShiftAtMaxCapacity_WhenUnderTargetWorkerCannotBeAddedToFullShift()
    {
        var underTargetResource = CreateResource(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "Ziv");

        var assignedResource = CreateResource(
            "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
            "Rafael");

        var shift = CreateShift(
            "cccccccc-cccc-cccc-cccc-cccccccccccc",
            new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc));

        var problem = new SchedulingProblem(
            new SchedulePeriod(
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc)),
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

        var diagnostic = new CleanGaTargetGapExplainabilityAnalyzer()
            .Analyze(
                problem,
                candidate,
                evaluation,
                weights);

        Assert.Equal(1, diagnostic.UnderTargetWorkerCount);
        Assert.Equal(1, diagnostic.CandidateAddMoveCount);
        Assert.Equal(0, diagnostic.ScoreImprovingAddMoveCount);
        Assert.Equal(0, diagnostic.FairnessImprovingButScoreNotImprovingAddMoveCount);
        Assert.Equal(1, diagnostic.RejectedShiftAtMaxCapacity);
        Assert.Equal(0, diagnostic.RejectedAlreadyAssignedToShift);
        Assert.Equal(0, diagnostic.RejectedUnavailable);
        Assert.Equal(0, diagnostic.RejectedMissingRequiredPreference);
        Assert.Equal(0, diagnostic.RejectedOverlap);
        Assert.Equal(0, diagnostic.RejectedHardViolation);
        Assert.Equal(0, diagnostic.RejectedNotImproving);
    }

    [Fact]
    public void Analyze_ShouldReportRejectedUnavailable_WhenUnderTargetWorkerIsNotAvailableForShift()
    {
        var resource = CreateResource(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "Ziv");

        var shift = CreateShift(
            "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
            new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc));

        var problem = new SchedulingProblem(
            new SchedulePeriod(
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc)),
            resources: [resource],
            shifts: [shift],
            availabilityWindows: [],
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

        var candidate = new ScheduleCandidate([]);

        var weights = ScheduleScoringWeights.CreateDefault();

        var evaluation = new ScheduleEvaluator(weights)
            .Evaluate(problem, candidate);

        Assert.True(evaluation.IsFeasible);

        var diagnostic = new CleanGaTargetGapExplainabilityAnalyzer()
            .Analyze(
                problem,
                candidate,
                evaluation,
                weights);

        Assert.Equal(1, diagnostic.UnderTargetWorkerCount);
        Assert.Equal(1, diagnostic.CandidateAddMoveCount);
        Assert.Equal(0, diagnostic.ScoreImprovingAddMoveCount);
        Assert.Equal(0, diagnostic.FairnessImprovingButScoreNotImprovingAddMoveCount);
        Assert.Equal(0, diagnostic.RejectedShiftAtMaxCapacity);
        Assert.Equal(0, diagnostic.RejectedAlreadyAssignedToShift);
        Assert.Equal(1, diagnostic.RejectedUnavailable);
        Assert.Equal(0, diagnostic.RejectedMissingRequiredPreference);
        Assert.Equal(0, diagnostic.RejectedOverlap);
        Assert.Equal(0, diagnostic.RejectedHardViolation);
        Assert.Equal(0, diagnostic.RejectedNotImproving);
    }

    [Fact]
    public void Analyze_ShouldReportRejectedMissingRequiredPreference_WhenShiftRequiresPreferAndWorkerHasNoPrefer()
    {
        var resource = CreateResource(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "Ziv");

        var shift = new Shift(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning,
            minResourceCount: 0,
            maxResourceCount: 1,
            requiresPreferenceToAssign: true);

        var problem = new SchedulingProblem(
            new SchedulePeriod(
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc)),
            resources: [resource],
            shifts: [shift],
            availabilityWindows:
            [
                CreateAvailability(resource, shift)
            ],
            resourcePreferences: [],
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

        var diagnostic = new CleanGaTargetGapExplainabilityAnalyzer()
            .Analyze(
                problem,
                candidate,
                evaluation,
                weights);

        Assert.Equal(1, diagnostic.UnderTargetWorkerCount);
        Assert.Equal(1, diagnostic.CandidateAddMoveCount);
        Assert.Equal(0, diagnostic.ScoreImprovingAddMoveCount);
        Assert.Equal(0, diagnostic.FairnessImprovingButScoreNotImprovingAddMoveCount);
        Assert.Equal(0, diagnostic.RejectedShiftAtMaxCapacity);
        Assert.Equal(0, diagnostic.RejectedAlreadyAssignedToShift);
        Assert.Equal(0, diagnostic.RejectedUnavailable);
        Assert.Equal(1, diagnostic.RejectedMissingRequiredPreference);
        Assert.Equal(0, diagnostic.RejectedOverlap);
        Assert.Equal(0, diagnostic.RejectedHardViolation);
        Assert.Equal(0, diagnostic.RejectedNotImproving);
    }

    [Fact]
    public void Analyze_ShouldReportRejectedOverlap_WhenUnderTargetWorkerAlreadyHasOverlappingAssignment()
    {
        var resource = CreateResource(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "Ziv");

        var assignedShift = new Shift(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning,
            minResourceCount: 0,
            maxResourceCount: 2);

        var overlappingShift = new Shift(
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 18, 0, 0, DateTimeKind.Utc),
            ShiftKind.Afternoon,
            minResourceCount: 0,
            maxResourceCount: 1,
            requiresPreferenceToAssign: false);

        var problem = new SchedulingProblem(
            new SchedulePeriod(
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc)),
            resources: [resource],
            shifts: [assignedShift, overlappingShift],
            availabilityWindows:
            [
                CreateAvailability(resource, assignedShift),
                CreateAvailability(resource, overlappingShift)
            ],
            resourcePreferences:
            [
                CreatePrefer(resource, assignedShift),
                CreatePrefer(resource, overlappingShift)
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
            new Assignment(resource.Id, assignedShift.Id)
        ]);

        var weights = ScheduleScoringWeights.CreateDefault();

        var evaluation = new ScheduleEvaluator(weights)
            .Evaluate(problem, candidate);

        Assert.True(evaluation.IsFeasible);

        var diagnostic = new CleanGaTargetGapExplainabilityAnalyzer()
            .Analyze(
                problem,
                candidate,
                evaluation,
                weights);

        Assert.Equal(1, diagnostic.UnderTargetWorkerCount);
        Assert.Equal(2, diagnostic.CandidateAddMoveCount);
        Assert.Equal(0, diagnostic.ScoreImprovingAddMoveCount);
        Assert.Equal(0, diagnostic.FairnessImprovingButScoreNotImprovingAddMoveCount);
        Assert.Equal(0, diagnostic.RejectedShiftAtMaxCapacity);
        Assert.Equal(1, diagnostic.RejectedAlreadyAssignedToShift);
        Assert.Equal(0, diagnostic.RejectedUnavailable);
        Assert.Equal(0, diagnostic.RejectedMissingRequiredPreference);
        Assert.Equal(1, diagnostic.RejectedOverlap);
        Assert.Equal(0, diagnostic.RejectedHardViolation);
        Assert.Equal(0, diagnostic.RejectedNotImproving);
    }

    [Fact]
    public void Analyze_ShouldReportRejectedHardViolationByType_WhenAddMoveWouldBreakShiftSequenceQuota()
    {
        var resource = CreateResource(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "Ziv");

        var assignedAfternoon1 = CreateSequenceShift(
            "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
            new DateTime(2026, 6, 1, 14, 20, 0, DateTimeKind.Utc),
            ShiftKind.Afternoon);

        var assignedMorning2 = CreateSequenceShift(
            "cccccccc-cccc-cccc-cccc-cccccccccccc",
            new DateTime(2026, 6, 2, 6, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning);

        var assignedNight2 = CreateSequenceShift(
            "dddddddd-dddd-dddd-dddd-dddddddddddd",
            new DateTime(2026, 6, 2, 22, 40, 0, DateTimeKind.Utc),
            ShiftKind.Night);

        var assignedAfternoon3 = CreateSequenceShift(
            "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee",
            new DateTime(2026, 6, 3, 14, 20, 0, DateTimeKind.Utc),
            ShiftKind.Afternoon);

        var assignedMorning4 = CreateSequenceShift(
            "ffffffff-ffff-ffff-ffff-ffffffffffff",
            new DateTime(2026, 6, 4, 6, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning);

        var assignedNight4 = CreateSequenceShift(
            "11111111-1111-1111-1111-111111111111",
            new DateTime(2026, 6, 4, 22, 40, 0, DateTimeKind.Utc),
            ShiftKind.Night);

        var assignedAfternoon5 = CreateSequenceShift(
            "22222222-2222-2222-2222-222222222222",
            new DateTime(2026, 6, 5, 14, 20, 0, DateTimeKind.Utc),
            ShiftKind.Afternoon);

        var candidateMorning6 = CreateSequenceShift(
            "33333333-3333-3333-3333-333333333333",
            new DateTime(2026, 6, 6, 6, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning);

        var shifts = new[]
        {
            assignedAfternoon1,
            assignedMorning2,
            assignedNight2,
            assignedAfternoon3,
            assignedMorning4,
            assignedNight4,
            assignedAfternoon5,
            candidateMorning6
        };

        var problem = new SchedulingProblem(
            new SchedulePeriod(
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 7, 0, 0, 0, DateTimeKind.Utc)),
            resources: [resource],
            shifts: shifts,
            availabilityWindows: shifts
                .Select(shift => CreateAvailability(resource, shift))
                .ToArray(),
            resourcePreferences: [],
            resourceWorkloadDemands:
            [
                new ResourceWorkloadDemand(
                    resource.Id,
                    requestedPreferredHours: 64,
                    minimumRequiredHours: 0)
            ]);

        var candidate = new ScheduleCandidate(
        [
            new Assignment(resource.Id, assignedAfternoon1.Id),
            new Assignment(resource.Id, assignedMorning2.Id),
            new Assignment(resource.Id, assignedNight2.Id),
            new Assignment(resource.Id, assignedAfternoon3.Id),
            new Assignment(resource.Id, assignedMorning4.Id),
            new Assignment(resource.Id, assignedNight4.Id),
            new Assignment(resource.Id, assignedAfternoon5.Id)
        ]);

        var weights = ScheduleScoringWeights.CreateDefault();

        var evaluation = new ScheduleEvaluator(weights)
            .Evaluate(problem, candidate);

        Assert.True(evaluation.IsFeasible);
        Assert.Equal(
            0,
            evaluation.Violations.Count(violation =>
                violation.Type == ConstraintViolationType.ShiftSequenceQuotaExceeded));

        var addCandidate = new ScheduleCandidate(
            candidate.Assignments
                .Concat(
                [
                    new Assignment(resource.Id, candidateMorning6.Id)
                ])
                .ToArray());

        var addEvaluation = new ScheduleEvaluator(weights)
            .Evaluate(problem, addCandidate);

        Assert.False(addEvaluation.IsFeasible);
        Assert.Contains(
            addEvaluation.Violations,
            violation =>
                violation.Type == ConstraintViolationType.ShiftSequenceQuotaExceeded &&
                violation.Severity == ConstraintViolationSeverity.Hard &&
                violation.ResourceId == resource.Id);

        var diagnostic = new CleanGaTargetGapExplainabilityAnalyzer()
            .Analyze(
                problem,
                candidate,
                evaluation,
                weights);

        Assert.Equal(1, diagnostic.UnderTargetWorkerCount);
        Assert.Equal(8, diagnostic.CandidateAddMoveCount);
        Assert.Equal(0, diagnostic.ScoreImprovingAddMoveCount);
        Assert.Equal(0, diagnostic.FairnessImprovingButScoreNotImprovingAddMoveCount);
        Assert.Equal(0, diagnostic.RejectedShiftAtMaxCapacity);
        Assert.Equal(7, diagnostic.RejectedAlreadyAssignedToShift);
        Assert.Equal(0, diagnostic.RejectedUnavailable);
        Assert.Equal(0, diagnostic.RejectedMissingRequiredPreference);
        Assert.Equal(0, diagnostic.RejectedOverlap);
        Assert.Equal(1, diagnostic.RejectedHardViolation);
        Assert.Equal(0, diagnostic.RejectedNotImproving);

        var rejectedHardViolation = Assert.Single(diagnostic.RejectedHardViolationByType);

        Assert.Equal(ConstraintViolationType.ShiftSequenceQuotaExceeded, rejectedHardViolation.Key);
        Assert.Equal(1, rejectedHardViolation.Value);
    }


    [Fact]
    public void Analyze_ShouldReportScoreImprovingAddMoveDetails_WhenAddMoveImprovesScore()
    {
        var underTargetResource = CreateResource(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "Ziv");

        var assignedResource = CreateResource(
            "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
            "Rafael");

        var shift = new Shift(
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning,
            minResourceCount: 0,
            maxResourceCount: 2);

        var problem = new SchedulingProblem(
            new SchedulePeriod(
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc)),
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

        var diagnostic = new CleanGaTargetGapExplainabilityAnalyzer()
            .Analyze(
                problem,
                candidate,
                evaluation,
                weights);

        var move = Assert.Single(diagnostic.ScoreImprovingAddMoveDetails);

        Assert.Equal(underTargetResource.Id, move.ResourceId);
        Assert.Equal("Ziv", move.ResourceName);
        Assert.Equal(shift.Id, move.ShiftId);
        Assert.Equal(shift.StartUtc, move.ShiftStartUtc);
        Assert.Equal(shift.EndUtc, move.ShiftEndUtc);
        Assert.Equal(ShiftKind.Morning, move.ShiftKind);
        Assert.Null(move.NightShiftCategory);

        Assert.Equal(200, move.BaseTotalPenalty);
        Assert.Equal(0, move.NewTotalPenalty);
        Assert.Equal(200, move.PenaltyDelta);

        Assert.Equal(0.0, move.BaseAssignedHours, HoursTolerance);
        Assert.Equal(8.0, move.NewAssignedHours, HoursTolerance);
        Assert.Equal(8.0, move.TargetHours, HoursTolerance);
    }
    private static Shift CreateSequenceShift(
        string id,
        DateTime startUtc,
        ShiftKind kind)
    {
        var endUtc = kind switch
        {
            ShiftKind.Morning => startUtc.Date.AddHours(14).AddMinutes(20),
            ShiftKind.Afternoon => startUtc.Date.AddHours(22).AddMinutes(40),
            ShiftKind.Night => startUtc.Date.AddDays(1).AddHours(6).AddMinutes(30),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), "Shift kind is not supported.")
        };

        return new Shift(
            Guid.Parse(id),
            startUtc,
            endUtc,
            kind,
            minResourceCount: 0,
            maxResourceCount: 2);
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
        DateTime startUtc)
    {
        return new Shift(
            Guid.Parse(id),
            startUtc,
            startUtc.AddHours(8),
            ShiftKind.Morning,
            minResourceCount: 0,
            maxResourceCount: 1);
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
}
