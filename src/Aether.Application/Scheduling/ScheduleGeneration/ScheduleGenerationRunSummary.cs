namespace Aether.Application.Scheduling.ScheduleGeneration;

public sealed record ScheduleGenerationRunSummary(
    int ImportedWorkerSubmissionCount,
    int SubmittedShiftSelectionCount,
    int ResourceCount,
    int ShiftCount,
    int ResourceWorkloadDemandCount,
    int AssignmentCount,
    bool IsFeasible,
    int HardViolationCount,
    int SoftViolationCount,
    int TotalPenalty,
    int GenerationDiagnosticCount,
    bool PostRunLocalAddImprovementApplied,
    int? PostRunLocalAddImprovementAcceptedAddMoveCount = null,
    int? PostRunLocalAddImprovementInitialTotalPenalty = null,
    int? PostRunLocalAddImprovementFinalTotalPenalty = null,
    int? PostRunLocalAddImprovementPenaltyDelta = null);
