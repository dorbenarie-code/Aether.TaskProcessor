namespace Aether.Domain.Optimization;

public sealed class Resource
{
    public Guid Id { get; }
    public string Name { get; }
    public decimal HourlyCost { get; }
    public ResourceWorkloadCategory WorkloadCategory { get; }

    public Resource(
        Guid id,
        string name,
        decimal hourlyCost,
        ResourceWorkloadCategory workloadCategory = ResourceWorkloadCategory.Full)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Resource id cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Resource name is required.", nameof(name));
        }

        if (hourlyCost < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hourlyCost), "Hourly cost cannot be negative.");
        }

        if (!Enum.IsDefined(typeof(ResourceWorkloadCategory), workloadCategory))
        {
            throw new ArgumentOutOfRangeException(
                nameof(workloadCategory),
                "Resource workload category is not supported.");
        }

        Id = id;
        Name = name.Trim();
        HourlyCost = hourlyCost;
        WorkloadCategory = workloadCategory;
    }
}
