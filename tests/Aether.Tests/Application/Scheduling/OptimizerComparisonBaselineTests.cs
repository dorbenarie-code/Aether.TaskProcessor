using Aether.Application.Scheduling.Optimization;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class OptimizerComparisonBaselineTests
{
    [Fact]
    public void GeneticOptimizer_ShouldBeatDeterministicOptimizer_WhenGreedyPicksAvoidedResource()
    {
        var avoidedResource = CreateResource("Yossi");
        var cleanResource = CreateResource("Dana");

        var shift = CreateShift(
            new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning);

        var problem = CreateProblem(
            resources:
            [
                avoidedResource,
                cleanResource
            ],
            shifts:
            [
                shift
            ],
            availabilityWindows:
            [
                CreateAvailability(avoidedResource, shift),
                CreateAvailability(cleanResource, shift)
            ],
            resourcePreferences:
            [
                CreateAvoidPreference(avoidedResource, shift)
            ]);

        var deterministicResult = new DeterministicScheduleOptimizer()
            .Optimize(problem);

        var geneticResult = new GeneticScheduleOptimizer(
                populationSize: 100,
                seed: 42)
            .Optimize(problem);

        Assert.True(deterministicResult.Evaluation.IsFeasible);
        Assert.True(geneticResult.Evaluation.IsFeasible);

        var deterministicAssignment = Assert.Single(
            deterministicResult.Candidate.Assignments);

        Assert.Equal(avoidedResource.Id, deterministicAssignment.ResourceId);
        Assert.Equal(shift.Id, deterministicAssignment.ShiftId);

        Assert.Contains(
            deterministicResult.Evaluation.Violations,
            violation => violation.Type == ConstraintViolationType.IgnoredAvoidPreference);

        var geneticAssignment = Assert.Single(
            geneticResult.Candidate.Assignments);

        Assert.Equal(cleanResource.Id, geneticAssignment.ResourceId);
        Assert.Equal(shift.Id, geneticAssignment.ShiftId);

        Assert.DoesNotContain(
            geneticResult.Evaluation.Violations,
            violation => violation.Type == ConstraintViolationType.IgnoredAvoidPreference);

        Assert.True(
            geneticResult.Evaluation.Score.TotalPenalty <
            deterministicResult.Evaluation.Score.TotalPenalty);

        var ranker = new ScheduleEvaluationResultRanker();

        Assert.True(ranker.IsBetterThan(
            geneticResult.Evaluation,
            deterministicResult.Evaluation));
    }

    private static SchedulingProblem CreateProblem(
        IReadOnlyCollection<Resource> resources,
        IReadOnlyCollection<Shift> shifts,
        IReadOnlyCollection<AvailabilityWindow> availabilityWindows,
        IReadOnlyCollection<ResourcePreference> resourcePreferences)
    {
        return new SchedulingProblem(
            period: new SchedulePeriod(
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc)),
            resources: resources,
            shifts: shifts,
            availabilityWindows: availabilityWindows,
            resourcePreferences: resourcePreferences);
    }

    private static Resource CreateResource(string name)
    {
        return new Resource(
            Guid.NewGuid(),
            name,
            hourlyCost: 100m);
    }

    private static Shift CreateShift(
        DateTime startUtc,
        DateTime endUtc,
        ShiftKind kind)
    {
        return new Shift(
            Guid.NewGuid(),
            startUtc,
            endUtc,
            kind,
            minResourceCount: 1,
            maxResourceCount: 1);
    }

    private static AvailabilityWindow CreateAvailability(
        Resource resource,
        Shift shift)
    {
        return new AvailabilityWindow(
            resource.Id,
            shift.StartUtc,
            shift.EndUtc);
    }

    private static ResourcePreference CreateAvoidPreference(
        Resource resource,
        Shift shift)
    {
        return new ResourcePreference(
            resource.Id,
            shift.StartUtc,
            shift.EndUtc,
            ResourcePreferenceType.Avoid,
            ResourcePreferencePriority.High);
    }
}
