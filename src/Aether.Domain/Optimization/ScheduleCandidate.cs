namespace Aether.Domain.Optimization;

public sealed class ScheduleCandidate
{
    public IReadOnlyCollection<Assignment> Assignments { get; }

    public ScheduleCandidate(IReadOnlyCollection<Assignment> assignments)
    {
        ArgumentNullException.ThrowIfNull(assignments);

        var hasDuplicateAssignment = assignments
            .GroupBy(assignment => new
            {
                assignment.ResourceId,
                assignment.ShiftId
            })
            .Any(group => group.Count() > 1);

        if (hasDuplicateAssignment)
        {
            throw new ArgumentException(
                "A resource cannot be assigned to the same shift more than once.",
                nameof(assignments));
        }

        Assignments = assignments.ToArray();
    }
}
