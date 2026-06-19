using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.Diagnostics;

public sealed record WorkerTargetGapDiagnostic(
    Guid ResourceId,
    string ResourceName,
    double TargetHours,
    double AssignedHours,
    double GapToTarget,
    double SubmittedPreferredHours,
    double AssignedPreferredHours,
    double UnsatisfiedPreferredHours);

public sealed record ScoreImprovingAddMoveDetail(
    Guid ResourceId,
    string ResourceName,
    Guid ShiftId,
    DateTime ShiftStartUtc,
    DateTime ShiftEndUtc,
    ShiftKind ShiftKind,
    NightShiftCategory? NightShiftCategory,
    int BaseTotalPenalty,
    int NewTotalPenalty,
    int PenaltyDelta,
    double BaseAssignedHours,
    double NewAssignedHours,
    double TargetHours);

public sealed record TargetGapExplainabilityDiagnostic(
    IReadOnlyList<WorkerTargetGapDiagnostic> WorkerDiagnostics,
    int UnderTargetWorkerCount,
    int CandidateAddMoveCount,
    int ScoreImprovingAddMoveCount,
    int FairnessImprovingButScoreNotImprovingAddMoveCount,
    int CandidateTransferMoveCount,
    int ScoreImprovingTransferMoveCount,
    int FairnessImprovingButScoreNotImprovingTransferMoveCount,
    int RejectedShiftAtMaxCapacity,
    int RejectedAlreadyAssignedToShift,
    int RejectedUnavailable,
    int RejectedMissingRequiredPreference,
    int RejectedOverlap,
    int RejectedHardViolation,
    IReadOnlyDictionary<ConstraintViolationType, int> RejectedHardViolationByType,
    int RejectedNotImproving)
{
    public IReadOnlyList<ScoreImprovingAddMoveDetail> ScoreImprovingAddMoveDetails { get; init; } = [];
}
