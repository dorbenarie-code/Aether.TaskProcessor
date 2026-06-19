namespace Aether.Domain.Optimization;

public sealed class ResourceWorkloadPolicy
{
    public ResourceWorkloadCategory Category { get; }
    public int MinimumRequiredAssignedHours { get; }
    public int? PreferredTargetAssignedHours { get; }
    public bool PenalizeAssignedHoursAbovePreferredTarget { get; }

    public ResourceWorkloadPolicy(
        ResourceWorkloadCategory category,
        int minimumRequiredAssignedHours,
        int? preferredTargetAssignedHours,
        bool penalizeAssignedHoursAbovePreferredTarget)
    {
        if (!Enum.IsDefined(typeof(ResourceWorkloadCategory), category))
        {
            throw new ArgumentOutOfRangeException(
                nameof(category),
                "Resource workload category is not supported.");
        }

        if (minimumRequiredAssignedHours < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumRequiredAssignedHours),
                "Minimum required assigned hours cannot be negative.");
        }

        if (preferredTargetAssignedHours is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(preferredTargetAssignedHours),
                "Preferred target assigned hours cannot be negative.");
        }

        if (preferredTargetAssignedHours < minimumRequiredAssignedHours)
        {
            throw new ArgumentException(
                "Preferred target assigned hours cannot be lower than minimum required assigned hours.",
                nameof(preferredTargetAssignedHours));
        }

        if (penalizeAssignedHoursAbovePreferredTarget && preferredTargetAssignedHours is null)
        {
            throw new ArgumentException(
                "Preferred target assigned hours is required when over-target penalty is enabled.",
                nameof(preferredTargetAssignedHours));
        }

        Category = category;
        MinimumRequiredAssignedHours = minimumRequiredAssignedHours;
        PreferredTargetAssignedHours = preferredTargetAssignedHours;
        PenalizeAssignedHoursAbovePreferredTarget = penalizeAssignedHoursAbovePreferredTarget;
    }

    public static ResourceWorkloadPolicy CreateFullPolicy()
    {
        return new ResourceWorkloadPolicy(
            ResourceWorkloadCategory.Full,
            minimumRequiredAssignedHours: 90,
            preferredTargetAssignedHours: null,
            penalizeAssignedHoursAbovePreferredTarget: false);
    }

    public static ResourceWorkloadPolicy CreateStudentPolicy()
    {
        return new ResourceWorkloadPolicy(
            ResourceWorkloadCategory.Student,
            minimumRequiredAssignedHours: 90,
            preferredTargetAssignedHours: 90,
            penalizeAssignedHoursAbovePreferredTarget: true);
    }

    public static ResourceWorkloadPolicy CreateSpecialPolicy()
    {
        return new ResourceWorkloadPolicy(
            ResourceWorkloadCategory.Special,
            minimumRequiredAssignedHours: 0,
            preferredTargetAssignedHours: 66,
            penalizeAssignedHoursAbovePreferredTarget: true);
    }
}
