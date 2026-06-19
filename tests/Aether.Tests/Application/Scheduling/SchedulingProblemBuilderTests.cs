using Aether.Application.Scheduling.Builders;
using Aether.Application.Scheduling.Contracts;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class SchedulingProblemBuilderTests
{
    [Fact]
    public void Build_ShouldCreateAvailabilityAndPrefer_WhenShiftSelectionExists()
    {
        var resource = CreateResource("Dana");

        var shift = CreateShift(
            new DateTime(2026, 6, 7, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 7, 14, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning);

        var request = CreateRequest(
            resources: [resource],
            shifts: [shift],
            submissions:
            [
                new ResourceSubmissionDto(
                    "Dana",
                    [
                        new ShiftSelectionDto(
                            DateOnly.FromDateTime(shift.StartUtc),
                            ShiftKind.Morning,
                            IsSelected: true)
                    ])
            ]);

        var builder = new SchedulingProblemBuilder();

        var result = builder.Build(request);

        var availability = Assert.Single(result.Problem.AvailabilityWindows);
        Assert.Equal(resource.Id, availability.ResourceId);
        Assert.Equal(shift.StartUtc, availability.StartUtc);
        Assert.Equal(shift.EndUtc, availability.EndUtc);

        var preference = Assert.Single(result.Problem.ResourcePreferences);
        Assert.Equal(resource.Id, preference.ResourceId);
        Assert.Equal(shift.StartUtc, preference.StartUtc);
        Assert.Equal(shift.EndUtc, preference.EndUtc);
        Assert.Equal(ResourcePreferenceType.Prefer, preference.Type);
        Assert.Equal(ResourcePreferencePriority.High, preference.Priority);

        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Build_ShouldCreateNothingForUnselectedShift()
    {
        var resource = CreateResource("Dana");
        var shift = CreateMorningShift();

        var request = CreateRequest(
            resources: [resource],
            shifts: [shift],
            submissions:
            [
                new ResourceSubmissionDto(
                    "Dana",
                    [
                        new ShiftSelectionDto(
                            DateOnly.FromDateTime(shift.StartUtc),
                            ShiftKind.Morning,
                            IsSelected: false)
                    ])
            ]);

        var builder = new SchedulingProblemBuilder();

        var result = builder.Build(request);

        Assert.Empty(result.Problem.AvailabilityWindows);
        Assert.Empty(result.Problem.ResourcePreferences);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Build_ShouldMatchNightShiftByStartDate_WhenShiftCrossesMidnight()
    {
        var resource = CreateResource("Dana");

        var nightShift = CreateShift(
            new DateTime(2026, 6, 7, 22, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 8, 6, 30, 0, DateTimeKind.Utc),
            ShiftKind.Night);

        var request = CreateRequest(
            resources: [resource],
            shifts: [nightShift],
            submissions:
            [
                new ResourceSubmissionDto(
                    "Dana",
                    [
                        new ShiftSelectionDto(
                            new DateOnly(2026, 6, 7),
                            ShiftKind.Night,
                            IsSelected: true)
                    ])
            ]);

        var builder = new SchedulingProblemBuilder();

        var result = builder.Build(request);

        var availability = Assert.Single(result.Problem.AvailabilityWindows);
        Assert.Equal(nightShift.StartUtc, availability.StartUtc);
        Assert.Equal(nightShift.EndUtc, availability.EndUtc);

        var preference = Assert.Single(result.Problem.ResourcePreferences);
        Assert.Equal(nightShift.StartUtc, preference.StartUtc);
        Assert.Equal(nightShift.EndUtc, preference.EndUtc);

        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Build_ShouldReturnWarning_WhenSubmissionResourceNameIsUnknown()
    {
        var knownResource = CreateResource("Dana");
        var shift = CreateMorningShift();

        var request = CreateRequest(
            resources: [knownResource],
            shifts: [shift],
            submissions:
            [
                new ResourceSubmissionDto(
                    "Unknown",
                    [
                        new ShiftSelectionDto(
                            DateOnly.FromDateTime(shift.StartUtc),
                            ShiftKind.Morning,
                            IsSelected: true)
                    ])
            ]);

        var builder = new SchedulingProblemBuilder();

        var result = builder.Build(request);

        var warning = Assert.Single(result.Warnings);
        Assert.Equal(SchedulingProblemBuildWarningType.UnknownResourceName, warning.Type);
        Assert.Equal("Unknown", warning.ResourceName);
        Assert.NotEmpty(warning.Message);

        Assert.Empty(result.Problem.AvailabilityWindows);
        Assert.Empty(result.Problem.ResourcePreferences);
    }

    [Fact]
    public void Build_ShouldReturnWarning_WhenSelectionHasNoMatchingShift()
    {
        var resource = CreateResource("Dana");
        var existingShift = CreateMorningShift();

        var missingDate = new DateOnly(2026, 6, 8);

        var request = CreateRequest(
            resources: [resource],
            shifts: [existingShift],
            submissions:
            [
                new ResourceSubmissionDto(
                    "Dana",
                    [
                        new ShiftSelectionDto(
                            missingDate,
                            ShiftKind.Morning,
                            IsSelected: true)
                    ])
            ]);

        var builder = new SchedulingProblemBuilder();

        var result = builder.Build(request);

        var warning = Assert.Single(result.Warnings);
        Assert.Equal(SchedulingProblemBuildWarningType.NoMatchingShift, warning.Type);
        Assert.Equal("Dana", warning.ResourceName);
        Assert.Equal(missingDate, warning.Date);
        Assert.Equal(ShiftKind.Morning, warning.ShiftKind);
        Assert.NotEmpty(warning.Message);

        Assert.Empty(result.Problem.AvailabilityWindows);
        Assert.Empty(result.Problem.ResourcePreferences);
    }

    [Fact]
    public void Build_ShouldReturnWarning_WhenRawSpecialRequestNoteExists()
    {
        var resource = CreateResource("Dana");
        var shift = CreateMorningShift();

        var request = CreateRequest(
            resources: [resource],
            shifts: [shift],
            submissions:
            [
                new ResourceSubmissionDto(
                    "Dana",
                    [
                        new ShiftSelectionDto(
                            DateOnly.FromDateTime(shift.StartUtc),
                            ShiftKind.Morning,
                            IsSelected: true)
                    ],
                    RawSpecialRequestNote: "Exam this week")
            ]);

        var builder = new SchedulingProblemBuilder();

        var result = builder.Build(request);

        var warning = Assert.Single(result.Warnings);
        Assert.Equal(SchedulingProblemBuildWarningType.RawSpecialRequestNote, warning.Type);
        Assert.Equal("Dana", warning.ResourceName);
        Assert.NotEmpty(warning.Message);

        Assert.Single(result.Problem.AvailabilityWindows);
        Assert.Single(result.Problem.ResourcePreferences);
    }

    [Fact]
    public void Build_ShouldPassSchedulingPoliciesToSchedulingProblem()
    {
        var resource = CreateResource("Dana");
        var shift = CreateMorningShift();

        var request = CreateRequest(
            resources: [resource],
            shifts: [shift],
            submissions: [],
            minimumAssignedHoursPerResource: 90,
            minimumMorningShiftsPerResourcePerFullWeek: 2,
            minimumAfternoonShiftsPerResourcePerFullWeek: 1);

        var builder = new SchedulingProblemBuilder();

        var result = builder.Build(request);

        Assert.Equal(90, result.Problem.MinimumAssignedHoursPerResource);
        Assert.Equal(2, result.Problem.MinimumMorningShiftsPerResourcePerFullWeek);
        Assert.Equal(1, result.Problem.MinimumAfternoonShiftsPerResourcePerFullWeek);
    }

    [Fact]
    public void Build_ShouldThrow_WhenRequestIsNull()
    {
        var builder = new SchedulingProblemBuilder();

        var exception = Assert.Throws<ArgumentNullException>(() =>
            builder.Build(null!));

        Assert.Equal("request", exception.ParamName);
    }

    [Fact]
    public void Build_ShouldMatchResourceNameAfterTrim()
    {
        var resource = CreateResource("Dor");
        var shift = CreateMorningShift();

        var request = CreateRequest(
            resources: [resource],
            shifts: [shift],
            submissions:
            [
                new ResourceSubmissionDto(
                    "  Dor  ",
                    [
                        new ShiftSelectionDto(
                            DateOnly.FromDateTime(shift.StartUtc),
                            ShiftKind.Morning,
                            IsSelected: true)
                    ])
            ]);

        var builder = new SchedulingProblemBuilder();

        var result = builder.Build(request);

        var availability = Assert.Single(result.Problem.AvailabilityWindows);
        Assert.Equal(resource.Id, availability.ResourceId);

        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Build_ShouldThrow_WhenResourceNamesAreDuplicatedAfterTrim()
    {
        var firstResource = CreateResource("Dor");
        var secondResource = CreateResource("  Dor  ");
        var shift = CreateMorningShift();

        var request = CreateRequest(
            resources: [firstResource, secondResource],
            shifts: [shift],
            submissions: []);

        var builder = new SchedulingProblemBuilder();

        var exception = Assert.Throws<ArgumentException>(() =>
            builder.Build(request));

        Assert.Equal("resources", exception.ParamName);
    }

    private static SchedulingProblemBuildRequest CreateRequest(
        IReadOnlyCollection<Resource> resources,
        IReadOnlyCollection<Shift> shifts,
        IReadOnlyCollection<ResourceSubmissionDto> submissions,
        int minimumAssignedHoursPerResource = 0,
        int minimumMorningShiftsPerResourcePerFullWeek = 0,
        int minimumAfternoonShiftsPerResourcePerFullWeek = 0)
    {
        return new SchedulingProblemBuildRequest(
            Period: new SchedulePeriod(
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc)),
            Resources: resources,
            Shifts: shifts,
            ResourceSubmissions: submissions,
            MinimumAssignedHoursPerResource: minimumAssignedHoursPerResource,
            MinimumMorningShiftsPerResourcePerFullWeek: minimumMorningShiftsPerResourcePerFullWeek,
            MinimumAfternoonShiftsPerResourcePerFullWeek: minimumAfternoonShiftsPerResourcePerFullWeek);
    }

    private static Resource CreateResource(string name)
    {
        return new Resource(
            Guid.NewGuid(),
            name,
            hourlyCost: 100m);
    }

    private static Shift CreateMorningShift()
    {
        return CreateShift(
            new DateTime(2026, 6, 7, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 7, 14, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning);
    }

    private static Shift CreateShift(
        DateTime startUtc,
        DateTime endUtc,
        ShiftKind kind)
    {
        return new Shift(
            Guid.NewGuid(),
            startUtc,
            endUtc,
            kind,
            minResourceCount: 0,
            maxResourceCount: 2);
    }
}
