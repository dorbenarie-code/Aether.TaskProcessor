namespace Aether.Application.Scheduling.SubmissionForms;

public sealed record GoogleFormsImportScopeDiscoveryResult(
    int TimestampColumnIndex,
    int WorkerNameColumnIndex,
    IReadOnlyList<ScheduleDateColumnMapping> ScheduleDateColumns,
    IReadOnlyList<int> SelectedRowIndexes,
    IReadOnlyList<GoogleFormsImportWarning> Warnings,
    IReadOnlyList<GoogleFormsImportFatalError> FatalErrors);
