using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class SchedulingProblemTests
{
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


    [Fact]
    public void Availability_window_covers_shift_when_shift_is_inside_window()
    {
        var resourceId = Guid.NewGuid();

        var window = new AvailabilityWindow(
            resourceId,
            new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 18, 0, 0, DateTimeKind.Utc));

        var shift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc),
            kind: ShiftKind.Morning,
            minResourceCount: 1,
            maxResourceCount: 1);

        Assert.True(window.Covers(shift));
    }

    [Fact]
    public void Scheduling_problem_requires_period()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new SchedulingProblem(
                period: null!,
                resources: [],
                shifts: [],
                availabilityWindows: [],
                resourcePreferences: []));

        Assert.Equal("period", exception.ParamName);
    }

    [Fact]
    public void Scheduling_problem_requires_resource_preferences_collection()
    {
        var resource = new Resource(
            Guid.NewGuid(),
            "Dana",
            hourlyCost: 100m);

        var shift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc),
            kind: ShiftKind.Morning,
            minResourceCount: 1,
            maxResourceCount: 1);

        var exception = Assert.Throws<ArgumentNullException>(() =>
            new SchedulingProblem(
                period: CreatePeriod(),
                resources: [resource],
                shifts: [shift],
                availabilityWindows: [],
                resourcePreferences: null!));

        Assert.Equal("resourcePreferences", exception.ParamName);
    }

    [Fact]
    public void Scheduling_problem_exposes_period()
    {
        var period = CreatePeriod();

        var resource = new Resource(
            Guid.NewGuid(),
            "Dana",
            hourlyCost: 100m);

        var shift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc),
            kind: ShiftKind.Morning,
            minResourceCount: 1,
            maxResourceCount: 1);

        var problem = new SchedulingProblem(
            period: period,
            resources: [resource],
            shifts: [shift],
            availabilityWindows: [],
            resourcePreferences: []);

        Assert.Equal(period, problem.Period);
    }

    [Fact]
    public void Scheduling_problem_accepts_resource_preferences()
    {
        var resource = new Resource(
            Guid.NewGuid(),
            "Dana",
            hourlyCost: 100m);

        var shift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc),
            kind: ShiftKind.Morning,
            minResourceCount: 1,
            maxResourceCount: 1);

        var resourcePreference = new ResourcePreference(
            resource.Id,
            new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 16, 0, 0, DateTimeKind.Utc),
            ResourcePreferenceType.Avoid,
            ResourcePreferencePriority.High);

        var problem = new SchedulingProblem(
            period: CreatePeriod(),
            resources: [resource],
            shifts: [shift],
            availabilityWindows: [],
            resourcePreferences: [resourcePreference]);

        var preference = Assert.Single(problem.ResourcePreferences);

        Assert.Equal(resourcePreference, preference);
    }

    [Fact]
    public void Scheduling_problem_rejects_resource_preference_for_unknown_resource()
    {
        var knownResource = new Resource(
            Guid.NewGuid(),
            "Dana",
            hourlyCost: 100m);

        var shift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc),
            kind: ShiftKind.Morning,
            minResourceCount: 1,
            maxResourceCount: 1);

        var resourcePreference = new ResourcePreference(
            Guid.NewGuid(),
            new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 16, 0, 0, DateTimeKind.Utc),
            ResourcePreferenceType.Avoid,
            ResourcePreferencePriority.High);

        var exception = Assert.Throws<ArgumentException>(() =>
            new SchedulingProblem(
                period: CreatePeriod(),
                resources: [knownResource],
                shifts: [shift],
                availabilityWindows: [],
                resourcePreferences: [resourcePreference]));

        Assert.Equal("resourcePreferences", exception.ParamName);
    }

    [Fact]
    public void Scheduling_problem_requires_at_least_one_resource()
    {
        var shift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc),
            kind: ShiftKind.Morning,
            minResourceCount: 1,
            maxResourceCount: 1);

        var exception = Assert.Throws<ArgumentException>(() =>
            new SchedulingProblem(
                period: CreatePeriod(),
                resources: [],
                shifts: [shift],
                availabilityWindows: [],
                resourcePreferences: []));

        Assert.Equal("resources", exception.ParamName);
    }

    [Fact]
    public void Scheduling_problem_rejects_availability_for_unknown_resource()
    {
        var knownResource = new Resource(
            Guid.NewGuid(),
            "Dana",
            hourlyCost: 100m);

        var shift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc),
            kind: ShiftKind.Morning,
            minResourceCount: 1,
            maxResourceCount: 1);

        var availabilityWindow = new AvailabilityWindow(
            Guid.NewGuid(),
            new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 18, 0, 0, DateTimeKind.Utc));

        var exception = Assert.Throws<ArgumentException>(() =>
            new SchedulingProblem(
                period: CreatePeriod(),
                resources: [knownResource],
                shifts: [shift],
                availabilityWindows: [availabilityWindow],
                resourcePreferences: []));

        Assert.Equal("availabilityWindows", exception.ParamName);
    }

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
    public void Scheduling_problem_requires_at_least_one_shift()
    {
        var resource = new Resource(
            Guid.NewGuid(),
            "Dana",
            hourlyCost: 100m);

        var exception = Assert.Throws<ArgumentException>(() =>
            new SchedulingProblem(
                period: CreatePeriod(),
                resources: [resource],
                shifts: [],
                availabilityWindows: [],
                resourcePreferences: []));

        Assert.Equal("shifts", exception.ParamName);
    }

    private static SchedulePeriod CreatePeriod()
    {
        return new SchedulePeriod(
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc));
    }
}
