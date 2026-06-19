namespace Aether.Application.Scheduling.SubmissionForms;

public sealed record GoogleFormsLatestWorkerSubmissionRowSelectionResult(
    IReadOnlyList<GoogleFormsResolvedWorkerRow> AcceptedRows,
    IReadOnlyList<GoogleFormsImportWarning> Warnings);
