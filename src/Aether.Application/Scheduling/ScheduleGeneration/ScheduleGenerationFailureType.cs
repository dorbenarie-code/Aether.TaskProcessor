namespace Aether.Application.Scheduling.ScheduleGeneration;

public enum ScheduleGenerationFailureType
{
    None = 0,
    AvailabilityMatrixImportFailed = 1,
    ManagerConstraintImportFailed = 2,
    OptimizationResultMissing = 3
}
