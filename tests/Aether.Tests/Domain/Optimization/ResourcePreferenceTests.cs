using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ResourcePreferenceTests
{
    [Fact]
    public void Resource_preference_requires_non_empty_resource_id()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new ResourcePreference(
                Guid.Empty,
                new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 1, 1, 16, 0, 0, DateTimeKind.Utc),
                ResourcePreferenceType.Avoid,
                ResourcePreferencePriority.Medium));

        Assert.Equal("resourceId", exception.ParamName);
    }

    [Fact]
    public void Resource_preference_requires_utc_start_time()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new ResourcePreference(
                Guid.NewGuid(),
                DateTime.Now,
                new DateTime(2026, 1, 1, 16, 0, 0, DateTimeKind.Utc),
                ResourcePreferenceType.Avoid,
                ResourcePreferencePriority.Medium));

        Assert.Equal("startUtc", exception.ParamName);
    }

    [Fact]
    public void Resource_preference_requires_utc_end_time()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new ResourcePreference(
                Guid.NewGuid(),
                new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc),
                DateTime.Now,
                ResourcePreferenceType.Avoid,
                ResourcePreferencePriority.Medium));

        Assert.Equal("endUtc", exception.ParamName);
    }

    [Fact]
    public void Resource_preference_requires_end_after_start()
    {
        var startUtc = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);

        var exception = Assert.Throws<ArgumentException>(() =>
            new ResourcePreference(
                Guid.NewGuid(),
                startUtc,
                startUtc,
                ResourcePreferenceType.Avoid,
                ResourcePreferencePriority.Medium));

        Assert.Equal("endUtc", exception.ParamName);
    }

    [Fact]
    public void Resource_preference_requires_supported_type()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ResourcePreference(
                Guid.NewGuid(),
                new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 1, 1, 16, 0, 0, DateTimeKind.Utc),
                (ResourcePreferenceType)999,
                ResourcePreferencePriority.Medium));

        Assert.Equal("type", exception.ParamName);
    }

    [Fact]
    public void Resource_preference_requires_supported_priority()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ResourcePreference(
                Guid.NewGuid(),
                new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 1, 1, 16, 0, 0, DateTimeKind.Utc),
                ResourcePreferenceType.Avoid,
                (ResourcePreferencePriority)999));

        Assert.Equal("priority", exception.ParamName);
    }

    [Fact]
    public void Resource_preference_exposes_values()
    {
        var resourceId = Guid.NewGuid();
        var startUtc = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var endUtc = new DateTime(2026, 1, 1, 16, 0, 0, DateTimeKind.Utc);

        var preference = new ResourcePreference(
            resourceId,
            startUtc,
            endUtc,
            ResourcePreferenceType.Prefer,
            ResourcePreferencePriority.High);

        Assert.Equal(resourceId, preference.ResourceId);
        Assert.Equal(startUtc, preference.StartUtc);
        Assert.Equal(endUtc, preference.EndUtc);
        Assert.Equal(ResourcePreferenceType.Prefer, preference.Type);
        Assert.Equal(ResourcePreferencePriority.High, preference.Priority);
    }
}
