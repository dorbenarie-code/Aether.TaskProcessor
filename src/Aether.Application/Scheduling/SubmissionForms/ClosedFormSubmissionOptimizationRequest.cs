using Aether.Application.Scheduling.ManagerConstraints;
using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.SubmissionForms;

public sealed record ClosedFormSubmissionOptimizationRequest(
    SchedulePeriod Period,
    IReadOnlyCollection<Resource> Resources,
    IReadOnlyCollection<Shift> Shifts,
    IReadOnlyCollection<WorkerSubmission> WorkerSubmissions,
    double TotalEffectiveTargetHours,
    double? MaximumAssignedHoursDeviationFromAverageHours = null,
    int? Seed = null,
    IReadOnlyCollection<ResourceMonthlyNightShiftHistory>? ResourceMonthlyNightShiftHistories = null,
    ManagerConstraintSet? ManagerConstraintSet = null,
    bool ApplyPostRunLocalAddImprovement = false);
