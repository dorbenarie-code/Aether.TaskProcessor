namespace Aether.Infrastructure.Scheduling.ScheduleGeneration;

public sealed record LocalCleanXlsxScheduleGenerationRequest(
    string InputWorkbookPath,
    string OutputDirectoryPath,
    bool ApplyPostRunLocalAddImprovement,
    IReadOnlyList<IReadOnlyList<string>>? ManualManagerConstraintRows = null);
