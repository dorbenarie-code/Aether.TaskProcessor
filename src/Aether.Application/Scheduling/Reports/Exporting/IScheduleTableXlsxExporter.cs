using Aether.Application.Scheduling.Reports;

namespace Aether.Application.Scheduling.Reports.Exporting;

public interface IScheduleTableXlsxExporter
{
    byte[] ExportToXlsx(ScheduleTableProjection projection);
}
