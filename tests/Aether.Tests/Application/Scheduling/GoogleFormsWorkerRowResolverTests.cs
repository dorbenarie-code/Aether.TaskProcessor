using Aether.Application.Scheduling.SubmissionForms;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class GoogleFormsWorkerRowResolverTests
{
    [Fact]
    public void Resolve_ShouldResolveSelectedRows_ByExactResourceName()
    {
        var rows = CreateTable(
            CreateRow("5/25/2026 16:19:01", "Worker14"),
            CreateRow("5/26/2026 08:21:47", "אמיר שם טוב"));

        var scope = DiscoverScope(rows);
        var resources = CreateResources();

        var resolver = new GoogleFormsWorkerRowResolver();

        var result = resolver.Resolve(new GoogleFormsWorkerRowResolutionRequest(
            rows,
            scope,
            resources));

        Assert.Empty(result.Warnings);
        Assert.Equal(2, result.ResolvedRows.Count);

        Assert.Contains(
            result.ResolvedRows,
            row =>
                row.RowIndex == 1 &&
                row.RawWorkerName == "Worker14" &&
                row.ResourceId == resources[0].Id &&
                row.ResourceName == resources[0].Name);

        Assert.Contains(
            result.ResolvedRows,
            row =>
                row.RowIndex == 2 &&
                row.RawWorkerName == "אמיר שם טוב" &&
                row.ResourceId == resources[1].Id &&
                row.ResourceName == resources[1].Name);
    }

    [Fact]
    public void Resolve_ShouldTrimAndCollapseWhitespace_BeforeExactResourceNameMatch()
    {
        var rows = CreateTable(
            CreateRow("5/25/2026 16:19:01", "  אמיר   שם   טוב  "));

        var scope = DiscoverScope(rows);
        var resources = CreateResources();

        var resolver = new GoogleFormsWorkerRowResolver();

        var result = resolver.Resolve(new GoogleFormsWorkerRowResolutionRequest(
            rows,
            scope,
            resources));

        Assert.Empty(result.Warnings);

        var resolvedRow = Assert.Single(result.ResolvedRows);

        Assert.Equal(1, resolvedRow.RowIndex);
        Assert.Equal("אמיר שם טוב", resolvedRow.RawWorkerName);
        Assert.Equal(resources[1].Id, resolvedRow.ResourceId);
        Assert.Equal(resources[1].Name, resolvedRow.ResourceName);
    }

    [Fact]
    public void Resolve_ShouldResolveSelectedRows_ByExplicitAlias()
    {
        var rows = CreateTable(
            CreateRow("5/25/2026 16:19:01", "Worker11"));

        var scope = DiscoverScope(rows);
        var resources = CreateResources();

        var aliases = new Dictionary<string, Guid>(StringComparer.Ordinal)
        {
            ["Worker11"] = resources[1].Id
        };

        var resolver = new GoogleFormsWorkerRowResolver();

        var result = resolver.Resolve(new GoogleFormsWorkerRowResolutionRequest(
            rows,
            scope,
            resources,
            aliases));

        Assert.Empty(result.Warnings);

        var resolvedRow = Assert.Single(result.ResolvedRows);

        Assert.Equal(1, resolvedRow.RowIndex);
        Assert.Equal("Worker11", resolvedRow.RawWorkerName);
        Assert.Equal(resources[1].Id, resolvedRow.ResourceId);
        Assert.Equal(resources[1].Name, resolvedRow.ResourceName);
    }

    [Fact]
    public void Resolve_ShouldReturnWarning_AndSkipUnknownWorkerName()
    {
        var rows = CreateTable(
            CreateRow("5/25/2026 16:19:01", "עובד לא מוכר"));

        var scope = DiscoverScope(rows);
        var resources = CreateResources();

        var resolver = new GoogleFormsWorkerRowResolver();

        var result = resolver.Resolve(new GoogleFormsWorkerRowResolutionRequest(
            rows,
            scope,
            resources));

        Assert.Empty(result.ResolvedRows);

        var warning = Assert.Single(result.Warnings);

        Assert.Equal(GoogleFormsImportWarningType.UnresolvedWorkerName, warning.Type);
        Assert.Equal(1, warning.RowIndex);
        Assert.Equal(1, warning.ColumnIndex);
        Assert.Equal("שם המאבטח", warning.Header);
        Assert.Equal("עובד לא מוכר", warning.RawValue);
    }

    [Fact]
    public void Resolve_ShouldIgnoreRowsOutsideSelectedScope()
    {
        var rows = CreateTable(
            CreateRow("5/25/2026 16:19:01", "Worker14"),
            CreateRow("5/29/2026 10:00:00", "עובד לא מוכר"));

        var scope = DiscoverScope(rows);
        var resources = CreateResources();

        Assert.Equal([1], scope.SelectedRowIndexes.ToArray());

        var resolver = new GoogleFormsWorkerRowResolver();

        var result = resolver.Resolve(new GoogleFormsWorkerRowResolutionRequest(
            rows,
            scope,
            resources));

        Assert.Empty(result.Warnings);

        var resolvedRow = Assert.Single(result.ResolvedRows);

        Assert.Equal(1, resolvedRow.RowIndex);
        Assert.Equal(resources[0].Id, resolvedRow.ResourceId);
    }

    [Fact]
    public void Resolve_ShouldKeepDuplicateResolvedRows_ForSameResource()
    {
        var rows = CreateTable(
            CreateRow("5/25/2026 16:19:01", "Worker14"),
            CreateRow("5/26/2026 08:21:47", "Worker14"));

        var scope = DiscoverScope(rows);
        var resources = CreateResources();

        var resolver = new GoogleFormsWorkerRowResolver();

        var result = resolver.Resolve(new GoogleFormsWorkerRowResolutionRequest(
            rows,
            scope,
            resources));

        Assert.Empty(result.Warnings);
        Assert.Equal(2, result.ResolvedRows.Count);

        Assert.All(result.ResolvedRows, row =>
        {
            Assert.Equal(resources[0].Id, row.ResourceId);
            Assert.Equal(resources[0].Name, row.ResourceName);
        });

        Assert.Equal(
            [1, 2],
            result.ResolvedRows
                .Select(row => row.RowIndex)
                .ToArray());
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

    private static IReadOnlyList<Resource> CreateResources()
    {
        return
        [
            new Resource(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                "Worker14",
                hourlyCost: 100m),
            new Resource(
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                "אמיר שם טוב",
                hourlyCost: 100m)
        ];
    }

    private static IReadOnlyList<IReadOnlyList<string>> CreateTable(
        params IReadOnlyList<string>[] rows)
    {
        return
        [
            CreateHeaders(),
            .. rows
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
