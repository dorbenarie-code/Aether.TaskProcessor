using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ScheduleEvaluatorMonthlyRegularNightQuotaTests
{
    [Fact]
    public void Evaluate_ShouldReportMonthlyNightShiftQuotaExceeded_WhenResourceHasTwoRegularNightAssignmentsInSameMonth()
    {
        var resource = CreateResource("Guard01");

        var firstRegularNight = CreateRegularNightShift(new DateOnly(2026, 6, 1));
        var secondRegularNight = CreateRegularNightShift(new DateOnly(2026, 6, 3));

        var problem = new SchedulingProblem(
            period: new SchedulePeriod(
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)),
            resources: [resource],
            shifts:
            [
                firstRegularNight,
                secondRegularNight
            ],
            availabilityWindows:
            [
                CreateAvailability(resource, firstRegularNight),
                CreateAvailability(resource, secondRegularNight)
            ],
            resourcePreferences:
            [
                CreatePreferPreference(resource, firstRegularNight),
                CreatePreferPreference(resource, secondRegularNight)
            ]);

        var candidate = new ScheduleCandidate(
        [
            new Assignment(resource.Id, firstRegularNight.Id),
            new Assignment(resource.Id, secondRegularNight.Id)
        ]);

        var result = new ScheduleEvaluator()
            .Evaluate(problem, candidate);

        Assert.Contains(
            result.Violations,
            violation =>
                violation.Type == ConstraintViolationType.ResourceMonthlyNightShiftQuotaExceeded &&
                violation.ResourceId == resource.Id);

        Assert.False(result.IsFeasible);
        Assert.True(result.Score.HardViolationCount > 0);
    }

    private static Resource CreateResource(string name)
    {
        return new Resource(
            Guid.NewGuid(),
            name,
            hourlyCost: 100m);
    }

    private static Shift CreateRegularNightShift(DateOnly date)
    {
        return new Shift(
            Guid.NewGuid(),
            date.ToDateTime(
                new TimeOnly(22, 30),
                DateTimeKind.Utc),
            date.AddDays(1).ToDateTime(
                new TimeOnly(6, 30),
                DateTimeKind.Utc),
            ShiftKind.Night,
            minResourceCount: 1,
            maxResourceCount: 1,
            requiresPreferenceToAssign: true,
            requiresMinimumWhenPreferenceExists: false,
            nightShiftCategory: NightShiftCategory.Regular);
    }

    private static AvailabilityWindow CreateAvailability(
        Resource resource,
        Shift shift)
    {
        return new AvailabilityWindow(
            resource.Id,
            shift.StartUtc,
            shift.EndUtc);
    }

    private static ResourcePreference CreatePreferPreference(
        Resource resource,
        Shift shift)
    {
        return new ResourcePreference(
            resource.Id,
            shift.StartUtc,
            shift.EndUtc,
            ResourcePreferenceType.Prefer,
            ResourcePreferencePriority.High);
    }
}
