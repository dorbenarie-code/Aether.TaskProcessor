using Aether.Application.Scheduling.SubmissionForms;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class MandatoryShiftAvailabilityPolicyTests
{
    [Fact]
    public void Apply_ShouldAddAvailabilityAndAvoid_ForNonSubmittedResources_OnWeekdayMorning()
    {
        var firstResource = CreateResource("Guard01");
        var secondResource = CreateResource("Guard02");

        var shift = CreateShift(
            new DateTime(2026, 6, 15, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 14, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning);

        var existingAvailability = new[]
        {
            CreateAvailability(firstResource, shift)
        };

        var existingPreferences = new[]
        {
            CreatePrefer(firstResource, shift)
        };

        var policy = new MandatoryShiftAvailabilityPolicy();

        var result = policy.Apply(
            [firstResource, secondResource],
            [shift],
            existingAvailability,
            existingPreferences);

        Assert.Equal(2, result.AvailabilityWindows.Count);

        Assert.Contains(
            result.AvailabilityWindows,
            window =>
                window.ResourceId == firstResource.Id &&
                window.StartUtc == shift.StartUtc &&
                window.EndUtc == shift.EndUtc);

        Assert.Contains(
            result.AvailabilityWindows,
            window =>
                window.ResourceId == secondResource.Id &&
                window.StartUtc == shift.StartUtc &&
                window.EndUtc == shift.EndUtc);

        Assert.Contains(
            result.ResourcePreferences,
            preference =>
                preference.ResourceId == firstResource.Id &&
                preference.Type == ResourcePreferenceType.Prefer &&
                preference.Priority == ResourcePreferencePriority.High);

        Assert.Contains(
            result.ResourcePreferences,
            preference =>
                preference.ResourceId == secondResource.Id &&
                preference.Type == ResourcePreferenceType.Avoid &&
                preference.Priority == ResourcePreferencePriority.High);

        Assert.DoesNotContain(
            result.ResourcePreferences,
            preference =>
                preference.ResourceId == firstResource.Id &&
                preference.Type == ResourcePreferenceType.Avoid);
    }

    [Fact]
    public void Apply_ShouldAddAvailabilityAndAvoid_ForNonSubmittedResources_OnMotzeiShabbatNight()
    {
        var firstResource = CreateResource("Guard01");
        var secondResource = CreateResource("Guard02");

        var shift = CreateShift(
            new DateTime(2026, 6, 20, 22, 40, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 21, 6, 30, 0, DateTimeKind.Utc),
            ShiftKind.Night,
            nightShiftCategory: NightShiftCategory.MotzeiShabbatNight);

        var existingAvailability = new[]
        {
            CreateAvailability(firstResource, shift)
        };

        var existingPreferences = new[]
        {
            CreatePrefer(firstResource, shift)
        };

        var policy = new MandatoryShiftAvailabilityPolicy();

        var result = policy.Apply(
            [firstResource, secondResource],
            [shift],
            existingAvailability,
            existingPreferences);

        Assert.Equal(2, result.AvailabilityWindows.Count);

        Assert.Contains(
            result.ResourcePreferences,
            preference =>
                preference.ResourceId == firstResource.Id &&
                preference.Type == ResourcePreferenceType.Prefer);

        Assert.Contains(
            result.ResourcePreferences,
            preference =>
                preference.ResourceId == secondResource.Id &&
                preference.Type == ResourcePreferenceType.Avoid &&
                preference.Priority == ResourcePreferencePriority.High);
    }

    [Fact]
    public void Apply_ShouldNotApplyPolicy_ForNonMandatoryShift()
    {
        var firstResource = CreateResource("Guard01");
        var secondResource = CreateResource("Guard02");

        var shift = CreateShift(
            new DateTime(2026, 6, 15, 14, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 22, 30, 0, DateTimeKind.Utc),
            ShiftKind.Afternoon);

        var existingAvailability = new[]
        {
            CreateAvailability(firstResource, shift)
        };

        var existingPreferences = new[]
        {
            CreatePrefer(firstResource, shift)
        };

        var policy = new MandatoryShiftAvailabilityPolicy();

        var result = policy.Apply(
            [firstResource, secondResource],
            [shift],
            existingAvailability,
            existingPreferences);

        Assert.Single(result.AvailabilityWindows);
        Assert.Single(result.ResourcePreferences);

        Assert.DoesNotContain(
            result.AvailabilityWindows,
            window => window.ResourceId == secondResource.Id);

        Assert.DoesNotContain(
            result.ResourcePreferences,
            preference => preference.ResourceId == secondResource.Id);
    }

    [Fact]
    public void Apply_ShouldNotCreateDuplicateAvailabilityOrPreferences()
    {
        var resource = CreateResource("Guard01");

        var shift = CreateShift(
            new DateTime(2026, 6, 15, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 14, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning);

        var existingAvailability = new[]
        {
            CreateAvailability(resource, shift)
        };

        var existingPreferences = new[]
        {
            CreateAvoid(resource, shift)
        };

        var policy = new MandatoryShiftAvailabilityPolicy();

        var result = policy.Apply(
            [resource],
            [shift],
            existingAvailability,
            existingPreferences);

        Assert.Single(result.AvailabilityWindows);
        Assert.Single(result.ResourcePreferences);

        var preference = Assert.Single(result.ResourcePreferences);
        Assert.Equal(ResourcePreferenceType.Avoid, preference.Type);
        Assert.Equal(ResourcePreferencePriority.High, preference.Priority);
    }

    [Fact]
    public void Apply_ShouldNotAddAvoid_WhenResourceAlreadyHasPreferForMandatoryShift()
    {
        var resource = CreateResource("Guard01");

        var shift = CreateShift(
            new DateTime(2026, 6, 15, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 14, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning);

        var existingAvailability = new[]
        {
            CreateAvailability(resource, shift)
        };

        var existingPreferences = new[]
        {
            CreatePrefer(resource, shift)
        };

        var policy = new MandatoryShiftAvailabilityPolicy();

        var result = policy.Apply(
            [resource],
            [shift],
            existingAvailability,
            existingPreferences);

        Assert.Single(result.AvailabilityWindows);
        Assert.Single(result.ResourcePreferences);

        var preference = Assert.Single(result.ResourcePreferences);
        Assert.Equal(ResourcePreferenceType.Prefer, preference.Type);
        Assert.Equal(ResourcePreferencePriority.High, preference.Priority);
    }

    private static Resource CreateResource(string name)
    {
        return new Resource(
            Guid.NewGuid(),
            name,
            hourlyCost: 0);
    }

    private static Shift CreateShift(
        DateTime startUtc,
        DateTime endUtc,
        ShiftKind kind,
        NightShiftCategory? nightShiftCategory = null)
    {
        return new Shift(
            Guid.NewGuid(),
            startUtc,
            endUtc,
            kind,
            minResourceCount: 1,
            maxResourceCount: 2,
            nightShiftCategory: nightShiftCategory);
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

    private static ResourcePreference CreateAvoid(
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
