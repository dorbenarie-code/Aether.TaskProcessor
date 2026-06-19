namespace Aether.Domain.Optimization;

public sealed class ScheduleConstraintEvaluator
{
    private const int MaxNightToAfternoonSequencesPerMonth = 2;
    private const int MaxAfternoonToMorningSequencesPerMonth = 2;
    private const int MaxTotalSequencesPerMonth = 4;
    private const int MaxMonthlyNightShiftsPerResourcePerCategory = 1;
    private const double HoursComparisonTolerance = 0.000001;

    public IReadOnlyCollection<ConstraintViolation> Evaluate(
        SchedulingProblem problem,
        ScheduleCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(candidate);

        ValidateCandidateReferences(problem, candidate);

        var violations = new List<ConstraintViolation>();

        AddResourceAvailabilityViolations(problem, candidate, violations);
        AddResourceOverlapViolations(problem, candidate, violations);
        AddResourceMinimumAssignedHoursViolations(problem, candidate, violations);
        AddResourceEffectiveTargetAssignedHoursGapViolations(problem, candidate, violations);
        AddResourceAssignedHoursBalanceViolations(problem, candidate, violations);
        AddResourceWeeklyMinimumShiftMixViolations(problem, candidate, violations);
        AddResourceMonthlyNightShiftQuotaViolations(problem, candidate, violations);
        AddResourceMonthlyNightShiftPreferenceViolations(problem, candidate, violations);
        AddResourceRequestedPreferredHoursNotSatisfiedViolations(problem, candidate, violations);
        AddUnderstaffedShiftViolations(problem, candidate, violations);
        AddOverstaffedShiftViolations(problem, candidate, violations);
        AddAssignedWithoutRequiredPreferenceViolations(problem, candidate, violations);
        AddIgnoredAvoidPreferenceViolations(problem, candidate, violations);
        AddResourceIgnoredAvoidPreferenceBurdenViolations(problem, candidate, violations);
        AddShiftSequenceQuotaViolations(problem, candidate, violations);

        return violations;
    }

    private static void ValidateCandidateReferences(
        SchedulingProblem problem,
        ScheduleCandidate candidate)
    {
        var resourceIds = problem.Resources
            .Select(resource => resource.Id)
            .ToHashSet();

        var shiftIds = problem.Shifts
            .Select(shift => shift.Id)
            .ToHashSet();

        var hasUnknownResource = candidate.Assignments
            .Any(assignment => !resourceIds.Contains(assignment.ResourceId));

        if (hasUnknownResource)
        {
            throw new ArgumentException(
                "Candidate contains an assignment for an unknown resource.",
                nameof(candidate));
        }

        var hasUnknownShift = candidate.Assignments
            .Any(assignment => !shiftIds.Contains(assignment.ShiftId));

        if (hasUnknownShift)
        {
            throw new ArgumentException(
                "Candidate contains an assignment for an unknown shift.",
                nameof(candidate));
        }
    }

    private static void AddResourceAvailabilityViolations(
        SchedulingProblem problem,
        ScheduleCandidate candidate,
        List<ConstraintViolation> violations)
    {
        var shiftsById = problem.Shifts
            .ToDictionary(shift => shift.Id);

        foreach (var assignment in candidate.Assignments)
        {
            var shift = shiftsById[assignment.ShiftId];

            var isAvailable = problem.AvailabilityWindows
                .Where(window => window.ResourceId == assignment.ResourceId)
                .Any(window => window.Covers(shift));

            if (isAvailable)
            {
                continue;
            }

            violations.Add(new ConstraintViolation(
                ConstraintViolationType.ResourceUnavailable,
                ConstraintViolationSeverity.Hard,
                "Resource is unavailable for the assigned shift.",
                assignment.ResourceId,
                assignment.ShiftId));
        }
    }

    private static void AddResourceOverlapViolations(
        SchedulingProblem problem,
        ScheduleCandidate candidate,
        List<ConstraintViolation> violations)
    {
        var shiftsById = problem.Shifts
            .ToDictionary(shift => shift.Id);

        var assignmentsByResource = candidate.Assignments
            .GroupBy(assignment => assignment.ResourceId);

        foreach (var resourceAssignments in assignmentsByResource)
        {
            var assignedShifts = resourceAssignments
                .Select(assignment => shiftsById[assignment.ShiftId])
                .OrderBy(shift => shift.StartUtc)
                .ToArray();

            for (var i = 0; i < assignedShifts.Length; i++)
            {
                for (var j = i + 1; j < assignedShifts.Length; j++)
                {
                    if (!Overlaps(assignedShifts[i], assignedShifts[j]))
                    {
                        continue;
                    }

                    violations.Add(new ConstraintViolation(
                        ConstraintViolationType.ResourceAssignedToOverlappingShifts,
                        ConstraintViolationSeverity.Hard,
                        "Resource is assigned to overlapping shifts.",
                        resourceAssignments.Key));

                    break;
                }
            }
        }
    }

    private static void AddResourceMinimumAssignedHoursViolations(
        SchedulingProblem problem,
        ScheduleCandidate candidate,
        List<ConstraintViolation> violations)
    {
        var workloadDemandByResourceId = problem.ResourceWorkloadDemands
            .ToDictionary(demand => demand.ResourceId);

        if (problem.MinimumAssignedHoursPerResource <= 0
            && workloadDemandByResourceId.Count == 0)
        {
            return;
        }

        var assignedHoursByResourceId = GetAssignedHoursByResourceId(
            problem,
            candidate);

        foreach (var resource in problem.Resources)
        {
            var minimumRequiredHours = GetMinimumRequiredAssignedHours(
                problem,
                workloadDemandByResourceId,
                resource.Id);

            if (minimumRequiredHours <= 0)
            {
                continue;
            }

            assignedHoursByResourceId.TryGetValue(
                resource.Id,
                out var assignedHours);

            if (assignedHours >= minimumRequiredHours)
            {
                continue;
            }

            violations.Add(new ConstraintViolation(
                ConstraintViolationType.ResourceMinimumAssignedHoursNotMet,
                ConstraintViolationSeverity.Hard,
                $"Resource is assigned {assignedHours:0.##} hours, below the minimum of {minimumRequiredHours:0.##} hours for the schedule period.",
                resource.Id));
        }
    }

    private static double GetMinimumRequiredAssignedHours(
        SchedulingProblem problem,
        IReadOnlyDictionary<Guid, ResourceWorkloadDemand> workloadDemandByResourceId,
        Guid resourceId)
    {
        if (workloadDemandByResourceId.TryGetValue(resourceId, out var demand))
        {
            return demand.EffectiveMinimumRequiredHours;
        }

        return problem.MinimumAssignedHoursPerResource;
    }


    private static void AddResourceEffectiveTargetAssignedHoursGapViolations(
        SchedulingProblem problem,
        ScheduleCandidate candidate,
        List<ConstraintViolation> violations)
    {
        if (problem.ResourceWorkloadDemands.Count == 0)
        {
            return;
        }

        var assignedHoursByResourceId = GetAssignedHoursByResourceId(
            problem,
            candidate);

        foreach (var demand in problem.ResourceWorkloadDemands)
        {
            assignedHoursByResourceId.TryGetValue(
                demand.ResourceId,
                out var assignedHours);

            if (assignedHours + HoursComparisonTolerance < demand.EffectiveMinimumRequiredHours)
            {
                continue;
            }

            if (assignedHours + HoursComparisonTolerance < demand.EffectiveTargetHours)
            {
                var gapHours = demand.EffectiveTargetHours - assignedHours;

                violations.Add(new ConstraintViolation(
                    ConstraintViolationType.ResourceEffectiveTargetAssignedHoursBelowTarget,
                    ConstraintViolationSeverity.Soft,
                    $"Resource is assigned {assignedHours:0.##} hours, below the effective target of {demand.EffectiveTargetHours:0.##} hours for the schedule period.",
                    demand.ResourceId,
                    magnitude: gapHours));

                continue;
            }

            if (assignedHours > demand.EffectiveTargetHours + HoursComparisonTolerance)
            {
                var gapHours = assignedHours - demand.EffectiveTargetHours;

                violations.Add(new ConstraintViolation(
                    ConstraintViolationType.ResourceEffectiveTargetAssignedHoursAboveTarget,
                    ConstraintViolationSeverity.Soft,
                    $"Resource is assigned {assignedHours:0.##} hours, above the effective target of {demand.EffectiveTargetHours:0.##} hours for the schedule period.",
                    demand.ResourceId,
                    magnitude: gapHours));
            }
        }
    }

    private static IReadOnlyDictionary<Guid, double> GetAssignedHoursByResourceId(
        SchedulingProblem problem,
        ScheduleCandidate candidate)
    {
        var shiftsById = problem.Shifts
            .ToDictionary(shift => shift.Id);

        return candidate.Assignments
            .GroupBy(assignment => assignment.ResourceId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(assignment =>
                {
                    var shift = shiftsById[assignment.ShiftId];

                    return (shift.EndUtc - shift.StartUtc).TotalHours;
                }));
    }

    private static void AddResourceAssignedHoursBalanceViolations(
        SchedulingProblem problem,
        ScheduleCandidate candidate,
        List<ConstraintViolation> violations)
    {
        if (problem.MaximumAssignedHoursDeviationFromAverageHours is null)
        {
            return;
        }

        var shiftsById = problem.Shifts
            .ToDictionary(shift => shift.Id);

        var assignedMinutesByResourceId = candidate.Assignments
            .GroupBy(assignment => assignment.ResourceId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(assignment =>
                {
                    var shift = shiftsById[assignment.ShiftId];

                    return (shift.EndUtc - shift.StartUtc).TotalMinutes;
                }));

        var totalAssignedMinutes = assignedMinutesByResourceId
            .Values
            .Sum();

        var averageAssignedMinutes = totalAssignedMinutes / problem.Resources.Count;
        var toleranceMinutes = problem.MaximumAssignedHoursDeviationFromAverageHours.Value * 60;

        foreach (var resource in problem.Resources)
        {
            assignedMinutesByResourceId.TryGetValue(
                resource.Id,
                out var assignedMinutes);

            var deviationMinutes = Math.Abs(assignedMinutes - averageAssignedMinutes);

            if (deviationMinutes <= toleranceMinutes)
            {
                continue;
            }

            var excessDeviationHours = (deviationMinutes - toleranceMinutes) / 60;

            violations.Add(new ConstraintViolation(
                ConstraintViolationType.ResourceAssignedHoursBalanceExceeded,
                ConstraintViolationSeverity.Soft,
                $"Resource assigned hours deviation from candidate average exceeds the allowed tolerance of {problem.MaximumAssignedHoursDeviationFromAverageHours.Value:0.##} hours.",
                resource.Id,
                magnitude: excessDeviationHours));
        }
    }

    private static void AddResourceWeeklyMinimumShiftMixViolations(
        SchedulingProblem problem,
        ScheduleCandidate candidate,
        List<ConstraintViolation> violations)
    {
        if (problem.MinimumMorningShiftsPerResourcePerFullWeek <= 0
            && problem.MinimumAfternoonShiftsPerResourcePerFullWeek <= 0)
        {
            return;
        }

        var shiftsById = problem.Shifts
            .ToDictionary(shift => shift.Id);

        var assignedShiftsByResourceId = candidate.Assignments
            .GroupBy(assignment => assignment.ResourceId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(assignment => shiftsById[assignment.ShiftId])
                    .ToArray());

        for (var weekStartUtc = problem.Period.StartUtc;
             weekStartUtc.AddDays(7) <= problem.Period.EndUtc;
             weekStartUtc = weekStartUtc.AddDays(7))
        {
            var weekEndUtc = weekStartUtc.AddDays(7);

            foreach (var resource in problem.Resources)
            {
                assignedShiftsByResourceId.TryGetValue(
                    resource.Id,
                    out var assignedShifts);

                var weeklyShifts = (assignedShifts ?? [])
                    .Where(shift => shift.StartUtc >= weekStartUtc)
                    .Where(shift => shift.StartUtc < weekEndUtc)
                    .ToArray();

                var morningShiftCount = weeklyShifts
                    .Count(shift => shift.Kind == ShiftKind.Morning);

                var afternoonShiftCount = weeklyShifts
                    .Count(shift => shift.Kind == ShiftKind.Afternoon);

                if (morningShiftCount >= problem.MinimumMorningShiftsPerResourcePerFullWeek
                    && afternoonShiftCount >= problem.MinimumAfternoonShiftsPerResourcePerFullWeek)
                {
                    continue;
                }

                violations.Add(new ConstraintViolation(
                    ConstraintViolationType.ResourceWeeklyMinimumShiftMixNotMet,
                    ConstraintViolationSeverity.Soft,
                    $"Resource weekly shift mix is below minimum for the full week starting {weekStartUtc:yyyy-MM-dd}.",
                    resource.Id));
            }
        }
    }

    private static void AddResourceMonthlyNightShiftQuotaViolations(
        SchedulingProblem problem,
        ScheduleCandidate candidate,
        List<ConstraintViolation> violations)
    {
        var shiftsById = problem.Shifts
            .ToDictionary(shift => shift.Id);

        var countsByResourceMonthCategory = new Dictionary<(
            Guid ResourceId,
            int Year,
            int Month,
            NightShiftCategory Category), int>();

        foreach (var history in problem.ResourceMonthlyNightShiftHistories)
        {
            if (!HasMonthlyNightShiftQuota(history.NightShiftCategory))
            {
                continue;
            }

            var key = (
                history.ResourceId,
                history.Year,
                history.Month,
                history.NightShiftCategory);

            countsByResourceMonthCategory.TryGetValue(key, out var currentCount);
            countsByResourceMonthCategory[key] = currentCount + history.AssignedCount;
        }

        foreach (var assignment in candidate.Assignments)
        {
            var shift = shiftsById[assignment.ShiftId];

            if (shift.NightShiftCategory is not { } category)
            {
                continue;
            }

            if (!HasMonthlyNightShiftQuota(category))
            {
                continue;
            }

            var key = (
                ResourceId: assignment.ResourceId,
                Year: shift.StartUtc.Year,
                Month: shift.StartUtc.Month,
                Category: category);

            countsByResourceMonthCategory.TryGetValue(key, out var currentCount);
            countsByResourceMonthCategory[key] = currentCount + 1;
        }

        foreach (var entry in countsByResourceMonthCategory)
        {
            if (entry.Value <= MaxMonthlyNightShiftsPerResourcePerCategory)
            {
                continue;
            }

            violations.Add(new ConstraintViolation(
                ConstraintViolationType.ResourceMonthlyNightShiftQuotaExceeded,
                ConstraintViolationSeverity.Hard,
                $"Resource exceeded the monthly {FormatNightShiftCategory(entry.Key.Category)} night quota.",
                entry.Key.ResourceId));
        }
    }

    private static bool HasMonthlyNightShiftQuota(NightShiftCategory category)
    {
        return category is NightShiftCategory.Regular or NightShiftCategory.MotzeiShabbatNight;
    }

    private static string FormatNightShiftCategory(NightShiftCategory category)
    {
        return category switch
        {
            NightShiftCategory.Regular => "Regular",
            NightShiftCategory.MotzeiShabbatNight => "Motzei Shabbat",
            _ => throw new ArgumentOutOfRangeException(
                nameof(category),
                "Night shift category does not have a monthly quota.")
        };
    }

    private static void AddResourceMonthlyNightShiftPreferenceViolations(
        SchedulingProblem problem,
        ScheduleCandidate candidate,
        List<ConstraintViolation> violations)
    {
        var motzeiShabbatShifts = problem.Shifts
            .Where(shift => shift.NightShiftCategory == NightShiftCategory.MotzeiShabbatNight)
            .ToArray();

        if (motzeiShabbatShifts.Length == 0)
        {
            return;
        }

        var requestedKeys = new HashSet<(Guid ResourceId, int Year, int Month)>();

        foreach (var preference in problem.ResourcePreferences
                     .Where(preference => preference.Type == ResourcePreferenceType.Prefer))
        {
            foreach (var shift in motzeiShabbatShifts.Where(shift => Overlaps(shift, preference)))
            {
                requestedKeys.Add((
                    preference.ResourceId,
                    shift.StartUtc.Year,
                    shift.StartUtc.Month));
            }
        }

        if (requestedKeys.Count == 0)
        {
            return;
        }

        var satisfiedKeys = new HashSet<(Guid ResourceId, int Year, int Month)>();

        foreach (var history in problem.ResourceMonthlyNightShiftHistories
                     .Where(history => history.NightShiftCategory == NightShiftCategory.MotzeiShabbatNight)
                     .Where(history => history.AssignedCount > 0))
        {
            satisfiedKeys.Add((
                history.ResourceId,
                history.Year,
                history.Month));
        }

        var shiftsById = problem.Shifts
            .ToDictionary(shift => shift.Id);

        foreach (var assignment in candidate.Assignments)
        {
            var shift = shiftsById[assignment.ShiftId];

            if (shift.NightShiftCategory != NightShiftCategory.MotzeiShabbatNight)
            {
                continue;
            }

            satisfiedKeys.Add((
                assignment.ResourceId,
                shift.StartUtc.Year,
                shift.StartUtc.Month));
        }

        foreach (var requestedKey in requestedKeys)
        {
            if (satisfiedKeys.Contains(requestedKey))
            {
                continue;
            }

            violations.Add(new ConstraintViolation(
                ConstraintViolationType.ResourceMonthlyNightShiftPreferenceNotSatisfied,
                ConstraintViolationSeverity.Soft,
                "Resource requested a Motzei Shabbat night shift but did not receive one in the month.",
                requestedKey.ResourceId));
        }
    }


    private static void AddResourceRequestedPreferredHoursNotSatisfiedViolations(
        SchedulingProblem problem,
        ScheduleCandidate candidate,
        List<ConstraintViolation> violations)
    {
        var assignedShiftIdsByResourceId = candidate.Assignments
            .GroupBy(assignment => assignment.ResourceId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(assignment => assignment.ShiftId)
                    .ToHashSet());

        var requestedPreferredShiftKeys = new HashSet<(Guid ResourceId, Guid ShiftId)>();

        foreach (var preference in problem.ResourcePreferences
                     .Where(preference => preference.Type == ResourcePreferenceType.Prefer))
        {
            foreach (var shift in problem.Shifts.Where(shift => Overlaps(shift, preference)))
            {
                var key = (preference.ResourceId, shift.Id);

                if (!requestedPreferredShiftKeys.Add(key))
                {
                    continue;
                }

                if (assignedShiftIdsByResourceId.TryGetValue(
                        preference.ResourceId,
                        out var assignedShiftIds) &&
                    assignedShiftIds.Contains(shift.Id))
                {
                    continue;
                }

                var requestedPreferredHours = GetShiftDurationHours(shift);

                violations.Add(new ConstraintViolation(
                    ConstraintViolationType.ResourceRequestedPreferredHoursNotSatisfied,
                    ConstraintViolationSeverity.Soft,
                    "Resource requested a preferred shift but was not assigned to it.",
                    preference.ResourceId,
                    shift.Id,
                    magnitude: requestedPreferredHours));
            }
        }
    }

    private static double GetShiftDurationHours(Shift shift)
    {
        return (shift.EndUtc - shift.StartUtc).TotalHours;
    }

    private static void AddUnderstaffedShiftViolations(
        SchedulingProblem problem,
        ScheduleCandidate candidate,
        List<ConstraintViolation> violations)
    {
        var assignmentCountByShiftId = candidate.Assignments
            .GroupBy(assignment => assignment.ShiftId)
            .ToDictionary(
                group => group.Key,
                group => group.Count());

        foreach (var shift in problem.Shifts)
        {
            assignmentCountByShiftId.TryGetValue(
                shift.Id,
                out var assignedResourceCount);

            var effectiveMinResourceCount = GetEffectiveMinResourceCount(problem, shift);

            if (assignedResourceCount >= effectiveMinResourceCount)
            {
                continue;
            }

            violations.Add(new ConstraintViolation(
                ConstraintViolationType.ShiftUnderstaffed,
                ConstraintViolationSeverity.Hard,
                "Shift does not have enough assigned resources.",
                shiftId: shift.Id));
        }
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
            .Any(preference => Overlaps(shift, preference));

        if (!hasPreferPreference)
        {
            return shift.MinResourceCount;
        }

        return Math.Max(shift.MinResourceCount, 1);
    }

    private static void AddOverstaffedShiftViolations(
        SchedulingProblem problem,
        ScheduleCandidate candidate,
        List<ConstraintViolation> violations)
    {
        var assignmentCountByShiftId = candidate.Assignments
            .GroupBy(assignment => assignment.ShiftId)
            .ToDictionary(
                group => group.Key,
                group => group.Count());

        foreach (var shift in problem.Shifts)
        {
            assignmentCountByShiftId.TryGetValue(
                shift.Id,
                out var assignedResourceCount);

            if (assignedResourceCount <= shift.MaxResourceCount)
            {
                continue;
            }

            violations.Add(new ConstraintViolation(
                ConstraintViolationType.ShiftOverstaffed,
                ConstraintViolationSeverity.Hard,
                "Shift has more assigned resources than allowed.",
                shiftId: shift.Id));
        }
    }

    private static void AddAssignedWithoutRequiredPreferenceViolations(
        SchedulingProblem problem,
        ScheduleCandidate candidate,
        List<ConstraintViolation> violations)
    {
        var shiftsById = problem.Shifts
            .ToDictionary(shift => shift.Id);

        foreach (var assignment in candidate.Assignments)
        {
            var shift = shiftsById[assignment.ShiftId];

            if (!shift.RequiresPreferenceToAssign)
            {
                continue;
            }

            var hasRequiredPreference = problem.ResourcePreferences
                .Where(preference => preference.ResourceId == assignment.ResourceId)
                .Where(preference => preference.Type == ResourcePreferenceType.Prefer)
                .Any(preference => Overlaps(shift, preference));

            if (hasRequiredPreference)
            {
                continue;
            }

            violations.Add(new ConstraintViolation(
                ConstraintViolationType.AssignedWithoutRequiredPreference,
                ConstraintViolationSeverity.Hard,
                "Resource is assigned to a shift that requires a matching prefer preference.",
                assignment.ResourceId,
                assignment.ShiftId));
        }
    }

    private static void AddIgnoredAvoidPreferenceViolations(
        SchedulingProblem problem,
        ScheduleCandidate candidate,
        List<ConstraintViolation> violations)
    {
        var shiftsById = problem.Shifts
            .ToDictionary(shift => shift.Id);

        foreach (var assignment in candidate.Assignments)
        {
            var shift = shiftsById[assignment.ShiftId];

            var hasIgnoredAvoidPreference = problem.ResourcePreferences
                .Where(preference => preference.ResourceId == assignment.ResourceId)
                .Where(preference => preference.Type == ResourcePreferenceType.Avoid)
                .Any(preference => Overlaps(shift, preference));

            if (!hasIgnoredAvoidPreference)
            {
                continue;
            }

            violations.Add(new ConstraintViolation(
                ConstraintViolationType.IgnoredAvoidPreference,
                ConstraintViolationSeverity.Soft,
                "Resource is assigned to a shift that overlaps an avoid preference.",
                assignment.ResourceId,
                assignment.ShiftId));
        }
    }


    private static void AddResourceIgnoredAvoidPreferenceBurdenViolations(
        SchedulingProblem problem,
        ScheduleCandidate candidate,
        List<ConstraintViolation> violations)
    {
        var shiftsById = problem.Shifts
            .ToDictionary(shift => shift.Id);

        var assignedHoursByResourceId = new Dictionary<Guid, double>();
        var ignoredAvoidHoursByResourceId = new Dictionary<Guid, double>();

        foreach (var assignment in candidate.Assignments)
        {
            var shift = shiftsById[assignment.ShiftId];
            var shiftHours = (shift.EndUtc - shift.StartUtc).TotalHours;

            assignedHoursByResourceId.TryGetValue(
                assignment.ResourceId,
                out var currentAssignedHours);

            assignedHoursByResourceId[assignment.ResourceId] =
                currentAssignedHours + shiftHours;

            var hasIgnoredAvoidPreference = problem.ResourcePreferences
                .Where(preference => preference.ResourceId == assignment.ResourceId)
                .Where(preference => preference.Type == ResourcePreferenceType.Avoid)
                .Any(preference => Overlaps(shift, preference));

            if (!hasIgnoredAvoidPreference)
            {
                continue;
            }

            ignoredAvoidHoursByResourceId.TryGetValue(
                assignment.ResourceId,
                out var currentIgnoredAvoidHours);

            ignoredAvoidHoursByResourceId[assignment.ResourceId] =
                currentIgnoredAvoidHours + shiftHours;
        }

        foreach (var resource in problem.Resources)
        {
            ignoredAvoidHoursByResourceId.TryGetValue(
                resource.Id,
                out var ignoredAvoidHours);

            if (ignoredAvoidHours <= 0)
            {
                continue;
            }

            assignedHoursByResourceId.TryGetValue(
                resource.Id,
                out var assignedHours);

            if (assignedHours <= 0)
            {
                continue;
            }

            var magnitude = ignoredAvoidHours * ignoredAvoidHours / assignedHours;

            violations.Add(new ConstraintViolation(
                ConstraintViolationType.ResourceIgnoredAvoidPreferenceBurden,
                ConstraintViolationSeverity.Soft,
                "Resource has concentrated ignored avoid preference burden.",
                resource.Id,
                magnitude: magnitude));
        }
    }


    private static void AddShiftSequenceQuotaViolations(
        SchedulingProblem problem,
        ScheduleCandidate candidate,
        List<ConstraintViolation> violations)
    {
        var shiftsById = problem.Shifts
            .ToDictionary(shift => shift.Id);

        var classifier = new ShiftSequenceClassifier();

        var sequenceCountsByResourceMonth = new Dictionary<
            (Guid ResourceId, int Year, int Month),
            ShiftSequenceQuotaCounts>();

        var assignmentsByResource = candidate.Assignments
            .GroupBy(assignment => assignment.ResourceId);

        foreach (var resourceAssignments in assignmentsByResource)
        {
            var assignedShifts = resourceAssignments
                .Select(assignment => shiftsById[assignment.ShiftId])
                .OrderBy(shift => shift.StartUtc)
                .ToArray();

            for (var i = 1; i < assignedShifts.Length; i++)
            {
                var previousShift = assignedShifts[i - 1];
                var nextShift = assignedShifts[i];

                var sequenceType = classifier.Classify(previousShift, nextShift);

                if (sequenceType is null)
                {
                    continue;
                }

                var key = (
                    ResourceId: resourceAssignments.Key,
                    Year: nextShift.StartUtc.Year,
                    Month: nextShift.StartUtc.Month);

                if (!sequenceCountsByResourceMonth.TryGetValue(key, out var counts))
                {
                    counts = new ShiftSequenceQuotaCounts();
                    sequenceCountsByResourceMonth[key] = counts;
                }

                counts.TotalCount++;

                if (sequenceType == ShiftSequenceType.NightToAfternoon)
                {
                    counts.NightToAfternoonCount++;
                }

                if (sequenceType == ShiftSequenceType.AfternoonToMorning)
                {
                    counts.AfternoonToMorningCount++;
                }
            }
        }

        foreach (var entry in sequenceCountsByResourceMonth)
        {
            var key = entry.Key;
            var counts = entry.Value;

            if (!counts.ExceedsQuota())
            {
                continue;
            }

            violations.Add(new ConstraintViolation(
                ConstraintViolationType.ShiftSequenceQuotaExceeded,
                ConstraintViolationSeverity.Hard,
                "Resource exceeded the monthly shift sequence quota.",
                key.ResourceId));
        }
    }

    private static bool Overlaps(Shift first, Shift second)
    {
        return first.StartUtc < second.EndUtc &&
               second.StartUtc < first.EndUtc;
    }

    private static bool Overlaps(Shift shift, ResourcePreference preference)
    {
        return preference.StartUtc < shift.EndUtc &&
               shift.StartUtc < preference.EndUtc;
    }

    private sealed class ShiftSequenceQuotaCounts
    {
        public int NightToAfternoonCount { get; set; }
        public int AfternoonToMorningCount { get; set; }
        public int TotalCount { get; set; }

        public bool ExceedsQuota()
        {
            return NightToAfternoonCount > MaxNightToAfternoonSequencesPerMonth ||
                   AfternoonToMorningCount > MaxAfternoonToMorningSequencesPerMonth ||
                   TotalCount > MaxTotalSequencesPerMonth;
        }
    }

}
