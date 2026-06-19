using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class SchedulingProblemResourceWorkloadDemandTests
{
    [Fact]
    public void Scheduling_problem_has_empty_resource_workload_demands_by_default()
    {
        var problem = CreateProblem();

        Assert.Empty(problem.ResourceWorkloadDemands);
    }

    [Fact]
    public void Scheduling_problem_accepts_resource_workload_demands()
    {
        var resource = CreateResource();

        var demand = new ResourceWorkloadDemand(
            resource.Id,
            requestedPreferredHours: 120,
            minimumRequiredHours: 90);

        var problem = CreateProblem(
            resources: [resource],
            resourceWorkloadDemands: [demand]);

        var storedDemand = Assert.Single(problem.ResourceWorkloadDemands);

        Assert.Equal(demand, storedDemand);
    }

    [Fact]
    public void Scheduling_problem_rejects_resource_workload_demand_for_unknown_resource()
    {
        var knownResource = CreateResource();

        var demand = new ResourceWorkloadDemand(
            Guid.NewGuid(),
            requestedPreferredHours: 120,
            minimumRequiredHours: 90);

        var exception = Assert.Throws<ArgumentException>(() =>
            CreateProblem(
                resources: [knownResource],
                resourceWorkloadDemands: [demand]));

        Assert.Equal("resourceWorkloadDemands", exception.ParamName);
    }

    [Fact]
    public void Scheduling_problem_rejects_duplicate_resource_workload_demands_for_same_resource()
    {
        var resource = CreateResource();

        var firstDemand = new ResourceWorkloadDemand(
            resource.Id,
            requestedPreferredHours: 120,
            minimumRequiredHours: 90);

        var secondDemand = new ResourceWorkloadDemand(
            resource.Id,
            requestedPreferredHours: 90,
            minimumRequiredHours: 90);

        var exception = Assert.Throws<ArgumentException>(() =>
            CreateProblem(
                resources: [resource],
                resourceWorkloadDemands: [firstDemand, secondDemand]));

        Assert.Equal("resourceWorkloadDemands", exception.ParamName);
    }

    [Fact]
    public void Scheduling_problem_copies_resource_workload_demands()
    {
        var resource = CreateResource();

        var demand = new ResourceWorkloadDemand(
            resource.Id,
            requestedPreferredHours: 120,
            minimumRequiredHours: 90);

        var demands = new List<ResourceWorkloadDemand>
        {
            demand
        };

        var problem = CreateProblem(
            resources: [resource],
            resourceWorkloadDemands: demands);

        demands.Clear();

        Assert.Single(problem.ResourceWorkloadDemands);
    }

    private static SchedulingProblem CreateProblem(
        IReadOnlyCollection<Resource>? resources = null,
        IReadOnlyCollection<ResourceWorkloadDemand>? resourceWorkloadDemands = null)
    {
        var actualResources = resources ?? [CreateResource()];

        var shift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc),
            kind: ShiftKind.Morning,
            minResourceCount: 1,
            maxResourceCount: 1);

        return new SchedulingProblem(
            period: new SchedulePeriod(
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)),
            resources: actualResources,
            shifts: [shift],
            availabilityWindows: [],
            resourcePreferences: [],
            resourceWorkloadDemands: resourceWorkloadDemands);
    }

    private static Resource CreateResource()
    {
        return new Resource(
            Guid.NewGuid(),
            "Dana",
            hourlyCost: 100m);
    }
}
