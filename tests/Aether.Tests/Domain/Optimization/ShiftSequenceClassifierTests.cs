using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ShiftSequenceClassifierTests
{
    private readonly ShiftSequenceClassifier _classifier = new();

    [Fact]
    public void Classify_returns_night_to_afternoon_when_rest_is_less_than_8_hours()
    {
        var nightShift = CreateShift(
            ShiftKind.Night,
            new DateTime(2026, 1, 7, 22, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 8, 6, 40, 0, DateTimeKind.Utc));

        var afternoonShift = CreateShift(
            ShiftKind.Afternoon,
            new DateTime(2026, 1, 8, 14, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 8, 22, 0, 0, DateTimeKind.Utc));

        var result = _classifier.Classify(nightShift, afternoonShift);

        Assert.Equal(ShiftSequenceType.NightToAfternoon, result);
    }

    [Fact]
    public void Classify_returns_afternoon_to_morning_when_rest_is_less_than_8_hours()
    {
        var afternoonShift = CreateShift(
            ShiftKind.Afternoon,
            new DateTime(2026, 1, 4, 14, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 4, 22, 30, 0, DateTimeKind.Utc));

        var morningShift = CreateShift(
            ShiftKind.Morning,
            new DateTime(2026, 1, 5, 6, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 5, 14, 0, 0, DateTimeKind.Utc));

        var result = _classifier.Classify(afternoonShift, morningShift);

        Assert.Equal(ShiftSequenceType.AfternoonToMorning, result);
    }

    [Fact]
    public void Classify_returns_null_when_rest_is_exactly_8_hours()
    {
        var afternoonShift = CreateShift(
            ShiftKind.Afternoon,
            new DateTime(2026, 1, 4, 14, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 4, 22, 30, 0, DateTimeKind.Utc));

        var morningShift = CreateShift(
            ShiftKind.Morning,
            new DateTime(2026, 1, 5, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 5, 14, 30, 0, DateTimeKind.Utc));

        var result = _classifier.Classify(afternoonShift, morningShift);

        Assert.Null(result);
    }

    [Fact]
    public void Classify_returns_null_when_rest_is_more_than_8_hours()
    {
        var afternoonShift = CreateShift(
            ShiftKind.Afternoon,
            new DateTime(2026, 1, 4, 14, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 4, 22, 30, 0, DateTimeKind.Utc));

        var morningShift = CreateShift(
            ShiftKind.Morning,
            new DateTime(2026, 1, 5, 7, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 5, 15, 30, 0, DateTimeKind.Utc));

        var result = _classifier.Classify(afternoonShift, morningShift);

        Assert.Null(result);
    }

    [Fact]
    public void Classify_returns_null_for_unsupported_sequence_pair()
    {
        var morningShift = CreateShift(
            ShiftKind.Morning,
            new DateTime(2026, 1, 4, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 4, 14, 30, 0, DateTimeKind.Utc));

        var afternoonShift = CreateShift(
            ShiftKind.Afternoon,
            new DateTime(2026, 1, 4, 14, 40, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 4, 22, 40, 0, DateTimeKind.Utc));

        var result = _classifier.Classify(morningShift, afternoonShift);

        Assert.Null(result);
    }

    [Fact]
    public void Classify_returns_null_for_overlapping_shifts()
    {
        var firstShift = CreateShift(
            ShiftKind.Afternoon,
            new DateTime(2026, 1, 4, 14, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 4, 22, 30, 0, DateTimeKind.Utc));

        var secondShift = CreateShift(
            ShiftKind.Morning,
            new DateTime(2026, 1, 4, 22, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 5, 6, 0, 0, DateTimeKind.Utc));

        var result = _classifier.Classify(firstShift, secondShift);

        Assert.Null(result);
    }

    [Fact]
    public void Classify_rejects_null_previous_shift()
    {
        var nextShift = CreateShift(
            ShiftKind.Morning,
            new DateTime(2026, 1, 5, 6, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 5, 14, 0, 0, DateTimeKind.Utc));

        Assert.Throws<ArgumentNullException>(() =>
            _classifier.Classify(null!, nextShift));
    }

    [Fact]
    public void Classify_rejects_null_next_shift()
    {
        var previousShift = CreateShift(
            ShiftKind.Afternoon,
            new DateTime(2026, 1, 4, 14, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 4, 22, 30, 0, DateTimeKind.Utc));

        Assert.Throws<ArgumentNullException>(() =>
            _classifier.Classify(previousShift, null!));
    }

    private static Shift CreateShift(
        ShiftKind kind,
        DateTime startUtc,
        DateTime endUtc)
    {
        return new Shift(
            Guid.NewGuid(),
            startUtc,
            endUtc,
            kind,
            minResourceCount: 1,
            maxResourceCount: 1);
    }
}
