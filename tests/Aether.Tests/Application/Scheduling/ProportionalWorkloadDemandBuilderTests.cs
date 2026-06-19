using Aether.Application.Scheduling.Builders;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class ProportionalWorkloadDemandBuilderTests
{
    private const double HoursTolerance = 0.000001;

    [Fact]
    public void Build_ShouldCreateTargetsProportionalToSubmittedPreferredHours()
    {
        var firstResource = CreateResource("Dana");
        var secondResource = CreateResource("Noam");

        var firstMorningShift = CreateShift(
            new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc),
            hours: 8);

        var firstAfternoonShift = CreateShift(
            new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Utc),
            hours: 8);

        var secondMorningShift = CreateShift(
            new DateTime(2026, 6, 2, 6, 30, 0, DateTimeKind.Utc),
            hours: 8);

        var shifts = new[]
        {
            firstMorningShift,
            firstAfternoonShift,
            secondMorningShift
        };

        var preferences = new[]
        {
            CreatePrefer(firstResource, firstMorningShift),
            CreatePrefer(firstResource, firstAfternoonShift),
            CreatePrefer(secondResource, secondMorningShift)
        };

        var builder = new ProportionalWorkloadDemandBuilder();

        var demands = builder.Build(
            resources: [firstResource, secondResource],
            shifts: shifts,
            preferences: preferences,
            totalEffectiveTargetHours: 60);

        var firstDemand = demands.Single(demand =>
            demand.ResourceId == firstResource.Id);

        var secondDemand = demands.Single(demand =>
            demand.ResourceId == secondResource.Id);

        Assert.Equal(40, firstDemand.RequestedPreferredHours, HoursTolerance);
        Assert.Equal(20, secondDemand.RequestedPreferredHours, HoursTolerance);
        Assert.Equal(0, firstDemand.MinimumRequiredHours);
        Assert.Equal(0, secondDemand.MinimumRequiredHours);
    }

    [Fact]
    public void Build_ShouldKeepTotalRequestedPreferredHoursEqualToTotalEffectiveTargetHours()
    {
        var firstResource = CreateResource("Dana");
        var secondResource = CreateResource("Noam");

        var firstShift = CreateShift(
            new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc),
            hours: 8);

        var secondShift = CreateShift(
            new DateTime(2026, 6, 2, 6, 30, 0, DateTimeKind.Utc),
            hours: 8);

        var builder = new ProportionalWorkloadDemandBuilder();

        var demands = builder.Build(
            resources: [firstResource, secondResource],
            shifts: [firstShift, secondShift],
            preferences:
            [
                CreatePrefer(firstResource, firstShift),
                CreatePrefer(secondResource, secondShift)
            ],
            totalEffectiveTargetHours: 60);

        var totalRequestedPreferredHours = demands
            .Sum(demand => demand.RequestedPreferredHours);

        Assert.Equal(60, totalRequestedPreferredHours, HoursTolerance);
    }

    [Fact]
    public void Build_ShouldThrow_WhenNoSubmittedPreferredHoursExist()
    {
        var resource = CreateResource("Dana");

        var shift = CreateShift(
            new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc),
            hours: 8);

        var builder = new ProportionalWorkloadDemandBuilder();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.Build(
                resources: [resource],
                shifts: [shift],
                preferences: [],
                totalEffectiveTargetHours: 60));

        Assert.Contains("submitted preferred hours", exception.Message);
    }

    [Fact]
    public void Build_ShouldThrow_WhenPreferenceDoesNotMatchKnownShift()
    {
        var resource = CreateResource("Dana");

        var knownShift = CreateShift(
            new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc),
            hours: 8);

        var unknownShift = CreateShift(
            new DateTime(2026, 6, 2, 6, 30, 0, DateTimeKind.Utc),
            hours: 8);

        var builder = new ProportionalWorkloadDemandBuilder();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.Build(
                resources: [resource],
                shifts: [knownShift],
                preferences: [CreatePrefer(resource, unknownShift)],
                totalEffectiveTargetHours: 60));

        Assert.Contains("matching shift", exception.Message);
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
        double hours)
    {
        return new Shift(
            Guid.NewGuid(),
            startUtc,
            startUtc.AddMinutes(hours * 60),
            kind: ShiftKind.Morning,
            minResourceCount: 0,
            maxResourceCount: 1);
    }

    private static ResourcePreference CreatePrefer(
        Resource resource,
        Shift shift)
    {
        return new ResourcePreference(
            resource.Id,
            shift.StartUtc,
            shift.EndUtc,
            ResourcePreferenceType.Prefer,
            ResourcePreferencePriority.High);
    }
}
