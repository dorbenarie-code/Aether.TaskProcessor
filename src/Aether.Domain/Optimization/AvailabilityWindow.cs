namespace Aether.Domain.Optimization;

public sealed class AvailabilityWindow
{
    public Guid ResourceId { get; }
    public DateTime StartUtc { get; }
    public DateTime EndUtc { get; }

    public AvailabilityWindow(
        Guid resourceId,
        DateTime startUtc,
        DateTime endUtc)
    {
        if (resourceId == Guid.Empty)
        {
            throw new ArgumentException("Resource id cannot be empty.", nameof(resourceId));
        }

        EnsureUtc(startUtc, nameof(startUtc));
        EnsureUtc(endUtc, nameof(endUtc));

        if (endUtc <= startUtc)
        {
            throw new ArgumentException("Availability end time must be later than start time.", nameof(endUtc));
        }

        ResourceId = resourceId;
        StartUtc = startUtc;
        EndUtc = endUtc;
    }

    public bool Covers(Shift shift)
    {
        ArgumentNullException.ThrowIfNull(shift);

        return StartUtc <= shift.StartUtc && EndUtc >= shift.EndUtc;
    }

    private static void EnsureUtc(DateTime value, string paramName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("DateTime value must be in UTC.", paramName);
        }
    }
}
