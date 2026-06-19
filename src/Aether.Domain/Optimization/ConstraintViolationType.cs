namespace Aether.Domain.Optimization;

public enum ConstraintViolationType
{
    ResourceUnavailable = 1,
    ResourceAssignedToOverlappingShifts = 2,
    ShiftUnderstaffed = 3,
    UnbalancedAssignments = 4,
    BudgetExceeded = 5,
    IgnoredAvoidPreference = 6,
    ShiftOverstaffed = 7,
    AssignedWithoutRequiredPreference = 8,
    ShiftSequenceQuotaExceeded = 9,
    ResourceMinimumAssignedHoursNotMet = 10,
    ResourceWeeklyMinimumShiftMixNotMet = 11,
    ResourceMonthlyNightShiftQuotaExceeded = 12,
    ResourceMonthlyNightShiftPreferenceNotSatisfied = 13,
    ResourceAssignedHoursBalanceExceeded = 14,
    ResourceEffectiveTargetAssignedHoursBelowTarget = 15,
    ResourceEffectiveTargetAssignedHoursAboveTarget = 16,
    ResourceRequestedPreferredHoursNotSatisfied = 17,
    ResourceIgnoredAvoidPreferenceBurden = 18
}
