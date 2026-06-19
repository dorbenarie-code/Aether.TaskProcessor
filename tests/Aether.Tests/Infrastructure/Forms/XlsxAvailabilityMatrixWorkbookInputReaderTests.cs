using Aether.Infrastructure.Forms;
using ClosedXML.Excel;

namespace Aether.Tests.Infrastructure.Forms;

public sealed class XlsxAvailabilityMatrixWorkbookInputReaderTests
{
    [Fact]
    public void Open_ShouldReturnAvailabilityMatrixStream_AndNullManagerConstraintRows_WhenManagerConstraintsSheetIsMissing()
    {
        var path = CreateWorkbookFile(workbook =>
        {
            AddMatrixWorksheet(workbook);
        });

        try
        {
            using var input = new XlsxAvailabilityMatrixWorkbookInputReader()
                .Open(path);

            var matrixRows = new XlsxFormTableReader()
                .Read(input.AvailabilityMatrixStream);

            Assert.Null(input.ManagerConstraintRows);
            Assert.Equal("שם המאבטח", matrixRows[0][0]);
            Assert.Equal("Worker16", matrixRows[1][0]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Open_ShouldReturnNullManagerConstraintRows_WhenManagerConstraintsSheetIsEmpty()
    {
        var path = CreateWorkbookFile(workbook =>
        {
            AddMatrixWorksheet(workbook);
            workbook.Worksheets.Add("ManagerConstraints");
        });

        try
        {
            using var input = new XlsxAvailabilityMatrixWorkbookInputReader()
                .Open(path);

            Assert.Null(input.ManagerConstraintRows);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Open_ShouldReturnManagerConstraintRows_WhenManagerConstraintsSheetHasOnlyHeaders()
    {
        var path = CreateWorkbookFile(workbook =>
        {
            AddMatrixWorksheet(workbook);

            var worksheet = workbook.Worksheets.Add("ManagerConstraints");
            AddManagerConstraintHeaders(worksheet);
        });

        try
        {
            using var input = new XlsxAvailabilityMatrixWorkbookInputReader()
                .Open(path);

            Assert.NotNull(input.ManagerConstraintRows);

            var rows = input.ManagerConstraintRows!;

            Assert.Single(rows);
            Assert.Equal("Type", rows[0][0]);
            Assert.Equal("WorkerName", rows[0][1]);
            Assert.Equal("Date", rows[0][2]);
            Assert.Equal("ShiftKind", rows[0][3]);
            Assert.Equal("MinResourceCount", rows[0][4]);
            Assert.Equal("MaxResourceCount", rows[0][5]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Open_ShouldReturnManagerConstraintRows_WhenManagerConstraintsSheetHasDataRows()
    {
        var path = CreateWorkbookFile(workbook =>
        {
            AddMatrixWorksheet(workbook);

            var worksheet = workbook.Worksheets.Add("ManagerConstraints");
            AddManagerConstraintHeaders(worksheet);

            worksheet.Cell(2, 1).Value = "ForbidAssignment";
            worksheet.Cell(2, 2).Value = "Worker16";
            worksheet.Cell(2, 3).Value = "2026-06-14";
            worksheet.Cell(2, 4).Value = "Morning";
        });

        try
        {
            using var input = new XlsxAvailabilityMatrixWorkbookInputReader()
                .Open(path);

            Assert.NotNull(input.ManagerConstraintRows);

            var rows = input.ManagerConstraintRows!;

            Assert.Equal(2, rows.Count);
            Assert.Equal("ForbidAssignment", rows[1][0]);
            Assert.Equal("Worker16", rows[1][1]);
            Assert.Equal("2026-06-14", rows[1][2]);
            Assert.Equal("Morning", rows[1][3]);
            Assert.Equal(string.Empty, rows[1][4]);
            Assert.Equal(string.Empty, rows[1][5]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateWorkbookFile(
        Action<XLWorkbook> configure)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid():N}.xlsx");

        using var workbook = new XLWorkbook();

        configure(workbook);

        workbook.SaveAs(path);

        return path;
    }

    private static void AddMatrixWorksheet(
        XLWorkbook workbook)
    {
        var worksheet = workbook.Worksheets.Add("Matrix");

        worksheet.Cell(1, 1).Value = "שם המאבטח";
        worksheet.Cell(1, 2).Value = "ראשון - 14/06";

        worksheet.Cell(2, 1).Value = "Worker16";
        worksheet.Cell(2, 2).Value = "בוקר";
    }

    private static void AddManagerConstraintHeaders(
        IXLWorksheet worksheet)
    {
        worksheet.Cell(1, 1).Value = "Type";
        worksheet.Cell(1, 2).Value = "WorkerName";
        worksheet.Cell(1, 3).Value = "Date";
        worksheet.Cell(1, 4).Value = "ShiftKind";
        worksheet.Cell(1, 5).Value = "MinResourceCount";
        worksheet.Cell(1, 6).Value = "MaxResourceCount";
    }
}
