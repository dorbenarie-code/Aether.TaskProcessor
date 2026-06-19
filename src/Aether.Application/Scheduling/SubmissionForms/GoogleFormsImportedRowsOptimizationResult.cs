namespace Aether.Application.Scheduling.SubmissionForms;

public sealed record GoogleFormsImportedRowsOptimizationResult(
    IReadOnlyList<WorkerSubmission> ImportedWorkerSubmissions,
    IReadOnlyList<GoogleFormsImportWarning> ImportWarnings,
    IReadOnlyList<GoogleFormsImportFatalError> ImportFatalErrors,
    ClosedFormSubmissionOptimizationResult? OptimizationResult);
