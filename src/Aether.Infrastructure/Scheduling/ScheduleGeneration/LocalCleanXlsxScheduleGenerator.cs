using Aether.Application.Scheduling.ScheduleGeneration;
using Aether.Application.Scheduling.SubmissionForms;
using Aether.Infrastructure.Forms;
using Aether.Infrastructure.Scheduling.Reports;

namespace Aether.Infrastructure.Scheduling.ScheduleGeneration;

public sealed class LocalCleanXlsxScheduleGenerator : ILocalCleanXlsxScheduleGenerator
{
    private readonly CleanXlsxScheduleGenerationUseCase useCase;

    public LocalCleanXlsxScheduleGenerator()
        : this(new CleanXlsxScheduleGenerationUseCase(
            new AvailabilityMatrixStreamOptimizationRunner(
                new XlsxFormTableReader()),
            new ScheduleTableXlsxExporter()))
    {
    }

    public LocalCleanXlsxScheduleGenerator(
        CleanXlsxScheduleGenerationUseCase useCase)
    {
        this.useCase = useCase ??
            throw new ArgumentNullException(nameof(useCase));
    }

    public CleanXlsxScheduleGenerationResult Run(
        CleanXlsxScheduleGenerationRequest request)
    {
        return useCase.Run(request);
    }
}
