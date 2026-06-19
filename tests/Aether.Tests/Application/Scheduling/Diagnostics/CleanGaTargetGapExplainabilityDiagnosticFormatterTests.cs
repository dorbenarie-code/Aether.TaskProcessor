using Aether.Application.Scheduling.Diagnostics;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling.Diagnostics;

public sealed class CleanGaTargetGapExplainabilityDiagnosticFormatterTests
{
    [Fact]
    public void Format_ShouldRenderWorkerGapsAndAddMoveDiagnostics()
    {
        var diagnostic = new TargetGapExplainabilityDiagnostic(
            WorkerDiagnostics:
            [
                new WorkerTargetGapDiagnostic(
                    ResourceId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    ResourceName: "Ziv",
                    TargetHours: 24,
                    AssignedHours: 8,
                    GapToTarget: -16,
                    SubmittedPreferredHours: 16,
                    AssignedPreferredHours: 8,
                    UnsatisfiedPreferredHours: 8)
            ],
            UnderTargetWorkerCount: 1,
            CandidateAddMoveCount: 3,
            ScoreImprovingAddMoveCount: 1,
            FairnessImprovingButScoreNotImprovingAddMoveCount: 1,
            CandidateTransferMoveCount: 0,
            ScoreImprovingTransferMoveCount: 0,
            FairnessImprovingButScoreNotImprovingTransferMoveCount: 0,
            RejectedShiftAtMaxCapacity: 2,
            RejectedAlreadyAssignedToShift: 3,
            RejectedUnavailable: 4,
            RejectedMissingRequiredPreference: 5,
            RejectedOverlap: 6,
            RejectedHardViolation: 7,
            RejectedHardViolationByType: new Dictionary<ConstraintViolationType, int>
            {
                [ConstraintViolationType.ShiftSequenceQuotaExceeded] = 2
            },
            RejectedNotImproving: 8)
        {
            ScoreImprovingAddMoveDetails =
            [
                new ScoreImprovingAddMoveDetail(
                    ResourceId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    ResourceName: "Ziv",
                    ShiftId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    ShiftStartUtc: new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc),
                    ShiftEndUtc: new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Utc),
                    ShiftKind: ShiftKind.Morning,
                    NightShiftCategory: null,
                    BaseTotalPenalty: 200,
                    NewTotalPenalty: 0,
                    PenaltyDelta: 200,
                    BaseAssignedHours: 0,
                    NewAssignedHours: 8,
                    TargetHours: 8)
            ]
        };

        var report = new CleanGaTargetGapExplainabilityDiagnosticFormatter()
            .Format(diagnostic);

        Assert.Contains("Clean GA Target Gap Explainability Diagnostics", report);
        Assert.Contains("WorkerTargetGaps:", report);
        Assert.Contains("- Ziv: Assigned=8.0h, Target=24.0h, Gap=-16.0h, SubmittedPreferred=16.0h, AssignedPreferred=8.0h, UnsatisfiedPreferred=8.0h", report);

        Assert.Contains("AddMoveDiagnostics:", report);
        Assert.Contains("UnderTargetWorkerCount: 1", report);
        Assert.Contains("CandidateAddMoveCount: 3", report);
        Assert.Contains("ScoreImprovingAddMoveCount: 1", report);
        Assert.Contains("FairnessImprovingButScoreNotImprovingAddMoveCount: 1", report);

        Assert.Contains("ScoreImprovingAddMoveDetails:", report);
        Assert.Contains("- Ziv | Shift=Morning 2026-06-01 06:30-14:30 UTC | Penalty=200->0 Delta=200 | Hours=0.0->8.0 Target=8.0", report);

        Assert.Contains("RejectedAddMoves:", report);
        Assert.Contains("RejectedShiftAtMaxCapacity: 2", report);
        Assert.Contains("RejectedAlreadyAssignedToShift: 3", report);
        Assert.Contains("RejectedUnavailable: 4", report);
        Assert.Contains("RejectedMissingRequiredPreference: 5", report);
        Assert.Contains("RejectedOverlap: 6", report);
        Assert.Contains("RejectedHardViolation: 7", report);
        Assert.Contains("RejectedNotImproving: 8", report);

        Assert.Contains("RejectedHardViolationByType:", report);
        Assert.Contains("- ShiftSequenceQuotaExceeded: 2", report);

        Assert.Contains("TransferMoveDiagnostics:", report);
        Assert.Contains("CandidateTransferMoveCount: 0", report);
        Assert.Contains("ScoreImprovingTransferMoveCount: 0", report);
        Assert.Contains("FairnessImprovingButScoreNotImprovingTransferMoveCount: 0", report);
    }
}
