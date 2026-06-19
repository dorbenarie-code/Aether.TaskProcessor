using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.SubmissionForms;

public sealed record GoogleFormsWorkerSubmissionImportRequest(
    IReadOnlyList<IReadOnlyList<string>> Rows,
    SchedulePeriod SchedulePeriod,
    DateOnly SubmittedAtFrom,
    DateOnly SubmittedAtTo,
    IReadOnlyList<Resource> Resources,
    IReadOnlyDictionary<string, Guid>? AliasesByWorkerName = null);
