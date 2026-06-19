using Aether.Application.Scheduling.Optimization;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class DeterministicScheduleOptimizerTests
{
    [Fact]
    public void Optimize_ShouldReturnEmptyCandidate_WhenNoAssignmentsArePossible()
    {
        var resource = CreateResource("Dana");
        var shift = CreateRequiredShift();

        var problem = CreateProblem(
            [resource],
            [shift],
            availabilityWindows: [],
            resourcePreferences: []);

        var optimizer = new DeterministicScheduleOptimizer();

        var result = optimizer.Optimize(problem);

        Assert.Empty(result.Candidate.Assignments);
        Assert.False(result.Evaluation.IsFeasible);
    }

    [Fact]
    public void Optimize_ShouldAssignAvailableResourceToRequiredShift()
    {
        var resource = CreateResource("Dana");
        var shift = CreateRequiredShift();

        var availability = CreateAvailability(resource, shift);

        var problem = CreateProblem(
            [resource],
            [shift],
            [availability],
            resourcePreferences: []);

        var optimizer = new DeterministicScheduleOptimizer();

        var result = optimizer.Optimize(problem);

        var assignment = Assert.Single(result.Candidate.Assignments);
        Assert.Equal(resource.Id, assignment.ResourceId);
        Assert.Equal(shift.Id, assignment.ShiftId);

        Assert.True(result.Evaluation.IsFeasible);
        Assert.Empty(result.Evaluation.Violations);
    }

    [Fact]
    public void Optimize_ShouldNotAssignResourceWithoutAvailability()
    {
        var resource = CreateResource("Dana");
        var shift = CreateRequiredShift();

        var problem = CreateProblem(
            [resource],
            [shift],
            availabilityWindows: [],
            resourcePreferences: []);

        var optimizer = new DeterministicScheduleOptimizer();

        var result = optimizer.Optimize(problem);

        Assert.Empty(result.Candidate.Assignments);
        Assert.Contains(
            result.Evaluation.Violations,
            violation => violation.Type == ConstraintViolationType.ShiftUnderstaffed);
    }

    [Fact]
    public void Optimize_ShouldNotAssignResourceWithoutPrefer_WhenShiftRequiresPreference()
    {
        var resource = CreateResource("Dana");

        var shift = CreateRequiredShift(
            requiresPreferenceToAssign: true);

        var availability = CreateAvailability(resource, shift);

        var problem = CreateProblem(
            [resource],
            [shift],
            [availability],
            resourcePreferences: []);

        var optimizer = new DeterministicScheduleOptimizer();

        var result = optimizer.Optimize(problem);

        Assert.Empty(result.Candidate.Assignments);
        Assert.Contains(
            result.Evaluation.Violations,
            violation => violation.Type == ConstraintViolationType.ShiftUnderstaffed);
    }

    [Fact]
    public void Optimize_ShouldAvoidOverlappingAssignmentsForSameResource()
    {
        var resource = CreateResource("Dana");

        var firstShift = CreateShift(
            new DateTime(2026, 6, 7, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 7, 12, 0, 0, DateTimeKind.Utc));

        var secondShift = CreateShift(
            new DateTime(2026, 6, 7, 11, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 7, 15, 0, 0, DateTimeKind.Utc));

        var problem = CreateProblem(
            [resource],
            [firstShift, secondShift],
            [
                CreateAvailability(resource, firstShift),
                CreateAvailability(resource, secondShift)
            ],
            resourcePreferences: []);

        var optimizer = new DeterministicScheduleOptimizer();

        var result = optimizer.Optimize(problem);

        var assignment = Assert.Single(result.Candidate.Assignments);
        Assert.Equal(resource.Id, assignment.ResourceId);
        Assert.Equal(firstShift.Id, assignment.ShiftId);
    }

    [Fact]
    public void Optimize_ShouldReturnEvaluationForCreatedCandidate()
    {
        var resource = CreateResource("Dana");
        var shift = CreateRequiredShift();
        var availability = CreateAvailability(resource, shift);

        var problem = CreateProblem(
            [resource],
            [shift],
            [availability],
            resourcePreferences: []);

        var optimizer = new DeterministicScheduleOptimizer();

        var result = optimizer.Optimize(problem);

        Assert.NotNull(result.Candidate);
        Assert.NotNull(result.Evaluation);
        Assert.Equal(1000, result.Evaluation.Score.Value);
        Assert.True(result.Evaluation.IsFeasible);
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

    private static Shift CreateRequiredShift(
        bool requiresPreferenceToAssign = false)
    {
        return CreateShift(
            new DateTime(2026, 6, 7, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 7, 14, 30, 0, DateTimeKind.Utc),
            requiresPreferenceToAssign);
    }

    private static Shift CreateShift(
        DateTime startUtc,
        DateTime endUtc,
        bool requiresPreferenceToAssign = false)
    {
        return new Shift(
            Guid.NewGuid(),
            startUtc,
            endUtc,
            kind: ShiftKind.Morning,
            minResourceCount: 1,
            maxResourceCount: 1,
            requiresPreferenceToAssign: requiresPreferenceToAssign);
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
}
