using Aether.Infrastructure.Forms;

namespace Aether.Infrastructure.Scheduling.ScheduleGeneration;

public sealed class LocalCleanXlsxWorkbookInputReaderAdapter : ILocalCleanXlsxWorkbookInputReader
{
    public LocalCleanXlsxWorkbookInput Open(string workbookPath)
    {
        var workbookInput = new XlsxAvailabilityMatrixWorkbookInputReader()
            .Open(workbookPath);

        return new LocalCleanXlsxWorkbookInput(
            workbookInput.AvailabilityMatrixStream,
            workbookInput.ManagerConstraintRows);
    }
}
