using Aether.Application.Scheduling.Optimization;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class ScheduleCrossoverOperatorTests
{
    [Fact]
    public void Crossover_ShouldThrow_WhenProblemIsNull()
    {
        var parent = new ScheduleCandidate([]);

        var crossover = new ScheduleCrossoverOperator(seed: 1);

        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            crossover.Crossover(null!, parent, parent);
        });

        Assert.Equal("problem", exception.ParamName);
    }

    [Fact]
    public void Crossover_ShouldThrow_WhenFirstParentIsNull()
    {
        var scenario = CreateScenario();

        var secondParent = new ScheduleCandidate([]);

        var crossover = new ScheduleCrossoverOperator(seed: 1);

        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            crossover.Crossover(scenario.Problem, null!, secondParent);
        });

        Assert.Equal("firstParent", exception.ParamName);
    }

    [Fact]
    public void Crossover_ShouldThrow_WhenSecondParentIsNull()
    {
        var scenario = CreateScenario();

        var firstParent = new ScheduleCandidate([]);

        var crossover = new ScheduleCrossoverOperator(seed: 1);

        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            crossover.Crossover(scenario.Problem, firstParent, null!);
        });

        Assert.Equal("secondParent", exception.ParamName);
    }

    [Fact]
    public void Crossover_ShouldCreateChildFromWholeShiftBuckets()
    {
        var scenario = CreateScenario();

        var firstParent = new ScheduleCandidate(
        [
            new Assignment(scenario.Dana.Id, scenario.MorningShift.Id),
            new Assignment(scenario.Yossi.Id, scenario.MorningShift.Id),
            new Assignment(scenario.Noa.Id, scenario.AfternoonShift.Id),
            new Assignment(scenario.Amit.Id, scenario.NightShift.Id)
        ]);

        var secondParent = new ScheduleCandidate(
        [
            new Assignment(scenario.Noa.Id, scenario.MorningShift.Id),
            new Assignment(scenario.Dana.Id, scenario.AfternoonShift.Id),
            new Assignment(scenario.Amit.Id, scenario.AfternoonShift.Id),
            new Assignment(scenario.Yossi.Id, scenario.NightShift.Id)
        ]);

        var crossover = new ScheduleCrossoverOperator(seed: 7);

        var child = crossover.Crossover(
            scenario.Problem,
            firstParent,
            secondParent);

        AssertShiftBucketComesFromOneParent(
            child,
            scenario.MorningShift.Id,
            GetResourceIds(firstParent, scenario.MorningShift.Id),
            GetResourceIds(secondParent, scenario.MorningShift.Id));

        AssertShiftBucketComesFromOneParent(
            child,
            scenario.AfternoonShift.Id,
            GetResourceIds(firstParent, scenario.AfternoonShift.Id),
            GetResourceIds(secondParent, scenario.AfternoonShift.Id));

        AssertShiftBucketComesFromOneParent(
            child,
            scenario.NightShift.Id,
            GetResourceIds(firstParent, scenario.NightShift.Id),
            GetResourceIds(secondParent, scenario.NightShift.Id));
    }

    [Fact]
    public void Crossover_ShouldNotChangeParentCandidates()
    {
        var scenario = CreateScenario();

        var firstParent = new ScheduleCandidate(
        [
            new Assignment(scenario.Dana.Id, scenario.MorningShift.Id),
            new Assignment(scenario.Yossi.Id, scenario.AfternoonShift.Id)
        ]);

        var secondParent = new ScheduleCandidate(
        [
            new Assignment(scenario.Noa.Id, scenario.MorningShift.Id),
            new Assignment(scenario.Amit.Id, scenario.NightShift.Id)
        ]);

        var firstParentBefore = ToAssignmentKeys(firstParent);
        var secondParentBefore = ToAssignmentKeys(secondParent);

        var crossover = new ScheduleCrossoverOperator(seed: 1);

        _ = crossover.Crossover(
            scenario.Problem,
            firstParent,
            secondParent);

        Assert.Equal(firstParentBefore, ToAssignmentKeys(firstParent));
        Assert.Equal(secondParentBefore, ToAssignmentKeys(secondParent));
    }

    [Fact]
    public void Crossover_ShouldReturnDeterministicChild_WhenSeedIsFixed()
    {
        var scenario = CreateScenario();

        var firstParent = new ScheduleCandidate(
        [
            new Assignment(scenario.Dana.Id, scenario.MorningShift.Id),
            new Assignment(scenario.Yossi.Id, scenario.AfternoonShift.Id),
            new Assignment(scenario.Noa.Id, scenario.NightShift.Id)
        ]);

        var secondParent = new ScheduleCandidate(
        [
            new Assignment(scenario.Amit.Id, scenario.MorningShift.Id),
            new Assignment(scenario.Noa.Id, scenario.AfternoonShift.Id),
            new Assignment(scenario.Dana.Id, scenario.NightShift.Id)
        ]);

        var firstCrossover = new ScheduleCrossoverOperator(seed: 42);
        var secondCrossover = new ScheduleCrossoverOperator(seed: 42);

        var firstChild = firstCrossover.Crossover(
            scenario.Problem,
            firstParent,
            secondParent);

        var secondChild = secondCrossover.Crossover(
            scenario.Problem,
            firstParent,
            secondParent);

        Assert.Equal(ToAssignmentKeys(firstChild), ToAssignmentKeys(secondChild));
    }

    private static void AssertShiftBucketComesFromOneParent(
        ScheduleCandidate child,
        Guid shiftId,
        IReadOnlyCollection<Guid> firstParentResourceIds,
        IReadOnlyCollection<Guid> secondParentResourceIds)
    {
        var actualResourceIds = GetResourceIds(child, shiftId);

        Assert.True(
            SameResourceIds(actualResourceIds, firstParentResourceIds) ||
            SameResourceIds(actualResourceIds, secondParentResourceIds),
            "Child shift bucket must be copied as a whole from one parent.");
    }

    private static IReadOnlyCollection<Guid> GetResourceIds(
        ScheduleCandidate candidate,
        Guid shiftId)
    {
        return candidate.Assignments
            .Where(assignment => assignment.ShiftId == shiftId)
            .Select(assignment => assignment.ResourceId)
            .OrderBy(resourceId => resourceId)
            .ToArray();
    }

    private static bool SameResourceIds(
        IReadOnlyCollection<Guid> left,
        IReadOnlyCollection<Guid> right)
    {
        return left.SequenceEqual(right.OrderBy(resourceId => resourceId));
    }

    private static IReadOnlyCollection<string> ToAssignmentKeys(
        ScheduleCandidate candidate)
    {
        return candidate.Assignments
            .Select(assignment => $"{assignment.ResourceId}:{assignment.ShiftId}")
            .OrderBy(key => key)
            .ToArray();
    }

    private static Scenario CreateScenario()
    {
        var dana = CreateResource("Dana");
        var yossi = CreateResource("Yossi");
        var noa = CreateResource("Noa");
        var amit = CreateResource("Amit");

        var morningShift = CreateShift(
            new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning,
            minResourceCount: 1,
            maxResourceCount: 2);

        var afternoonShift = CreateShift(
            new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 22, 30, 0, DateTimeKind.Utc),
            ShiftKind.Afternoon,
            minResourceCount: 1,
            maxResourceCount: 2);

        var nightShift = CreateShift(
            new DateTime(2026, 6, 1, 22, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 2, 6, 30, 0, DateTimeKind.Utc),
            ShiftKind.Night,
            minResourceCount: 1,
            maxResourceCount: 1);

        var resources = new[]
        {
            dana,
            yossi,
            noa,
            amit
        };

        var shifts = new[]
        {
            morningShift,
            afternoonShift,
            nightShift
        };

        var problem = new SchedulingProblem(
            period: new SchedulePeriod(
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc)),
            resources: resources,
            shifts: shifts,
            availabilityWindows: CreateAvailabilityForAll(resources, shifts),
            resourcePreferences: []);

        return new Scenario(
            Problem: problem,
            Dana: dana,
            Yossi: yossi,
            Noa: noa,
            Amit: amit,
            MorningShift: morningShift,
            AfternoonShift: afternoonShift,
            NightShift: nightShift);
    }

    private static Resource CreateResource(string name)
    {
        return new Resource(
            Guid.NewGuid(),
            name,
            hourlyCost: 100m);
    }

    private static Shift CreateShift(
        DateTime startUtc,
        DateTime endUtc,
        ShiftKind kind,
        int minResourceCount,
        int maxResourceCount)
    {
        return new Shift(
            Guid.NewGuid(),
            startUtc,
            endUtc,
            kind,
            minResourceCount,
            maxResourceCount);
    }

    private static IReadOnlyCollection<AvailabilityWindow> CreateAvailabilityForAll(
        IReadOnlyCollection<Resource> resources,
        IReadOnlyCollection<Shift> shifts)
    {
        return resources
            .SelectMany(resource => shifts.Select(shift =>
                new AvailabilityWindow(
                    resource.Id,
                    shift.StartUtc,
                    shift.EndUtc)))
            .ToArray();
    }

    private sealed record Scenario(
        SchedulingProblem Problem,
        Resource Dana,
        Resource Yossi,
        Resource Noa,
        Resource Amit,
        Shift MorningShift,
        Shift AfternoonShift,
        Shift NightShift);
}
