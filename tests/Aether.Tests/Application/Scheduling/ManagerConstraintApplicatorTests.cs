using Aether.Application.Scheduling.ManagerConstraints;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class ManagerConstraintApplicatorTests
{
    [Fact]
    public void Apply_ShouldSubtractForbiddenShiftInterval_FromWideAvailabilityAndPreference()
    {
        var resource = CreateResource(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "Dana");

        var date = new DateOnly(2026, 6, 15);

        var morningShift = CreateShift(
            "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
            date,
            ShiftKind.Morning);

        var afternoonShift = CreateShift(
            "cccccccc-cccc-cccc-cccc-cccccccccccc",
            date,
            ShiftKind.Afternoon);

        var wideAvailability = new AvailabilityWindow(
            resource.Id,
            morningShift.StartUtc,
            afternoonShift.EndUtc);

        var widePreference = new ResourcePreference(
            resource.Id,
            morningShift.StartUtc,
            afternoonShift.EndUtc,
            ResourcePreferenceType.Prefer,
            ResourcePreferencePriority.High);

        var constraintSet = new ManagerConstraintSet([
            new ManagerForbiddenAssignment(resource.Id, morningShift.Id)
        ]);

        var result = new ManagerConstraintApplicator().Apply(
            [resource],
            [morningShift, afternoonShift],
            [wideAvailability],
            [widePreference],
            constraintSet);

        var availability = Assert.Single(result.AvailabilityWindows);

        Assert.Equal(resource.Id, availability.ResourceId);
        Assert.Equal(morningShift.EndUtc, availability.StartUtc);
        Assert.Equal(afternoonShift.EndUtc, availability.EndUtc);

        var preference = Assert.Single(result.ResourcePreferences);

        Assert.Equal(resource.Id, preference.ResourceId);
        Assert.Equal(morningShift.EndUtc, preference.StartUtc);
        Assert.Equal(afternoonShift.EndUtc, preference.EndUtc);
        Assert.Equal(ResourcePreferenceType.Prefer, preference.Type);
        Assert.Equal(ResourcePreferencePriority.High, preference.Priority);
    }

    [Fact]
    public void Apply_ShouldLeaveOtherResourcesAndNonOverlappingIntervalsUnchanged()
    {
        var forbiddenResource = CreateResource(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "Dana");

        var otherResource = CreateResource(
            "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
            "Yossi");

        var date = new DateOnly(2026, 6, 15);

        var morningShift = CreateShift(
            "cccccccc-cccc-cccc-cccc-cccccccccccc",
            date,
            ShiftKind.Morning);

        var afternoonShift = CreateShift(
            "dddddddd-dddd-dddd-dddd-dddddddddddd",
            date,
            ShiftKind.Afternoon);

        var forbiddenMorningAvailability = new AvailabilityWindow(
            forbiddenResource.Id,
            morningShift.StartUtc,
            morningShift.EndUtc);

        var forbiddenAfternoonAvailability = new AvailabilityWindow(
            forbiddenResource.Id,
            afternoonShift.StartUtc,
            afternoonShift.EndUtc);

        var otherMorningAvailability = new AvailabilityWindow(
            otherResource.Id,
            morningShift.StartUtc,
            morningShift.EndUtc);

        var forbiddenMorningPreference = CreatePrefer(forbiddenResource, morningShift);
        var forbiddenAfternoonPreference = CreatePrefer(forbiddenResource, afternoonShift);
        var otherMorningPreference = CreatePrefer(otherResource, morningShift);

        var constraintSet = new ManagerConstraintSet([
            new ManagerForbiddenAssignment(forbiddenResource.Id, morningShift.Id)
        ]);

        var result = new ManagerConstraintApplicator().Apply(
            [forbiddenResource, otherResource],
            [morningShift, afternoonShift],
            [
                forbiddenMorningAvailability,
                forbiddenAfternoonAvailability,
                otherMorningAvailability
            ],
            [
                forbiddenMorningPreference,
                forbiddenAfternoonPreference,
                otherMorningPreference
            ],
            constraintSet);

        Assert.Equal(2, result.AvailabilityWindows.Count);
        Assert.Equal(2, result.ResourcePreferences.Count);

        Assert.DoesNotContain(result.AvailabilityWindows, window =>
            window.ResourceId == forbiddenResource.Id &&
            window.StartUtc == morningShift.StartUtc &&
            window.EndUtc == morningShift.EndUtc);

        Assert.Contains(result.AvailabilityWindows, window =>
            window.ResourceId == forbiddenResource.Id &&
            window.StartUtc == afternoonShift.StartUtc &&
            window.EndUtc == afternoonShift.EndUtc);

        Assert.Contains(result.AvailabilityWindows, window =>
            window.ResourceId == otherResource.Id &&
            window.StartUtc == morningShift.StartUtc &&
            window.EndUtc == morningShift.EndUtc);

        Assert.DoesNotContain(result.ResourcePreferences, preference =>
            preference.ResourceId == forbiddenResource.Id &&
            preference.StartUtc == morningShift.StartUtc &&
            preference.EndUtc == morningShift.EndUtc);

        Assert.Contains(result.ResourcePreferences, preference =>
            preference.ResourceId == forbiddenResource.Id &&
            preference.StartUtc == afternoonShift.StartUtc &&
            preference.EndUtc == afternoonShift.EndUtc);

        Assert.Contains(result.ResourcePreferences, preference =>
            preference.ResourceId == otherResource.Id &&
            preference.StartUtc == morningShift.StartUtc &&
            preference.EndUtc == morningShift.EndUtc);
    }

    [Fact]
    public void Apply_ShouldMakeForbiddenAssignmentResourceUnavailable_WhenAssignedAnyway()
    {
        var resource = CreateResource(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "Dana");

        var date = new DateOnly(2026, 6, 15);

        var morningShift = CreateShift(
            "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
            date,
            ShiftKind.Morning);

        var availability = new AvailabilityWindow(
            resource.Id,
            morningShift.StartUtc,
            morningShift.EndUtc);

        var preference = CreatePrefer(resource, morningShift);

        var constraintSet = new ManagerConstraintSet([
            new ManagerForbiddenAssignment(resource.Id, morningShift.Id)
        ]);

        var applicationResult = new ManagerConstraintApplicator().Apply(
            [resource],
            [morningShift],
            [availability],
            [preference],
            constraintSet);

        var problem = new SchedulingProblem(
            new SchedulePeriod(
                date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)),
            [resource],
            [morningShift],
            applicationResult.AvailabilityWindows,
            applicationResult.ResourcePreferences);

        var candidate = new ScheduleCandidate([
            new Assignment(resource.Id, morningShift.Id)
        ]);

        var evaluation = new ScheduleEvaluator().Evaluate(problem, candidate);

        var violation = Assert.Single(evaluation.Violations);

        Assert.False(evaluation.IsFeasible);
        Assert.Equal(ConstraintViolationType.ResourceUnavailable, violation.Type);
        Assert.Equal(ConstraintViolationSeverity.Hard, violation.Severity);
        Assert.Equal(resource.Id, violation.ResourceId);
        Assert.Equal(morningShift.Id, violation.ShiftId);
    }

    [Fact]
    public void Apply_ShouldAddAvoidAndSubtractOverlappingPrefer_ForManagerAvoidAssignment()
    {
        var resource = CreateResource(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "Dana");

        var date = new DateOnly(2026, 6, 15);

        var morningShift = CreateShift(
            "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
            date,
            ShiftKind.Morning);

        var afternoonShift = CreateShift(
            "cccccccc-cccc-cccc-cccc-cccccccccccc",
            date,
            ShiftKind.Afternoon);

        var wideAvailability = new AvailabilityWindow(
            resource.Id,
            morningShift.StartUtc,
            afternoonShift.EndUtc);

        var widePrefer = new ResourcePreference(
            resource.Id,
            morningShift.StartUtc,
            afternoonShift.EndUtc,
            ResourcePreferenceType.Prefer,
            ResourcePreferencePriority.High);

        var constraintSet = new ManagerConstraintSet(
            avoidAssignments:
            [
                new ManagerAvoidAssignment(
                    resource.Id,
                    morningShift.Id)
            ]);

        var result = new ManagerConstraintApplicator().Apply(
            [resource],
            [morningShift, afternoonShift],
            [wideAvailability],
            [widePrefer],
            constraintSet);

        var availability = Assert.Single(result.AvailabilityWindows);

        Assert.Equal(resource.Id, availability.ResourceId);
        Assert.Equal(morningShift.StartUtc, availability.StartUtc);
        Assert.Equal(afternoonShift.EndUtc, availability.EndUtc);

        Assert.Equal(2, result.ResourcePreferences.Count);

        var remainingPrefer = Assert.Single(
            result.ResourcePreferences,
            preference => preference.Type == ResourcePreferenceType.Prefer);

        Assert.Equal(resource.Id, remainingPrefer.ResourceId);
        Assert.Equal(morningShift.EndUtc, remainingPrefer.StartUtc);
        Assert.Equal(afternoonShift.EndUtc, remainingPrefer.EndUtc);
        Assert.Equal(ResourcePreferencePriority.High, remainingPrefer.Priority);

        var managerAvoid = Assert.Single(
            result.ResourcePreferences,
            preference => preference.Type == ResourcePreferenceType.Avoid);

        Assert.Equal(resource.Id, managerAvoid.ResourceId);
        Assert.Equal(morningShift.StartUtc, managerAvoid.StartUtc);
        Assert.Equal(morningShift.EndUtc, managerAvoid.EndUtc);
        Assert.Equal(ResourcePreferencePriority.High, managerAvoid.Priority);
    }

    private static ResourcePreference CreatePrefer(
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

    private static Resource CreateResource(
        string id,
        string name)
    {
        return new Resource(
            Guid.Parse(id),
            name,
            hourlyCost: 0);
    }

    private static Shift CreateShift(
        string id,
        DateOnly date,
        ShiftKind kind)
    {
        return new Shift(
            Guid.Parse(id),
            GetStartUtc(date, kind),
            GetEndUtc(date, kind),
            kind,
            minResourceCount: 0,
            maxResourceCount: 4);
    }

    private static DateTime GetStartUtc(DateOnly date, ShiftKind kind)
    {
        return kind switch
        {
            ShiftKind.Morning => date.ToDateTime(new TimeOnly(6, 30), DateTimeKind.Utc),
            ShiftKind.Afternoon => date.ToDateTime(new TimeOnly(14, 20), DateTimeKind.Utc),
            ShiftKind.Night => date.ToDateTime(new TimeOnly(22, 40), DateTimeKind.Utc),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), "Shift kind is not supported.")
        };
    }

    private static DateTime GetEndUtc(DateOnly date, ShiftKind kind)
    {
        return kind switch
        {
            ShiftKind.Morning => date.ToDateTime(new TimeOnly(14, 20), DateTimeKind.Utc),
            ShiftKind.Afternoon => date.ToDateTime(new TimeOnly(22, 40), DateTimeKind.Utc),
            ShiftKind.Night => date.AddDays(1).ToDateTime(new TimeOnly(6, 30), DateTimeKind.Utc),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), "Shift kind is not supported.")
        };
    }
}
