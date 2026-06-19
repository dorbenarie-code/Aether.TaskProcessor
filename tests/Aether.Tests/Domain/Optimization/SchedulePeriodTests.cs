using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class SchedulePeriodTests
{
    [Fact]
    public void Should_create_schedule_period()
    {
        var startUtc = new DateTime(2026, 5, 17, 0, 0, 0, DateTimeKind.Utc);
        var endUtc = new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc);

        var period = new SchedulePeriod(startUtc, endUtc);

        Assert.Equal(startUtc, period.StartUtc);
        Assert.Equal(endUtc, period.EndUtc);
    }

    [Fact]
    public void Should_reject_period_with_end_before_start()
    {
        var startUtc = new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc);
        var endUtc = new DateTime(2026, 5, 17, 0, 0, 0, DateTimeKind.Utc);

        Assert.Throws<ArgumentException>(() =>
            new SchedulePeriod(startUtc, endUtc));
    }

    [Fact]
    public void Should_reject_period_with_end_equal_to_start()
    {
        var startUtc = new DateTime(2026, 5, 17, 0, 0, 0, DateTimeKind.Utc);
        var endUtc = startUtc;

        Assert.Throws<ArgumentException>(() =>
            new SchedulePeriod(startUtc, endUtc));
    }

    [Fact]
    public void Should_reject_schedule_period_start_that_is_not_utc()
    {
        var start = new DateTime(2026, 5, 17, 0, 0, 0, DateTimeKind.Local);
        var endUtc = new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc);

        Assert.Throws<ArgumentException>(() =>
            new SchedulePeriod(start, endUtc));
    }

    [Fact]
    public void Should_reject_schedule_period_end_that_is_not_utc()
    {
        var startUtc = new DateTime(2026, 5, 17, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Local);

        Assert.Throws<ArgumentException>(() =>
            new SchedulePeriod(startUtc, end));
    }
}
