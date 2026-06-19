using Aether.Application.Scheduling.SubmissionForms;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class GoogleFormsImportScopeDiscovererTests
{
    [Fact]
    public void Discover_ShouldMapScheduleDateColumns_ByDateInsideSchedulePeriod_RegardlessOfColumnOrder()
    {
        var discoverer = new GoogleFormsImportScopeDiscoverer();

        var request = new GoogleFormsImportScopeDiscoveryRequest(
            Rows: CreateTableWithAllScheduleHeadersInUnsafeOrder(),
            SchedulePeriod: CreateSchedulePeriod(),
            SubmittedAtFrom: new DateOnly(2026, 5, 25),
            SubmittedAtTo: new DateOnly(2026, 5, 28));

        var result = discoverer.Discover(request);

        Assert.Empty(result.FatalErrors);

        Assert.Equal(0, result.TimestampColumnIndex);
        Assert.Equal(1, result.WorkerNameColumnIndex);
        Assert.Equal(14, result.ScheduleDateColumns.Count);

        var columnsByDate = result.ScheduleDateColumns.ToDictionary(
            column => column.Date);

        Assert.Equal(2, columnsByDate[new DateOnly(2026, 5, 31)].ColumnIndex);
        Assert.Equal(18, columnsByDate[new DateOnly(2026, 6, 1)].ColumnIndex);
        Assert.Equal(4, columnsByDate[new DateOnly(2026, 6, 2)].ColumnIndex);
        Assert.Equal(16, columnsByDate[new DateOnly(2026, 6, 13)].ColumnIndex);

        Assert.DoesNotContain(
            result.ScheduleDateColumns,
            column => column.Header.Contains("06/10", StringComparison.Ordinal));

        Assert.Contains(
            result.Warnings,
            warning =>
                warning.Type == GoogleFormsImportWarningType.DateColumnOutsideSchedulePeriod &&
                warning.ColumnIndex == 3);
    }

    [Fact]
    public void Discover_ShouldSelectOnlyRowsInsideSubmittedAtWindow()
    {
        var discoverer = new GoogleFormsImportScopeDiscoverer();

        var request = new GoogleFormsImportScopeDiscoveryRequest(
            Rows: CreateTableWithRowsAcrossMultipleResponseWindows(),
            SchedulePeriod: CreateSchedulePeriod(),
            SubmittedAtFrom: new DateOnly(2026, 5, 25),
            SubmittedAtTo: new DateOnly(2026, 5, 28));

        var result = discoverer.Discover(request);

        Assert.Empty(result.FatalErrors);

        Assert.Equal(
            [2, 3, 4],
            result.SelectedRowIndexes.ToArray());
    }

    [Fact]
    public void Discover_ShouldReturnFatalError_WhenRequiredColumnsAreMissing()
    {
        var discoverer = new GoogleFormsImportScopeDiscoverer();

        var request = new GoogleFormsImportScopeDiscoveryRequest(
            Rows: CreateTableWithoutTimestampHeader(),
            SchedulePeriod: CreateSchedulePeriod(),
            SubmittedAtFrom: new DateOnly(2026, 5, 25),
            SubmittedAtTo: new DateOnly(2026, 5, 28));

        var result = discoverer.Discover(request);

        Assert.Contains(
            result.FatalErrors,
            error => error.Type == GoogleFormsImportFatalErrorType.MissingTimestampColumn);
    }

    [Fact]
    public void Discover_ShouldReturnFatalError_WhenScheduleDateColumnIsMissing()
    {
        var discoverer = new GoogleFormsImportScopeDiscoverer();

        var request = new GoogleFormsImportScopeDiscoveryRequest(
            Rows: CreateTableWithMissingScheduleDateHeader(),
            SchedulePeriod: CreateSchedulePeriod(),
            SubmittedAtFrom: new DateOnly(2026, 5, 25),
            SubmittedAtTo: new DateOnly(2026, 5, 28));

        var result = discoverer.Discover(request);

        Assert.Contains(
            result.FatalErrors,
            error =>
                error.Type == GoogleFormsImportFatalErrorType.MissingScheduleDateColumn &&
                error.Date == new DateOnly(2026, 6, 13));
    }

    private static SchedulePeriod CreateSchedulePeriod()
    {
        return new SchedulePeriod(
            new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc));
    }

    private static IReadOnlyList<IReadOnlyList<string>> CreateTableWithAllScheduleHeadersInUnsafeOrder()
    {
        return
        [
            CreateHeaders(),
            CreateRow("5/25/2026 16:19:01", "Worker14")
        ];
    }

    private static IReadOnlyList<IReadOnlyList<string>> CreateTableWithRowsAcrossMultipleResponseWindows()
    {
        return
        [
            CreateHeaders(),
            CreateRow("3/17/2025 20:19:13", "Worker14"),
            CreateRow("5/25/2026 16:19:01", "Worker14"),
            CreateRow("5/26/2026 8:21:47", "נתי"),
            CreateRow("5/28/2026 19:12:45", "Worker11"),
            CreateRow("5/29/2026 10:00:00", "Worker10")
        ];
    }

    private static IReadOnlyList<IReadOnlyList<string>> CreateTableWithoutTimestampHeader()
    {
        var headers = CreateHeaders().ToArray();
        headers[0] = "זמן שליחה";

        return
        [
            headers,
            CreateRow("5/25/2026 16:19:01", "Worker14")
        ];
    }

    private static IReadOnlyList<IReadOnlyList<string>> CreateTableWithMissingScheduleDateHeader()
    {
        var headers = CreateHeaders().ToArray();
        headers[16] = "שבוע שני [שבת - ללא תאריך]";

        return
        [
            headers,
            CreateRow("5/25/2026 16:19:01", "Worker14")
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

    private static IReadOnlyList<string> CreateRow(
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
