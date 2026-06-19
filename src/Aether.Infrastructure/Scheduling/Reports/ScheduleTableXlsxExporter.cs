using System.Globalization;
using Aether.Application.Scheduling.Reports;
using Aether.Application.Scheduling.Reports.Exporting;
using ClosedXML.Excel;

namespace Aether.Infrastructure.Scheduling.Reports;

public sealed class ScheduleTableXlsxExporter : IScheduleTableXlsxExporter
{
    private const string WorksheetName = "סידור";
    private const int HeaderRowCount = 2;
    private const string MorningBlockFillColor = "#E2F0D9";
    private const string AfternoonBlockFillColor = "#DDEBF7";
    private const string NightBlockFillColor = "#E7E6E6";

    private static readonly string[] WeekdayMorningOperationalTimeRows =
    [
        "06:30-14:30",
        "06:30-14:30",
        "06:30-14:30",
        "07:00-15:00",
        "07:30-15:30",
        "07:50-16:00",
        "07:50-16:00",
        "07:50-16:00",
        "08:00-13:30"
    ];

    private static readonly string[] AfternoonOperationalTimeRows =
    [
        "14:30-22:30",
        "14:30-22:30",
        "14:30-22:30",
        "14:30-22:30",
        "14:30-22:30"
    ];

    public void Export(
        ScheduleTableProjection projection,
        string outputPath)
    {
        ArgumentNullException.ThrowIfNull(projection);

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Output path is required.", nameof(outputPath));
        }

        var outputDirectory = Path.GetDirectoryName(outputPath);

        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        File.WriteAllBytes(
            outputPath,
            ExportToXlsx(projection));
    }

    public byte[] ExportToXlsx(
        ScheduleTableProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        using var workbook = CreateWorkbook(projection);
        using var stream = new MemoryStream();

        workbook.SaveAs(stream);

        return stream.ToArray();
    }

    private static XLWorkbook CreateWorkbook(
        ScheduleTableProjection projection)
    {
        var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(WorksheetName);

        worksheet.RightToLeft = true;

        WriteHeaders(worksheet, projection);

        var morningStartRow = HeaderRowCount + 1;
        var morningRowCount = GetMorningOperationalRowCount(projection);

        WriteShiftBlock(
            worksheet,
            morningStartRow,
            "בוקר",
            morningRowCount,
            projection.Days,
            day => day.MorningWorkerNames,
            CreateMorningOperationalRowLabel);

        var afternoonStartRow = morningStartRow + morningRowCount;
        var afternoonRowCount = GetAfternoonOperationalRowCount(projection);

        WriteShiftBlock(
            worksheet,
            afternoonStartRow,
            "ערב",
            afternoonRowCount,
            projection.Days,
            day => day.AfternoonWorkerNames,
            CreateAfternoonOperationalRowLabel);

        var nightStartRow = afternoonStartRow + afternoonRowCount;

        WriteShiftBlock(
            worksheet,
            nightStartRow,
            CreateShiftLabel("לילה", projection.NightTimeRangeText),
            projection.NightSlotCount,
            projection.Days,
            day => day.NightWorkerNames);

        ApplyBasicFormatting(
            worksheet,
            projection,
            morningStartRow,
            afternoonStartRow,
            nightStartRow);

        return workbook;
    }

    private static void WriteHeaders(
        IXLWorksheet worksheet,
        ScheduleTableProjection projection)
    {
        worksheet.Cell(1, 1).Value = "משמרת";
        worksheet.Cell(2, 1).Value = "יום";

        for (var dayIndex = 0; dayIndex < projection.Days.Count; dayIndex++)
        {
            var column = dayIndex + 2;
            var day = projection.Days[dayIndex];

            worksheet.Cell(1, column).Value = FormatDateHeader(day.Date);
            worksheet.Cell(2, column).Value = FormatDayOfWeek(day.DayOfWeek);
        }
    }

    private static void WriteShiftBlock(
        IXLWorksheet worksheet,
        int startRow,
        string label,
        int slotCount,
        IReadOnlyList<ScheduleTableDayProjection> days,
        Func<ScheduleTableDayProjection, IReadOnlyList<string>> workerNamesSelector,
        Func<int, string, string>? rowLabelFactory = null)
    {
        if (slotCount <= 0)
        {
            return;
        }

        for (var slotIndex = 0; slotIndex < slotCount; slotIndex++)
        {
            var row = startRow + slotIndex;
            var rowLabel = rowLabelFactory is null
                ? slotIndex == 0 ? label : string.Empty
                : rowLabelFactory(slotIndex, label);

            worksheet.Cell(row, 1).Value = rowLabel;

            for (var dayIndex = 0; dayIndex < days.Count; dayIndex++)
            {
                var workers = workerNamesSelector(days[dayIndex]);
                var value = slotIndex < workers.Count
                    ? workers[slotIndex]
                    : string.Empty;

                worksheet.Cell(row, dayIndex + 2).Value = value;
            }
        }
    }

    private static void ApplyBasicFormatting(
        IXLWorksheet worksheet,
        ScheduleTableProjection projection,
        int morningStartRow,
        int afternoonStartRow,
        int nightStartRow)
    {
        var lastColumn = projection.Days.Count + 1;
        var lastRow =
            HeaderRowCount +
            GetMorningOperationalRowCount(projection) +
            GetAfternoonOperationalRowCount(projection) +
            projection.NightSlotCount;

        var usedRange = worksheet.Range(1, 1, lastRow, lastColumn);

        usedRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        usedRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        usedRange.Style.Alignment.WrapText = true;
        usedRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        usedRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        worksheet.Range(1, 1, HeaderRowCount, lastColumn).Style.Font.Bold = true;

        worksheet.Column(1).Width = 24;

        for (var column = 2; column <= lastColumn; column++)
        {
            worksheet.Column(column).Width = 22;
        }

        for (var row = 1; row <= lastRow; row++)
        {
            worksheet.Row(row).Height = row <= HeaderRowCount
                ? 24
                : 22;
        }

        var morningRowCount = GetMorningOperationalRowCount(projection);

        ApplyShiftBlockHeaderStyle(
            worksheet,
            morningStartRow,
            morningRowCount,
            lastColumn,
            XLColor.FromHtml(MorningBlockFillColor));

        ApplyOperationalTimeLabelStyle(
            worksheet,
            morningStartRow,
            morningRowCount,
            XLColor.FromHtml(MorningBlockFillColor));

        var afternoonRowCount = GetAfternoonOperationalRowCount(projection);

        ApplyShiftBlockHeaderStyle(
            worksheet,
            afternoonStartRow,
            afternoonRowCount,
            lastColumn,
            XLColor.FromHtml(AfternoonBlockFillColor));

        ApplyOperationalTimeLabelStyle(
            worksheet,
            afternoonStartRow,
            afternoonRowCount,
            XLColor.FromHtml(AfternoonBlockFillColor));

        ApplyShiftBlockHeaderStyle(
            worksheet,
            nightStartRow,
            projection.NightSlotCount,
            lastColumn,
            XLColor.FromHtml(NightBlockFillColor));

        worksheet.SheetView.FreezeRows(HeaderRowCount);
    }

    private static void ApplyShiftBlockHeaderStyle(
        IXLWorksheet worksheet,
        int startRow,
        int slotCount,
        int lastColumn,
        XLColor fillColor)
    {
        if (slotCount <= 0)
        {
            return;
        }

        var blockHeaderRange = worksheet.Range(startRow, 1, startRow, lastColumn);

        blockHeaderRange.Style.Font.Bold = true;
        blockHeaderRange.Style.Fill.BackgroundColor = fillColor;
        blockHeaderRange.Style.Border.TopBorder = XLBorderStyleValues.Medium;
    }

    private static int GetMorningOperationalRowCount(
        ScheduleTableProjection projection)
    {
        if (projection.MorningSlotCount <= 0)
        {
            return 0;
        }

        return Math.Max(
            projection.MorningSlotCount,
            WeekdayMorningOperationalTimeRows.Length);
    }

    private static string CreateMorningOperationalRowLabel(
        int slotIndex,
        string label)
    {
        if (slotIndex >= WeekdayMorningOperationalTimeRows.Length)
        {
            return string.Empty;
        }

        var timeRange = WeekdayMorningOperationalTimeRows[slotIndex];

        return slotIndex == 0
            ? $"{label} {timeRange}"
            : timeRange;
    }

    private static int GetAfternoonOperationalRowCount(
        ScheduleTableProjection projection)
    {
        if (projection.AfternoonSlotCount <= 0)
        {
            return 0;
        }

        return Math.Max(
            projection.AfternoonSlotCount,
            AfternoonOperationalTimeRows.Length);
    }

    private static string CreateAfternoonOperationalRowLabel(
        int slotIndex,
        string label)
    {
        if (slotIndex >= AfternoonOperationalTimeRows.Length)
        {
            return string.Empty;
        }

        var timeRange = AfternoonOperationalTimeRows[slotIndex];

        return slotIndex == 0
            ? $"{label} {timeRange}"
            : timeRange;
    }

    private static void ApplyOperationalTimeLabelStyle(
        IXLWorksheet worksheet,
        int startRow,
        int rowCount,
        XLColor fillColor)
    {
        if (rowCount <= 0)
        {
            return;
        }

        worksheet.Range(startRow, 1, startRow + rowCount - 1, 1)
            .Style.Fill.BackgroundColor = fillColor;
    }

    private static string CreateShiftLabel(
        string label,
        string timeRangeText)
    {
        return string.IsNullOrWhiteSpace(timeRangeText)
            ? label
            : $"{label} {timeRangeText}";
    }

    private static string FormatDateHeader(DateOnly date)
    {
        return date.Day.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatDayOfWeek(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Sunday => "ראשון",
            DayOfWeek.Monday => "שני",
            DayOfWeek.Tuesday => "שלWorker19",
            DayOfWeek.Wednesday => "רביעי",
            DayOfWeek.Thursday => "חמWorker19",
            DayOfWeek.Friday => "שWorker19",
            DayOfWeek.Saturday => "שבת",
            _ => throw new ArgumentOutOfRangeException(nameof(dayOfWeek))
        };
    }
}
