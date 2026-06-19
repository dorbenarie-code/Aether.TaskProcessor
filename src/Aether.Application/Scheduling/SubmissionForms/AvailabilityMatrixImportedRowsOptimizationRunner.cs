using Aether.Application.Scheduling.ManagerConstraints;

namespace Aether.Application.Scheduling.SubmissionForms;

public sealed class AvailabilityMatrixImportedRowsOptimizationRunner
{
    public AvailabilityMatrixImportedRowsOptimizationResult Run(
        AvailabilityMatrixImportedRowsOptimizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var importResult = new AvailabilityMatrixWorkerSubmissionImporter()
            .Import(new AvailabilityMatrixWorkerSubmissionImportRequest(
                request.Rows,
                request.SchedulePeriod,
                request.Resources.ToArray(),
                request.AliasesByWorkerName));

        if (importResult.FatalErrors.Count > 0)
        {
            return new AvailabilityMatrixImportedRowsOptimizationResult(
                importResult.WorkerSubmissions,
                importResult.Warnings,
                importResult.FatalErrors,
                OptimizationResult: null);
        }

        var managerConstraintSet = request.ManagerConstraintSet;
        IReadOnlyList<ManagerConstraintRowsImportWarning> managerConstraintImportWarnings = [];
        IReadOnlyList<ManagerConstraintRowsImportFatalError> managerConstraintImportFatalErrors = [];
        var managerConstraintImportSummary = ManagerConstraintImportSummary.Empty;

        if (request.ManagerConstraintRows is not null)
        {
            var managerConstraintImportResult = new ManagerConstraintRowsImporter()
                .Import(new ManagerConstraintRowsImportRequest(
                    request.ManagerConstraintRows,
                    request.SchedulePeriod,
                    request.Resources.ToArray(),
                    request.Shifts.ToArray(),
                    request.AliasesByWorkerName));

            managerConstraintImportWarnings = managerConstraintImportResult.Warnings;
            managerConstraintImportFatalErrors = managerConstraintImportResult.FatalErrors;
            managerConstraintImportSummary = managerConstraintImportResult.Summary;
            managerConstraintImportSummary = managerConstraintImportResult.Summary;

            if (managerConstraintImportFatalErrors.Count > 0)
            {
                return new AvailabilityMatrixImportedRowsOptimizationResult(
                    importResult.WorkerSubmissions,
                    importResult.Warnings,
                    importResult.FatalErrors,
                    OptimizationResult: null,
                    managerConstraintImportWarnings,
                    managerConstraintImportFatalErrors,
                    managerConstraintImportSummary);
            }

            managerConstraintSet = CombineManagerConstraintSets(
                managerConstraintSet,
                managerConstraintImportResult.ConstraintSet);
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
                managerConstraintSet,
                request.ApplyPostRunLocalAddImprovement));

        return new AvailabilityMatrixImportedRowsOptimizationResult(
            importResult.WorkerSubmissions,
            importResult.Warnings,
            importResult.FatalErrors,
            optimizationResult,
            managerConstraintImportWarnings,
            managerConstraintImportFatalErrors,
            managerConstraintImportSummary);
    }

    private static ManagerConstraintSet? CombineManagerConstraintSets(
        ManagerConstraintSet? existingConstraintSet,
        ManagerConstraintSet importedConstraintSet)
    {
        ArgumentNullException.ThrowIfNull(importedConstraintSet);

        if (existingConstraintSet is null)
        {
            return importedConstraintSet;
        }

        return new ManagerConstraintSet(
            existingConstraintSet.ForbiddenAssignments
                .Concat(importedConstraintSet.ForbiddenAssignments)
                .ToArray(),
            existingConstraintSet.ShiftCapacityOverrides
                .Concat(importedConstraintSet.ShiftCapacityOverrides)
                .ToArray(),
            existingConstraintSet.AvoidAssignments
                .Concat(importedConstraintSet.AvoidAssignments)
                .ToArray());
    }
}
