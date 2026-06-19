namespace Aether.Application.Scheduling.ManagerConstraints;

public sealed class ManagerShiftCapacityOverride
{
    public Guid ShiftId { get; }
    public int MinResourceCount { get; }
    public int MaxResourceCount { get; }

    public ManagerShiftCapacityOverride(
        Guid shiftId,
        int minResourceCount,
        int maxResourceCount)
    {
        if (shiftId == Guid.Empty)
        {
            throw new ArgumentException("Shift id cannot be empty.", nameof(shiftId));
        }

        if (minResourceCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minResourceCount),
                "Minimum resource count cannot be negative.");
        }

        if (maxResourceCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxResourceCount),
                "Maximum resource count must be greater than zero.");
        }

        if (maxResourceCount < minResourceCount)
        {
            throw new ArgumentException(
                "Maximum resource count cannot be lower than minimum resource count.",
                nameof(maxResourceCount));
        }

        ShiftId = shiftId;
        MinResourceCount = minResourceCount;
        MaxResourceCount = maxResourceCount;
    }
}
