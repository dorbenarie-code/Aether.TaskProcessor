using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.SubmissionForms;

public sealed record GoogleFormsImportScopeDiscoveryRequest(
    IReadOnlyList<IReadOnlyList<string>> Rows,
    SchedulePeriod SchedulePeriod,
    DateOnly SubmittedAtFrom,
    DateOnly SubmittedAtTo);
