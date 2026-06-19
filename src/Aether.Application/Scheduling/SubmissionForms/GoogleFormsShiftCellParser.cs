using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.SubmissionForms;

public sealed class GoogleFormsShiftCellParser
{
    public GoogleFormsShiftCellParsingResult Parse(
        GoogleFormsShiftCellParsingRequest request)
    {
        var selections = new List<GoogleFormsShiftCellSelection>();
        var warnings = new List<GoogleFormsImportWarning>();

        foreach (var rowIndex in request.Scope.SelectedRowIndexes)
        {
            if (rowIndex < 0 || rowIndex >= request.Rows.Count)
            {
                continue;
            }

            var row = request.Rows[rowIndex];

            foreach (var dateColumn in request.Scope.ScheduleDateColumns)
            {
                if (dateColumn.ColumnIndex < 0 ||
                    dateColumn.ColumnIndex >= row.Count)
                {
                    continue;
                }

                var rawCellValue = row[dateColumn.ColumnIndex];

                if (string.IsNullOrWhiteSpace(rawCellValue))
                {
                    continue;
                }

                ParseCell(
                    rawCellValue,
                    rowIndex,
                    dateColumn,
                    selections,
                    warnings);
            }
        }

        return new GoogleFormsShiftCellParsingResult(
            selections,
            warnings);
    }

    private static void ParseCell(
        string rawCellValue,
        int rowIndex,
        ScheduleDateColumnMapping dateColumn,
        List<GoogleFormsShiftCellSelection> selections,
        List<GoogleFormsImportWarning> warnings)
    {
        foreach (var token in SplitTokens(rawCellValue))
        {
            if (!TryMapShiftKind(token, out var shiftKind))
            {
                warnings.Add(new GoogleFormsImportWarning(
                    GoogleFormsImportWarningType.InvalidShiftSelectionToken,
                    RowIndex: rowIndex,
                    ColumnIndex: dateColumn.ColumnIndex,
                    Header: dateColumn.Header,
                    RawValue: token));

                continue;
            }

            selections.Add(new GoogleFormsShiftCellSelection(
                rowIndex,
                dateColumn.ColumnIndex,
                dateColumn.Date,
                shiftKind,
                ShiftSubmissionChoice.StrongAvailable));
        }
    }

    private static IEnumerable<string> SplitTokens(string rawCellValue)
    {
        return rawCellValue
            .Split(
                new[] { ',', '\n', '\r' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => !string.IsNullOrWhiteSpace(token));
    }

    private static bool TryMapShiftKind(
        string token,
        out ShiftKind shiftKind)
    {
        shiftKind = default;

        switch (token.Trim())
        {
            case "בוקר":
                shiftKind = ShiftKind.Morning;
                return true;

            case "צהריים":
                shiftKind = ShiftKind.Afternoon;
                return true;

            case "ערב":
                shiftKind = ShiftKind.Night;
                return true;

            default:
                return false;
        }
    }
}
