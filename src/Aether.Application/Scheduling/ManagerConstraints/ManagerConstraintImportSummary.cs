namespace Aether.Application.Scheduling.ManagerConstraints;

public sealed record ManagerConstraintImportSummary(
    int ImportedForbiddenAssignmentCount,
    int ImportedAvoidAssignmentCount,
    int ImportedShiftCapacityOverrideCount)
{
    public static ManagerConstraintImportSummary Empty { get; } = new(
        ImportedForbiddenAssignmentCount: 0,
        ImportedAvoidAssignmentCount: 0,
        ImportedShiftCapacityOverrideCount: 0);

    public static ManagerConstraintImportSummary FromConstraintSet(
        ManagerConstraintSet constraintSet)
    {
        ArgumentNullException.ThrowIfNull(constraintSet);

        return new ManagerConstraintImportSummary(
            constraintSet.ForbiddenAssignments.Count,
            constraintSet.AvoidAssignments.Count,
            constraintSet.ShiftCapacityOverrides.Count);
    }
}
