namespace Aether.Api.Scheduling;

public sealed record ClosedFormOptimizationResponse(
    bool IsFeasible,
    int ScoreValue,
    int HardViolationCount,
    int SoftViolationCount,
    int TotalPenalty,
    int AssignmentCount,
    int GenerationDiagnosticCount,
    IReadOnlyCollection<ClosedFormOptimizationAssignmentResponse> Assignments,
    IReadOnlyCollection<ClosedFormOptimizationResourceLoadResponse> LoadByResource,
    IReadOnlyDictionary<string, int> ViolationsByType,
    IReadOnlyCollection<ClosedFormOptimizationWarningResponse> Warnings);

public sealed record ClosedFormOptimizationAssignmentResponse(
    Guid ResourceId,
    Guid ShiftId);

public sealed record ClosedFormOptimizationResourceLoadResponse(
    Guid ResourceId,
    string ResourceName,
    double AssignedHours,
    int AssignmentCount);

public sealed record ClosedFormOptimizationWarningResponse(
    string Type,
    Guid? ResourceId,
    DateOnly? Date,
    string? ShiftKind);
