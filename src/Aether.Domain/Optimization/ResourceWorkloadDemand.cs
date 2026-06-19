namespace Aether.Domain.Optimization;

public sealed class ResourceWorkloadDemand
{
    public Guid ResourceId { get; }
    public double RequestedPreferredHours { get; }
    public double MinimumRequiredHours { get; }
    public double EffectiveMinimumRequiredHours => Math.Min(MinimumRequiredHours, RequestedPreferredHours);
    public double EffectiveTargetHours => RequestedPreferredHours;

    public ResourceWorkloadDemand(
        Guid resourceId,
        double requestedPreferredHours,
        double minimumRequiredHours)
    {
        if (resourceId == Guid.Empty)
        {
            throw new ArgumentException("Resource id cannot be empty.", nameof(resourceId));
        }

        if (!double.IsFinite(requestedPreferredHours))
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestedPreferredHours),
                "Requested preferred hours must be a finite number.");
        }

        if (!double.IsFinite(minimumRequiredHours))
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumRequiredHours),
                "Minimum required hours must be a finite number.");
        }

        if (requestedPreferredHours < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestedPreferredHours),
                "Requested preferred hours cannot be negative.");
        }

        if (minimumRequiredHours < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumRequiredHours),
                "Minimum required hours cannot be negative.");
        }

        ResourceId = resourceId;
        RequestedPreferredHours = requestedPreferredHours;
        MinimumRequiredHours = minimumRequiredHours;
    }
}
