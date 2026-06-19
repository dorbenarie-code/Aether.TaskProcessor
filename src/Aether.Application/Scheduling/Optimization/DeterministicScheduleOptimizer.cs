using Aether.Application.Scheduling.Contracts;
using Aether.Application.Scheduling.Interfaces;
using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.Optimization;

public sealed class DeterministicScheduleOptimizer : IScheduleOptimizer
{
    public ScheduleOptimizationResult Optimize(SchedulingProblem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);

        var assignments = new List<Assignment>();

        var shiftsById = problem.Shifts.ToDictionary(shift => shift.Id);

        foreach (var shift in problem.Shifts.OrderBy(shift => shift.StartUtc))
        {
            var requiredAssignmentCount = GetRequiredAssignmentCount(problem, shift);

            if (requiredAssignmentCount <= 0)
            {
                continue;
            }

            foreach (var resource in problem.Resources)
            {
                if (assignments.Count(assignment => assignment.ShiftId == shift.Id) >= requiredAssignmentCount)
                {
                    break;
                }

                if (!CanAssignResource(problem, resource, shift, assignments, shiftsById))
                {
                    continue;
                }

                assignments.Add(new Assignment(resource.Id, shift.Id));
            }
        }

        var candidate = new ScheduleCandidate(assignments);

        var evaluator = new ScheduleEvaluator();
        var evaluation = evaluator.Evaluate(problem, candidate);

        return new ScheduleOptimizationResult(candidate, evaluation);
    }

    private static int GetRequiredAssignmentCount(
        SchedulingProblem problem,
        Shift shift)
    {
        var requiredAssignmentCount = shift.MinResourceCount;

        if (shift.RequiresMinimumWhenPreferenceExists &&
            HasAnyPreferPreferenceForShift(problem, shift))
        {
            requiredAssignmentCount = Math.Max(requiredAssignmentCount, 1);
        }

        return Math.Min(requiredAssignmentCount, shift.MaxResourceCount);
    }

    private static bool CanAssignResource(
        SchedulingProblem problem,
        Resource resource,
        Shift shift,
        IReadOnlyCollection<Assignment> existingAssignments,
        IReadOnlyDictionary<Guid, Shift> shiftsById)
    {
        if (!IsAvailableForShift(problem, resource, shift))
        {
            return false;
        }

        if (shift.RequiresPreferenceToAssign &&
            !HasPreferPreferenceForShift(problem, resource, shift))
        {
            return false;
        }

        if (HasOverlappingAssignment(resource, shift, existingAssignments, shiftsById))
        {
            return false;
        }

        return true;
    }

    private static bool IsAvailableForShift(
        SchedulingProblem problem,
        Resource resource,
        Shift shift)
    {
        return problem.AvailabilityWindows.Any(window =>
            window.ResourceId == resource.Id &&
            window.Covers(shift));
    }

    private static bool HasPreferPreferenceForShift(
        SchedulingProblem problem,
        Resource resource,
        Shift shift)
    {
        return problem.ResourcePreferences.Any(preference =>
            preference.ResourceId == resource.Id &&
            preference.Type == ResourcePreferenceType.Prefer &&
            Overlaps(preference.StartUtc, preference.EndUtc, shift.StartUtc, shift.EndUtc));
    }

    private static bool HasAnyPreferPreferenceForShift(
        SchedulingProblem problem,
        Shift shift)
    {
        return problem.ResourcePreferences.Any(preference =>
            preference.Type == ResourcePreferenceType.Prefer &&
            Overlaps(preference.StartUtc, preference.EndUtc, shift.StartUtc, shift.EndUtc));
    }

    private static bool HasOverlappingAssignment(
        Resource resource,
        Shift candidateShift,
        IReadOnlyCollection<Assignment> existingAssignments,
        IReadOnlyDictionary<Guid, Shift> shiftsById)
    {
        return existingAssignments
            .Where(assignment => assignment.ResourceId == resource.Id)
            .Select(assignment => shiftsById[assignment.ShiftId])
            .Any(existingShift => Overlaps(
                existingShift.StartUtc,
                existingShift.EndUtc,
                candidateShift.StartUtc,
                candidateShift.EndUtc));
    }

    private static bool Overlaps(
        DateTime firstStartUtc,
        DateTime firstEndUtc,
        DateTime secondStartUtc,
        DateTime secondEndUtc)
    {
        return firstStartUtc < secondEndUtc &&
               secondStartUtc < firstEndUtc;
    }
}
