using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ShiftTests
{
    [Fact]
    public void Shift_requires_non_empty_id()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new Shift(
                Guid.Empty,
                new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc),
                kind: ShiftKind.Morning,
                minResourceCount: 1,
                maxResourceCount: 1));

        Assert.Equal("id", exception.ParamName);
    }

    [Fact]
    public void Shift_requires_utc_start_date()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new Shift(
                Guid.NewGuid(),
                DateTime.Now,
                DateTime.UtcNow.AddHours(8),
                kind: ShiftKind.Morning,
                minResourceCount: 1,
                maxResourceCount: 1));

        Assert.Equal("startUtc", exception.ParamName);
    }

    [Fact]
    public void Shift_requires_utc_end_date()
    {
        var startUtc = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Local);

        var exception = Assert.Throws<ArgumentException>(() =>
            new Shift(
                Guid.NewGuid(),
                startUtc,
                end,
                kind: ShiftKind.Morning,
                minResourceCount: 1,
                maxResourceCount: 1));

        Assert.Equal("endUtc", exception.ParamName);
    }

    [Fact]
    public void Shift_requires_end_after_start()
    {
        var startUtc = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var endUtc = startUtc;

        var exception = Assert.Throws<ArgumentException>(() =>
            new Shift(
                Guid.NewGuid(),
                startUtc,
                endUtc,
                kind: ShiftKind.Morning,
                minResourceCount: 1,
                maxResourceCount: 1));

        Assert.Equal("endUtc", exception.ParamName);
    }

    [Fact]
    public void Shift_allows_zero_min_resource_count()
    {
        var shift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc),
            kind: ShiftKind.Morning,
            minResourceCount: 0,
            maxResourceCount: 2);

        Assert.Equal(0, shift.MinResourceCount);
        Assert.Equal(2, shift.MaxResourceCount);
    }

    [Fact]
    public void Shift_rejects_negative_min_resource_count()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Shift(
                Guid.NewGuid(),
                new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc),
                kind: ShiftKind.Morning,
                minResourceCount: -1,
                maxResourceCount: 1));

        Assert.Equal("minResourceCount", exception.ParamName);
    }

    [Fact]
    public void Shift_rejects_zero_max_resource_count()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Shift(
                Guid.NewGuid(),
                new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc),
                kind: ShiftKind.Morning,
                minResourceCount: 0,
                maxResourceCount: 0));

        Assert.Equal("maxResourceCount", exception.ParamName);
    }

    [Fact]
    public void Shift_rejects_max_resource_count_lower_than_min_resource_count()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new Shift(
                Guid.NewGuid(),
                new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc),
                kind: ShiftKind.Morning,
                minResourceCount: 2,
                maxResourceCount: 1));

        Assert.Equal("maxResourceCount", exception.ParamName);
    }

    [Fact]
    public void Shift_stores_min_and_max_resource_counts()
    {
        var shift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc),
            kind: ShiftKind.Morning,
            minResourceCount: 4,
            maxResourceCount: 6);

        Assert.Equal(4, shift.MinResourceCount);
        Assert.Equal(6, shift.MaxResourceCount);
    }


    [Fact]
    public void Shift_does_not_require_preference_to_assign_by_default()
    {
        var shift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc),
            kind: ShiftKind.Morning,
            minResourceCount: 1,
            maxResourceCount: 1);

        Assert.False(shift.RequiresPreferenceToAssign);
    }

    [Fact]
    public void Shift_stores_requires_preference_to_assign()
    {
        var shift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc),
            kind: ShiftKind.Morning,
            minResourceCount: 0,
            maxResourceCount: 2,
            requiresPreferenceToAssign: true);

        Assert.True(shift.RequiresPreferenceToAssign);
    }


    [Fact]
    public void Shift_does_not_require_minimum_when_preference_exists_by_default()
    {
        var shift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc),
            kind: ShiftKind.Morning,
            minResourceCount: 0,
            maxResourceCount: 2);

        Assert.False(shift.RequiresMinimumWhenPreferenceExists);
    }

    [Fact]
    public void Shift_stores_requires_minimum_when_preference_exists()
    {
        var shift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc),
            kind: ShiftKind.Morning,
            minResourceCount: 0,
            maxResourceCount: 2,
            requiresMinimumWhenPreferenceExists: true);

        Assert.True(shift.RequiresMinimumWhenPreferenceExists);
    }


    [Fact]
    public void Shift_requires_supported_kind()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Shift(
                Guid.NewGuid(),
                new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 1, 1, 17, 0, 0, DateTimeKind.Utc),
                kind: (ShiftKind)999,
                minResourceCount: 1,
                maxResourceCount: 1));

        Assert.Equal("kind", exception.ParamName);
    }

    [Fact]
    public void Shift_stores_kind()
    {
        var shift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 1, 1, 14, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 22, 30, 0, DateTimeKind.Utc),
            kind: ShiftKind.Afternoon,
            minResourceCount: 1,
            maxResourceCount: 4);

        Assert.Equal(ShiftKind.Afternoon, shift.Kind);
    }

}
