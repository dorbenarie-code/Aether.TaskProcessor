namespace Aether.Domain.Optimization;

public sealed record ScheduleScoringWeights
{
    public int ResourceUnavailablePenalty { get; init; } = 100000;
    public int ResourceAssignedToOverlappingShiftsPenalty { get; init; } = 100000;
    public int ShiftUnderstaffedPenalty { get; init; } = 50000;
    public int ShiftOverstaffedPenalty { get; init; } = 50000;
    public int AssignedWithoutRequiredPreferencePenalty { get; init; } = 50000;
    public int ShiftSequenceQuotaExceededPenalty { get; init; } = 30000;
    public int ResourceMonthlyNightShiftQuotaExceededPenalty { get; init; } = 30000;
    public int ResourceMonthlyNightShiftPreferenceNotSatisfiedPenalty { get; init; } = 500;
    public int ResourceMinimumAssignedHoursNotMetPenalty { get; init; } = 50000;
    public int ResourceWeeklyMinimumShiftMixNotMetPenalty { get; init; } = 1000;
    public int ResourceAssignedHoursBalanceExceededPenalty { get; init; } = 400;
    public int ResourceAssignedHoursBalanceExceededPenaltyPerExcessHour { get; init; } = 100;
    public int ResourceEffectiveTargetAssignedHoursBelowTargetPenaltyPerHour { get; init; } = 10;
    public int ResourceEffectiveTargetAssignedHoursAboveTargetPenaltyPerHour { get; init; } = 20;
    public int ResourceRequestedPreferredHoursNotSatisfiedPenaltyPerHour { get; init; } = 15;
    public int UnbalancedAssignmentsPenalty { get; init; } = 200;
    public int BudgetExceededPenalty { get; init; } = 300;
    public int IgnoredAvoidPreferencePenalty { get; init; } = 300;
    public int ResourceIgnoredAvoidPreferenceBurdenPenaltyPerHour { get; init; } = 50;

    public static ScheduleScoringWeights CreateDefault()
    {
        return new ScheduleScoringWeights();
    }

    public void Validate()
    {
        EnsureNonNegative(ResourceUnavailablePenalty, nameof(ResourceUnavailablePenalty));
        EnsureNonNegative(ResourceAssignedToOverlappingShiftsPenalty, nameof(ResourceAssignedToOverlappingShiftsPenalty));
        EnsureNonNegative(ShiftUnderstaffedPenalty, nameof(ShiftUnderstaffedPenalty));
        EnsureNonNegative(ShiftOverstaffedPenalty, nameof(ShiftOverstaffedPenalty));
        EnsureNonNegative(AssignedWithoutRequiredPreferencePenalty, nameof(AssignedWithoutRequiredPreferencePenalty));
        EnsureNonNegative(ShiftSequenceQuotaExceededPenalty, nameof(ShiftSequenceQuotaExceededPenalty));
        EnsureNonNegative(ResourceMonthlyNightShiftQuotaExceededPenalty, nameof(ResourceMonthlyNightShiftQuotaExceededPenalty));
        EnsureNonNegative(ResourceMonthlyNightShiftPreferenceNotSatisfiedPenalty, nameof(ResourceMonthlyNightShiftPreferenceNotSatisfiedPenalty));
        EnsureNonNegative(ResourceMinimumAssignedHoursNotMetPenalty, nameof(ResourceMinimumAssignedHoursNotMetPenalty));
        EnsureNonNegative(ResourceWeeklyMinimumShiftMixNotMetPenalty, nameof(ResourceWeeklyMinimumShiftMixNotMetPenalty));
        EnsureNonNegative(ResourceAssignedHoursBalanceExceededPenalty, nameof(ResourceAssignedHoursBalanceExceededPenalty));
        EnsureNonNegative(ResourceAssignedHoursBalanceExceededPenaltyPerExcessHour, nameof(ResourceAssignedHoursBalanceExceededPenaltyPerExcessHour));
        EnsureNonNegative(ResourceEffectiveTargetAssignedHoursBelowTargetPenaltyPerHour, nameof(ResourceEffectiveTargetAssignedHoursBelowTargetPenaltyPerHour));
        EnsureNonNegative(ResourceEffectiveTargetAssignedHoursAboveTargetPenaltyPerHour, nameof(ResourceEffectiveTargetAssignedHoursAboveTargetPenaltyPerHour));
        EnsureNonNegative(ResourceRequestedPreferredHoursNotSatisfiedPenaltyPerHour, nameof(ResourceRequestedPreferredHoursNotSatisfiedPenaltyPerHour));
        EnsureNonNegative(UnbalancedAssignmentsPenalty, nameof(UnbalancedAssignmentsPenalty));
        EnsureNonNegative(BudgetExceededPenalty, nameof(BudgetExceededPenalty));
        EnsureNonNegative(IgnoredAvoidPreferencePenalty, nameof(IgnoredAvoidPreferencePenalty));
        EnsureNonNegative(
            ResourceIgnoredAvoidPreferenceBurdenPenaltyPerHour,
            nameof(ResourceIgnoredAvoidPreferenceBurdenPenaltyPerHour));
    }

    private static void EnsureNonNegative(int value, string paramName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                "Schedule scoring weight cannot be negative.");
        }
    }
}
