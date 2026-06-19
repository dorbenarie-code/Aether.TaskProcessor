using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ResourceTests
{
    [Fact]
    public void Resource_requires_non_empty_id()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new Resource(
                Guid.Empty,
                "Dana",
                hourlyCost: 100m));

        Assert.Equal("id", exception.ParamName);
    }

    [Fact]
    public void Resource_requires_non_empty_name()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new Resource(
                Guid.NewGuid(),
                " ",
                hourlyCost: 100m));

        Assert.Equal("name", exception.ParamName);
    }

    [Fact]
    public void Resource_requires_non_negative_hourly_cost()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Resource(
                Guid.NewGuid(),
                "Dana",
                hourlyCost: -1m));

        Assert.Equal("hourlyCost", exception.ParamName);
    }

    [Fact]
    public void Resource_defaults_to_full_workload_category()
    {
        var resource = new Resource(
            Guid.NewGuid(),
            "Dana",
            hourlyCost: 100m);

        Assert.Equal(ResourceWorkloadCategory.Full, resource.WorkloadCategory);
    }

    [Theory]
    [InlineData(ResourceWorkloadCategory.Full)]
    [InlineData(ResourceWorkloadCategory.Student)]
    [InlineData(ResourceWorkloadCategory.Special)]
    public void Resource_stores_workload_category(ResourceWorkloadCategory workloadCategory)
    {
        var resource = new Resource(
            Guid.NewGuid(),
            "Dana",
            hourlyCost: 100m,
            workloadCategory: workloadCategory);

        Assert.Equal(workloadCategory, resource.WorkloadCategory);
    }

    [Fact]
    public void Resource_rejects_unsupported_workload_category()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Resource(
                Guid.NewGuid(),
                "Dana",
                hourlyCost: 100m,
                workloadCategory: (ResourceWorkloadCategory)999));

        Assert.Equal("workloadCategory", exception.ParamName);
    }
}
