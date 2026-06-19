using Aether.Application.Scheduling.Contracts;
using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.Reports;

public sealed class ScheduleTableProjectionBuilder
{
    public ScheduleTableProjection Build(
        SchedulingProblem problem,
        SchedulingRunOptimizationResult result)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(result);

        var resourcesById = problem.Resources.ToDictionary(resource => resource.Id);
        var shiftsById = problem.Shifts.ToDictionary(shift => shift.Id);

        var assignedNamesByDateAndKind = result.Candidate.Assignments
            .Where(assignment => shiftsById.ContainsKey(assignment.ShiftId))
            .GroupBy(assignment =>
            {
                var shift = shiftsById[assignment.ShiftId];

                return new ScheduleTableCellKey(
                    DateOnly.FromDateTime(shift.StartUtc),
                    shift.Kind);
            })
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(assignment =>
                        resourcesById.TryGetValue(assignment.ResourceId, out var resource)
                            ? resource.Name
                            : assignment.ResourceId.ToString())
                    .OrderBy(name => name)
                    .ToArray());

        var days = problem.Shifts
            .Select(shift => DateOnly.FromDateTime(shift.StartUtc))
            .Distinct()
            .OrderBy(date => date)
            .Select(date => new ScheduleTableDayProjection(
                date,
                date.DayOfWeek,
                GetAssignedWorkerNames(assignedNamesByDateAndKind, date, ShiftKind.Morning),
                GetAssignedWorkerNames(assignedNamesByDateAndKind, date, ShiftKind.Afternoon),
                GetAssignedWorkerNames(assignedNamesByDateAndKind, date, ShiftKind.Night)))
            .ToArray();

        return new ScheduleTableProjection(
            days,
            GetMaximumSlotCount(problem, ShiftKind.Morning),
            GetMaximumSlotCount(problem, ShiftKind.Afternoon),
            GetMaximumSlotCount(problem, ShiftKind.Night))
        {
            MorningTimeRangeText = GetShiftTimeRangeText(problem, ShiftKind.Morning),
            AfternoonTimeRangeText = GetShiftTimeRangeText(problem, ShiftKind.Afternoon),
            NightTimeRangeText = GetShiftTimeRangeText(problem, ShiftKind.Night)
        };
    }

    private static string GetShiftTimeRangeText(
        SchedulingProblem problem,
        ShiftKind shiftKind)
    {
        var firstShift = problem.Shifts
            .Where(candidate => candidate.Kind == shiftKind)
            .OrderBy(candidate => candidate.StartUtc)
            .FirstOrDefault();

        if (firstShift is null)
        {
            return string.Empty;
        }

        return $"{firstShift.StartUtc:HH:mm}-{firstShift.EndUtc:HH:mm}";
    }

    private static IReadOnlyList<string> GetAssignedWorkerNames(
        IReadOnlyDictionary<ScheduleTableCellKey, string[]> assignedNamesByDateAndKind,
        DateOnly date,
        ShiftKind shiftKind)
    {
        var key = new ScheduleTableCellKey(date, shiftKind);

        return assignedNamesByDateAndKind.TryGetValue(key, out var assignedNames)
            ? assignedNames
            : Array.Empty<string>();
    }

    private static int GetMaximumSlotCount(
        SchedulingProblem problem,
        ShiftKind shiftKind)
    {
        return problem.Shifts
            .Where(shift => shift.Kind == shiftKind)
            .Select(shift => shift.MaxResourceCount)
            .DefaultIfEmpty(0)
            .Max();
    }

    private sealed record ScheduleTableCellKey(
        DateOnly Date,
        ShiftKind ShiftKind);
}
