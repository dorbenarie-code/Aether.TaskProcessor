using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.Optimization;

public sealed class CleanScheduleMutationOperator
{
    private readonly Random _random;

    public CleanScheduleMutationOperator(int? seed = null)
    {
        _random = seed.HasValue
            ? new Random(seed.Value)
            : new Random();
    }

    public ScheduleCandidate Mutate(
        SchedulingProblem problem,
        ScheduleCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(candidate);

        var assignments = candidate.Assignments.ToList();

        var shiftsById = problem.Shifts
            .ToDictionary(shift => shift.Id);

        var operationIndexes = new List<int>
        {
            0,
            1,
            2,
            3
        };

        Shuffle(operationIndexes);

        foreach (var operationIndex in operationIndexes)
        {
            if (operationIndex == 0 &&
                TryReassignExistingAssignment(
                    problem,
                    assignments,
                    shiftsById,
                    out var reassignedCandidate))
            {
                return reassignedCandidate;
            }

            if (operationIndex == 1 &&
                TryAddAssignment(
                    problem,
                    assignments,
                    shiftsById,
                    out var addedCandidate))
            {
                return addedCandidate;
            }

            if (operationIndex == 2 &&
                TryRemoveAssignment(
                    problem,
                    assignments,
                    shiftsById,
                    out var removedCandidate))
            {
                return removedCandidate;
            }

            if (operationIndex == 3 &&
                TrySwapAssignments(
                    problem,
                    assignments,
                    shiftsById,
                    out var swappedCandidate))
            {
                return swappedCandidate;
            }
        }

        return new ScheduleCandidate(assignments);
    }

    private bool TryReassignExistingAssignment(
        SchedulingProblem problem,
        IReadOnlyList<Assignment> assignments,
        IReadOnlyDictionary<Guid, Shift> shiftsById,
        out ScheduleCandidate candidate)
    {
        var options = assignments
            .Select((assignment, index) => new MutationOption(
                Assignment: assignment,
                AssignmentIndex: index))
            .Where(option => shiftsById.ContainsKey(option.Assignment.ShiftId))
            .Select(option => new ReassignOption(
                Assignment: option.Assignment,
                AssignmentIndex: option.AssignmentIndex,
                Shift: shiftsById[option.Assignment.ShiftId],
                ReplacementResources: GetReplacementResources(
                    problem,
                    option.Assignment,
                    shiftsById[option.Assignment.ShiftId],
                    assignments,
                    shiftsById)))
            .Where(option => option.ReplacementResources.Count > 0)
            .ToList();

        if (options.Count == 0)
        {
            candidate = new ScheduleCandidate(assignments);
            return false;
        }

        var selectedOption = options[_random.Next(options.Count)];
        var replacementResource = selectedOption
            .ReplacementResources[_random.Next(selectedOption.ReplacementResources.Count)];

        var mutatedAssignments = assignments.ToList();

        mutatedAssignments[selectedOption.AssignmentIndex] = new Assignment(
            replacementResource.Id,
            selectedOption.Shift.Id);

        candidate = new ScheduleCandidate(mutatedAssignments);
        return true;
    }

    private bool TryAddAssignment(
        SchedulingProblem problem,
        IReadOnlyList<Assignment> assignments,
        IReadOnlyDictionary<Guid, Shift> shiftsById,
        out ScheduleCandidate candidate)
    {
        var assignmentCountByShiftId = assignments
            .GroupBy(assignment => assignment.ShiftId)
            .ToDictionary(
                group => group.Key,
                group => group.Count());

        var options = problem.Shifts
            .Where(shift =>
            {
                assignmentCountByShiftId.TryGetValue(
                    shift.Id,
                    out var assignedResourceCount);

                return assignedResourceCount < shift.MaxResourceCount;
            })
            .Select(shift => new AddOption(
                Shift: shift,
                AssignableResources: problem.Resources
                    .Where(resource => CanAssignResource(
                        problem,
                        resource,
                        shift,
                        assignments,
                        shiftsById))
                    .ToList()))
            .Where(option => option.AssignableResources.Count > 0)
            .ToList();

        if (options.Count == 0)
        {
            candidate = new ScheduleCandidate(assignments);
            return false;
        }

        var selectedOption = options[_random.Next(options.Count)];
        var selectedResource = selectedOption
            .AssignableResources[_random.Next(selectedOption.AssignableResources.Count)];

        var mutatedAssignments = assignments.ToList();

        mutatedAssignments.Add(new Assignment(
            selectedResource.Id,
            selectedOption.Shift.Id));

        candidate = new ScheduleCandidate(mutatedAssignments);
        return true;
    }

    private bool TryRemoveAssignment(
        SchedulingProblem problem,
        IReadOnlyList<Assignment> assignments,
        IReadOnlyDictionary<Guid, Shift> shiftsById,
        out ScheduleCandidate candidate)
    {
        var assignmentCountByShiftId = assignments
            .GroupBy(assignment => assignment.ShiftId)
            .ToDictionary(
                group => group.Key,
                group => group.Count());

        var options = assignments
            .Select((assignment, index) => new MutationOption(
                Assignment: assignment,
                AssignmentIndex: index))
            .Where(option => shiftsById.ContainsKey(option.Assignment.ShiftId))
            .Where(option =>
            {
                var shift = shiftsById[option.Assignment.ShiftId];

                assignmentCountByShiftId.TryGetValue(
                    shift.Id,
                    out var assignedResourceCount);

                return assignedResourceCount > GetEffectiveMinResourceCount(
                    problem,
                    shift);
            })
            .ToList();

        if (options.Count == 0)
        {
            candidate = new ScheduleCandidate(assignments);
            return false;
        }

        var selectedOption = options[_random.Next(options.Count)];

        var mutatedAssignments = assignments
            .Where((_, index) => index != selectedOption.AssignmentIndex)
            .ToList();

        candidate = new ScheduleCandidate(mutatedAssignments);
        return true;
    }

    private bool TrySwapAssignments(
        SchedulingProblem problem,
        IReadOnlyList<Assignment> assignments,
        IReadOnlyDictionary<Guid, Shift> shiftsById,
        out ScheduleCandidate candidate)
    {
        var options = new List<SwapOption>();

        for (var firstIndex = 0; firstIndex < assignments.Count; firstIndex++)
        {
            var firstAssignment = assignments[firstIndex];

            if (!shiftsById.TryGetValue(
                    firstAssignment.ShiftId,
                    out var firstShift))
            {
                continue;
            }

            for (var secondIndex = firstIndex + 1; secondIndex < assignments.Count; secondIndex++)
            {
                var secondAssignment = assignments[secondIndex];

                if (firstAssignment.ResourceId == secondAssignment.ResourceId ||
                    firstAssignment.ShiftId == secondAssignment.ShiftId)
                {
                    continue;
                }

                if (!shiftsById.TryGetValue(
                        secondAssignment.ShiftId,
                        out var secondShift))
                {
                    continue;
                }

                if (CanSwapAssignments(
                        problem,
                        assignments,
                        shiftsById,
                        firstAssignment,
                        firstShift,
                        secondAssignment,
                        secondShift))
                {
                    options.Add(new SwapOption(
                        FirstAssignmentIndex: firstIndex,
                        SecondAssignmentIndex: secondIndex,
                        FirstShift: firstShift,
                        SecondShift: secondShift,
                        FirstResourceId: firstAssignment.ResourceId,
                        SecondResourceId: secondAssignment.ResourceId));
                }
            }
        }

        if (options.Count == 0)
        {
            candidate = new ScheduleCandidate(assignments);
            return false;
        }

        var selectedOption = options[_random.Next(options.Count)];
        var mutatedAssignments = assignments.ToList();

        mutatedAssignments[selectedOption.FirstAssignmentIndex] = new Assignment(
            selectedOption.SecondResourceId,
            selectedOption.FirstShift.Id);

        mutatedAssignments[selectedOption.SecondAssignmentIndex] = new Assignment(
            selectedOption.FirstResourceId,
            selectedOption.SecondShift.Id);

        candidate = new ScheduleCandidate(mutatedAssignments);
        return true;
    }

    private static bool CanSwapAssignments(
        SchedulingProblem problem,
        IReadOnlyList<Assignment> assignments,
        IReadOnlyDictionary<Guid, Shift> shiftsById,
        Assignment firstAssignment,
        Shift firstShift,
        Assignment secondAssignment,
        Shift secondShift)
    {
        var baseAssignments = assignments
            .Where(assignment => !IsSameAssignment(assignment, firstAssignment))
            .Where(assignment => !IsSameAssignment(assignment, secondAssignment))
            .ToList();

        var secondResource = problem.Resources
            .FirstOrDefault(resource => resource.Id == secondAssignment.ResourceId);

        var firstResource = problem.Resources
            .FirstOrDefault(resource => resource.Id == firstAssignment.ResourceId);

        if (secondResource is null || firstResource is null)
        {
            return false;
        }

        if (!CanAssignResource(
                problem,
                secondResource,
                firstShift,
                baseAssignments,
                shiftsById))
        {
            return false;
        }

        baseAssignments.Add(new Assignment(
            secondResource.Id,
            firstShift.Id));

        if (!CanAssignResource(
                problem,
                firstResource,
                secondShift,
                baseAssignments,
                shiftsById))
        {
            return false;
        }

        return true;
    }

    private static IReadOnlyList<Resource> GetReplacementResources(
        SchedulingProblem problem,
        Assignment assignment,
        Shift shift,
        IReadOnlyList<Assignment> assignments,
        IReadOnlyDictionary<Guid, Shift> shiftsById)
    {
        var baseAssignments = assignments
            .Where(existingAssignment => !IsSameAssignment(
                existingAssignment,
                assignment))
            .ToList();

        return problem.Resources
            .Where(resource => resource.Id != assignment.ResourceId)
            .Where(resource => CanAssignResource(
                problem,
                resource,
                shift,
                baseAssignments,
                shiftsById))
            .ToList();
    }

    private static bool CanAssignResource(
        SchedulingProblem problem,
        Resource resource,
        Shift shift,
        IReadOnlyCollection<Assignment> existingAssignments,
        IReadOnlyDictionary<Guid, Shift> shiftsById)
    {
        if (existingAssignments.Any(assignment =>
                assignment.ResourceId == resource.Id &&
                assignment.ShiftId == shift.Id))
        {
            return false;
        }

        if (!IsAvailableForShift(problem, resource, shift))
        {
            return false;
        }

        if (shift.RequiresPreferenceToAssign &&
            !HasPreferPreferenceForShift(problem, resource, shift))
        {
            return false;
        }

        if (HasOverlappingAssignment(
                resource,
                shift,
                existingAssignments,
                shiftsById))
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
            Overlaps(
                preference.StartUtc,
                preference.EndUtc,
                shift.StartUtc,
                shift.EndUtc));
    }

    private static bool HasOverlappingAssignment(
        Resource resource,
        Shift candidateShift,
        IReadOnlyCollection<Assignment> existingAssignments,
        IReadOnlyDictionary<Guid, Shift> shiftsById)
    {
        return existingAssignments
            .Where(assignment => assignment.ResourceId == resource.Id)
            .Where(assignment => shiftsById.ContainsKey(assignment.ShiftId))
            .Select(assignment => shiftsById[assignment.ShiftId])
            .Any(existingShift => Overlaps(
                existingShift.StartUtc,
                existingShift.EndUtc,
                candidateShift.StartUtc,
                candidateShift.EndUtc));
    }

    private static int GetEffectiveMinResourceCount(
        SchedulingProblem problem,
        Shift shift)
    {
        if (!shift.RequiresMinimumWhenPreferenceExists)
        {
            return shift.MinResourceCount;
        }

        var hasPreferPreference = problem.ResourcePreferences
            .Where(preference => preference.Type == ResourcePreferenceType.Prefer)
            .Any(preference => Overlaps(
                preference.StartUtc,
                preference.EndUtc,
                shift.StartUtc,
                shift.EndUtc));

        if (!hasPreferPreference)
        {
            return shift.MinResourceCount;
        }

        return Math.Max(shift.MinResourceCount, 1);
    }

    private void Shuffle<T>(IList<T> values)
    {
        for (var index = values.Count - 1; index > 0; index--)
        {
            var swapIndex = _random.Next(index + 1);
            (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
        }
    }

    private static bool IsSameAssignment(
        Assignment left,
        Assignment right)
    {
        return left.ResourceId == right.ResourceId &&
               left.ShiftId == right.ShiftId;
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

    private sealed record MutationOption(
        Assignment Assignment,
        int AssignmentIndex);

    private sealed record ReassignOption(
        Assignment Assignment,
        int AssignmentIndex,
        Shift Shift,
        IReadOnlyList<Resource> ReplacementResources);

    private sealed record AddOption(
        Shift Shift,
        IReadOnlyList<Resource> AssignableResources);

    private sealed record SwapOption(
        int FirstAssignmentIndex,
        int SecondAssignmentIndex,
        Shift FirstShift,
        Shift SecondShift,
        Guid FirstResourceId,
        Guid SecondResourceId);
}
