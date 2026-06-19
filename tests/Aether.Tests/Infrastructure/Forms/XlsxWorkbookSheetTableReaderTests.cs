using Aether.Infrastructure.Forms;
using ClosedXML.Excel;

namespace Aether.Tests.Infrastructure.Forms;

public sealed class XlsxWorkbookSheetTableReaderTests
{
    [Fact]
    public void ReadOptionalSheet_ShouldReturnRows_FromNamedWorksheet()
    {
        using var stream = CreateWorkbookStream(workbook =>
        {
            var availabilityWorksheet = workbook.Worksheets.Add("Sheet1");
            availabilityWorksheet.Cell(1, 1).Value = "AvailabilityHeader";
            availabilityWorksheet.Cell(2, 1).Value = "ShouldNotBeRead";

            var managerWorksheet = workbook.Worksheets.Add("ManagerConstraints");
            managerWorksheet.Cell(1, 1).Value = "Type";
            managerWorksheet.Cell(1, 2).Value = "WorkerName";
            managerWorksheet.Cell(1, 3).Value = "Date";
            managerWorksheet.Cell(1, 4).Value = "ShiftKind";
            managerWorksheet.Cell(1, 5).Value = "MinResourceCount";
            managerWorksheet.Cell(1, 6).Value = "MaxResourceCount";

            managerWorksheet.Cell(2, 1).Value = "ForbidAssignment";
            managerWorksheet.Cell(2, 2).Value = "דור";
            managerWorksheet.Cell(2, 3).Value = "2026-06-15";
            managerWorksheet.Cell(2, 4).Value = "Morning";
        });

        var reader = new XlsxWorkbookSheetTableReader();

        var result = reader.ReadOptionalSheet(stream, "ManagerConstraints");

        Assert.True(result.SheetFound);
        Assert.Equal(2, result.Rows.Count);

        Assert.Equal("Type", result.Rows[0][0]);
        Assert.Equal("WorkerName", result.Rows[0][1]);

        Assert.Equal("ForbidAssignment", result.Rows[1][0]);
        Assert.Equal("דור", result.Rows[1][1]);
        Assert.Equal("2026-06-15", result.Rows[1][2]);
        Assert.Equal("Morning", result.Rows[1][3]);
    }

    [Fact]
    public void ReadOptionalSheet_ShouldReturnSheetFoundFalse_WhenWorksheetDoesNotExist()
    {
        using var stream = CreateWorkbookStream(workbook =>
        {
            var worksheet = workbook.Worksheets.Add("Sheet1");
            worksheet.Cell(1, 1).Value = "AvailabilityHeader";
        });

        var reader = new XlsxWorkbookSheetTableReader();

        var result = reader.ReadOptionalSheet(stream, "ManagerConstraints");

        Assert.False(result.SheetFound);
        Assert.Empty(result.Rows);
    }

    [Fact]
    public void ReadOptionalSheet_ShouldReturnEmptyRows_WhenNamedWorksheetIsEmpty()
    {
        using var stream = CreateWorkbookStream(workbook =>
        {
            workbook.Worksheets.Add("ManagerConstraints");
        });

        var reader = new XlsxWorkbookSheetTableReader();

        var result = reader.ReadOptionalSheet(stream, "ManagerConstraints");

        Assert.True(result.SheetFound);
        Assert.Empty(result.Rows);
    }

    [Fact]
    public void ReadOptionalSheet_ShouldReturnEmptyString_ForEmptyCellsInsideUsedRange()
    {
        using var stream = CreateWorkbookStream(workbook =>
        {
            var worksheet = workbook.Worksheets.Add("ManagerConstraints");

            worksheet.Cell(1, 1).Value = "Type";
            worksheet.Cell(1, 2).Value = "WorkerName";
            worksheet.Cell(1, 3).Value = "Date";
            worksheet.Cell(1, 4).Value = "ShiftKind";
            worksheet.Cell(1, 5).Value = "MinResourceCount";
            worksheet.Cell(1, 6).Value = "MaxResourceCount";

            worksheet.Cell(2, 1).Value = "ShiftCapacityOverride";
            worksheet.Cell(2, 3).Value = "2026-06-18";
            worksheet.Cell(2, 4).Value = "Night";
            worksheet.Cell(2, 5).Value = "2";
            worksheet.Cell(2, 6).Value = "2";
        });

        var reader = new XlsxWorkbookSheetTableReader();

        var result = reader.ReadOptionalSheet(stream, "ManagerConstraints");

        Assert.True(result.SheetFound);
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(6, result.Rows[1].Count);

        Assert.Equal("ShiftCapacityOverride", result.Rows[1][0]);
        Assert.Equal(string.Empty, result.Rows[1][1]);
        Assert.Equal("2026-06-18", result.Rows[1][2]);
        Assert.Equal("Night", result.Rows[1][3]);
        Assert.Equal("2", result.Rows[1][4]);
        Assert.Equal("2", result.Rows[1][5]);
    }

    [Fact]
    public void ReadOptionalSheet_ShouldUseExactWorksheetName()
    {
        using var stream = CreateWorkbookStream(workbook =>
        {
            var worksheet = workbook.Worksheets.Add("managerconstraints");
            worksheet.Cell(1, 1).Value = "Type";
        });

        var reader = new XlsxWorkbookSheetTableReader();

        var result = reader.ReadOptionalSheet(stream, "ManagerConstraints");

        Assert.False(result.SheetFound);
        Assert.Empty(result.Rows);
    }

    private static MemoryStream CreateWorkbookStream(
        Action<XLWorkbook> configure)
    {
        var stream = new MemoryStream();

        using (var workbook = new XLWorkbook())
        {
            configure(workbook);
            workbook.SaveAs(stream);
        }

        stream.Position = 0;

        return stream;
    }
}
