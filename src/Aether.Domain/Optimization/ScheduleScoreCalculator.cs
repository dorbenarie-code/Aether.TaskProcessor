namespace Aether.Domain.Optimization;

public sealed class ScheduleScoreCalculator
{
    private const int MaxScore = ScheduleScore.MaximumValue;

    private readonly ScheduleScoringWeights _weights;

    public ScheduleScoreCalculator()
        : this(ScheduleScoringWeights.CreateDefault())
    {
    }

    public ScheduleScoreCalculator(ScheduleScoringWeights weights)
    {
        ArgumentNullException.ThrowIfNull(weights);

        weights.Validate();

        _weights = weights;
    }

    public ScheduleScore Calculate(IReadOnlyCollection<ConstraintViolation> violations)
    {
        ArgumentNullException.ThrowIfNull(violations);

        var hardViolationCount = violations.Count(
            violation => violation.Severity == ConstraintViolationSeverity.Hard);

        var softViolationCount = violations.Count(
            violation => violation.Severity == ConstraintViolationSeverity.Soft);

        var totalPenalty = violations.Sum(GetPenalty);

        var scoreValue = Math.Max(
            ScheduleScore.MinimumValue,
            MaxScore - totalPenalty);

        return new ScheduleScore(
            scoreValue,
            hardViolationCount,
            softViolationCount,
            totalPenalty);
    }

    private int GetPenalty(ConstraintViolation violation)
    {
        return violation.Type switch
        {
            ConstraintViolationType.ResourceUnavailable => _weights.ResourceUnavailablePenalty,
            ConstraintViolationType.ResourceAssignedToOverlappingShifts => _weights.ResourceAssignedToOverlappingShiftsPenalty,
            ConstraintViolationType.ShiftUnderstaffed => _weights.ShiftUnderstaffedPenalty,
            ConstraintViolationType.ShiftOverstaffed => _weights.ShiftOverstaffedPenalty,
            ConstraintViolationType.AssignedWithoutRequiredPreference => _weights.AssignedWithoutRequiredPreferencePenalty,
            ConstraintViolationType.ShiftSequenceQuotaExceeded => _weights.ShiftSequenceQuotaExceededPenalty,
            ConstraintViolationType.ResourceMonthlyNightShiftQuotaExceeded => _weights.ResourceMonthlyNightShiftQuotaExceededPenalty,
            ConstraintViolationType.ResourceMonthlyNightShiftPreferenceNotSatisfied => _weights.ResourceMonthlyNightShiftPreferenceNotSatisfiedPenalty,
            ConstraintViolationType.ResourceMinimumAssignedHoursNotMet => _weights.ResourceMinimumAssignedHoursNotMetPenalty,
            ConstraintViolationType.ResourceWeeklyMinimumShiftMixNotMet => _weights.ResourceWeeklyMinimumShiftMixNotMetPenalty,
            ConstraintViolationType.ResourceAssignedHoursBalanceExceeded => CalculateAssignedHoursBalancePenalty(violation),
            ConstraintViolationType.ResourceEffectiveTargetAssignedHoursBelowTarget => CalculateMagnitudePenalty(
                violation,
                _weights.ResourceEffectiveTargetAssignedHoursBelowTargetPenaltyPerHour),
            ConstraintViolationType.ResourceEffectiveTargetAssignedHoursAboveTarget => CalculateMagnitudePenalty(
                violation,
                _weights.ResourceEffectiveTargetAssignedHoursAboveTargetPenaltyPerHour),
            ConstraintViolationType.ResourceRequestedPreferredHoursNotSatisfied => CalculateMagnitudePenalty(
                violation,
                _weights.ResourceRequestedPreferredHoursNotSatisfiedPenaltyPerHour),
            ConstraintViolationType.UnbalancedAssignments => _weights.UnbalancedAssignmentsPenalty,
            ConstraintViolationType.BudgetExceeded => _weights.BudgetExceededPenalty,
            ConstraintViolationType.IgnoredAvoidPreference => _weights.IgnoredAvoidPreferencePenalty,
            ConstraintViolationType.ResourceIgnoredAvoidPreferenceBurden => CalculateIgnoredAvoidPreferenceBurdenPenalty(violation),
            _ => throw new ArgumentOutOfRangeException(
                nameof(violation.Type),
                "Constraint violation type is not supported.")
        };
    }


    private int CalculateAssignedHoursBalancePenalty(ConstraintViolation violation)
    {
        if (_weights.ResourceAssignedHoursBalanceExceededPenaltyPerExcessHour == 0)
        {
            return _weights.ResourceAssignedHoursBalanceExceededPenalty;
        }

        return _weights.ResourceAssignedHoursBalanceExceededPenalty +
               CalculateMagnitudePenalty(
                   violation,
                   _weights.ResourceAssignedHoursBalanceExceededPenaltyPerExcessHour);
    }

    private int CalculateIgnoredAvoidPreferenceBurdenPenalty(ConstraintViolation violation)
    {
        if (_weights.ResourceIgnoredAvoidPreferenceBurdenPenaltyPerHour == 0)
        {
            return 0;
        }

        return CalculateMagnitudePenalty(
            violation,
            _weights.ResourceIgnoredAvoidPreferenceBurdenPenaltyPerHour);
    }

    private static int CalculateMagnitudePenalty(
        ConstraintViolation violation,
        int penaltyPerMagnitudeUnit)
    {
        var magnitude = GetRequiredMagnitude(violation);

        return (int)Math.Ceiling(magnitude * penaltyPerMagnitudeUnit);
    }

    private static double GetRequiredMagnitude(ConstraintViolation violation)
    {
        if (violation.Magnitude is null)
        {
            throw new InvalidOperationException(
                $"Constraint violation magnitude is required for violation type {violation.Type}.");
        }

        if (!double.IsFinite(violation.Magnitude.Value))
        {
            throw new InvalidOperationException(
                $"Constraint violation magnitude must be finite for violation type {violation.Type}.");
        }

        if (violation.Magnitude.Value <= 0)
        {
            throw new InvalidOperationException(
                $"Constraint violation magnitude must be greater than zero for violation type {violation.Type}.");
        }

        return violation.Magnitude.Value;
    }
}
