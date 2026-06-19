using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.Optimization;

public sealed class PostRunLocalAddImprovementOptimizer
{
    private const double HoursTolerance = 0.000001;

    public PostRunLocalAddImprovementResult Improve(
        SchedulingProblem problem,
        ScheduleCandidate candidate,
        ScheduleEvaluationResult evaluation,
        ScheduleScoringWeights scoringWeights)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(evaluation);
        ArgumentNullException.ThrowIfNull(scoringWeights);

        scoringWeights.Validate();

        var currentCandidate = candidate;
        var currentEvaluation = evaluation;
        var acceptedMoves = new List<AcceptedLocalAddMove>();

        while (true)
        {
            var bestMove = FindBestScoreImprovingAddMove(
                problem,
                currentCandidate,
                currentEvaluation,
                scoringWeights);

            if (bestMove is null)
            {
                break;
            }

            currentCandidate = bestMove.Candidate;
            currentEvaluation = bestMove.Evaluation;
            acceptedMoves.Add(bestMove.Move);
        }

        return new PostRunLocalAddImprovementResult(
            currentCandidate,
            currentEvaluation,
            AcceptedAddMoveCount: acceptedMoves.Count,
            IterationCount: acceptedMoves.Count,
            InitialTotalPenalty: evaluation.Score.TotalPenalty,
            FinalTotalPenalty: currentEvaluation.Score.TotalPenalty,
            AcceptedMoves: acceptedMoves.ToArray());
    }

    private static LocalAddImprovement? FindBestScoreImprovingAddMove(
        SchedulingProblem problem,
        ScheduleCandidate candidate,
        ScheduleEvaluationResult currentEvaluation,
        ScheduleScoringWeights scoringWeights)
    {
        var evaluator = new ScheduleEvaluator(scoringWeights);
        var ranker = new ScheduleEvaluationResultRanker();

        var shiftsById = problem.Shifts
            .ToDictionary(shift => shift.Id);

        var resourcesById = problem.Resources
            .ToDictionary(resource => resource.Id);

        var assignments = candidate.Assignments.ToArray();

        var assignedShiftIdsByResourceId = problem.Resources
            .ToDictionary(
                resource => resource.Id,
                _ => new HashSet<Guid>());

        var assignedHoursByResourceId = problem.Resources
            .ToDictionary(
                resource => resource.Id,
                _ => 0.0);

        foreach (var assignment in assignments)
        {
            var shift = shiftsById[assignment.ShiftId];

            assignedShiftIdsByResourceId[assignment.ResourceId].Add(assignment.ShiftId);
            assignedHoursByResourceId[assignment.ResourceId] += GetShiftHours(shift);
        }

        var assignmentCountByShiftId = assignments
            .GroupBy(assignment => assignment.ShiftId)
            .ToDictionary(
                group => group.Key,
                group => group.Count());

        var underTargetResourceIds = problem.ResourceWorkloadDemands
            .Where(demand =>
            {
                assignedHoursByResourceId.TryGetValue(
                    demand.ResourceId,
                    out var assignedHours);

                return assignedHours + HoursTolerance < demand.EffectiveTargetHours;
            })
            .Select(demand => demand.ResourceId)
            .Where(resourcesById.ContainsKey)
            .OrderBy(resourceId => resourcesById[resourceId].Name, StringComparer.Ordinal)
            .ToArray();

        LocalAddImprovement? bestMove = null;

        foreach (var targetResourceId in underTargetResourceIds)
        {
            foreach (var shift in problem.Shifts
                         .OrderBy(shift => shift.StartUtc)
                         .ThenBy(shift => shift.Id.ToString(), StringComparer.Ordinal))
            {
                assignmentCountByShiftId.TryGetValue(
                    shift.Id,
                    out var assignedResourceCount);

                if (assignedResourceCount >= shift.MaxResourceCount)
                {
                    continue;
                }

                if (assignedShiftIdsByResourceId[targetResourceId].Contains(shift.Id))
                {
                    continue;
                }

                if (!IsAvailableForShift(problem, targetResourceId, shift))
                {
                    continue;
                }

                if (shift.RequiresPreferenceToAssign &&
                    !HasPreferPreferenceForShift(problem, targetResourceId, shift))
                {
                    continue;
                }

                if (HasOverlappingAssignment(
                        assignments,
                        shiftsById,
                        targetResourceId,
                        shift))
                {
                    continue;
                }

                var addCandidate = CreateAddCandidate(
                    assignments,
                    targetResourceId,
                    shift.Id);

                var addEvaluation = evaluator.Evaluate(
                    problem,
                    addCandidate);

                if (addEvaluation.Score.HardViolationCount > 0)
                {
                    continue;
                }

                if (!ranker.IsBetterThan(addEvaluation, currentEvaluation))
                {
                    continue;
                }

                var move = CreateAcceptedMove(
                    problem,
                    resourcesById,
                    shiftsById,
                    assignments,
                    targetResourceId,
                    shift,
                    currentEvaluation,
                    addEvaluation);

                var improvement = new LocalAddImprovement(
                    addCandidate,
                    addEvaluation,
                    move);

                if (bestMove is null ||
                    IsBetterLocalImprovement(improvement, bestMove, ranker))
                {
                    bestMove = improvement;
                }
            }
        }

        return bestMove;
    }

    private static bool IsBetterLocalImprovement(
        LocalAddImprovement candidate,
        LocalAddImprovement currentBest,
        ScheduleEvaluationResultRanker ranker)
    {
        if (ranker.IsBetterThan(candidate.Evaluation, currentBest.Evaluation))
        {
            return true;
        }

        if (ranker.IsBetterThan(currentBest.Evaluation, candidate.Evaluation))
        {
            return false;
        }

        if (candidate.Move.PenaltyDelta != currentBest.Move.PenaltyDelta)
        {
            return candidate.Move.PenaltyDelta > currentBest.Move.PenaltyDelta;
        }

        var resourceNameComparison = string.Compare(
            candidate.Move.ResourceName,
            currentBest.Move.ResourceName,
            StringComparison.Ordinal);

        if (resourceNameComparison != 0)
        {
            return resourceNameComparison < 0;
        }

        if (candidate.Move.ShiftStartUtc != currentBest.Move.ShiftStartUtc)
        {
            return candidate.Move.ShiftStartUtc < currentBest.Move.ShiftStartUtc;
        }

        return string.Compare(
            candidate.Move.ShiftId.ToString(),
            currentBest.Move.ShiftId.ToString(),
            StringComparison.Ordinal) < 0;
    }

    private static AcceptedLocalAddMove CreateAcceptedMove(
        SchedulingProblem problem,
        IReadOnlyDictionary<Guid, Resource> resourcesById,
        IReadOnlyDictionary<Guid, Shift> shiftsById,
        IReadOnlyCollection<Assignment> assignments,
        Guid resourceId,
        Shift addedShift,
        ScheduleEvaluationResult previousEvaluation,
        ScheduleEvaluationResult newEvaluation)
    {
        var resource = resourcesById[resourceId];

        var demand = problem.ResourceWorkloadDemands
            .SingleOrDefault(item => item.ResourceId == resourceId);

        var previousAssignedHours = assignments
            .Where(assignment => assignment.ResourceId == resourceId)
            .Sum(assignment => GetShiftHours(shiftsById[assignment.ShiftId]));

        var addedShiftHours = GetShiftHours(addedShift);

        return new AcceptedLocalAddMove(
            ResourceId: resourceId,
            ResourceName: resource.Name,
            ShiftId: addedShift.Id,
            ShiftStartUtc: addedShift.StartUtc,
            ShiftEndUtc: addedShift.EndUtc,
            ShiftKind: addedShift.Kind,
            NightShiftCategory: addedShift.NightShiftCategory,
            PreviousTotalPenalty: previousEvaluation.Score.TotalPenalty,
            NewTotalPenalty: newEvaluation.Score.TotalPenalty,
            PenaltyDelta: previousEvaluation.Score.TotalPenalty - newEvaluation.Score.TotalPenalty,
            PreviousAssignedHours: previousAssignedHours,
            NewAssignedHours: previousAssignedHours + addedShiftHours,
            TargetHours: demand?.EffectiveTargetHours ?? 0.0);
    }

    private static ScheduleCandidate CreateAddCandidate(
        IReadOnlyCollection<Assignment> assignments,
        Guid resourceId,
        Guid shiftId)
    {
        return new ScheduleCandidate(
            assignments
                .Concat(
                [
                    new Assignment(resourceId, shiftId)
                ])
                .ToArray());
    }

    private static bool HasOverlappingAssignment(
        IReadOnlyCollection<Assignment> assignments,
        IReadOnlyDictionary<Guid, Shift> shiftsById,
        Guid resourceId,
        Shift targetShift)
    {
        return assignments
            .Where(assignment => assignment.ResourceId == resourceId)
            .Select(assignment => shiftsById[assignment.ShiftId])
            .Any(assignedShift => Overlaps(assignedShift, targetShift));
    }

    private static bool IsAvailableForShift(
        SchedulingProblem problem,
        Guid resourceId,
        Shift shift)
    {
        return problem.AvailabilityWindows
            .Where(window => window.ResourceId == resourceId)
            .Any(window => window.Covers(shift));
    }

    private static bool HasPreferPreferenceForShift(
        SchedulingProblem problem,
        Guid resourceId,
        Shift shift)
    {
        return problem.ResourcePreferences
            .Where(preference => preference.ResourceId == resourceId)
            .Where(preference => preference.Type == ResourcePreferenceType.Prefer)
            .Any(preference => Overlaps(shift, preference));
    }

    private static double GetShiftHours(Shift shift)
    {
        return (shift.EndUtc - shift.StartUtc).TotalHours;
    }

    private static bool Overlaps(
        Shift first,
        Shift second)
    {
        return first.StartUtc < second.EndUtc &&
               second.StartUtc < first.EndUtc;
    }

    private static bool Overlaps(
        Shift shift,
        ResourcePreference preference)
    {
        return preference.StartUtc < shift.EndUtc &&
               shift.StartUtc < preference.EndUtc;
    }

    private sealed record LocalAddImprovement(
        ScheduleCandidate Candidate,
        ScheduleEvaluationResult Evaluation,
        AcceptedLocalAddMove Move);
}
