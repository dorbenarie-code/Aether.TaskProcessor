using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.Diagnostics;

public sealed class CleanGaTargetGapExplainabilityAnalyzer
{
    private const double HoursTolerance = 0.000001;

    public TargetGapExplainabilityDiagnostic Analyze(
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

        var shiftsById = problem.Shifts
            .ToDictionary(shift => shift.Id);

        var workloadDemandByResourceId = problem.ResourceWorkloadDemands
            .ToDictionary(demand => demand.ResourceId);

        var assignedShiftIdsByResourceId = problem.Resources
            .ToDictionary(
                resource => resource.Id,
                _ => new HashSet<Guid>());

        var assignedHoursByResourceId = problem.Resources
            .ToDictionary(
                resource => resource.Id,
                _ => 0.0);

        foreach (var assignment in candidate.Assignments)
        {
            var shift = shiftsById[assignment.ShiftId];

            assignedShiftIdsByResourceId[assignment.ResourceId].Add(assignment.ShiftId);
            assignedHoursByResourceId[assignment.ResourceId] += GetShiftHours(shift);
        }

        var preferredShiftIdsByResourceId = problem.Resources
            .ToDictionary(
                resource => resource.Id,
                _ => new HashSet<Guid>());

        foreach (var preference in problem.ResourcePreferences
                     .Where(preference => preference.Type == ResourcePreferenceType.Prefer))
        {
            foreach (var shift in problem.Shifts.Where(shift => Overlaps(shift, preference)))
            {
                preferredShiftIdsByResourceId[preference.ResourceId].Add(shift.Id);
            }
        }

        var workerDiagnostics = problem.Resources
            .Select(resource =>
            {
                assignedHoursByResourceId.TryGetValue(
                    resource.Id,
                    out var assignedHours);

                workloadDemandByResourceId.TryGetValue(
                    resource.Id,
                    out var demand);

                preferredShiftIdsByResourceId.TryGetValue(
                    resource.Id,
                    out var preferredShiftIds);

                assignedShiftIdsByResourceId.TryGetValue(
                    resource.Id,
                    out var assignedShiftIds);

                var submittedPreferredHours = SumShiftHours(
                    shiftsById,
                    preferredShiftIds ?? []);

                var assignedPreferredHours = SumAssignedPreferredHours(
                    shiftsById,
                    preferredShiftIds ?? [],
                    assignedShiftIds ?? []);

                var targetHours = demand?.EffectiveTargetHours ?? 0.0;
                var gapToTarget = assignedHours - targetHours;

                return new WorkerTargetGapDiagnostic(
                    resource.Id,
                    resource.Name,
                    TargetHours: targetHours,
                    AssignedHours: assignedHours,
                    GapToTarget: gapToTarget,
                    SubmittedPreferredHours: submittedPreferredHours,
                    AssignedPreferredHours: assignedPreferredHours,
                    UnsatisfiedPreferredHours: submittedPreferredHours - assignedPreferredHours);
            })
            .OrderBy(diagnostic => diagnostic.ResourceName, StringComparer.Ordinal)
            .ToArray();

        var underTargetResourceIds = workerDiagnostics
            .Where(diagnostic => diagnostic.GapToTarget < -HoursTolerance)
            .Select(diagnostic => diagnostic.ResourceId)
            .ToArray();

        var addMoveDiagnostics = AnalyzeAddMoves(
            problem,
            candidate,
            evaluation,
            scoringWeights,
            shiftsById,
            assignedShiftIdsByResourceId,
            underTargetResourceIds);

        return new TargetGapExplainabilityDiagnostic(
            WorkerDiagnostics: workerDiagnostics,
            UnderTargetWorkerCount: underTargetResourceIds.Length,
            CandidateAddMoveCount: addMoveDiagnostics.CandidateAddMoveCount,
            ScoreImprovingAddMoveCount: addMoveDiagnostics.ScoreImprovingAddMoveCount,
            FairnessImprovingButScoreNotImprovingAddMoveCount: addMoveDiagnostics.FairnessImprovingButScoreNotImprovingAddMoveCount,
            CandidateTransferMoveCount: 0,
            ScoreImprovingTransferMoveCount: 0,
            FairnessImprovingButScoreNotImprovingTransferMoveCount: 0,
            RejectedShiftAtMaxCapacity: addMoveDiagnostics.RejectedShiftAtMaxCapacity,
            RejectedAlreadyAssignedToShift: addMoveDiagnostics.RejectedAlreadyAssignedToShift,
            RejectedUnavailable: addMoveDiagnostics.RejectedUnavailable,
            RejectedMissingRequiredPreference: addMoveDiagnostics.RejectedMissingRequiredPreference,
            RejectedOverlap: addMoveDiagnostics.RejectedOverlap,
            RejectedHardViolation: addMoveDiagnostics.RejectedHardViolation,
            RejectedHardViolationByType: addMoveDiagnostics.RejectedHardViolationByType,
            RejectedNotImproving: addMoveDiagnostics.RejectedNotImproving)
        {
            ScoreImprovingAddMoveDetails = addMoveDiagnostics.ScoreImprovingAddMoveDetails
        };
    }

    private static AddMoveDiagnostics AnalyzeAddMoves(
        SchedulingProblem problem,
        ScheduleCandidate candidate,
        ScheduleEvaluationResult baseEvaluation,
        ScheduleScoringWeights scoringWeights,
        IReadOnlyDictionary<Guid, Shift> shiftsById,
        IReadOnlyDictionary<Guid, HashSet<Guid>> assignedShiftIdsByResourceId,
        IReadOnlyCollection<Guid> underTargetResourceIds)
    {
        var evaluator = new ScheduleEvaluator(scoringWeights);
        var ranker = new ScheduleEvaluationResultRanker();

        var resourcesById = problem.Resources
            .ToDictionary(resource => resource.Id);

        var workloadDemandByResourceId = problem.ResourceWorkloadDemands
            .ToDictionary(demand => demand.ResourceId);

        var assignments = candidate.Assignments.ToArray();

        var assignmentCountByShiftId = assignments
            .GroupBy(assignment => assignment.ShiftId)
            .ToDictionary(
                group => group.Key,
                group => group.Count());

        var candidateAddMoveCount = 0;
        var scoreImprovingAddMoveCount = 0;
        var fairnessImprovingButScoreNotImprovingAddMoveCount = 0;
        var scoreImprovingAddMoveDetails = new List<ScoreImprovingAddMoveDetail>();
        var rejectedShiftAtMaxCapacity = 0;
        var rejectedAlreadyAssignedToShift = 0;
        var rejectedUnavailable = 0;
        var rejectedMissingRequiredPreference = 0;
        var rejectedOverlap = 0;
        var rejectedHardViolation = 0;
        var rejectedNotImproving = 0;
        var rejectedHardViolationByType = new Dictionary<ConstraintViolationType, int>();

        foreach (var targetResourceId in underTargetResourceIds)
        {
            foreach (var shift in problem.Shifts)
            {
                candidateAddMoveCount++;

                assignmentCountByShiftId.TryGetValue(
                    shift.Id,
                    out var assignedResourceCount);

                if (assignedResourceCount >= shift.MaxResourceCount)
                {
                    rejectedShiftAtMaxCapacity++;
                    continue;
                }

                if (assignedShiftIdsByResourceId[targetResourceId].Contains(shift.Id))
                {
                    rejectedAlreadyAssignedToShift++;
                    continue;
                }

                if (!IsAvailableForShift(problem, targetResourceId, shift))
                {
                    rejectedUnavailable++;
                    continue;
                }

                if (shift.RequiresPreferenceToAssign &&
                    !HasPreferPreferenceForShift(problem, targetResourceId, shift))
                {
                    rejectedMissingRequiredPreference++;
                    continue;
                }

                if (HasOverlappingAssignment(
                        assignments,
                        shiftsById,
                        targetResourceId,
                        shift))
                {
                    rejectedOverlap++;
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
                    rejectedHardViolation++;

                    foreach (var violation in addEvaluation.Violations
                                 .Where(violation => violation.Severity == ConstraintViolationSeverity.Hard))
                    {
                        rejectedHardViolationByType.TryGetValue(
                            violation.Type,
                            out var currentCount);

                        rejectedHardViolationByType[violation.Type] = currentCount + 1;
                    }

                    continue;
                }

                if (!ranker.IsBetterThan(addEvaluation, baseEvaluation))
                {
                    if (IsTargetGapFairnessImproving(
                            problem,
                            shiftsById,
                            assignments,
                            targetResourceId,
                            shift))
                    {
                        fairnessImprovingButScoreNotImprovingAddMoveCount++;
                        continue;
                    }

                    rejectedNotImproving++;
                    continue;
                }

                scoreImprovingAddMoveCount++;

                scoreImprovingAddMoveDetails.Add(CreateScoreImprovingAddMoveDetail(
                    resourcesById,
                    workloadDemandByResourceId,
                    shiftsById,
                    assignments,
                    targetResourceId,
                    shift,
                    baseEvaluation,
                    addEvaluation));
            }
        }

        return new AddMoveDiagnostics(
            CandidateAddMoveCount: candidateAddMoveCount,
            ScoreImprovingAddMoveCount: scoreImprovingAddMoveCount,
            FairnessImprovingButScoreNotImprovingAddMoveCount: fairnessImprovingButScoreNotImprovingAddMoveCount,
            ScoreImprovingAddMoveDetails: scoreImprovingAddMoveDetails
                .OrderByDescending(move => move.PenaltyDelta)
                .ThenBy(move => move.ResourceName, StringComparer.Ordinal)
                .ThenBy(move => move.ShiftStartUtc)
                .ToArray(),
            RejectedShiftAtMaxCapacity: rejectedShiftAtMaxCapacity,
            RejectedAlreadyAssignedToShift: rejectedAlreadyAssignedToShift,
            RejectedUnavailable: rejectedUnavailable,
            RejectedMissingRequiredPreference: rejectedMissingRequiredPreference,
            RejectedOverlap: rejectedOverlap,
            RejectedHardViolation: rejectedHardViolation,
            RejectedHardViolationByType: rejectedHardViolationByType
                .OrderBy(item => item.Key.ToString())
                .ToDictionary(
                    item => item.Key,
                    item => item.Value),
            RejectedNotImproving: rejectedNotImproving);
    }

    private static ScoreImprovingAddMoveDetail CreateScoreImprovingAddMoveDetail(
        IReadOnlyDictionary<Guid, Resource> resourcesById,
        IReadOnlyDictionary<Guid, ResourceWorkloadDemand> workloadDemandByResourceId,
        IReadOnlyDictionary<Guid, Shift> shiftsById,
        IReadOnlyCollection<Assignment> assignments,
        Guid resourceId,
        Shift addedShift,
        ScheduleEvaluationResult baseEvaluation,
        ScheduleEvaluationResult addEvaluation)
    {
        var resource = resourcesById[resourceId];

        workloadDemandByResourceId.TryGetValue(
            resourceId,
            out var demand);

        var baseAssignedHours = assignments
            .Where(assignment => assignment.ResourceId == resourceId)
            .Sum(assignment => GetShiftHours(shiftsById[assignment.ShiftId]));

        var addedShiftHours = GetShiftHours(addedShift);

        return new ScoreImprovingAddMoveDetail(
            ResourceId: resourceId,
            ResourceName: resource.Name,
            ShiftId: addedShift.Id,
            ShiftStartUtc: addedShift.StartUtc,
            ShiftEndUtc: addedShift.EndUtc,
            ShiftKind: addedShift.Kind,
            NightShiftCategory: addedShift.NightShiftCategory,
            BaseTotalPenalty: baseEvaluation.Score.TotalPenalty,
            NewTotalPenalty: addEvaluation.Score.TotalPenalty,
            PenaltyDelta: baseEvaluation.Score.TotalPenalty - addEvaluation.Score.TotalPenalty,
            BaseAssignedHours: baseAssignedHours,
            NewAssignedHours: baseAssignedHours + addedShiftHours,
            TargetHours: demand?.EffectiveTargetHours ?? 0.0);
    }

    private static bool IsTargetGapFairnessImproving(
        SchedulingProblem problem,
        IReadOnlyDictionary<Guid, Shift> shiftsById,
        IReadOnlyCollection<Assignment> assignments,
        Guid resourceId,
        Shift addedShift)
    {
        var demand = problem.ResourceWorkloadDemands
            .SingleOrDefault(item => item.ResourceId == resourceId);

        if (demand is null)
        {
            return false;
        }

        var assignedHours = assignments
            .Where(assignment => assignment.ResourceId == resourceId)
            .Sum(assignment => GetShiftHours(shiftsById[assignment.ShiftId]));

        var baseAbsoluteGap = Math.Abs(demand.EffectiveTargetHours - assignedHours);
        var addAbsoluteGap = Math.Abs(
            demand.EffectiveTargetHours -
            (assignedHours + GetShiftHours(addedShift)));

        return addAbsoluteGap + HoursTolerance < baseAbsoluteGap;
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

    private static double SumAssignedPreferredHours(
        IReadOnlyDictionary<Guid, Shift> shiftsById,
        IReadOnlySet<Guid> preferredShiftIds,
        IReadOnlySet<Guid> assignedShiftIds)
    {
        return assignedShiftIds
            .Where(preferredShiftIds.Contains)
            .Sum(shiftId => GetShiftHours(shiftsById[shiftId]));
    }

    private static double SumShiftHours(
        IReadOnlyDictionary<Guid, Shift> shiftsById,
        IReadOnlySet<Guid> shiftIds)
    {
        return shiftIds.Sum(shiftId => GetShiftHours(shiftsById[shiftId]));
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

    private sealed record AddMoveDiagnostics(
        int CandidateAddMoveCount,
        int ScoreImprovingAddMoveCount,
        int FairnessImprovingButScoreNotImprovingAddMoveCount,
        IReadOnlyList<ScoreImprovingAddMoveDetail> ScoreImprovingAddMoveDetails,
        int RejectedShiftAtMaxCapacity,
        int RejectedAlreadyAssignedToShift,
        int RejectedUnavailable,
        int RejectedMissingRequiredPreference,
        int RejectedOverlap,
        int RejectedHardViolation,
        IReadOnlyDictionary<ConstraintViolationType, int> RejectedHardViolationByType,
        int RejectedNotImproving);
}
