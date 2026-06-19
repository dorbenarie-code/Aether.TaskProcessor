using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.Optimization;

public sealed class ScheduleCrossoverOperator
{
    private readonly Random _random;

    public ScheduleCrossoverOperator(int? seed = null)
    {
        _random = seed.HasValue
            ? new Random(seed.Value)
            : new Random();
    }

    public ScheduleCandidate Crossover(
        SchedulingProblem problem,
        ScheduleCandidate firstParent,
        ScheduleCandidate secondParent)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(firstParent);
        ArgumentNullException.ThrowIfNull(secondParent);

        var knownResourceIds = problem.Resources
            .Select(resource => resource.Id)
            .ToHashSet();

        var knownShiftIds = problem.Shifts
            .Select(shift => shift.Id)
            .ToHashSet();

        var firstParentAssignmentsByShiftId = GroupValidAssignmentsByShiftId(
            firstParent,
            knownResourceIds,
            knownShiftIds);

        var secondParentAssignmentsByShiftId = GroupValidAssignmentsByShiftId(
            secondParent,
            knownResourceIds,
            knownShiftIds);

        var childAssignments = new List<Assignment>();

        foreach (var shift in problem.Shifts.OrderBy(shift => shift.StartUtc))
        {
            var selectedParentAssignmentsByShiftId = _random.Next(2) == 0
                ? firstParentAssignmentsByShiftId
                : secondParentAssignmentsByShiftId;

            if (!selectedParentAssignmentsByShiftId.TryGetValue(
                    shift.Id,
                    out var selectedAssignments))
            {
                continue;
            }

            foreach (var assignment in selectedAssignments)
            {
                childAssignments.Add(new Assignment(
                    assignment.ResourceId,
                    assignment.ShiftId));
            }
        }

        return new ScheduleCandidate(childAssignments);
    }

    private static Dictionary<Guid, List<Assignment>> GroupValidAssignmentsByShiftId(
        ScheduleCandidate parent,
        IReadOnlySet<Guid> knownResourceIds,
        IReadOnlySet<Guid> knownShiftIds)
    {
        return parent.Assignments
            .Where(assignment => knownResourceIds.Contains(assignment.ResourceId))
            .Where(assignment => knownShiftIds.Contains(assignment.ShiftId))
            .GroupBy(assignment => assignment.ShiftId)
            .ToDictionary(
                group => group.Key,
                group => group.ToList());
    }
}
