using Aether.Application.Scheduling.SubmissionForms;
using ClosedXML.Excel;

namespace Aether.Infrastructure.Forms;

public sealed class XlsxFormTableReader : IFormTableReader
{
    public IReadOnlyList<IReadOnlyList<string>> Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var workbook = new XLWorkbook(stream);

        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet is null)
        {
            return [];
        }

        var usedRange = worksheet.RangeUsed();

        if (usedRange is null)
        {
            return [];
        }

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
