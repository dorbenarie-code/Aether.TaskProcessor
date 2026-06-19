using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.Contracts;

public sealed record SchedulingProblemBuildRequest(
    SchedulePeriod Period,
    IReadOnlyCollection<Resource> Resources,
    IReadOnlyCollection<Shift> Shifts,
    IReadOnlyCollection<ResourceSubmissionDto> ResourceSubmissions,
    int MinimumAssignedHoursPerResource = 0,
    int MinimumMorningShiftsPerResourcePerFullWeek = 0,
    int MinimumAfternoonShiftsPerResourcePerFullWeek = 0);
