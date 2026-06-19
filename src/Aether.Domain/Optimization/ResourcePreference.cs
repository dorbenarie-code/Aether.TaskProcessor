namespace Aether.Domain.Optimization;

public sealed class ResourcePreference
{
    public Guid ResourceId { get; }
    public DateTime StartUtc { get; }
    public DateTime EndUtc { get; }
    public ResourcePreferenceType Type { get; }
    public ResourcePreferencePriority Priority { get; }

    public ResourcePreference(
        Guid resourceId,
        DateTime startUtc,
        DateTime endUtc,
        ResourcePreferenceType type,
        ResourcePreferencePriority priority)
    {
        if (resourceId == Guid.Empty)
        {
            throw new ArgumentException("Resource id cannot be empty.", nameof(resourceId));
        }

        EnsureUtc(startUtc, nameof(startUtc));
        EnsureUtc(endUtc, nameof(endUtc));

        if (endUtc <= startUtc)
        {
            throw new ArgumentException("Preference end time must be later than start time.", nameof(endUtc));
        }

        if (!Enum.IsDefined(typeof(ResourcePreferenceType), type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), "Resource preference type is not supported.");
        }

        if (!Enum.IsDefined(typeof(ResourcePreferencePriority), priority))
        {
            throw new ArgumentOutOfRangeException(nameof(priority), "Resource preference priority is not supported.");
        }

        ResourceId = resourceId;
        StartUtc = startUtc;
        EndUtc = endUtc;
        Type = type;
        Priority = priority;
    }

    private static void EnsureUtc(DateTime value, string paramName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("DateTime value must be in UTC.", paramName);
        }
    }
}
