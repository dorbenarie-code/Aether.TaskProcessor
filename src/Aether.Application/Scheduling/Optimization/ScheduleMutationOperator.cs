using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.Optimization;

public sealed class ScheduleMutationOperator
{
    private const double HoursComparisonTolerance = 0.000001;

    private readonly Random _random;
    private readonly ScheduleEvaluator _evaluator = new();
    private readonly ScheduleEvaluationResultRanker _ranker = new();

    public ScheduleMutationOperator(int? seed = null)
    {
        _random = seed.HasValue
            ? new Random(seed.Value)
            : new Random();
    }

    public ScheduleCandidate Mutate(
        SchedulingProblem problem,
        ScheduleCandidate candidate,
        ScheduleEvaluationResult? parentEvaluation = null)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(candidate);

        var assignments = candidate.Assignments.ToList();

        var shiftsById = problem.Shifts
            .ToDictionary(shift => shift.Id);

        if (parentEvaluation is not null &&
            HasIgnoredAvoidPreference(parentEvaluation) &&
            TryCreateChainReassignCandidate(
                problem,
                assignments,
                shiftsById,
                parentEvaluation,
                out var chainCandidate))
        {
            return chainCandidate;
        }

        if (parentEvaluation is not null &&
            HasEffectiveTargetAboveTarget(parentEvaluation) &&
            TryCreateOverTargetAssignmentRemovalCandidate(
                problem,
                assignments,
                shiftsById,
                parentEvaluation,
                out var overTargetRemovalCandidate))
        {
            return overTargetRemovalCandidate;
        }

        if (parentEvaluation is not null &&
            HasEffectiveTargetBelowTarget(
                problem,
                assignments,
                shiftsById) &&
            TryCreateUnderTargetAddCandidate(
                problem,
                assignments,
                shiftsById,
                parentEvaluation,
                out var underTargetAddCandidate))
        {
            return underTargetAddCandidate;
        }

        if (assignments.Count == 0)
        {
            return new ScheduleCandidate(assignments);
        }

        return MutateSingleReassign(problem, assignments, shiftsById);
    }

    private bool TryCreateOverTargetAssignmentRemovalCandidate(
        SchedulingProblem problem,
        IReadOnlyList<Assignment> assignments,
        IReadOnlyDictionary<Guid, Shift> shiftsById,
        ScheduleEvaluationResult parentEvaluation,
        out ScheduleCandidate candidate)
    {
        var overTargetResourceIds = parentEvaluation.Violations
            .Where(violation =>
                violation.Type == ConstraintViolationType.ResourceEffectiveTargetAssignedHoursAboveTarget)
            .Where(violation => violation.ResourceId is not null)
            .Select(violation => violation.ResourceId!.Value)
            .Distinct()
            .ToList();

        Shuffle(overTargetResourceIds);

        var assignmentCountByShiftId = assignments
            .GroupBy(assignment => assignment.ShiftId)
            .ToDictionary(
                group => group.Key,
                group => group.Count());

        foreach (var resourceId in overTargetResourceIds)
        {
            var removalOptions = assignments
                .Select((assignment, index) => new MutationOption(
                    Assignment: assignment,
                    AssignmentIndex: index))
                .Where(option => option.Assignment.ResourceId == resourceId)
                .Where(option => shiftsById.ContainsKey(option.Assignment.ShiftId))
                .Select(option => new OverTargetRemovalOption(
                    Assignment: option.Assignment,
                    AssignmentIndex: option.AssignmentIndex,
                    Shift: shiftsById[option.Assignment.ShiftId]))
                .Where(option => HasRemovableCapacity(
                    problem,
                    option.Shift,
                    assignmentCountByShiftId))
                .ToList();

            Shuffle(removalOptions);

            foreach (var option in removalOptions)
            {
                var mutatedAssignments = assignments
                    .Where((_, index) => index != option.AssignmentIndex)
                    .ToList();

                var mutatedCandidate = new ScheduleCandidate(mutatedAssignments);
                var childEvaluation = _evaluator.Evaluate(problem, mutatedCandidate);

                if (childEvaluation.Score.HardViolationCount != 0)
                {
                    continue;
                }

                if (!_ranker.IsBetterThan(childEvaluation, parentEvaluation))
                {
                    continue;
                }

                candidate = mutatedCandidate;
                return true;
            }
        }

        candidate = new ScheduleCandidate(assignments);
        return false;
    }

    private static bool HasRemovableCapacity(
        SchedulingProblem problem,
        Shift shift,
        IReadOnlyDictionary<Guid, int> assignmentCountByShiftId)
    {
        assignmentCountByShiftId.TryGetValue(
            shift.Id,
            out var assignedResourceCount);

        return assignedResourceCount > GetEffectiveMinResourceCount(problem, shift);
    }

    private bool TryCreateUnderTargetAddCandidate(
        SchedulingProblem problem,
        IReadOnlyList<Assignment> assignments,
        IReadOnlyDictionary<Guid, Shift> shiftsById,
        ScheduleEvaluationResult parentEvaluation,
        out ScheduleCandidate candidate)
    {
        var assignmentCountByShiftId = assignments
            .GroupBy(assignment => assignment.ShiftId)
            .ToDictionary(
                group => group.Key,
                group => group.Count());

        var underTargetResourceIds = GetUnderTargetResourceIds(
            problem,
            assignments,
            shiftsById);

        Shuffle(underTargetResourceIds);

        var addableShifts = problem.Shifts
            .Where(shift =>
            {
                assignmentCountByShiftId.TryGetValue(
                    shift.Id,
                    out var assignedResourceCount);

                return assignedResourceCount < shift.MaxResourceCount;
            })
            .ToList();

        Shuffle(addableShifts);

        foreach (var resourceId in underTargetResourceIds)
        {
            var resource = problem.Resources
                .FirstOrDefault(candidateResource => candidateResource.Id == resourceId);

            if (resource is null)
            {
                continue;
            }

            foreach (var shift in addableShifts)
            {
                if (!CanAssignResource(
                        problem,
                        resource,
                        shift,
                        assignments,
                        shiftsById))
                {
                    continue;
                }

                var mutatedAssignments = assignments.ToList();

                mutatedAssignments.Add(new Assignment(
                    resource.Id,
                    shift.Id));

                var mutatedCandidate = new ScheduleCandidate(mutatedAssignments);
                var childEvaluation = _evaluator.Evaluate(problem, mutatedCandidate);

                if (childEvaluation.Score.HardViolationCount != 0)
                {
                    continue;
                }

                if (!_ranker.IsBetterThan(childEvaluation, parentEvaluation))
                {
                    continue;
                }

                candidate = mutatedCandidate;
                return true;
            }
        }

        candidate = new ScheduleCandidate(assignments);
        return false;
    }

    private static List<Guid> GetUnderTargetResourceIds(
        SchedulingProblem problem,
        IReadOnlyList<Assignment> assignments,
        IReadOnlyDictionary<Guid, Shift> shiftsById)
    {
        var assignedHoursByResourceId = assignments
            .Where(assignment => shiftsById.ContainsKey(assignment.ShiftId))
            .GroupBy(assignment => assignment.ResourceId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(assignment =>
                {
                    var shift = shiftsById[assignment.ShiftId];

                    return (shift.EndUtc - shift.StartUtc).TotalHours;
                }));

        return problem.ResourceWorkloadDemands
            .Where(demand =>
            {
                assignedHoursByResourceId.TryGetValue(
                    demand.ResourceId,
                    out var assignedHours);

                return assignedHours + HoursComparisonTolerance < demand.EffectiveTargetHours;
            })
            .Select(demand => demand.ResourceId)
            .Distinct()
            .ToList();
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

    private ScheduleCandidate MutateSingleReassign(
        SchedulingProblem problem,
        List<Assignment> assignments,
        IReadOnlyDictionary<Guid, Shift> shiftsById)
    {
        var mutationOptions = assignments
            .Select((assignment, index) => new MutationOption(
                Assignment: assignment,
                AssignmentIndex: index))
            .Where(option => shiftsById.ContainsKey(option.Assignment.ShiftId))
            .Select(option => new MutationTarget(
                Assignment: option.Assignment,
                AssignmentIndex: option.AssignmentIndex,
                Shift: shiftsById[option.Assignment.ShiftId],
                ReplacementResources: GetReplacementResources(
                    problem,
                    option.Assignment,
                    shiftsById[option.Assignment.ShiftId],
                    assignments,
                    shiftsById)))
            .Where(target => target.ReplacementResources.Count > 0)
            .ToList();

        if (mutationOptions.Count == 0)
        {
            return new ScheduleCandidate(assignments);
        }

        var selectedTarget = mutationOptions[_random.Next(mutationOptions.Count)];
        var replacementResource = selectedTarget
            .ReplacementResources[_random.Next(selectedTarget.ReplacementResources.Count)];

        assignments[selectedTarget.AssignmentIndex] = new Assignment(
            replacementResource.Id,
            selectedTarget.Shift.Id);

        return new ScheduleCandidate(assignments);
    }

    private bool TryCreateChainReassignCandidate(
        SchedulingProblem problem,
        IReadOnlyList<Assignment> assignments,
        IReadOnlyDictionary<Guid, Shift> shiftsById,
        ScheduleEvaluationResult parentEvaluation,
        out ScheduleCandidate candidate)
    {
        var avoidViolations = parentEvaluation.Violations
            .Where(violation => violation.Type == ConstraintViolationType.IgnoredAvoidPreference)
            .ToList();

        Shuffle(avoidViolations);

        foreach (var violation in avoidViolations)
        {
            if (violation.ResourceId is null || violation.ShiftId is null)
            {
                continue;
            }

            var avoidedResourceId = violation.ResourceId.Value;
            var avoidedShiftId = violation.ShiftId.Value;

            if (!shiftsById.TryGetValue(avoidedShiftId, out var avoidedShift))
            {
                continue;
            }

            var avoidedAssignmentIndex = FindAssignmentIndex(
                assignments,
                avoidedResourceId,
                avoidedShiftId);

            if (avoidedAssignmentIndex < 0)
            {
                continue;
            }

            var options = CreateChainReassignOptions(
                problem,
                assignments,
                shiftsById,
                avoidedResourceId,
                avoidedShift);

            Shuffle(options);

            foreach (var option in options)
            {
                if (!IsChainReassignLegal(
                        problem,
                        assignments,
                        shiftsById,
                        avoidedResourceId,
                        avoidedShift,
                        option))
                {
                    continue;
                }

                var mutatedAssignments = assignments.ToList();

                mutatedAssignments[avoidedAssignmentIndex] = new Assignment(
                    option.ReplacementResourceId,
                    avoidedShift.Id);

                mutatedAssignments[option.SecondAssignmentIndex] = new Assignment(
                    avoidedResourceId,
                    option.SecondShift.Id);

                candidate = new ScheduleCandidate(mutatedAssignments);
                return true;
            }
        }

        candidate = new ScheduleCandidate(assignments);
        return false;
    }

    private static List<ChainReassignOption> CreateChainReassignOptions(
        SchedulingProblem problem,
        IReadOnlyList<Assignment> assignments,
        IReadOnlyDictionary<Guid, Shift> shiftsById,
        Guid avoidedResourceId,
        Shift avoidedShift)
    {
        return assignments
            .Select((assignment, index) => new
            {
                Assignment = assignment,
                AssignmentIndex = index
            })
            .Where(item => item.Assignment.ResourceId != avoidedResourceId)
            .Where(item => shiftsById.ContainsKey(item.Assignment.ShiftId))
            .Select(item => new ChainReassignOption(
                ReplacementResourceId: item.Assignment.ResourceId,
                SecondAssignmentIndex: item.AssignmentIndex,
                SecondShift: shiftsById[item.Assignment.ShiftId]))
            .Where(option => option.SecondShift.Id != avoidedShift.Id)
            .ToList();
    }

    private static bool IsChainReassignLegal(
        SchedulingProblem problem,
        IReadOnlyList<Assignment> assignments,
        IReadOnlyDictionary<Guid, Shift> shiftsById,
        Guid avoidedResourceId,
        Shift avoidedShift,
        ChainReassignOption option)
    {
        var baseAssignments = assignments
            .Where(assignment =>
                !(assignment.ResourceId == avoidedResourceId &&
                  assignment.ShiftId == avoidedShift.Id))
            .Where(assignment =>
                !(assignment.ResourceId == option.ReplacementResourceId &&
                  assignment.ShiftId == option.SecondShift.Id))
            .ToList();

        var replacementResource = problem.Resources
            .FirstOrDefault(resource => resource.Id == option.ReplacementResourceId);

        var avoidedResource = problem.Resources
            .FirstOrDefault(resource => resource.Id == avoidedResourceId);

        if (replacementResource is null || avoidedResource is null)
        {
            return false;
        }

        if (!CanAssignResource(
                problem,
                replacementResource,
                avoidedShift,
                baseAssignments,
                shiftsById))
        {
            return false;
        }

        if (!CanAssignResource(
                problem,
                avoidedResource,
                option.SecondShift,
                baseAssignments,
                shiftsById))
        {
            return false;
        }

        return true;
    }

    private static int FindAssignmentIndex(
        IReadOnlyList<Assignment> assignments,
        Guid resourceId,
        Guid shiftId)
    {
        for (var i = 0; i < assignments.Count; i++)
        {
            if (assignments[i].ResourceId == resourceId &&
                assignments[i].ShiftId == shiftId)
            {
                return i;
            }
        }

        return -1;
    }

    private static bool HasIgnoredAvoidPreference(
        ScheduleEvaluationResult evaluation)
    {
        return evaluation.Violations.Any(
            violation => violation.Type == ConstraintViolationType.IgnoredAvoidPreference);
    }

    private static bool HasEffectiveTargetAboveTarget(
        ScheduleEvaluationResult evaluation)
    {
        return evaluation.Violations.Any(
            violation => violation.Type == ConstraintViolationType.ResourceEffectiveTargetAssignedHoursAboveTarget);
    }

    private static bool HasEffectiveTargetBelowTarget(
        SchedulingProblem problem,
        IReadOnlyList<Assignment> assignments,
        IReadOnlyDictionary<Guid, Shift> shiftsById)
    {
        return GetUnderTargetResourceIds(
                problem,
                assignments,
                shiftsById)
            .Count > 0;
    }

    private static IReadOnlyList<Resource> GetReplacementResources(
        SchedulingProblem problem,
        Assignment currentAssignment,
        Shift shift,
        IReadOnlyCollection<Assignment> existingAssignments,
        IReadOnlyDictionary<Guid, Shift> shiftsById)
    {
        return problem.Resources
            .Where(resource => resource.Id != currentAssignment.ResourceId)
            .Where(resource => CanAssignResource(
                problem,
                resource,
                shift,
                existingAssignments,
                shiftsById,
                currentAssignment))
            .ToList();
    }

    private static bool CanAssignResource(
        SchedulingProblem problem,
        Resource resource,
        Shift shift,
        IReadOnlyCollection<Assignment> existingAssignments,
        IReadOnlyDictionary<Guid, Shift> shiftsById,
        Assignment? ignoredAssignment = null)
    {
        if (IsAlreadyAssignedToShift(resource, shift, existingAssignments, ignoredAssignment))
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
                shiftsById,
                ignoredAssignment))
        {
            return false;
        }

        return true;
    }

    private static bool IsAlreadyAssignedToShift(
        Resource resource,
        Shift shift,
        IReadOnlyCollection<Assignment> existingAssignments,
        Assignment? ignoredAssignment)
    {
        return existingAssignments
            .Where(assignment => ignoredAssignment is null ||
                                 !IsSameAssignment(assignment, ignoredAssignment))
            .Any(assignment =>
                assignment.ResourceId == resource.Id &&
                assignment.ShiftId == shift.Id);
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
        IReadOnlyDictionary<Guid, Shift> shiftsById,
        Assignment? ignoredAssignment)
    {
        return existingAssignments
            .Where(assignment => ignoredAssignment is null ||
                                 !IsSameAssignment(assignment, ignoredAssignment))
            .Where(assignment => assignment.ResourceId == resource.Id)
            .Where(assignment => shiftsById.ContainsKey(assignment.ShiftId))
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

    private static bool IsSameAssignment(
        Assignment first,
        Assignment second)
    {
        return first.ResourceId == second.ResourceId &&
               first.ShiftId == second.ShiftId;
    }

    private void Shuffle<T>(IList<T> items)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var randomIndex = _random.Next(i + 1);

            (items[i], items[randomIndex]) = (items[randomIndex], items[i]);
        }
    }

    private sealed record MutationOption(
        Assignment Assignment,
        int AssignmentIndex);

    private sealed record MutationTarget(
        Assignment Assignment,
        int AssignmentIndex,
        Shift Shift,
        IReadOnlyList<Resource> ReplacementResources);

    private sealed record OverTargetRemovalOption(
        Assignment Assignment,
        int AssignmentIndex,
        Shift Shift);

    private sealed record ChainReassignOption(
        Guid ReplacementResourceId,
        int SecondAssignmentIndex,
        Shift SecondShift);
}
