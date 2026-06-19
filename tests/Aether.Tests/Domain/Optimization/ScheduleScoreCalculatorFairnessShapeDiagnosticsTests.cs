using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ScheduleScoreCalculatorFairnessShapeDiagnosticsTests
{
    [Fact]
    public void Calculate_below_target_linear_penalty_is_indifferent_to_gap_distribution()
    {
        var calculator = new ScheduleScoreCalculator();

        var distributedGapScore = calculator.Calculate(
        [
            CreateMagnitudeViolation(
                ConstraintViolationType.ResourceEffectiveTargetAssignedHoursBelowTarget,
                resourceId: Guid.NewGuid(),
                magnitude: 2),
            CreateMagnitudeViolation(
                ConstraintViolationType.ResourceEffectiveTargetAssignedHoursBelowTarget,
                resourceId: Guid.NewGuid(),
                magnitude: 2),
            CreateMagnitudeViolation(
                ConstraintViolationType.ResourceEffectiveTargetAssignedHoursBelowTarget,
                resourceId: Guid.NewGuid(),
                magnitude: 2),
            CreateMagnitudeViolation(
                ConstraintViolationType.ResourceEffectiveTargetAssignedHoursBelowTarget,
                resourceId: Guid.NewGuid(),
                magnitude: 2),
            CreateMagnitudeViolation(
                ConstraintViolationType.ResourceEffectiveTargetAssignedHoursBelowTarget,
                resourceId: Guid.NewGuid(),
                magnitude: 2)
        ]);

        var concentratedGapScore = calculator.Calculate(
        [
            CreateMagnitudeViolation(
                ConstraintViolationType.ResourceEffectiveTargetAssignedHoursBelowTarget,
                resourceId: Guid.NewGuid(),
                magnitude: 10)
        ]);

        Assert.Equal(100, distributedGapScore.TotalPenalty);
        Assert.Equal(100, concentratedGapScore.TotalPenalty);
        Assert.Equal(distributedGapScore.TotalPenalty, concentratedGapScore.TotalPenalty);
    }

    [Fact]
    public void Calculate_requested_preferred_penalty_is_indifferent_to_resource_concentration_when_shift_durations_are_equal()
    {
        var calculator = new ScheduleScoreCalculator();

        var concentratedResourceId = Guid.NewGuid();

        var oneResourceMissesFivePreferredShiftsScore = calculator.Calculate(
        [
            CreateRequestedPreferredViolation(concentratedResourceId, Guid.NewGuid(), magnitude: 8),
            CreateRequestedPreferredViolation(concentratedResourceId, Guid.NewGuid(), magnitude: 8),
            CreateRequestedPreferredViolation(concentratedResourceId, Guid.NewGuid(), magnitude: 8),
            CreateRequestedPreferredViolation(concentratedResourceId, Guid.NewGuid(), magnitude: 8),
            CreateRequestedPreferredViolation(concentratedResourceId, Guid.NewGuid(), magnitude: 8)
        ]);

        var fiveResourcesMissOnePreferredShiftEachScore = calculator.Calculate(
        [
            CreateRequestedPreferredViolation(Guid.NewGuid(), Guid.NewGuid(), magnitude: 8),
            CreateRequestedPreferredViolation(Guid.NewGuid(), Guid.NewGuid(), magnitude: 8),
            CreateRequestedPreferredViolation(Guid.NewGuid(), Guid.NewGuid(), magnitude: 8),
            CreateRequestedPreferredViolation(Guid.NewGuid(), Guid.NewGuid(), magnitude: 8),
            CreateRequestedPreferredViolation(Guid.NewGuid(), Guid.NewGuid(), magnitude: 8)
        ]);

        Assert.Equal(600, oneResourceMissesFivePreferredShiftsScore.TotalPenalty);
        Assert.Equal(600, fiveResourcesMissOnePreferredShiftEachScore.TotalPenalty);
        Assert.Equal(
            oneResourceMissesFivePreferredShiftsScore.TotalPenalty,
            fiveResourcesMissOnePreferredShiftEachScore.TotalPenalty);
    }

    [Fact]
    public void Calculate_balance_exceeded_penalty_can_remain_flat_when_excess_penalty_is_disabled()
    {
        var calculator = new ScheduleScoreCalculator(
            ScheduleScoringWeights.CreateDefault() with
            {
                ResourceAssignedHoursBalanceExceededPenaltyPerExcessHour = 0
            });

        var smallExcessScore = calculator.Calculate(
        [
            new ConstraintViolation(
                ConstraintViolationType.ResourceAssignedHoursBalanceExceeded,
                ConstraintViolationSeverity.Soft,
                "Resource assigned hours deviation from candidate average exceeds the allowed tolerance.",
                resourceId: Guid.NewGuid(),
                magnitude: 1)
        ]);

        var largeExcessScore = calculator.Calculate(
        [
            new ConstraintViolation(
                ConstraintViolationType.ResourceAssignedHoursBalanceExceeded,
                ConstraintViolationSeverity.Soft,
                "Resource assigned hours deviation from candidate average exceeds the allowed tolerance.",
                resourceId: Guid.NewGuid(),
                magnitude: 50)
        ]);

        Assert.Equal(400, smallExcessScore.TotalPenalty);
        Assert.Equal(400, largeExcessScore.TotalPenalty);
        Assert.Equal(smallExcessScore.TotalPenalty, largeExcessScore.TotalPenalty);
    }

    private static ConstraintViolation CreateMagnitudeViolation(
        ConstraintViolationType type,
        Guid resourceId,
        double magnitude)
    {
        return new ConstraintViolation(
            type,
            ConstraintViolationSeverity.Soft,
            "Magnitude-based diagnostic violation.",
            resourceId,
            magnitude: magnitude);
    }

    private static ConstraintViolation CreateRequestedPreferredViolation(
        Guid resourceId,
        Guid shiftId,
        double magnitude)
    {
        return new ConstraintViolation(
            ConstraintViolationType.ResourceRequestedPreferredHoursNotSatisfied,
            ConstraintViolationSeverity.Soft,
            "Resource requested a preferred shift but was not assigned to it.",
            resourceId,
            shiftId,
            magnitude);
    }
}
