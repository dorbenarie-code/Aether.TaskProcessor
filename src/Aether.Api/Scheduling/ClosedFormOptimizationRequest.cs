namespace Aether.Api.Scheduling;

public sealed record ClosedFormOptimizationRequest(
    DateTime PeriodStartUtc,
    DateTime PeriodEndUtc,
    IReadOnlyCollection<ClosedFormOptimizationResourceRequest> Resources,
    IReadOnlyCollection<ClosedFormOptimizationShiftRequest> Shifts,
    IReadOnlyCollection<ClosedFormOptimizationWorkerSubmissionRequest> WorkerSubmissions,
    double TotalEffectiveTargetHours,
    double? MaximumAssignedHoursDeviationFromAverageHours = null,
    int? Seed = null,
    IReadOnlyCollection<ClosedFormOptimizationResourceMonthlyNightShiftHistoryRequest>? ResourceMonthlyNightShiftHistories = null);

public sealed record ClosedFormOptimizationResourceRequest(
    Guid Id,
    string Name,
    decimal HourlyCost,
    string WorkloadCategory);

public sealed record ClosedFormOptimizationShiftRequest(
    Guid Id,
    DateTime StartUtc,
    DateTime EndUtc,
    string Kind,
    int MinResourceCount,
    int MaxResourceCount,
    bool RequiresPreferenceToAssign = false,
    bool RequiresMinimumWhenPreferenceExists = false,
    string? NightShiftCategory = null);

public sealed record ClosedFormOptimizationWorkerSubmissionRequest(
    Guid ResourceId,
    IReadOnlyCollection<ClosedFormOptimizationWorkerShiftSubmissionRequest> ShiftSubmissions);

public sealed record ClosedFormOptimizationWorkerShiftSubmissionRequest(
    DateOnly Date,
    string ShiftKind,
    string Choice);

public sealed record ClosedFormOptimizationResourceMonthlyNightShiftHistoryRequest(
    Guid ResourceId,
    int Year,
    int Month,
    string NightShiftCategory,
    int AssignedCount);
