namespace Aether.Application.Scheduling.SubmissionForms;

public interface IAvailabilityMatrixStreamOptimizationRunner
{
    AvailabilityMatrixImportedRowsOptimizationResult Run(
        AvailabilityMatrixStreamOptimizationRequest request);
}
