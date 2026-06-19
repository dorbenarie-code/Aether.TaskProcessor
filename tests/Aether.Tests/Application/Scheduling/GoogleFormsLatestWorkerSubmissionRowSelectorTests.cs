using Aether.Application.Scheduling.SubmissionForms;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class GoogleFormsLatestWorkerSubmissionRowSelectorTests
{
    [Fact]
    public void Select_ShouldKeepSingleSubmission_WhenWorkerAppearsOnce()
    {
        var resources = CreateResources();

        var rows = CreateTable(
            CreateRow("5/25/2026 16:19:01", "Worker16 אלדר"));

        var scope = CreateScope(1);

        var resolvedRows = new[]
        {
            CreateResolvedRow(1, resources[0])
        };

        var selector = new GoogleFormsLatestWorkerSubmissionRowSelector();

        var result = selector.Select(new GoogleFormsLatestWorkerSubmissionRowSelectionRequest(
            rows,
            scope,
            resolvedRows));

        Assert.Empty(result.Warnings);

        var acceptedRow = Assert.Single(result.AcceptedRows);

        Assert.Equal(1, acceptedRow.RowIndex);
        Assert.Equal(resources[0].Id, acceptedRow.ResourceId);
        Assert.Equal(resources[0].Name, acceptedRow.ResourceName);
    }

    [Fact]
    public void Select_ShouldKeepLatestSubmission_WhenWorkerAppearsMultipleTimes()
    {
        var resources = CreateResources();

        var rows = CreateTable(
            CreateRow("5/25/2026 08:00:00", "Worker16 אלדר"),
            CreateRow("5/25/2026 20:00:00", "Worker16 אלדר"),
            CreateRow("5/26/2026 09:00:00", "Worker16 אלדר"));

        var scope = CreateScope(1, 2, 3);

        var resolvedRows = new[]
        {
            CreateResolvedRow(1, resources[0]),
            CreateResolvedRow(2, resources[0]),
            CreateResolvedRow(3, resources[0])
        };

        var selector = new GoogleFormsLatestWorkerSubmissionRowSelector();

        var result = selector.Select(new GoogleFormsLatestWorkerSubmissionRowSelectionRequest(
            rows,
            scope,
            resolvedRows));

        var acceptedRow = Assert.Single(result.AcceptedRows);

        Assert.Equal(3, acceptedRow.RowIndex);
        Assert.Equal(resources[0].Id, acceptedRow.ResourceId);
    }

    [Fact]
    public void Select_ShouldReturnWarning_ForOlderDuplicateSubmissions()
    {
        var resources = CreateResources();

        var rows = CreateTable(
            CreateRow("5/25/2026 08:00:00", "Worker16 אלדר"),
            CreateRow("5/25/2026 20:00:00", "Worker16 אלדר"),
            CreateRow("5/26/2026 09:00:00", "Worker16 אלדר"));

        var scope = CreateScope(1, 2, 3);

        var resolvedRows = new[]
        {
            CreateResolvedRow(1, resources[0]),
            CreateResolvedRow(2, resources[0]),
            CreateResolvedRow(3, resources[0])
        };

        var selector = new GoogleFormsLatestWorkerSubmissionRowSelector();

        var result = selector.Select(new GoogleFormsLatestWorkerSubmissionRowSelectionRequest(
            rows,
            scope,
            resolvedRows));

        Assert.Single(result.AcceptedRows);
        Assert.Equal(2, result.Warnings.Count);

        Assert.Contains(
            result.Warnings,
            warning =>
                warning.Type == GoogleFormsImportWarningType.DuplicateWorkerSubmissionIgnored &&
                warning.RowIndex == 1 &&
                warning.ColumnIndex == 1 &&
                warning.Header == "שם המאבטח" &&
                warning.RawValue == "Worker16 אלדר" &&
                warning.ResourceId == resources[0].Id &&
                warning.ResourceName == resources[0].Name);

        Assert.Contains(
            result.Warnings,
            warning =>
                warning.Type == GoogleFormsImportWarningType.DuplicateWorkerSubmissionIgnored &&
                warning.RowIndex == 2 &&
                warning.ResourceId == resources[0].Id &&
                warning.ResourceName == resources[0].Name);
    }

    [Fact]
    public void Select_ShouldReturnWarning_AndSkip_WhenTimestampCannotBeParsed()
    {
        var resources = CreateResources();

        var rows = CreateTable(
            CreateRow("not a timestamp", "Worker16 אלדר"));

        var scope = CreateScope(1);

        var resolvedRows = new[]
        {
            CreateResolvedRow(1, resources[0])
        };

        var selector = new GoogleFormsLatestWorkerSubmissionRowSelector();

        var result = selector.Select(new GoogleFormsLatestWorkerSubmissionRowSelectionRequest(
            rows,
            scope,
            resolvedRows));

        Assert.Empty(result.AcceptedRows);

        var warning = Assert.Single(result.Warnings);

        Assert.Equal(GoogleFormsImportWarningType.InvalidResolvedWorkerSubmissionTimestamp, warning.Type);
        Assert.Equal(1, warning.RowIndex);
        Assert.Equal(0, warning.ColumnIndex);
        Assert.Equal("חותמת זמן", warning.Header);
        Assert.Equal("not a timestamp", warning.RawValue);
        Assert.Equal(resources[0].Id, warning.ResourceId);
        Assert.Equal(resources[0].Name, warning.ResourceName);
    }

    [Fact]
    public void Select_ShouldUseHigherRowIndex_WhenDuplicateSubmissionsHaveSameTimestamp()
    {
        var resources = CreateResources();

        var rows = CreateTable(
            CreateRow("5/25/2026 16:19:01", "Worker16 אלדר"),
            CreateRow("5/25/2026 16:19:01", "Worker16 אלדר"));

        var scope = CreateScope(1, 2);

        var resolvedRows = new[]
        {
            CreateResolvedRow(1, resources[0]),
            CreateResolvedRow(2, resources[0])
        };

        var selector = new GoogleFormsLatestWorkerSubmissionRowSelector();

        var result = selector.Select(new GoogleFormsLatestWorkerSubmissionRowSelectionRequest(
            rows,
            scope,
            resolvedRows));

        var acceptedRow = Assert.Single(result.AcceptedRows);

        Assert.Equal(2, acceptedRow.RowIndex);

        var warning = Assert.Single(result.Warnings);

        Assert.Equal(GoogleFormsImportWarningType.DuplicateWorkerSubmissionIgnored, warning.Type);
        Assert.Equal(1, warning.RowIndex);
    }

    private static GoogleFormsImportScopeDiscoveryResult CreateScope(
        params int[] selectedRowIndexes)
    {
        return new GoogleFormsImportScopeDiscoveryResult(
            TimestampColumnIndex: 0,
            WorkerNameColumnIndex: 1,
            ScheduleDateColumns: [],
            SelectedRowIndexes: selectedRowIndexes,
            Warnings: [],
            FatalErrors: []);
    }

    private static IReadOnlyList<Resource> CreateResources()
    {
        return
        [
            new Resource(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                "Worker16 אלדר",
                hourlyCost: 100m),
            new Resource(
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                "Worker14",
                hourlyCost: 100m)
        ];
    }

    private static GoogleFormsResolvedWorkerRow CreateResolvedRow(
        int rowIndex,
        Resource resource)
    {
        return new GoogleFormsResolvedWorkerRow(
            rowIndex,
            resource.Name,
            resource.Id,
            resource.Name);
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
            "שם המאבטח"
        ];
    }

    private static IReadOnlyList<string> CreateRow(
        string timestamp,
        string workerName)
    {
        return
        [
            timestamp,
            workerName
        ];
    }
}
