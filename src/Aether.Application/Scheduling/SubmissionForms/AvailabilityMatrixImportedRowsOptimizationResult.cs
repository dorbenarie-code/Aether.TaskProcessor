using Aether.Application.Scheduling.ManagerConstraints;

namespace Aether.Application.Scheduling.SubmissionForms;

public sealed record AvailabilityMatrixImportedRowsOptimizationResult
{
    public IReadOnlyList<WorkerSubmission> ImportedWorkerSubmissions { get; }
    public IReadOnlyList<AvailabilityMatrixImportWarning> ImportWarnings { get; }
    public IReadOnlyList<AvailabilityMatrixImportFatalError> ImportFatalErrors { get; }
    public ClosedFormSubmissionOptimizationResult? OptimizationResult { get; }
    public IReadOnlyList<ManagerConstraintRowsImportWarning> ManagerConstraintImportWarnings { get; }
    public IReadOnlyList<ManagerConstraintRowsImportFatalError> ManagerConstraintImportFatalErrors { get; }
    public ManagerConstraintImportSummary ManagerConstraintImportSummary { get; }

    public AvailabilityMatrixImportedRowsOptimizationResult(
        IReadOnlyList<WorkerSubmission> ImportedWorkerSubmissions,
        IReadOnlyList<AvailabilityMatrixImportWarning> ImportWarnings,
        IReadOnlyList<AvailabilityMatrixImportFatalError> ImportFatalErrors,
        ClosedFormSubmissionOptimizationResult? OptimizationResult,
        IReadOnlyList<ManagerConstraintRowsImportWarning>? ManagerConstraintImportWarnings = null,
        IReadOnlyList<ManagerConstraintRowsImportFatalError>? ManagerConstraintImportFatalErrors = null,
        ManagerConstraintImportSummary? managerConstraintImportSummary = null)
    {
        ArgumentNullException.ThrowIfNull(ImportedWorkerSubmissions);
        ArgumentNullException.ThrowIfNull(ImportWarnings);
        ArgumentNullException.ThrowIfNull(ImportFatalErrors);

        this.ImportedWorkerSubmissions = ImportedWorkerSubmissions.ToArray();
        this.ImportWarnings = ImportWarnings.ToArray();
        this.ImportFatalErrors = ImportFatalErrors.ToArray();
        this.OptimizationResult = OptimizationResult;
        this.ManagerConstraintImportWarnings = (ManagerConstraintImportWarnings ??
            Array.Empty<ManagerConstraintRowsImportWarning>()).ToArray();
        this.ManagerConstraintImportFatalErrors = (ManagerConstraintImportFatalErrors ??
            Array.Empty<ManagerConstraintRowsImportFatalError>()).ToArray();
        this.ManagerConstraintImportSummary = managerConstraintImportSummary ??
            ManagerConstraintImportSummary.Empty;
    }
}
