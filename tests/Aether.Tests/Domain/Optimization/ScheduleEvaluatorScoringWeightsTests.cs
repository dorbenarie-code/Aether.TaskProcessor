using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ScheduleEvaluatorScoringWeightsTests
{
    [Fact]
    public void Evaluate_uses_custom_scoring_weights()
    {
        var problem = CreateOverTargetProblem();
        var resource = problem.Resources.Single();

        var candidate = new ScheduleCandidate(
            problem.Shifts
                .Select(shift => new Assignment(resource.Id, shift.Id))
                .ToArray());

        var defaultEvaluation = new ScheduleEvaluator()
            .Evaluate(problem, candidate);

        var customWeights = ScheduleScoringWeights.CreateDefault() with
        {
            ResourceEffectiveTargetAssignedHoursAboveTargetPenaltyPerHour = 5
        };

        var customEvaluation = new ScheduleEvaluator(customWeights)
            .Evaluate(problem, candidate);

        Assert.True(defaultEvaluation.IsFeasible);
        Assert.True(customEvaluation.IsFeasible);

        Assert.Equal(160, defaultEvaluation.Score.TotalPenalty);
        Assert.Equal(40, customEvaluation.Score.TotalPenalty);

        Assert.Contains(
            customEvaluation.Violations,
            violation =>
                violation.Type == ConstraintViolationType.ResourceEffectiveTargetAssignedHoursAboveTarget &&
                violation.Magnitude == 8);
    }

    private static SchedulingProblem CreateOverTargetProblem()
    {
        var resource = new Resource(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "Guard01",
            hourlyCost: 0);

        var startUtc = new DateTime(2026, 6, 1, 6, 0, 0, DateTimeKind.Utc);

        var firstShift = new Shift(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            startUtc,
            startUtc.AddHours(8),
            ShiftKind.Morning,
            minResourceCount: 0,
            maxResourceCount: 1);

        var secondShift = new Shift(
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            startUtc.AddHours(8),
            startUtc.AddHours(16),
            ShiftKind.Afternoon,
            minResourceCount: 0,
            maxResourceCount: 1);

        return new SchedulingProblem(
            new SchedulePeriod(startUtc, startUtc.AddHours(16)),
            new[] { resource },
            new[] { firstShift, secondShift },
            new[]
            {
                new AvailabilityWindow(
                    resource.Id,
                    startUtc,
                    startUtc.AddHours(16))
            },
            Array.Empty<ResourcePreference>(),
            resourceWorkloadDemands: new[]
            {
                new ResourceWorkloadDemand(
                    resource.Id,
                    requestedPreferredHours: 8,
                    minimumRequiredHours: 0)
            });
    }
}
