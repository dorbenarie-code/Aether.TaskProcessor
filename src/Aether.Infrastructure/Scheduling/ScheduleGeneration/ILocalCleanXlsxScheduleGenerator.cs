using Aether.Application.Scheduling.ScheduleGeneration;

namespace Aether.Infrastructure.Scheduling.ScheduleGeneration;

public interface ILocalCleanXlsxScheduleGenerator
{
    CleanXlsxScheduleGenerationResult Run(
        CleanXlsxScheduleGenerationRequest request);
}
