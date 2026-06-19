using System.IO.Compression;
using System.Xml.Linq;
using Aether.Application.Scheduling.Reports;
using Aether.Application.Scheduling.Reports.Exporting;
using Aether.Infrastructure.Scheduling.Reports;
using ClosedXML.Excel;

namespace Aether.Tests.Infrastructure.Scheduling;

public sealed class ScheduleTableXlsxExporterTests
{
    [Fact]
    public void ExportToXlsx_ShouldReturnManagerReadableWorkbookBytes()
    {
        var projection = new ScheduleTableProjection(
            Days:
            [
                new ScheduleTableDayProjection(
                    new DateOnly(2026, 6, 1),
                    DayOfWeek.Monday,
                    MorningWorkerNames: ["Dana"],
                    AfternoonWorkerNames: ["Noa"],
                    NightWorkerNames: ["Ziv"])
            ],
            MorningSlotCount: 1,
            AfternoonSlotCount: 1,
            NightSlotCount: 1)
        {
            MorningTimeRangeText = "06:30-14:20",
            AfternoonTimeRangeText = "14:20-22:40",
            NightTimeRangeText = "22:40-06:30"
        };

        IScheduleTableXlsxExporter exporter = new ScheduleTableXlsxExporter();

        var bytes = exporter.ExportToXlsx(projection);

        Assert.NotEmpty(bytes);

        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);

        var worksheet = workbook.Worksheet("סידור");

        Assert.True(worksheet.RightToLeft);
        Assert.Equal("משמרת", worksheet.Cell(1, 1).GetString());
        Assert.Equal("יום", worksheet.Cell(2, 1).GetString());
        Assert.Equal("בוקר 06:30-14:30", worksheet.Cell(3, 1).GetString());
        Assert.Equal("Dana", worksheet.Cell(3, 2).GetString());
    }


    [Fact]
    public void Export_ShouldWriteManagerReadableScheduleWorkbook()
    {
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            $"aether-schedule-xlsx-export-{Guid.NewGuid():N}");

        Directory.CreateDirectory(tempDirectory);

        var outputPath = Path.Combine(tempDirectory, "schedule.xlsx");

        var projection = new ScheduleTableProjection(
            Days:
            [
                new ScheduleTableDayProjection(
                    new DateOnly(2026, 6, 1),
                    DayOfWeek.Monday,
                    MorningWorkerNames: ["Dana", "Yossi"],
                    AfternoonWorkerNames: ["Noa"],
                    NightWorkerNames: []),
                new ScheduleTableDayProjection(
                    new DateOnly(2026, 6, 2),
                    DayOfWeek.Tuesday,
                    MorningWorkerNames: [],
                    AfternoonWorkerNames: [],
                    NightWorkerNames: ["Dana"])
            ],
            MorningSlotCount: 3,
            AfternoonSlotCount: 2,
            NightSlotCount: 1)
        {
            MorningTimeRangeText = "06:30-14:20",
            AfternoonTimeRangeText = "14:20-22:40",
            NightTimeRangeText = "22:40-06:30"
        };

        try
        {
            new ScheduleTableXlsxExporter()
                .Export(
                    projection,
                    outputPath);

            Assert.True(File.Exists(outputPath));

            using var workbook = new XLWorkbook(outputPath);

            Assert.Contains(
                workbook.Worksheets,
                worksheet => worksheet.Name == "סידור");

            Assert.DoesNotContain(
                workbook.Worksheets,
                worksheet => worksheet.Name == "Schedule");

            var worksheet = workbook.Worksheet("סידור");

            Assert.True(worksheet.RightToLeft);

            Assert.Equal("משמרת", worksheet.Cell(1, 1).GetString());
            Assert.Equal("1", worksheet.Cell(1, 2).GetString());
            Assert.Equal("2", worksheet.Cell(1, 3).GetString());

            Assert.Equal("יום", worksheet.Cell(2, 1).GetString());
            Assert.Equal("שני", worksheet.Cell(2, 2).GetString());
            Assert.Equal("שלWorker19", worksheet.Cell(2, 3).GetString());

            Assert.Equal("בוקר 06:30-14:30", worksheet.Cell(3, 1).GetString());
            Assert.Equal("Dana", worksheet.Cell(3, 2).GetString());
            Assert.Equal("Yossi", worksheet.Cell(4, 2).GetString());
            Assert.Equal(string.Empty, worksheet.Cell(5, 2).GetString());
            Assert.Equal(string.Empty, worksheet.Cell(11, 2).GetString());
            Assert.Equal(string.Empty, worksheet.Cell(3, 3).GetString());

            Assert.Equal("ערב 14:30-22:30", worksheet.Cell(12, 1).GetString());
            Assert.Equal("Noa", worksheet.Cell(12, 2).GetString());
            Assert.Equal(string.Empty, worksheet.Cell(13, 2).GetString());
            Assert.Equal(string.Empty, worksheet.Cell(12, 3).GetString());

            Assert.Equal("לילה 22:40-06:30", worksheet.Cell(17, 1).GetString());
            Assert.Equal(string.Empty, worksheet.Cell(17, 2).GetString());
            Assert.Equal("Dana", worksheet.Cell(17, 3).GetString());

            Assert.Equal(
                XLAlignmentHorizontalValues.Center,
                worksheet.Cell(3, 2).Style.Alignment.Horizontal);

            Assert.Equal(
                XLAlignmentVerticalValues.Center,
                worksheet.Cell(3, 2).Style.Alignment.Vertical);

            Assert.True(worksheet.Column(1).Width >= 22);
            Assert.True(worksheet.Column(2).Width >= 22);
            Assert.True(worksheet.Row(3).Height >= 22);

            Assert.Equal(
                XLBorderStyleValues.Medium,
                worksheet.Cell(3, 1).Style.Border.TopBorder);

            Assert.Equal(
                XLBorderStyleValues.Medium,
                worksheet.Cell(12, 1).Style.Border.TopBorder);

            Assert.Equal(
                XLBorderStyleValues.Medium,
                worksheet.Cell(17, 1).Style.Border.TopBorder);

            Assert.Equal(
                XLColor.FromHtml("#E2F0D9"),
                worksheet.Cell(3, 1).Style.Fill.BackgroundColor);

            Assert.Equal(
                XLColor.FromHtml("#E2F0D9"),
                worksheet.Cell(3, 2).Style.Fill.BackgroundColor);

            Assert.Equal(
                XLColor.FromHtml("#DDEBF7"),
                worksheet.Cell(12, 1).Style.Fill.BackgroundColor);

            Assert.Equal(
                XLColor.FromHtml("#DDEBF7"),
                worksheet.Cell(12, 2).Style.Fill.BackgroundColor);

            Assert.Equal(
                XLColor.FromHtml("#E7E6E6"),
                worksheet.Cell(17, 1).Style.Fill.BackgroundColor);

            Assert.Equal(
                XLColor.FromHtml("#E7E6E6"),
                worksheet.Cell(17, 2).Style.Fill.BackgroundColor);

            AssertWorksheetHasFrozenHeaderRows(outputPath);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }



    [Fact]
    public void Export_ShouldRenderAfternoonOperationalTimeRows()
    {
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            $"aether-schedule-xlsx-export-{Guid.NewGuid():N}");

        Directory.CreateDirectory(tempDirectory);

        var outputPath = Path.Combine(tempDirectory, "schedule.xlsx");

        var projection = new ScheduleTableProjection(
            Days:
            [
                new ScheduleTableDayProjection(
                    new DateOnly(2026, 6, 1),
                    DayOfWeek.Monday,
                    MorningWorkerNames:
                    [
                        "Avi",
                        "Dana",
                        "Gal",
                        "Hila",
                        "Noa",
                        "Yossi"
                    ],
                    AfternoonWorkerNames:
                    [
                        "Rafael",
                        "Maor",
                        "Amir",
                        "Gal"
                    ],
                    NightWorkerNames: ["Ziv"])
            ],
            MorningSlotCount: 6,
            AfternoonSlotCount: 4,
            NightSlotCount: 1)
        {
            MorningTimeRangeText = "06:30-14:20",
            AfternoonTimeRangeText = "14:20-22:40",
            NightTimeRangeText = "22:40-06:30"
        };

        try
        {
            new ScheduleTableXlsxExporter()
                .Export(
                    projection,
                    outputPath);

            using var workbook = new XLWorkbook(outputPath);
            var worksheet = workbook.Worksheet("סידור");

            Assert.Equal("ערב 14:30-22:30", worksheet.Cell(12, 1).GetString());
            Assert.Equal("14:30-22:30", worksheet.Cell(13, 1).GetString());
            Assert.Equal("14:30-22:30", worksheet.Cell(14, 1).GetString());
            Assert.Equal("14:30-22:30", worksheet.Cell(15, 1).GetString());
            Assert.Equal("14:30-22:30", worksheet.Cell(16, 1).GetString());

            Assert.Equal("Rafael", worksheet.Cell(12, 2).GetString());
            Assert.Equal("Maor", worksheet.Cell(13, 2).GetString());
            Assert.Equal("Amir", worksheet.Cell(14, 2).GetString());
            Assert.Equal("Gal", worksheet.Cell(15, 2).GetString());
            Assert.Equal(string.Empty, worksheet.Cell(16, 2).GetString());

            Assert.Equal("לילה 22:40-06:30", worksheet.Cell(17, 1).GetString());
            Assert.Equal("Ziv", worksheet.Cell(17, 2).GetString());

            Assert.Equal(
                XLColor.FromHtml("#DDEBF7"),
                worksheet.Cell(12, 1).Style.Fill.BackgroundColor);

            Assert.Equal(
                XLColor.FromHtml("#DDEBF7"),
                worksheet.Cell(16, 1).Style.Fill.BackgroundColor);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void Export_ShouldRenderWeekdayMorningOperationalTimeRows()
    {
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            $"aether-schedule-xlsx-export-{Guid.NewGuid():N}");

        Directory.CreateDirectory(tempDirectory);

        var outputPath = Path.Combine(tempDirectory, "schedule.xlsx");

        var projection = new ScheduleTableProjection(
            Days:
            [
                new ScheduleTableDayProjection(
                    new DateOnly(2026, 6, 1),
                    DayOfWeek.Monday,
                    MorningWorkerNames:
                    [
                        "Avi",
                        "Dana",
                        "Gal",
                        "Hila",
                        "Noa",
                        "Yossi"
                    ],
                    AfternoonWorkerNames: ["Rafael"],
                    NightWorkerNames: ["Ziv"])
            ],
            MorningSlotCount: 6,
            AfternoonSlotCount: 1,
            NightSlotCount: 1)
        {
            MorningTimeRangeText = "06:30-14:20",
            AfternoonTimeRangeText = "14:20-22:40",
            NightTimeRangeText = "22:40-06:30"
        };

        try
        {
            new ScheduleTableXlsxExporter()
                .Export(
                    projection,
                    outputPath);

            using var workbook = new XLWorkbook(outputPath);
            var worksheet = workbook.Worksheet("סידור");

            Assert.Equal("בוקר 06:30-14:30", worksheet.Cell(3, 1).GetString());
            Assert.Equal("06:30-14:30", worksheet.Cell(4, 1).GetString());
            Assert.Equal("06:30-14:30", worksheet.Cell(5, 1).GetString());
            Assert.Equal("07:00-15:00", worksheet.Cell(6, 1).GetString());
            Assert.Equal("07:30-15:30", worksheet.Cell(7, 1).GetString());
            Assert.Equal("07:50-16:00", worksheet.Cell(8, 1).GetString());
            Assert.Equal("07:50-16:00", worksheet.Cell(9, 1).GetString());
            Assert.Equal("07:50-16:00", worksheet.Cell(10, 1).GetString());
            Assert.Equal("08:00-13:30", worksheet.Cell(11, 1).GetString());

            Assert.Equal("Avi", worksheet.Cell(3, 2).GetString());
            Assert.Equal("Dana", worksheet.Cell(4, 2).GetString());
            Assert.Equal("Gal", worksheet.Cell(5, 2).GetString());
            Assert.Equal("Hila", worksheet.Cell(6, 2).GetString());
            Assert.Equal("Noa", worksheet.Cell(7, 2).GetString());
            Assert.Equal("Yossi", worksheet.Cell(8, 2).GetString());
            Assert.Equal(string.Empty, worksheet.Cell(9, 2).GetString());
            Assert.Equal(string.Empty, worksheet.Cell(10, 2).GetString());
            Assert.Equal(string.Empty, worksheet.Cell(11, 2).GetString());

            Assert.Equal("ערב 14:30-22:30", worksheet.Cell(12, 1).GetString());
            Assert.Equal("Rafael", worksheet.Cell(12, 2).GetString());

            Assert.Equal("לילה 22:40-06:30", worksheet.Cell(17, 1).GetString());
            Assert.Equal("Ziv", worksheet.Cell(17, 2).GetString());

            Assert.Equal(
                XLColor.FromHtml("#E2F0D9"),
                worksheet.Cell(3, 1).Style.Fill.BackgroundColor);

            Assert.Equal(
                XLColor.FromHtml("#E2F0D9"),
                worksheet.Cell(11, 1).Style.Fill.BackgroundColor);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    private static void AssertWorksheetHasFrozenHeaderRows(string outputPath)
    {
        using var archive = ZipFile.OpenRead(outputPath);

        var worksheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml") ??
            throw new InvalidOperationException("Worksheet XML entry was not found.");

        using var worksheetStream = worksheetEntry.Open();

        var document = XDocument.Load(worksheetStream);

        var pane = document
            .Descendants()
            .SingleOrDefault(element => element.Name.LocalName == "pane");

        Assert.NotNull(pane);
        var paneState = pane.Attribute("state")?.Value;

        Assert.True(
            paneState is "frozen" or "frozenSplit",
            $"Unexpected pane state: {paneState ?? "-"}.");

        Assert.Equal("2", pane.Attribute("ySplit")?.Value);
    }
}
