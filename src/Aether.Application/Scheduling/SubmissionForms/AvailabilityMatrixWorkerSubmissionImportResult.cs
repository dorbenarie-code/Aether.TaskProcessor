namespace Aether.Application.Scheduling.SubmissionForms;

public sealed record AvailabilityMatrixWorkerSubmissionImportResult(
    IReadOnlyList<WorkerSubmission> WorkerSubmissions,
    IReadOnlyList<AvailabilityMatrixImportWarning> Warnings,
    IReadOnlyList<AvailabilityMatrixImportFatalError> FatalErrors);
