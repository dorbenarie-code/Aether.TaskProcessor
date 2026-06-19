namespace Aether.Infrastructure.Scheduling.ScheduleGeneration;

public sealed record LocalCleanXlsxScheduleGenerationResult(
    bool Succeeded,
    LocalCleanXlsxScheduleGenerationFailureType FailureType,
    string Message,
    string? ScheduleTableXlsxPath = null);
