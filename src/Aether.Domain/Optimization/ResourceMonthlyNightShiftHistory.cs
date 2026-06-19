namespace Aether.Domain.Optimization;

public sealed class ResourceMonthlyNightShiftHistory
{
    public Guid ResourceId { get; }
    public int Year { get; }
    public int Month { get; }
    public NightShiftCategory NightShiftCategory { get; }
    public int AssignedCount { get; }

    public ResourceMonthlyNightShiftHistory(
        Guid resourceId,
        int year,
        int month,
        NightShiftCategory nightShiftCategory,
        int assignedCount)
    {
        if (resourceId == Guid.Empty)
        {
            throw new ArgumentException("Resource id cannot be empty.", nameof(resourceId));
        }

        if (year <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(year), "Year must be positive.");
        }

        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month), "Month must be between 1 and 12.");
        }

        if (!Enum.IsDefined(typeof(NightShiftCategory), nightShiftCategory))
        {
            throw new ArgumentOutOfRangeException(
                nameof(nightShiftCategory),
                "Night shift category is not supported.");
        }

        if (assignedCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(assignedCount),
                "Assigned count cannot be negative.");
        }

        ResourceId = resourceId;
        Year = year;
        Month = month;
        NightShiftCategory = nightShiftCategory;
        AssignedCount = assignedCount;
    }
}
