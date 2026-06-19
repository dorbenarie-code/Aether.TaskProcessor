using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ShiftNightShiftCategoryTests
{
    [Theory]
    [InlineData(NightShiftCategory.Regular)]
    [InlineData(NightShiftCategory.FridayNight)]
    [InlineData(NightShiftCategory.HolidayEveNight)]
    [InlineData(NightShiftCategory.MotzeiShabbatNight)]
    public void Night_shift_accepts_supported_night_shift_category(
        NightShiftCategory nightShiftCategory)
    {
        var shift = CreateShift(
            ShiftKind.Night,
            nightShiftCategory: nightShiftCategory);

        Assert.Equal(ShiftKind.Night, shift.Kind);
        Assert.Equal(nightShiftCategory, shift.NightShiftCategory);
    }

    [Fact]
    public void Night_shift_allows_missing_night_shift_category()
    {
        var shift = CreateShift(ShiftKind.Night);

        Assert.Equal(ShiftKind.Night, shift.Kind);
        Assert.Null(shift.NightShiftCategory);
    }

    [Theory]
    [InlineData(ShiftKind.Morning)]
    [InlineData(ShiftKind.Afternoon)]
    public void Non_night_shift_rejects_night_shift_category(
        ShiftKind shiftKind)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CreateShift(
                shiftKind,
                nightShiftCategory: NightShiftCategory.FridayNight));

        Assert.Equal("nightShiftCategory", exception.ParamName);
    }

    [Theory]
    [InlineData(ShiftKind.Morning)]
    [InlineData(ShiftKind.Afternoon)]
    public void Non_night_shift_has_no_night_shift_category_by_default(
        ShiftKind shiftKind)
    {
        var shift = CreateShift(shiftKind);

        Assert.Equal(shiftKind, shift.Kind);
        Assert.Null(shift.NightShiftCategory);
    }

    [Fact]
    public void Shift_rejects_unsupported_night_shift_category()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateShift(
                ShiftKind.Night,
                nightShiftCategory: (NightShiftCategory)999));

        Assert.Equal("nightShiftCategory", exception.ParamName);
    }

    private static Shift CreateShift(
        ShiftKind kind,
        NightShiftCategory? nightShiftCategory = null)
    {
        return new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 1, 1, 22, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 2, 6, 30, 0, DateTimeKind.Utc),
            kind,
            minResourceCount: 0,
            maxResourceCount: 2,
            nightShiftCategory: nightShiftCategory);
    }
}
