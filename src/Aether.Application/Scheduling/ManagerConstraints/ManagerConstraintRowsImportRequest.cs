using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.ManagerConstraints;

public sealed record ManagerConstraintRowsImportRequest(
    IReadOnlyList<IReadOnlyList<string>> Rows,
    SchedulePeriod SchedulePeriod,
    IReadOnlyList<Resource> Resources,
    IReadOnlyList<Shift> Shifts,
    IReadOnlyDictionary<string, Guid>? AliasesByWorkerName = null);
