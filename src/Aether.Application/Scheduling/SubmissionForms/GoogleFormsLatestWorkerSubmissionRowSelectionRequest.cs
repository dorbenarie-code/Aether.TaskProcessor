namespace Aether.Application.Scheduling.SubmissionForms;

public sealed record GoogleFormsLatestWorkerSubmissionRowSelectionRequest(
    IReadOnlyList<IReadOnlyList<string>> Rows,
    GoogleFormsImportScopeDiscoveryResult Scope,
    IReadOnlyList<GoogleFormsResolvedWorkerRow> ResolvedRows);
