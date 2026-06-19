namespace Aether.Infrastructure.Scheduling.ScheduleGeneration;

public interface ILocalCleanXlsxWorkbookInputReader
{
    LocalCleanXlsxWorkbookInput Open(string workbookPath);
}
