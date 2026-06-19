using Aether.Application.Scheduling.SubmissionForms;
using Aether.Infrastructure.Forms;
using ClosedXML.Excel;

namespace Aether.Tests.Infrastructure.Forms;

public sealed class XlsxFormTableReaderTests
{
    [Fact]
    public void Read_ShouldReturnRowsAndCells_FromXlsxWorksheet()
    {
        using var stream = CreateWorkbookStream(workbook =>
        {
            var worksheet = workbook.Worksheets.Add("Responses");

            worksheet.Cell(1, 1).Value = "חותמת זמן";
            worksheet.Cell(1, 2).Value = "שם המאבטח";
            worksheet.Cell(1, 3).Value = "שבוע ראשון [ראשון - 31/05]";

            worksheet.Cell(2, 1).Value = "5/25/2026 08:00:00";
            worksheet.Cell(2, 2).Value = "Worker16 אלדר";
            worksheet.Cell(2, 3).Value = "בוקר";

            worksheet.Cell(3, 1).Value = "5/25/2026 09:00:00";
            worksheet.Cell(3, 2).Value = "Worker14";
            worksheet.Cell(3, 3).Value = "ערב";
        });

        IFormTableReader reader = new XlsxFormTableReader();

        var rows = reader.Read(stream);

        Assert.Equal(3, rows.Count);

        Assert.Equal("חותמת זמן", rows[0][0]);
        Assert.Equal("שם המאבטח", rows[0][1]);
        Assert.Equal("שבוע ראשון [ראשון - 31/05]", rows[0][2]);

        Assert.Equal("5/25/2026 08:00:00", rows[1][0]);
        Assert.Equal("Worker16 אלדר", rows[1][1]);
        Assert.Equal("בוקר", rows[1][2]);

        Assert.Equal("5/25/2026 09:00:00", rows[2][0]);
        Assert.Equal("Worker14", rows[2][1]);
        Assert.Equal("ערב", rows[2][2]);
    }

    [Fact]
    public void Read_ShouldReturnEmptyString_ForEmptyCellsInsideUsedRange()
    {
        using var stream = CreateWorkbookStream(workbook =>
        {
            var worksheet = workbook.Worksheets.Add("Responses");

            worksheet.Cell(1, 1).Value = "A";
            worksheet.Cell(1, 2).Value = "B";
            worksheet.Cell(1, 3).Value = "C";

            worksheet.Cell(2, 1).Value = "left";
            worksheet.Cell(2, 3).Value = "right";
        });

        var reader = new XlsxFormTableReader();

        var rows = reader.Read(stream);

        Assert.Equal(2, rows.Count);
        Assert.Equal(3, rows[1].Count);

        Assert.Equal("left", rows[1][0]);
        Assert.Equal(string.Empty, rows[1][1]);
        Assert.Equal("right", rows[1][2]);
    }

    [Fact]
    public void Read_ShouldReturnEmptyTable_WhenWorksheetIsEmpty()
    {
        using var stream = CreateWorkbookStream(workbook =>
        {
            workbook.Worksheets.Add("Responses");
        });

        var reader = new XlsxFormTableReader();

        var rows = reader.Read(stream);

        Assert.Empty(rows);
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
