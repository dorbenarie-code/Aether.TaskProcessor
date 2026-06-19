namespace Aether.Application.Scheduling.SubmissionForms;

public sealed record GoogleFormsWorkerSubmissionAssemblyResult(
    IReadOnlyList<WorkerSubmission> WorkerSubmissions,
    IReadOnlyList<GoogleFormsImportWarning> Warnings);
