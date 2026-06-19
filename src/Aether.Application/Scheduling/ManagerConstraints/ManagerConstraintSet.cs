namespace Aether.Application.Scheduling.ManagerConstraints;

public sealed class ManagerConstraintSet
{
    public IReadOnlyCollection<ManagerForbiddenAssignment> ForbiddenAssignments { get; }
    public IReadOnlyCollection<ManagerShiftCapacityOverride> ShiftCapacityOverrides { get; }
    public IReadOnlyCollection<ManagerAvoidAssignment> AvoidAssignments { get; }

    public ManagerConstraintSet(
        IReadOnlyCollection<ManagerForbiddenAssignment>? forbiddenAssignments = null,
        IReadOnlyCollection<ManagerShiftCapacityOverride>? shiftCapacityOverrides = null,
        IReadOnlyCollection<ManagerAvoidAssignment>? avoidAssignments = null)
    {
        ForbiddenAssignments = (forbiddenAssignments ?? Array.Empty<ManagerForbiddenAssignment>())
            .ToArray();

        ShiftCapacityOverrides = (shiftCapacityOverrides ?? Array.Empty<ManagerShiftCapacityOverride>())
            .ToArray();

        AvoidAssignments = (avoidAssignments ?? Array.Empty<ManagerAvoidAssignment>())
            .ToArray();
    }

    public static ManagerConstraintSet Empty { get; } = new();
}
