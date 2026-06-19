using Aether.Application.Scheduling.SubmissionForms;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class GoogleFormsShiftCellParserTests
{
    [Fact]
    public void Parse_ShouldCreateStrongAvailableSelections_FromHebrewShiftTokens()
    {
        var rows = CreateTableWithSelectedShiftCells();
        var discovery = DiscoverScope(rows);

        var parser = new GoogleFormsShiftCellParser();

        var result = parser.Parse(new GoogleFormsShiftCellParsingRequest(
            rows,
            discovery));

        Assert.Empty(result.Warnings);

        Assert.Equal(4, result.Selections.Count);

        Assert.Contains(
            result.Selections,
            selection =>
                selection.RowIndex == 1 &&
                selection.ColumnIndex == 2 &&
                selection.Date == new DateOnly(2026, 5, 31) &&
                selection.ShiftKind == ShiftKind.Morning &&
                selection.Choice == ShiftSubmissionChoice.StrongAvailable);

        Assert.Contains(
            result.Selections,
            selection =>
                selection.RowIndex == 1 &&
                selection.ColumnIndex == 2 &&
                selection.Date == new DateOnly(2026, 5, 31) &&
                selection.ShiftKind == ShiftKind.Afternoon &&
                selection.Choice == ShiftSubmissionChoice.StrongAvailable);

        Assert.Contains(
            result.Selections,
            selection =>
                selection.RowIndex == 1 &&
                selection.ColumnIndex == 2 &&
                selection.Date == new DateOnly(2026, 5, 31) &&
                selection.ShiftKind == ShiftKind.Night &&
                selection.Choice == ShiftSubmissionChoice.StrongAvailable);

        Assert.Contains(
            result.Selections,
            selection =>
                selection.RowIndex == 1 &&
                selection.ColumnIndex == 18 &&
                selection.Date == new DateOnly(2026, 6, 1) &&
                selection.ShiftKind == ShiftKind.Afternoon &&
                selection.Choice == ShiftSubmissionChoice.StrongAvailable);
    }

    [Fact]
    public void Parse_ShouldIgnoreEmptyCells_AndRowsOutsideSelectedScope()
    {
        var rows = CreateTableWithOutsideWindowShiftCell();
        var discovery = DiscoverScope(rows);

        Assert.Equal([1], discovery.SelectedRowIndexes.ToArray());

        var parser = new GoogleFormsShiftCellParser();

        var result = parser.Parse(new GoogleFormsShiftCellParsingRequest(
            rows,
            discovery));

        Assert.Empty(result.Warnings);
        Assert.Empty(result.Selections);
    }

    [Fact]
    public void Parse_ShouldReturnWarning_AndSkipUnknownShiftToken()
    {
        var rows = CreateTableWithUnknownShiftToken();
        var discovery = DiscoverScope(rows);

        var parser = new GoogleFormsShiftCellParser();

        var result = parser.Parse(new GoogleFormsShiftCellParsingRequest(
            rows,
            discovery));

        Assert.Equal(2, result.Selections.Count);

        Assert.Contains(
            result.Selections,
            selection => selection.ShiftKind == ShiftKind.Morning);

        Assert.Contains(
            result.Selections,
            selection => selection.ShiftKind == ShiftKind.Night);

        var warning = Assert.Single(result.Warnings);

        Assert.Equal(
            GoogleFormsImportWarningType.InvalidShiftSelectionToken,
            warning.Type);

        Assert.Equal(1, warning.RowIndex);
        Assert.Equal(2, warning.ColumnIndex);
        Assert.Equal("לא יודע", warning.RawValue);
    }

    private static GoogleFormsImportScopeDiscoveryResult DiscoverScope(
        IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var discoverer = new GoogleFormsImportScopeDiscoverer();

        var result = discoverer.Discover(new GoogleFormsImportScopeDiscoveryRequest(
            Rows: rows,
            SchedulePeriod: CreateSchedulePeriod(),
            SubmittedAtFrom: new DateOnly(2026, 5, 25),
            SubmittedAtTo: new DateOnly(2026, 5, 28)));

        Assert.Empty(result.FatalErrors);

        return result;
    }

    private static SchedulePeriod CreateSchedulePeriod()
    {
        return new SchedulePeriod(
            new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc));
    }

    private static IReadOnlyList<IReadOnlyList<string>> CreateTableWithSelectedShiftCells()
    {
        var row = CreateRow("5/25/2026 16:19:01", "Worker14");

        row[2] = "בוקר, צהריים, ערב";
        row[18] = "צהריים";

        return
        [
            CreateHeaders(),
            row
        ];
    }

    private static IReadOnlyList<IReadOnlyList<string>> CreateTableWithOutsideWindowShiftCell()
    {
        var selectedRow = CreateRow("5/25/2026 16:19:01", "Worker14");
        var outsideWindowRow = CreateRow("5/29/2026 10:00:00", "Worker10");

        outsideWindowRow[2] = "בוקר";

        return
        [
            CreateHeaders(),
            selectedRow,
            outsideWindowRow
        ];
    }

    private static IReadOnlyList<IReadOnlyList<string>> CreateTableWithUnknownShiftToken()
    {
        var row = CreateRow("5/25/2026 16:19:01", "Worker14");

        row[2] = "בוקר, לא יודע, ערב";

        return
        [
            CreateHeaders(),
            row
        ];
    }

    private static IReadOnlyList<string> CreateHeaders()
    {
        return
        [
            "חותמת זמן",
            "שם המאבטח",
            "שבוע ראשון [ראשון - 31/05]",
            "שבוע ראשון [שני - 06/10 - ערב סוכות]",
            "שבוע ראשון [שלWorker19 02/06-]",
            "שבוע ראשון [רביעי - 03/06]",
            "שבוע ראשון [חמWorker19 - 04/06]",
            "שבוע ראשון [שWorker19 - 05/06]",
            "שבוע ראשון [שבת - 06/06]",
            "בקשות מיוחדות שבוע ראשון",
            "שבוע שני [ראשון - 07/06]",
            "שבוע שני [שני - 08/06]",
            "שבוע שני [שלWorker19 - 09/06]",
            "שבוע שני [רביעי - 10/06]",
            "שבוע שני [חמWorker19 - 11/06]",
            "שבוע שני [שWorker19 - 12/06]",
            "שבוע שני [שבת - 13/06]",
            "בקשות מיוחדות שבוע שני",
            "שבוע ראשון [שני -01/06]"
        ];
    }

    private static string[] CreateRow(
        string timestamp,
        string workerName)
    {
        var cells = Enumerable
            .Repeat(string.Empty, CreateHeaders().Count)
            .ToArray();

        cells[0] = timestamp;
        cells[1] = workerName;

        return cells;
    }
}
