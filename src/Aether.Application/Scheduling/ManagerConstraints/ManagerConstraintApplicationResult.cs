using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.ManagerConstraints;

public sealed record ManagerConstraintApplicationResult(
    IReadOnlyCollection<AvailabilityWindow> AvailabilityWindows,
    IReadOnlyCollection<ResourcePreference> ResourcePreferences);
