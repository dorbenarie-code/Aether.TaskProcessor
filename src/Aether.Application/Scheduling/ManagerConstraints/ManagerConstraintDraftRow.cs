using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.ManagerConstraints;

public sealed class ManagerConstraintDraftRow
{
    public ManagerConstraintDraftType? Type { get; set; }

    public string? WorkerName { get; set; }

    public DateTime? Date { get; set; }

    public ShiftKind? ShiftKind { get; set; }

    public int? MinResourceCount { get; set; }

    public int? MaxResourceCount { get; set; }
}
