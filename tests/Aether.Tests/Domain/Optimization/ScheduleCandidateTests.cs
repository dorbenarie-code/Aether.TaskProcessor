using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ScheduleCandidateTests
{
    [Fact]
    public void Assignment_requires_non_empty_resource_id()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new Assignment(
                Guid.Empty,
                Guid.NewGuid()));

        Assert.Equal("resourceId", exception.ParamName);
    }

    [Fact]
    public void Assignment_requires_non_empty_shift_id()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new Assignment(
                Guid.NewGuid(),
                Guid.Empty));

        Assert.Equal("shiftId", exception.ParamName);
    }

    [Fact]
    public void Schedule_candidate_accepts_empty_assignments()
    {
        var candidate = new ScheduleCandidate([]);

        Assert.Empty(candidate.Assignments);
    }

    [Fact]
    public void Schedule_candidate_rejects_duplicate_resource_shift_assignment()
    {
        var resourceId = Guid.NewGuid();
        var shiftId = Guid.NewGuid();

        var exception = Assert.Throws<ArgumentException>(() =>
            new ScheduleCandidate(
            [
                new Assignment(resourceId, shiftId),
                new Assignment(resourceId, shiftId)
            ]));

        Assert.Equal("assignments", exception.ParamName);
    }

    [Fact]
    public void Schedule_candidate_copies_assignments()
    {
        var assignments = new List<Assignment>
        {
            new(
                Guid.NewGuid(),
                Guid.NewGuid())
        };

        var candidate = new ScheduleCandidate(assignments);

        assignments.Clear();

        Assert.Single(candidate.Assignments);
    }
}
