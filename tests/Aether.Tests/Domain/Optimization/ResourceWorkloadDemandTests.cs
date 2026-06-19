using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ResourceWorkloadDemandTests
{
    [Fact]
    public void Resource_workload_demand_requires_non_empty_resource_id()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new ResourceWorkloadDemand(
                Guid.Empty,
                requestedPreferredHours: 120,
                minimumRequiredHours: 90));

        Assert.Equal("resourceId", exception.ParamName);
    }

    [Fact]
    public void Resource_workload_demand_rejects_negative_requested_preferred_hours()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ResourceWorkloadDemand(
                Guid.NewGuid(),
                requestedPreferredHours: -1,
                minimumRequiredHours: 90));

        Assert.Equal("requestedPreferredHours", exception.ParamName);
    }

    [Fact]
    public void Resource_workload_demand_rejects_negative_minimum_required_hours()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ResourceWorkloadDemand(
                Guid.NewGuid(),
                requestedPreferredHours: 120,
                minimumRequiredHours: -1));

        Assert.Equal("minimumRequiredHours", exception.ParamName);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Resource_workload_demand_rejects_non_finite_requested_preferred_hours(
        double requestedPreferredHours)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ResourceWorkloadDemand(
                Guid.NewGuid(),
                requestedPreferredHours,
                minimumRequiredHours: 90));

        Assert.Equal("requestedPreferredHours", exception.ParamName);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Resource_workload_demand_rejects_non_finite_minimum_required_hours(
        double minimumRequiredHours)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ResourceWorkloadDemand(
                Guid.NewGuid(),
                requestedPreferredHours: 120,
                minimumRequiredHours));

        Assert.Equal("minimumRequiredHours", exception.ParamName);
    }

    [Fact]
    public void Resource_workload_demand_uses_requested_hours_as_effective_target_when_requested_is_above_minimum()
    {
        var resourceId = Guid.NewGuid();

        var demand = new ResourceWorkloadDemand(
            resourceId,
            requestedPreferredHours: 120,
            minimumRequiredHours: 90);

        Assert.Equal(resourceId, demand.ResourceId);
        Assert.Equal(120, demand.RequestedPreferredHours);
        Assert.Equal(90, demand.MinimumRequiredHours);
        Assert.Equal(120, demand.EffectiveTargetHours);
    }

    [Fact]
    public void Resource_workload_demand_uses_requested_hours_as_effective_target_when_requested_is_below_minimum()
    {
        var demand = new ResourceWorkloadDemand(
            Guid.NewGuid(),
            requestedPreferredHours: 40,
            minimumRequiredHours: 90);

        Assert.Equal(40, demand.EffectiveMinimumRequiredHours);
        Assert.Equal(40, demand.EffectiveTargetHours);
    }

    [Fact]
    public void Resource_workload_demand_allows_fractional_requested_hours()
    {
        var demand = new ResourceWorkloadDemand(
            Guid.NewGuid(),
            requestedPreferredHours: 66.5,
            minimumRequiredHours: 66);

        Assert.Equal(66.5, demand.RequestedPreferredHours);
        Assert.Equal(66, demand.MinimumRequiredHours);
        Assert.Equal(66, demand.EffectiveMinimumRequiredHours);
        Assert.Equal(66.5, demand.EffectiveTargetHours);
    }

    [Fact]
    public void Resource_workload_demand_caps_effective_minimum_to_requested_hours_when_requested_is_below_fractional_minimum()
    {
        var demand = new ResourceWorkloadDemand(
            Guid.NewGuid(),
            requestedPreferredHours: 40,
            minimumRequiredHours: 66.5);

        Assert.Equal(66.5, demand.MinimumRequiredHours);
        Assert.Equal(40, demand.EffectiveMinimumRequiredHours);
        Assert.Equal(40, demand.EffectiveTargetHours);
    }
}
