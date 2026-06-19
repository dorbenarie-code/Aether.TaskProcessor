using Aether.Application.Scheduling.SubmissionForms;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class SubmissionFormTemplateBuilderTests
{
    [Fact]
    public void Build_ShouldCreateFourteenDays_WhenStartDateIsSunday()
    {
        var builder = new SubmissionFormTemplateBuilder();
        var startDate = new DateOnly(2026, 6, 14);

        var template = builder.Build(startDate);

        Assert.Equal(startDate, template.StartDate);
        Assert.Equal(14, template.Days.Count);
        Assert.Equal(startDate, template.Days.First().Date);
        Assert.Equal(startDate.AddDays(13), template.Days.Last().Date);
    }

    [Fact]
    public void Build_ShouldEndOnSaturday_WhenStartDateIsSunday()
    {
        var builder = new SubmissionFormTemplateBuilder();
        var startDate = new DateOnly(2026, 6, 14);

        var template = builder.Build(startDate);

        Assert.Equal(new DateOnly(2026, 6, 27), template.EndDate);
        Assert.Equal(DayOfWeek.Saturday, template.EndDate.DayOfWeek);
    }

    [Fact]
    public void Build_ShouldCreateNextPeriodStartingFourteenDaysLater()
    {
        var builder = new SubmissionFormTemplateBuilder();
        var startDate = new DateOnly(2026, 6, 14);

        var template = builder.Build(startDate);

        Assert.Equal(new DateOnly(2026, 6, 28), template.NextStartDate);
        Assert.Equal(DayOfWeek.Sunday, template.NextStartDate.DayOfWeek);
    }

    [Fact]
    public void Build_ShouldCreateThreeShiftOptionsForEachDay()
    {
        var builder = new SubmissionFormTemplateBuilder();
        var startDate = new DateOnly(2026, 6, 14);

        var template = builder.Build(startDate);

        foreach (var day in template.Days)
        {
            Assert.Equal(day.Date.DayOfWeek, day.DayOfWeek);
            Assert.Equal(3, day.ShiftOptions.Count);

            Assert.Equal(
                [ShiftKind.Morning, ShiftKind.Afternoon, ShiftKind.Night],
                day.ShiftOptions.Select(option => option.ShiftKind).ToArray());

            Assert.All(day.ShiftOptions, option =>
            {
                Assert.Equal(day.Date, option.Date);
                Assert.Equal(ShiftSubmissionChoice.Unavailable, option.DefaultChoice);
            });
        }
    }

    [Fact]
    public void Build_ShouldThrow_WhenStartDateIsNotSunday()
    {
        var builder = new SubmissionFormTemplateBuilder();
        var monday = new DateOnly(2026, 6, 15);

        var exception = Assert.Throws<ArgumentException>(() =>
        {
            builder.Build(monday);
        });

        Assert.Equal("startDate", exception.ParamName);
    }

    [Fact]
    public void ShiftSubmissionChoice_ShouldExposeOnlyClosedChoices()
    {
        Assert.Equal(
            [
                ShiftSubmissionChoice.Unavailable,
                ShiftSubmissionChoice.Available,
                ShiftSubmissionChoice.StrongAvailable
            ],
            Enum.GetValues<ShiftSubmissionChoice>());
    }
}
