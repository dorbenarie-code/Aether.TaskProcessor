namespace Aether.Domain.Optimization;

public sealed class ResourceUnavailability
{
    public Guid ResourceId { get; }
    public DateTime StartUtc { get; }
    public DateTime EndUtc { get; }

    public ResourceUnavailability(
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
            throw new ArgumentException("Resource unavailability end time must be later than start time.", nameof(endUtc));
        }

        ResourceId = resourceId;
        StartUtc = startUtc;
        EndUtc = endUtc;
    }

    private static void EnsureUtc(DateTime value, string paramName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("DateTime value must be in UTC.", paramName);
        }
    }
}
