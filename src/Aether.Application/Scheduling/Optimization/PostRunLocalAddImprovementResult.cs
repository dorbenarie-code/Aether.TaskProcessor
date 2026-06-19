using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.Optimization;

public sealed record AcceptedLocalAddMove(
    Guid ResourceId,
    string ResourceName,
    Guid ShiftId,
    DateTime ShiftStartUtc,
    DateTime ShiftEndUtc,
    ShiftKind ShiftKind,
    NightShiftCategory? NightShiftCategory,
    int PreviousTotalPenalty,
    int NewTotalPenalty,
    int PenaltyDelta,
    double PreviousAssignedHours,
    double NewAssignedHours,
    double TargetHours);

public sealed record PostRunLocalAddImprovementResult(
    ScheduleCandidate Candidate,
    ScheduleEvaluationResult Evaluation,
    int AcceptedAddMoveCount,
    int IterationCount,
    int InitialTotalPenalty,
    int FinalTotalPenalty,
    IReadOnlyList<AcceptedLocalAddMove> AcceptedMoves);
