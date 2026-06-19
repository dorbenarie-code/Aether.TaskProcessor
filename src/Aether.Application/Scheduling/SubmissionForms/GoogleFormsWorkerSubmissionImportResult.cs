namespace Aether.Application.Scheduling.SubmissionForms;

public sealed record GoogleFormsWorkerSubmissionImportResult(
    IReadOnlyList<WorkerSubmission> WorkerSubmissions,
    IReadOnlyList<GoogleFormsImportWarning> Warnings,
    IReadOnlyList<GoogleFormsImportFatalError> FatalErrors);
