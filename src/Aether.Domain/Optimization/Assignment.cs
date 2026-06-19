namespace Aether.Domain.Optimization;

public sealed class Assignment
{
    public Guid ResourceId { get; }
    public Guid ShiftId { get; }

    public Assignment(
        Guid resourceId,
        Guid shiftId)
    {
        if (resourceId == Guid.Empty)
        {
            throw new ArgumentException("Resource id cannot be empty.", nameof(resourceId));
        }

        if (shiftId == Guid.Empty)
        {
            throw new ArgumentException("Shift id cannot be empty.", nameof(shiftId));
        }

        ResourceId = resourceId;
        ShiftId = shiftId;
    }
}
