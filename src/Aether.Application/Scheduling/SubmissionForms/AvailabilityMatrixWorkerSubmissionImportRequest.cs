using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.SubmissionForms;

public sealed record AvailabilityMatrixWorkerSubmissionImportRequest(
    IReadOnlyList<IReadOnlyList<string>> Rows,
    SchedulePeriod SchedulePeriod,
    IReadOnlyList<Resource> Resources,
    IReadOnlyDictionary<string, Guid>? AliasesByWorkerName = null);
