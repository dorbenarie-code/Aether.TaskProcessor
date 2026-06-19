using Aether.Application.Scheduling.Optimization;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class CleanScheduleMutationOperatorTests
{
    [Fact]
    public void Mutate_ShouldThrow_WhenProblemIsNull()
    {
        var candidate = new ScheduleCandidate([]);

        var mutation = new CleanScheduleMutationOperator(seed: 1);

        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            mutation.Mutate(null!, candidate);
        });

        Assert.Equal("problem", exception.ParamName);
    }

    [Fact]
    public void Mutate_ShouldThrow_WhenCandidateIsNull()
    {
        var scenario = CreateScenario();

        var mutation = new CleanScheduleMutationOperator(seed: 1);

        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            mutation.Mutate(scenario.Problem, null!);
        });

        Assert.Equal("candidate", exception.ParamName);
    }

    [Fact]
    public void Mutate_ShouldReturnNewCandidate_WithoutChangingOriginalCandidate()
    {
        var scenario = CreateScenario();

        var originalCandidate = new ScheduleCandidate(
        [
            new Assignment(scenario.Dana.Id, scenario.MorningShift.Id)
        ]);

        var before = ToAssignmentKeys(originalCandidate);

        var mutation = new CleanScheduleMutationOperator(seed: 1);

        var mutatedCandidate = mutation.Mutate(
            scenario.Problem,
            originalCandidate);

        Assert.NotSame(originalCandidate, mutatedCandidate);
        Assert.Equal(before, ToAssignmentKeys(originalCandidate));
    }

    [Fact]
    public void Mutate_ShouldAddAssignment_WhenCandidateIsEmptyAndAssignableShiftHasCapacity()
    {
        var scenario = CreateScenario();

        var originalCandidate = new ScheduleCandidate([]);

        var mutation = new CleanScheduleMutationOperator(seed: 1);

        var mutatedCandidate = mutation.Mutate(
            scenario.Problem,
            originalCandidate);

        Assert.Single(mutatedCandidate.Assignments);

        var evaluation = new ScheduleEvaluator()
            .Evaluate(scenario.Problem, mutatedCandidate);

        Assert.DoesNotContain(
            evaluation.Violations,
            violation => violation.Type == ConstraintViolationType.ResourceUnavailable);

        Assert.DoesNotContain(
            evaluation.Violations,
            violation => violation.Type == ConstraintViolationType.AssignedWithoutRequiredPreference);
    }

    [Fact]
    public void Mutate_ShouldRemoveAssignment_WhenShiftRemainsAboveMinimum()
    {
        var dana = CreateResource("Dana");
        var yossi = CreateResource("Yossi");

        var shift = CreateShift(
            new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning,
            minResourceCount: 1,
            maxResourceCount: 2);

        var problem = CreateProblem(
            resources: [dana, yossi],
            shifts: [shift],
            availabilityWindows:
            [
                CreateAvailability(dana, shift),
                CreateAvailability(yossi, shift)
            ],
            resourcePreferences: []);

        var originalCandidate = new ScheduleCandidate(
        [
            new Assignment(dana.Id, shift.Id),
            new Assignment(yossi.Id, shift.Id)
        ]);

        var mutation = new CleanScheduleMutationOperator(seed: 2);

        var mutatedCandidate = mutation.Mutate(
            problem,
            originalCandidate);

        Assert.Single(mutatedCandidate.Assignments);

        var evaluation = new ScheduleEvaluator()
            .Evaluate(problem, mutatedCandidate);

        Assert.DoesNotContain(
            evaluation.Violations,
            violation => violation.Type == ConstraintViolationType.ShiftUnderstaffed);
    }

    [Fact]
    public void Mutate_ShouldNotRemoveAssignment_WhenShiftIsAtMinimumAndNoOtherMutationIsAvailable()
    {
        var dana = CreateResource("Dana");

        var shift = CreateShift(
            new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning,
            minResourceCount: 1,
            maxResourceCount: 1);

        var problem = CreateProblem(
            resources: [dana],
            shifts: [shift],
            availabilityWindows:
            [
                CreateAvailability(dana, shift)
            ],
            resourcePreferences: []);

        var originalCandidate = new ScheduleCandidate(
        [
            new Assignment(dana.Id, shift.Id)
        ]);

        var mutation = new CleanScheduleMutationOperator(seed: 1);

        var mutatedCandidate = mutation.Mutate(
            problem,
            originalCandidate);

        Assert.Equal(
            ToAssignmentKeys(originalCandidate),
            ToAssignmentKeys(mutatedCandidate));
    }

    [Fact]
    public void Mutate_ShouldRespectRequiresPreferenceToAssign_WhenAddingAssignment()
    {
        var dana = CreateResource("Dana");
        var yossi = CreateResource("Yossi");

        var shift = CreateShift(
            new DateTime(2026, 6, 1, 22, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 2, 6, 30, 0, DateTimeKind.Utc),
            ShiftKind.Night,
            minResourceCount: 0,
            maxResourceCount: 1,
            requiresPreferenceToAssign: true);

        var problem = CreateProblem(
            resources: [dana, yossi],
            shifts: [shift],
            availabilityWindows:
            [
                CreateAvailability(dana, shift),
                CreateAvailability(yossi, shift)
            ],
            resourcePreferences:
            [
                CreatePreferPreference(yossi, shift)
            ]);

        var originalCandidate = new ScheduleCandidate([]);

        var mutation = new CleanScheduleMutationOperator(seed: 1);

        var mutatedCandidate = mutation.Mutate(
            problem,
            originalCandidate);

        var assignment = Assert.Single(mutatedCandidate.Assignments);

        Assert.Equal(yossi.Id, assignment.ResourceId);
        Assert.Equal(shift.Id, assignment.ShiftId);
    }

    [Fact]
    public void Mutate_ShouldReturnDeterministicCandidate_WhenSeedIsFixed()
    {
        var scenario = CreateScenario();

        var originalCandidate = new ScheduleCandidate(
        [
            new Assignment(scenario.Dana.Id, scenario.MorningShift.Id),
            new Assignment(scenario.Yossi.Id, scenario.AfternoonShift.Id)
        ]);

        var firstMutation = new CleanScheduleMutationOperator(seed: 42);
        var secondMutation = new CleanScheduleMutationOperator(seed: 42);

        var firstCandidate = firstMutation.Mutate(
            scenario.Problem,
            originalCandidate);

        var secondCandidate = secondMutation.Mutate(
            scenario.Problem,
            originalCandidate);

        Assert.Equal(
            ToAssignmentKeys(firstCandidate),
            ToAssignmentKeys(secondCandidate));
    }

    [Fact]
    public void Mutate_ShouldReturnEvaluatableCandidate()
    {
        var scenario = CreateScenario();

        var originalCandidate = new ScheduleCandidate(
        [
            new Assignment(scenario.Dana.Id, scenario.MorningShift.Id),
            new Assignment(scenario.Yossi.Id, scenario.AfternoonShift.Id)
        ]);

        var mutation = new CleanScheduleMutationOperator(seed: 5);

        var mutatedCandidate = mutation.Mutate(
            scenario.Problem,
            originalCandidate);

        var evaluation = new ScheduleEvaluator()
            .Evaluate(scenario.Problem, mutatedCandidate);

        Assert.NotNull(evaluation);
    }

    private static Scenario CreateScenario()
    {
        var dana = CreateResource("Dana");
        var yossi = CreateResource("Yossi");
        var noa = CreateResource("Noa");

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

        var resources = new[]
        {
            dana,
            yossi,
            noa
        };

        var shifts = new[]
        {
            morningShift,
            afternoonShift
        };

        var problem = CreateProblem(
            resources: resources,
            shifts: shifts,
            availabilityWindows: CreateAvailabilityForAll(resources, shifts),
            resourcePreferences: []);

        return new Scenario(
            Problem: problem,
            Dana: dana,
            Yossi: yossi,
            Noa: noa,
            MorningShift: morningShift,
            AfternoonShift: afternoonShift);
    }

    private static SchedulingProblem CreateProblem(
        IReadOnlyCollection<Resource> resources,
        IReadOnlyCollection<Shift> shifts,
        IReadOnlyCollection<AvailabilityWindow> availabilityWindows,
        IReadOnlyCollection<ResourcePreference> resourcePreferences)
    {
        return new SchedulingProblem(
            period: new SchedulePeriod(
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc)),
            resources: resources,
            shifts: shifts,
            availabilityWindows: availabilityWindows,
            resourcePreferences: resourcePreferences);
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
        int maxResourceCount,
        bool requiresPreferenceToAssign = false)
    {
        return new Shift(
            Guid.NewGuid(),
            startUtc,
            endUtc,
            kind,
            minResourceCount,
            maxResourceCount,
            requiresPreferenceToAssign);
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

    private static IReadOnlyCollection<AvailabilityWindow> CreateAvailabilityForAll(
        IReadOnlyCollection<Resource> resources,
        IReadOnlyCollection<Shift> shifts)
    {
        return resources
            .SelectMany(resource => shifts.Select(shift =>
                CreateAvailability(resource, shift)))
            .ToArray();
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

    private static IReadOnlyCollection<string> ToAssignmentKeys(
        ScheduleCandidate candidate)
    {
        return candidate.Assignments
            .Select(assignment => $"{assignment.ResourceId}:{assignment.ShiftId}")
            .OrderBy(key => key)
            .ToArray();
    }

    private sealed record Scenario(
        SchedulingProblem Problem,
        Resource Dana,
        Resource Yossi,
        Resource Noa,
        Shift MorningShift,
        Shift AfternoonShift);
}
