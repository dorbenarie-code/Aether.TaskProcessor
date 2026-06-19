using Aether.Application.Scheduling.SubmissionForms;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class WorkerSubmissionNormalizerTests
{
    [Fact]
    public void Normalize_ShouldCreateAvailabilityAndHighPrefer_WhenChoiceIsStrongAvailable()
    {
        var scenario = CreateScenario();
        var submission = new WorkerSubmission(
            scenario.Resource.Id,
            [
                new WorkerShiftSubmission(
                    scenario.MorningDate,
                    ShiftKind.Morning,
                    ShiftSubmissionChoice.StrongAvailable)
            ]);

        var normalizer = new WorkerSubmissionNormalizer();

        var result = normalizer.Normalize(
            scenario.Period,
            scenario.Resource,
            scenario.Shifts,
            submission);

        var availability = Assert.Single(result.AvailabilityWindows);
        Assert.Equal(scenario.Resource.Id, availability.ResourceId);
        Assert.Equal(scenario.MorningShift.StartUtc, availability.StartUtc);
        Assert.Equal(scenario.MorningShift.EndUtc, availability.EndUtc);

        var preference = Assert.Single(result.ResourcePreferences);
        Assert.Equal(scenario.Resource.Id, preference.ResourceId);
        Assert.Equal(scenario.MorningShift.StartUtc, preference.StartUtc);
        Assert.Equal(scenario.MorningShift.EndUtc, preference.EndUtc);
        Assert.Equal(ResourcePreferenceType.Prefer, preference.Type);
        Assert.Equal(ResourcePreferencePriority.High, preference.Priority);

        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Normalize_ShouldCreateAvailabilityAndMediumPrefer_WhenChoiceIsAvailable()
    {
        var scenario = CreateScenario();
        var submission = new WorkerSubmission(
            scenario.Resource.Id,
            [
                new WorkerShiftSubmission(
                    scenario.MorningDate,
                    ShiftKind.Morning,
                    ShiftSubmissionChoice.Available)
            ]);

        var normalizer = new WorkerSubmissionNormalizer();

        var result = normalizer.Normalize(
            scenario.Period,
            scenario.Resource,
            scenario.Shifts,
            submission);

        Assert.Single(result.AvailabilityWindows);

        var preference = Assert.Single(result.ResourcePreferences);
        Assert.Equal(ResourcePreferenceType.Prefer, preference.Type);
        Assert.Equal(ResourcePreferencePriority.Medium, preference.Priority);

        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Normalize_ShouldNotCreateAvailabilityOrPreference_WhenChoiceIsUnavailable()
    {
        var scenario = CreateScenario();
        var submission = new WorkerSubmission(
            scenario.Resource.Id,
            [
                new WorkerShiftSubmission(
                    scenario.MorningDate,
                    ShiftKind.Morning,
                    ShiftSubmissionChoice.Unavailable)
            ]);

        var normalizer = new WorkerSubmissionNormalizer();

        var result = normalizer.Normalize(
            scenario.Period,
            scenario.Resource,
            scenario.Shifts,
            submission);

        Assert.Empty(result.AvailabilityWindows);
        Assert.Empty(result.ResourcePreferences);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Normalize_ShouldNotCreateDuplicateDomainEntries_ForDuplicateWorkerShiftSubmission()
    {
        var scenario = CreateScenario();
        var submission = new WorkerSubmission(
            scenario.Resource.Id,
            [
                new WorkerShiftSubmission(
                    scenario.MorningDate,
                    ShiftKind.Morning,
                    ShiftSubmissionChoice.StrongAvailable),
                new WorkerShiftSubmission(
                    scenario.MorningDate,
                    ShiftKind.Morning,
                    ShiftSubmissionChoice.Available)
            ]);

        var normalizer = new WorkerSubmissionNormalizer();

        var result = normalizer.Normalize(
            scenario.Period,
            scenario.Resource,
            scenario.Shifts,
            submission);

        Assert.Single(result.AvailabilityWindows);
        Assert.Single(result.ResourcePreferences);

        var warning = Assert.Single(result.Warnings);
        Assert.Equal(WorkerSubmissionNormalizationWarningType.DuplicateShiftSubmission, warning.Type);
        Assert.Equal(scenario.Resource.Name, warning.ResourceName);
        Assert.Equal(scenario.MorningDate, warning.Date);
        Assert.Equal(ShiftKind.Morning, warning.ShiftKind);
    }

    [Fact]
    public void Normalize_ShouldRejectSelection_WhenDateIsOutsidePeriod()
    {
        var scenario = CreateScenario();
        var outsideDate = scenario.MorningDate.AddDays(14);

        var submission = new WorkerSubmission(
            scenario.Resource.Id,
            [
                new WorkerShiftSubmission(
                    outsideDate,
                    ShiftKind.Morning,
                    ShiftSubmissionChoice.StrongAvailable)
            ]);

        var normalizer = new WorkerSubmissionNormalizer();

        var result = normalizer.Normalize(
            scenario.Period,
            scenario.Resource,
            scenario.Shifts,
            submission);

        Assert.Empty(result.AvailabilityWindows);
        Assert.Empty(result.ResourcePreferences);

        var warning = Assert.Single(result.Warnings);
        Assert.Equal(WorkerSubmissionNormalizationWarningType.DateOutsidePeriod, warning.Type);
        Assert.Equal(scenario.Resource.Name, warning.ResourceName);
        Assert.Equal(outsideDate, warning.Date);
        Assert.Equal(ShiftKind.Morning, warning.ShiftKind);
    }

    [Fact]
    public void Normalize_ShouldRejectSelection_WhenMatchingShiftDoesNotExist()
    {
        var scenario = CreateScenario();

        var submission = new WorkerSubmission(
            scenario.Resource.Id,
            [
                new WorkerShiftSubmission(
                    scenario.MorningDate,
                    ShiftKind.Night,
                    ShiftSubmissionChoice.StrongAvailable)
            ]);

        var normalizer = new WorkerSubmissionNormalizer();

        var result = normalizer.Normalize(
            scenario.Period,
            scenario.Resource,
            scenario.Shifts,
            submission);

        Assert.Empty(result.AvailabilityWindows);
        Assert.Empty(result.ResourcePreferences);

        var warning = Assert.Single(result.Warnings);
        Assert.Equal(WorkerSubmissionNormalizationWarningType.NoMatchingShift, warning.Type);
        Assert.Equal(scenario.Resource.Name, warning.ResourceName);
        Assert.Equal(scenario.MorningDate, warning.Date);
        Assert.Equal(ShiftKind.Night, warning.ShiftKind);
    }

    private static TestScenario CreateScenario()
    {
        var resource = new Resource(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "Guard01",
            hourlyCost: 0);

        var periodStartUtc = new DateTime(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc);
        var periodEndUtc = periodStartUtc.AddDays(14);

        var morningStartUtc = new DateTime(2026, 6, 14, 6, 30, 0, DateTimeKind.Utc);
        var morningEndUtc = new DateTime(2026, 6, 14, 14, 30, 0, DateTimeKind.Utc);

        var morningShift = new Shift(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            morningStartUtc,
            morningEndUtc,
            ShiftKind.Morning,
            minResourceCount: 1,
            maxResourceCount: 2);

        return new TestScenario(
            new SchedulePeriod(periodStartUtc, periodEndUtc),
            resource,
            [morningShift],
            morningShift,
            DateOnly.FromDateTime(morningStartUtc));
    }

    private sealed record TestScenario(
        SchedulePeriod Period,
        Resource Resource,
        IReadOnlyCollection<Shift> Shifts,
        Shift MorningShift,
        DateOnly MorningDate);
}
