using Aether.Application.Scheduling.ManagerConstraints;
using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.SubmissionForms;

public sealed record GoogleFormsImportedRowsOptimizationRequest(
    IReadOnlyList<IReadOnlyList<string>> Rows,
    SchedulePeriod SchedulePeriod,
    DateOnly SubmittedAtFrom,
    DateOnly SubmittedAtTo,
    IReadOnlyCollection<Resource> Resources,
    IReadOnlyCollection<Shift> Shifts,
    double TotalEffectiveTargetHours,
    double? MaximumAssignedHoursDeviationFromAverageHours = null,
    int? Seed = null,
    IReadOnlyDictionary<string, Guid>? AliasesByWorkerName = null,
    IReadOnlyCollection<ResourceMonthlyNightShiftHistory>? ResourceMonthlyNightShiftHistories = null,
    ManagerConstraintSet? ManagerConstraintSet = null);
