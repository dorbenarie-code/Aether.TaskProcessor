using Aether.Application.Scheduling.ManagerConstraints;
using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.SubmissionForms;

public sealed record FormSubmissionSchedulingProblemBuildRequest(
    SchedulePeriod Period,
    IReadOnlyCollection<Resource> Resources,
    IReadOnlyCollection<Shift> Shifts,
    IReadOnlyCollection<WorkerSubmission> WorkerSubmissions,
    double? TotalEffectiveTargetHours = null,
    int MinimumAssignedHoursPerResource = 0,
    int MinimumMorningShiftsPerResourcePerFullWeek = 0,
    int MinimumAfternoonShiftsPerResourcePerFullWeek = 0,
    IReadOnlyCollection<ResourceMonthlyNightShiftHistory>? ResourceMonthlyNightShiftHistories = null,
    double? MaximumAssignedHoursDeviationFromAverageHours = null,
    bool ApplyMandatoryShiftAvailabilityPolicy = false,
    ManagerConstraintSet? ManagerConstraintSet = null);
