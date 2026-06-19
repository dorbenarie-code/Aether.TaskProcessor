using Aether.Application.Scheduling.SubmissionForms;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class FormSubmissionSchedulingProblemBuilderTests
{
    private const double HoursTolerance = 0.000001;

    [Fact]
    public void Build_ShouldCreateSchedulingProblem_FromWorkerSubmissions()
    {
        var scenario = CreateScenario();

        var request = new FormSubmissionSchedulingProblemBuildRequest(
            scenario.Period,
            scenario.Resources,
            scenario.Shifts,
            [
                new WorkerSubmission(
                    scenario.FirstResource.Id,
                    [
                        new WorkerShiftSubmission(
                            scenario.MorningDate,
                            ShiftKind.Morning,
                            ShiftSubmissionChoice.StrongAvailable)
                    ]),
                new WorkerSubmission(
                    scenario.SecondResource.Id,
                    [
                        new WorkerShiftSubmission(
                            scenario.AfternoonDate,
                            ShiftKind.Afternoon,
                            ShiftSubmissionChoice.Available)
                    ])
            ]);

        var builder = new FormSubmissionSchedulingProblemBuilder();

        var result = builder.Build(request);

        Assert.Same(scenario.Period, result.Problem.Period);
        Assert.Equal(scenario.Resources, result.Problem.Resources);
        Assert.Equal(scenario.Shifts, result.Problem.Shifts);

        Assert.Equal(2, result.Problem.AvailabilityWindows.Count);
        Assert.Equal(2, result.Problem.ResourcePreferences.Count);
        Assert.Empty(result.Problem.ResourceWorkloadDemands);
        Assert.Empty(result.Warnings);

        Assert.Contains(
            result.Problem.AvailabilityWindows,
            window =>
                window.ResourceId == scenario.FirstResource.Id &&
                window.StartUtc == scenario.MorningShift.StartUtc &&
                window.EndUtc == scenario.MorningShift.EndUtc);

        Assert.Contains(
            result.Problem.AvailabilityWindows,
            window =>
                window.ResourceId == scenario.SecondResource.Id &&
                window.StartUtc == scenario.AfternoonShift.StartUtc &&
                window.EndUtc == scenario.AfternoonShift.EndUtc);
    }

    [Fact]
    public void Build_ShouldPreserveHighAndMediumPreferencePriorities()
    {
        var scenario = CreateScenario();

        var request = new FormSubmissionSchedulingProblemBuildRequest(
            scenario.Period,
            scenario.Resources,
            scenario.Shifts,
            [
                new WorkerSubmission(
                    scenario.FirstResource.Id,
                    [
                        new WorkerShiftSubmission(
                            scenario.MorningDate,
                            ShiftKind.Morning,
                            ShiftSubmissionChoice.StrongAvailable)
                    ]),
                new WorkerSubmission(
                    scenario.SecondResource.Id,
                    [
                        new WorkerShiftSubmission(
                            scenario.AfternoonDate,
                            ShiftKind.Afternoon,
                            ShiftSubmissionChoice.Available)
                    ])
            ]);

        var builder = new FormSubmissionSchedulingProblemBuilder();

        var result = builder.Build(request);

        var highPreference = Assert.Single(
            result.Problem.ResourcePreferences,
            preference => preference.ResourceId == scenario.FirstResource.Id);

        Assert.Equal(ResourcePreferenceType.Prefer, highPreference.Type);
        Assert.Equal(ResourcePreferencePriority.High, highPreference.Priority);

        var mediumPreference = Assert.Single(
            result.Problem.ResourcePreferences,
            preference => preference.ResourceId == scenario.SecondResource.Id);

        Assert.Equal(ResourcePreferenceType.Prefer, mediumPreference.Type);
        Assert.Equal(ResourcePreferencePriority.Medium, mediumPreference.Priority);
    }

    [Fact]
    public void Build_ShouldReturnAggregationWarnings()
    {
        var scenario = CreateScenario();

        var unknownResourceId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        var request = new FormSubmissionSchedulingProblemBuildRequest(
            scenario.Period,
            scenario.Resources,
            scenario.Shifts,
            [
                new WorkerSubmission(
                    unknownResourceId,
                    [
                        new WorkerShiftSubmission(
                            scenario.MorningDate,
                            ShiftKind.Morning,
                            ShiftSubmissionChoice.StrongAvailable)
                    ])
            ]);

        var builder = new FormSubmissionSchedulingProblemBuilder();

        var result = builder.Build(request);

        Assert.Empty(result.Problem.AvailabilityWindows);
        Assert.Empty(result.Problem.ResourcePreferences);

        var warning = Assert.Single(result.Warnings);
        Assert.Equal(WorkerSubmissionAggregationWarningType.UnknownResource, warning.Type);
        Assert.Equal(unknownResourceId, warning.ResourceId);
    }

    [Fact]
    public void Build_ShouldCreateWorkloadDemands_WhenTotalEffectiveTargetHoursIsProvided()
    {
        var scenario = CreateScenario();

        var request = new FormSubmissionSchedulingProblemBuildRequest(
            scenario.Period,
            scenario.Resources,
            scenario.Shifts,
            [
                new WorkerSubmission(
                    scenario.FirstResource.Id,
                    [
                        new WorkerShiftSubmission(
                            scenario.MorningDate,
                            ShiftKind.Morning,
                            ShiftSubmissionChoice.StrongAvailable),
                        new WorkerShiftSubmission(
                            scenario.AfternoonDate,
                            ShiftKind.Afternoon,
                            ShiftSubmissionChoice.StrongAvailable)
                    ]),
                new WorkerSubmission(
                    scenario.SecondResource.Id,
                    [
                        new WorkerShiftSubmission(
                            scenario.NightDate,
                            ShiftKind.Night,
                            ShiftSubmissionChoice.Available)
                    ])
            ],
            TotalEffectiveTargetHours: 60);

        var builder = new FormSubmissionSchedulingProblemBuilder();

        var result = builder.Build(request);

        Assert.Equal(2, result.Problem.ResourceWorkloadDemands.Count);

        var firstDemand = result.Problem.ResourceWorkloadDemands.Single(
            demand => demand.ResourceId == scenario.FirstResource.Id);

        var secondDemand = result.Problem.ResourceWorkloadDemands.Single(
            demand => demand.ResourceId == scenario.SecondResource.Id);

        Assert.Equal(40, firstDemand.RequestedPreferredHours, HoursTolerance);
        Assert.Equal(20, secondDemand.RequestedPreferredHours, HoursTolerance);

        Assert.Equal(0, firstDemand.MinimumRequiredHours);
        Assert.Equal(0, secondDemand.MinimumRequiredHours);
    }


    [Fact]
    public void Build_ShouldApplyMandatoryShiftAvailabilityPolicy_WhenRequested()
    {
        var scenario = CreateScenario();

        var request = new FormSubmissionSchedulingProblemBuildRequest(
            scenario.Period,
            scenario.Resources,
            scenario.Shifts,
            [
                new WorkerSubmission(
                    scenario.FirstResource.Id,
                    [
                        new WorkerShiftSubmission(
                            scenario.MorningDate,
                            ShiftKind.Morning,
                            ShiftSubmissionChoice.StrongAvailable)
                    ])
            ],
            ApplyMandatoryShiftAvailabilityPolicy: true);

        var builder = new FormSubmissionSchedulingProblemBuilder();

        var result = builder.Build(request);

        Assert.Equal(2, result.Problem.AvailabilityWindows.Count);
        Assert.Equal(2, result.Problem.ResourcePreferences.Count);

        Assert.Contains(
            result.Problem.AvailabilityWindows,
            window =>
                window.ResourceId == scenario.FirstResource.Id &&
                window.StartUtc == scenario.MorningShift.StartUtc &&
                window.EndUtc == scenario.MorningShift.EndUtc);

        Assert.Contains(
            result.Problem.AvailabilityWindows,
            window =>
                window.ResourceId == scenario.SecondResource.Id &&
                window.StartUtc == scenario.MorningShift.StartUtc &&
                window.EndUtc == scenario.MorningShift.EndUtc);

        Assert.Contains(
            result.Problem.ResourcePreferences,
            preference =>
                preference.ResourceId == scenario.FirstResource.Id &&
                preference.Type == ResourcePreferenceType.Prefer &&
                preference.Priority == ResourcePreferencePriority.High);

        Assert.Contains(
            result.Problem.ResourcePreferences,
            preference =>
                preference.ResourceId == scenario.SecondResource.Id &&
                preference.Type == ResourcePreferenceType.Avoid &&
                preference.Priority == ResourcePreferencePriority.High);
    }

    [Fact]
    public void Build_ShouldNotApplyMandatoryShiftAvailabilityPolicy_ByDefault()
    {
        var scenario = CreateScenario();

        var request = new FormSubmissionSchedulingProblemBuildRequest(
            scenario.Period,
            scenario.Resources,
            scenario.Shifts,
            [
                new WorkerSubmission(
                    scenario.FirstResource.Id,
                    [
                        new WorkerShiftSubmission(
                            scenario.MorningDate,
                            ShiftKind.Morning,
                            ShiftSubmissionChoice.StrongAvailable)
                    ])
            ]);

        var builder = new FormSubmissionSchedulingProblemBuilder();

        var result = builder.Build(request);

        Assert.Single(result.Problem.AvailabilityWindows);
        Assert.Single(result.Problem.ResourcePreferences);

        Assert.DoesNotContain(
            result.Problem.AvailabilityWindows,
            window => window.ResourceId == scenario.SecondResource.Id);

        Assert.DoesNotContain(
            result.Problem.ResourcePreferences,
            preference => preference.ResourceId == scenario.SecondResource.Id);
    }

    [Fact]
    public void Build_ShouldKeepWorkloadDemandsBasedOnPreferPreferences_WhenMandatoryPolicyAddsAvoidPreferences()
    {
        var scenario = CreateScenario();

        var request = new FormSubmissionSchedulingProblemBuildRequest(
            scenario.Period,
            scenario.Resources,
            scenario.Shifts,
            [
                new WorkerSubmission(
                    scenario.FirstResource.Id,
                    [
                        new WorkerShiftSubmission(
                            scenario.MorningDate,
                            ShiftKind.Morning,
                            ShiftSubmissionChoice.StrongAvailable)
                    ]),
                new WorkerSubmission(
                    scenario.SecondResource.Id,
                    [
                        new WorkerShiftSubmission(
                            scenario.AfternoonDate,
                            ShiftKind.Afternoon,
                            ShiftSubmissionChoice.Available)
                    ])
            ],
            TotalEffectiveTargetHours: 60,
            ApplyMandatoryShiftAvailabilityPolicy: true);

        var builder = new FormSubmissionSchedulingProblemBuilder();

        var result = builder.Build(request);

        Assert.Equal(3, result.Problem.ResourcePreferences.Count);

        Assert.Contains(
            result.Problem.ResourcePreferences,
            preference =>
                preference.ResourceId == scenario.SecondResource.Id &&
                preference.Type == ResourcePreferenceType.Avoid &&
                preference.StartUtc == scenario.MorningShift.StartUtc &&
                preference.EndUtc == scenario.MorningShift.EndUtc);

        var firstDemand = result.Problem.ResourceWorkloadDemands.Single(
            demand => demand.ResourceId == scenario.FirstResource.Id);

        var secondDemand = result.Problem.ResourceWorkloadDemands.Single(
            demand => demand.ResourceId == scenario.SecondResource.Id);

        Assert.Equal(30, firstDemand.RequestedPreferredHours, HoursTolerance);
        Assert.Equal(30, secondDemand.RequestedPreferredHours, HoursTolerance);
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

        var nightStartUtc = new DateTime(2026, 6, 16, 22, 30, 0, DateTimeKind.Utc);
        var nightEndUtc = new DateTime(2026, 6, 17, 6, 30, 0, DateTimeKind.Utc);

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

        var nightShift = new Shift(
            Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
            nightStartUtc,
            nightEndUtc,
            ShiftKind.Night,
            minResourceCount: 0,
            maxResourceCount: 1,
            requiresPreferenceToAssign: true,
            nightShiftCategory: NightShiftCategory.Regular);

        return new TestScenario(
            Period: new SchedulePeriod(periodStartUtc, periodEndUtc),
            Resources: [firstResource, secondResource],
            Shifts: [morningShift, afternoonShift, nightShift],
            FirstResource: firstResource,
            SecondResource: secondResource,
            MorningShift: morningShift,
            AfternoonShift: afternoonShift,
            NightShift: nightShift,
            MorningDate: DateOnly.FromDateTime(morningStartUtc),
            AfternoonDate: DateOnly.FromDateTime(afternoonStartUtc),
            NightDate: DateOnly.FromDateTime(nightStartUtc));
    }

    private sealed record TestScenario(
        SchedulePeriod Period,
        IReadOnlyCollection<Resource> Resources,
        IReadOnlyCollection<Shift> Shifts,
        Resource FirstResource,
        Resource SecondResource,
        Shift MorningShift,
        Shift AfternoonShift,
        Shift NightShift,
        DateOnly MorningDate,
        DateOnly AfternoonDate,
        DateOnly NightDate);
}
