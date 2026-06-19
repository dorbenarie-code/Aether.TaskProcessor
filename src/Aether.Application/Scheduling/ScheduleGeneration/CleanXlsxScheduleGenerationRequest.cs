using Aether.Application.Scheduling.ManagerConstraints;
using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.ScheduleGeneration;

public sealed record CleanXlsxScheduleGenerationRequest(
    Stream AvailabilityMatrixStream,
    SchedulePeriod SchedulePeriod,
    IReadOnlyCollection<Resource> Resources,
    IReadOnlyCollection<Shift> Shifts,
    double TotalEffectiveTargetHours,
    double? MaximumAssignedHoursDeviationFromAverageHours = null,
    int? Seed = null,
    IReadOnlyDictionary<string, Guid>? AliasesByWorkerName = null,
    IReadOnlyCollection<ResourceMonthlyNightShiftHistory>? ResourceMonthlyNightShiftHistories = null,
    ManagerConstraintSet? ManagerConstraintSet = null,
    IReadOnlyList<IReadOnlyList<string>>? ManagerConstraintRows = null,
    bool ApplyPostRunLocalAddImprovement = false,
    bool IncludeTargetGapDiagnostics = false);
