namespace Aether.Application.Scheduling.Contracts;

public sealed record ResourceLoadSummary(
    Guid ResourceId,
    string ResourceName,
    double AssignedHours,
    int AssignmentCount);
