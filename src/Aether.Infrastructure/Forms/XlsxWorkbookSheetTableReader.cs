using ClosedXML.Excel;

namespace Aether.Infrastructure.Forms;

public sealed record XlsxWorksheetTableReadResult(
    bool SheetFound,
    IReadOnlyList<IReadOnlyList<string>> Rows);

public sealed class XlsxWorkbookSheetTableReader
{
    public XlsxWorksheetTableReadResult ReadOptionalSheet(
        Stream stream,
        string worksheetName)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (string.IsNullOrWhiteSpace(worksheetName))
        {
            throw new ArgumentException(
                "Worksheet name is required.",
                nameof(worksheetName));
        }

        using var workbook = new XLWorkbook(stream);

        var worksheet = workbook.Worksheets.FirstOrDefault(candidate =>
            string.Equals(
                candidate.Name,
                worksheetName,
                StringComparison.Ordinal));

        if (worksheet is null)
        {
            return new XlsxWorksheetTableReadResult(
                SheetFound: false,
                Rows: []);
        }

        var usedRange = worksheet.RangeUsed();

        if (usedRange is null)
        {
            return new XlsxWorksheetTableReadResult(
                SheetFound: true,
                Rows: []);
        }

        return new XlsxWorksheetTableReadResult(
            SheetFound: true,
            Rows: ReadRows(worksheet, usedRange));
    }

    private static IReadOnlyList<IReadOnlyList<string>> ReadRows(
        IXLWorksheet worksheet,
        IXLRange usedRange)
    {
        var firstRow = usedRange.RangeAddress.FirstAddress.RowNumber;
        var lastRow = usedRange.RangeAddress.LastAddress.RowNumber;
        var firstColumn = usedRange.RangeAddress.FirstAddress.ColumnNumber;
        var lastColumn = usedRange.RangeAddress.LastAddress.ColumnNumber;

        var rows = new List<IReadOnlyList<string>>();

        for (var rowNumber = firstRow; rowNumber <= lastRow; rowNumber++)
        {
            var cells = new List<string>();

            for (var columnNumber = firstColumn; columnNumber <= lastColumn; columnNumber++)
            {
                var cell = worksheet.Cell(rowNumber, columnNumber);

                cells.Add(cell.IsEmpty()
                    ? string.Empty
                    : cell.GetFormattedString());
            }

            rows.Add(cells);
        }

        return rows;
    }
}
