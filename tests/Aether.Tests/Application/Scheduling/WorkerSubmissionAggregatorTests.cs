using Aether.Application.Scheduling.SubmissionForms;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class WorkerSubmissionAggregatorTests
{
    [Fact]
    public void Aggregate_ShouldCombineAvailabilityAndPreferences_ForMultipleWorkers()
    {
        var scenario = CreateScenario();

        var firstSubmission = new WorkerSubmission(
            scenario.FirstResource.Id,
            [
                new WorkerShiftSubmission(
                    scenario.MorningDate,
                    ShiftKind.Morning,
                    ShiftSubmissionChoice.StrongAvailable)
            ]);

        var secondSubmission = new WorkerSubmission(
            scenario.SecondResource.Id,
            [
                new WorkerShiftSubmission(
                    scenario.AfternoonDate,
                    ShiftKind.Afternoon,
                    ShiftSubmissionChoice.Available)
            ]);

        var aggregator = new WorkerSubmissionAggregator();

        var result = aggregator.Aggregate(
            scenario.Period,
            scenario.Resources,
            scenario.Shifts,
            [firstSubmission, secondSubmission]);

        Assert.Equal(2, result.AvailabilityWindows.Count);
        Assert.Equal(2, result.ResourcePreferences.Count);
        Assert.Empty(result.Warnings);

        Assert.Contains(
            result.AvailabilityWindows,
            window =>
                window.ResourceId == scenario.FirstResource.Id &&
                window.StartUtc == scenario.MorningShift.StartUtc &&
                window.EndUtc == scenario.MorningShift.EndUtc);

        Assert.Contains(
            result.AvailabilityWindows,
            window =>
                window.ResourceId == scenario.SecondResource.Id &&
                window.StartUtc == scenario.AfternoonShift.StartUtc &&
                window.EndUtc == scenario.AfternoonShift.EndUtc);
    }

    [Fact]
    public void Aggregate_ShouldPreserveHighAndMediumPreferencePriorities()
    {
        var scenario = CreateScenario();

        var firstSubmission = new WorkerSubmission(
            scenario.FirstResource.Id,
            [
                new WorkerShiftSubmission(
                    scenario.MorningDate,
                    ShiftKind.Morning,
                    ShiftSubmissionChoice.StrongAvailable)
            ]);

        var secondSubmission = new WorkerSubmission(
            scenario.SecondResource.Id,
            [
                new WorkerShiftSubmission(
                    scenario.AfternoonDate,
                    ShiftKind.Afternoon,
                    ShiftSubmissionChoice.Available)
            ]);

        var aggregator = new WorkerSubmissionAggregator();

        var result = aggregator.Aggregate(
            scenario.Period,
            scenario.Resources,
            scenario.Shifts,
            [firstSubmission, secondSubmission]);

        var highPreference = Assert.Single(
            result.ResourcePreferences,
            preference => preference.ResourceId == scenario.FirstResource.Id);

        Assert.Equal(ResourcePreferenceType.Prefer, highPreference.Type);
        Assert.Equal(ResourcePreferencePriority.High, highPreference.Priority);

        var mediumPreference = Assert.Single(
            result.ResourcePreferences,
            preference => preference.ResourceId == scenario.SecondResource.Id);

        Assert.Equal(ResourcePreferenceType.Prefer, mediumPreference.Type);
        Assert.Equal(ResourcePreferencePriority.Medium, mediumPreference.Priority);
    }

    [Fact]
    public void Aggregate_ShouldReturnUnknownResourceWarning_WhenSubmissionResourceDoesNotExist()
    {
        var scenario = CreateScenario();

        var unknownResourceId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        var submission = new WorkerSubmission(
            unknownResourceId,
            [
                new WorkerShiftSubmission(
                    scenario.MorningDate,
                    ShiftKind.Morning,
                    ShiftSubmissionChoice.StrongAvailable)
            ]);

        var aggregator = new WorkerSubmissionAggregator();

        var result = aggregator.Aggregate(
            scenario.Period,
            scenario.Resources,
            scenario.Shifts,
            [submission]);

        Assert.Empty(result.AvailabilityWindows);
        Assert.Empty(result.ResourcePreferences);

        var warning = Assert.Single(result.Warnings);
        Assert.Equal(WorkerSubmissionAggregationWarningType.UnknownResource, warning.Type);
        Assert.Equal(unknownResourceId, warning.ResourceId);
    }

    [Fact]
    public void Aggregate_ShouldSkipDuplicateWorkerSubmission_AndReturnWarning()
    {
        var scenario = CreateScenario();

        var firstSubmission = new WorkerSubmission(
            scenario.FirstResource.Id,
            [
                new WorkerShiftSubmission(
                    scenario.MorningDate,
                    ShiftKind.Morning,
                    ShiftSubmissionChoice.StrongAvailable)
            ]);

        var duplicateSubmission = new WorkerSubmission(
            scenario.FirstResource.Id,
            [
                new WorkerShiftSubmission(
                    scenario.AfternoonDate,
                    ShiftKind.Afternoon,
                    ShiftSubmissionChoice.Available)
            ]);

        var aggregator = new WorkerSubmissionAggregator();

        var result = aggregator.Aggregate(
            scenario.Period,
            scenario.Resources,
            scenario.Shifts,
            [firstSubmission, duplicateSubmission]);

        Assert.Single(result.AvailabilityWindows);
        Assert.Single(result.ResourcePreferences);

        Assert.Contains(
            result.AvailabilityWindows,
            window =>
                window.ResourceId == scenario.FirstResource.Id &&
                window.StartUtc == scenario.MorningShift.StartUtc &&
                window.EndUtc == scenario.MorningShift.EndUtc);

        Assert.DoesNotContain(
            result.AvailabilityWindows,
            window =>
                window.ResourceId == scenario.FirstResource.Id &&
                window.StartUtc == scenario.AfternoonShift.StartUtc &&
                window.EndUtc == scenario.AfternoonShift.EndUtc);

        var warning = Assert.Single(result.Warnings);
        Assert.Equal(WorkerSubmissionAggregationWarningType.DuplicateWorkerSubmission, warning.Type);
        Assert.Equal(scenario.FirstResource.Id, warning.ResourceId);
        Assert.Equal(scenario.FirstResource.Name, warning.ResourceName);
    }

    [Fact]
    public void Aggregate_ShouldMapNormalizerWarnings_ToAggregationWarnings()
    {
        var scenario = CreateScenario();

        var submission = new WorkerSubmission(
            scenario.FirstResource.Id,
            [
                new WorkerShiftSubmission(
                    scenario.MorningDate,
                    ShiftKind.Night,
                    ShiftSubmissionChoice.StrongAvailable)
            ]);

        var aggregator = new WorkerSubmissionAggregator();

        var result = aggregator.Aggregate(
            scenario.Period,
            scenario.Resources,
            scenario.Shifts,
            [submission]);

        Assert.Empty(result.AvailabilityWindows);
        Assert.Empty(result.ResourcePreferences);

        var warning = Assert.Single(result.Warnings);
        Assert.Equal(WorkerSubmissionAggregationWarningType.NoMatchingShift, warning.Type);
        Assert.Equal(scenario.FirstResource.Id, warning.ResourceId);
        Assert.Equal(scenario.FirstResource.Name, warning.ResourceName);
        Assert.Equal(scenario.MorningDate, warning.Date);
        Assert.Equal(ShiftKind.Night, warning.ShiftKind);
    }

    private static TestScenario CreateScenario()
    {
        var firstResource = new Resource(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "Guard01",
            hourlyCost: 0);

        var secondResource = new Resource(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            "Guard02",
            hourlyCost: 0);

        var periodStartUtc = new DateTime(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc);
        var periodEndUtc = periodStartUtc.AddDays(14);

        var morningStartUtc = new DateTime(2026, 6, 14, 6, 30, 0, DateTimeKind.Utc);
        var morningEndUtc = new DateTime(2026, 6, 14, 14, 30, 0, DateTimeKind.Utc);

        var afternoonStartUtc = new DateTime(2026, 6, 15, 14, 30, 0, DateTimeKind.Utc);
        var afternoonEndUtc = new DateTime(2026, 6, 15, 22, 30, 0, DateTimeKind.Utc);

        var morningShift = new Shift(
            Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            morningStartUtc,
            morningEndUtc,
            ShiftKind.Morning,
            minResourceCount: 1,
            maxResourceCount: 2);

        var afternoonShift = new Shift(
            Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
            afternoonStartUtc,
            afternoonEndUtc,
            ShiftKind.Afternoon,
            minResourceCount: 1,
            maxResourceCount: 2);

        return new TestScenario(
            Period: new SchedulePeriod(periodStartUtc, periodEndUtc),
            Resources: [firstResource, secondResource],
            Shifts: [morningShift, afternoonShift],
            FirstResource: firstResource,
            SecondResource: secondResource,
            MorningShift: morningShift,
            AfternoonShift: afternoonShift,
            MorningDate: DateOnly.FromDateTime(morningStartUtc),
            AfternoonDate: DateOnly.FromDateTime(afternoonStartUtc));
    }

    private sealed record TestScenario(
        SchedulePeriod Period,
        IReadOnlyCollection<Resource> Resources,
        IReadOnlyCollection<Shift> Shifts,
        Resource FirstResource,
        Resource SecondResource,
        Shift MorningShift,
        Shift AfternoonShift,
        DateOnly MorningDate,
        DateOnly AfternoonDate);
}
