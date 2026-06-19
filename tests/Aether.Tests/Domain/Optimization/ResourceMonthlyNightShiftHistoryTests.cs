using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ResourceMonthlyNightShiftHistoryTests
{
    [Fact]
    public void Resource_monthly_night_shift_history_requires_non_empty_resource_id()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new ResourceMonthlyNightShiftHistory(
                Guid.Empty,
                year: 2026,
                month: 1,
                NightShiftCategory.MotzeiShabbatNight,
                assignedCount: 1));

        Assert.Equal("resourceId", exception.ParamName);
    }

    [Fact]
    public void Resource_monthly_night_shift_history_rejects_non_positive_year()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ResourceMonthlyNightShiftHistory(
                Guid.NewGuid(),
                year: 0,
                month: 1,
                NightShiftCategory.MotzeiShabbatNight,
                assignedCount: 1));

        Assert.Equal("year", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void Resource_monthly_night_shift_history_rejects_month_out_of_range(int month)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ResourceMonthlyNightShiftHistory(
                Guid.NewGuid(),
                year: 2026,
                month: month,
                NightShiftCategory.MotzeiShabbatNight,
                assignedCount: 1));

        Assert.Equal("month", exception.ParamName);
    }

    [Fact]
    public void Resource_monthly_night_shift_history_rejects_unsupported_night_shift_category()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ResourceMonthlyNightShiftHistory(
                Guid.NewGuid(),
                year: 2026,
                month: 1,
                (NightShiftCategory)999,
                assignedCount: 1));

        Assert.Equal("nightShiftCategory", exception.ParamName);
    }

    [Fact]
    public void Resource_monthly_night_shift_history_rejects_negative_assigned_count()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ResourceMonthlyNightShiftHistory(
                Guid.NewGuid(),
                year: 2026,
                month: 1,
                NightShiftCategory.MotzeiShabbatNight,
                assignedCount: -1));

        Assert.Equal("assignedCount", exception.ParamName);
    }

    [Fact]
    public void Resource_monthly_night_shift_history_exposes_values()
    {
        var resourceId = Guid.NewGuid();

        var history = new ResourceMonthlyNightShiftHistory(
            resourceId,
            year: 2026,
            month: 1,
            NightShiftCategory.MotzeiShabbatNight,
            assignedCount: 1);

        Assert.Equal(resourceId, history.ResourceId);
        Assert.Equal(2026, history.Year);
        Assert.Equal(1, history.Month);
        Assert.Equal(NightShiftCategory.MotzeiShabbatNight, history.NightShiftCategory);
        Assert.Equal(1, history.AssignedCount);
    }
}
