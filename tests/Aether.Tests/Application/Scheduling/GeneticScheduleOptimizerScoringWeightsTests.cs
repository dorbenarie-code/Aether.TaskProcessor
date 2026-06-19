using Aether.Application.Scheduling.Optimization;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class GeneticScheduleOptimizerScoringWeightsTests
{
    [Fact]
    public void Optimize_uses_custom_scoring_weights_when_provided()
    {
        var problem = CreateForcedOverTargetProblem();

        var customWeights = ScheduleScoringWeights.CreateDefault() with
        {
            ResourceEffectiveTargetAssignedHoursAboveTargetPenaltyPerHour = 20
        };

        var optimizer = new GeneticScheduleOptimizer(
            populationSize: 1,
            seed: 123,
            generationCount: 0,
            evolutionMode: GeneticEvolutionMode.Clean,
            scoringWeights: customWeights);

        var result = optimizer.Optimize(problem);

        Assert.True(result.Evaluation.IsFeasible);
        Assert.Equal(2, result.Candidate.Assignments.Count);
        Assert.Equal(160, result.Evaluation.Score.TotalPenalty);

        Assert.Contains(
            result.Evaluation.Violations,
            violation =>
                violation.Type == ConstraintViolationType.ResourceEffectiveTargetAssignedHoursAboveTarget &&
                violation.Magnitude == 8);
    }

    private static SchedulingProblem CreateForcedOverTargetProblem()
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
            minResourceCount: 1,
            maxResourceCount: 1);

        var secondShift = new Shift(
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            startUtc.AddHours(8),
            startUtc.AddHours(16),
            ShiftKind.Afternoon,
            minResourceCount: 1,
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
