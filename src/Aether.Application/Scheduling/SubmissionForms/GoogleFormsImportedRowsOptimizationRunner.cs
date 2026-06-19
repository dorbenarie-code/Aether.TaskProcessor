namespace Aether.Application.Scheduling.SubmissionForms;

public sealed class GoogleFormsImportedRowsOptimizationRunner
{
    public GoogleFormsImportedRowsOptimizationResult Run(
        GoogleFormsImportedRowsOptimizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var importResult = new GoogleFormsWorkerSubmissionImporter()
            .Import(new GoogleFormsWorkerSubmissionImportRequest(
                request.Rows,
                request.SchedulePeriod,
                request.SubmittedAtFrom,
                request.SubmittedAtTo,
                request.Resources.ToArray(),
                request.AliasesByWorkerName));

        if (importResult.FatalErrors.Count > 0)
        {
            return new GoogleFormsImportedRowsOptimizationResult(
                importResult.WorkerSubmissions,
                importResult.Warnings,
                importResult.FatalErrors,
                OptimizationResult: null);
        }

        var optimizationResult = new ClosedFormSubmissionOptimizationRunner()
            .Run(new ClosedFormSubmissionOptimizationRequest(
                request.SchedulePeriod,
                request.Resources,
                request.Shifts,
                importResult.WorkerSubmissions,
                request.TotalEffectiveTargetHours,
                request.MaximumAssignedHoursDeviationFromAverageHours,
                request.Seed,
                request.ResourceMonthlyNightShiftHistories,
                request.ManagerConstraintSet));

        return new GoogleFormsImportedRowsOptimizationResult(
            importResult.WorkerSubmissions,
            importResult.Warnings,
            importResult.FatalErrors,
            optimizationResult);
    }
}
