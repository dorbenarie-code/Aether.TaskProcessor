namespace Aether.Application.Scheduling.SubmissionForms;

public sealed class AvailabilityMatrixStreamOptimizationRunner : IAvailabilityMatrixStreamOptimizationRunner
{
    private readonly IFormTableReader tableReader;

    public AvailabilityMatrixStreamOptimizationRunner(
        IFormTableReader tableReader)
    {
        this.tableReader = tableReader ??
            throw new ArgumentNullException(nameof(tableReader));
    }

    public AvailabilityMatrixImportedRowsOptimizationResult Run(
        AvailabilityMatrixStreamOptimizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rows = tableReader.Read(request.InputStream);

        return new AvailabilityMatrixImportedRowsOptimizationRunner()
            .Run(new AvailabilityMatrixImportedRowsOptimizationRequest(
                rows,
                request.SchedulePeriod,
                request.Resources,
                request.Shifts,
                request.TotalEffectiveTargetHours,
                request.MaximumAssignedHoursDeviationFromAverageHours,
                request.Seed,
                request.AliasesByWorkerName,
                request.ResourceMonthlyNightShiftHistories,
                request.ManagerConstraintSet,
                request.ManagerConstraintRows,
            ApplyPostRunLocalAddImprovement: request.ApplyPostRunLocalAddImprovement));
    }
}
