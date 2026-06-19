using Aether.Application.Scheduling.SubmissionForms;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class GoogleFormsWorkerSubmissionImporterTests
{
    [Fact]
    public void Import_ShouldComposeFullPipeline_FromGoogleFormsRows()
    {
        var resources = CreateResources();

        var rows = CreateTable(
            CreateRow("5/25/2026 08:00:00", "Worker16 אלדר"),
            CreateRow("5/26/2026 09:00:00", "Worker16 אלדר"),
            CreateRow("5/26/2026 10:00:00", "Worker14"),
            CreateRow("5/27/2026 11:00:00", "Worker11"),
            CreateRow("5/29/2026 10:00:00", "עובד מחוץ לחלון"));

        rows[1][2] = "בוקר";
        rows[1][9] = "הערה חופשית שלא אמורה להיקלט";

        rows[2][18] = "ערב";

        rows[3][2] = "בוקר, צהריים, ערב";
        rows[3][17] = "עוד הערה חופשית";
        rows[3][18] = "צהריים";

        rows[4][4] = "בוקר";

        rows[5][2] = "בוקר";

        var aliases = new Dictionary<string, Guid>(StringComparer.Ordinal)
        {
            ["Worker11"] = resources[2].Id
        };

        var importer = new GoogleFormsWorkerSubmissionImporter();

        var result = importer.Import(new GoogleFormsWorkerSubmissionImportRequest(
            rows,
            CreateSchedulePeriod(),
            SubmittedAtFrom: new DateOnly(2026, 5, 25),
            SubmittedAtTo: new DateOnly(2026, 5, 28),
            resources,
            aliases));

        Assert.Empty(result.FatalErrors);

        Assert.Contains(
            result.Warnings,
            warning =>
                warning.Type == GoogleFormsImportWarningType.DateColumnOutsideSchedulePeriod &&
                warning.ColumnIndex == 3);

        Assert.Contains(
            result.Warnings,
            warning =>
                warning.Type == GoogleFormsImportWarningType.DuplicateWorkerSubmissionIgnored &&
                warning.RowIndex == 1 &&
                warning.ResourceId == resources[0].Id);

        Assert.Equal(3, result.WorkerSubmissions.Count);

        var zivSubmission = result.WorkerSubmissions.Single(
            submission => submission.ResourceId == resources[0].Id);

        var zivShift = Assert.Single(zivSubmission.ShiftSubmissions);

        Assert.Equal(new DateOnly(2026, 6, 1), zivShift.Date);
        Assert.Equal(ShiftKind.Night, zivShift.ShiftKind);

        Assert.DoesNotContain(
            zivSubmission.ShiftSubmissions,
            shift => shift.Date == new DateOnly(2026, 5, 31));

        var maorSubmission = result.WorkerSubmissions.Single(
            submission => submission.ResourceId == resources[1].Id);

        Assert.Equal(4, maorSubmission.ShiftSubmissions.Count);

        Assert.Contains(
            maorSubmission.ShiftSubmissions,
            shift =>
                shift.Date == new DateOnly(2026, 5, 31) &&
                shift.ShiftKind == ShiftKind.Morning);

        Assert.Contains(
            maorSubmission.ShiftSubmissions,
            shift =>
                shift.Date == new DateOnly(2026, 5, 31) &&
                shift.ShiftKind == ShiftKind.Afternoon);

        Assert.Contains(
            maorSubmission.ShiftSubmissions,
            shift =>
                shift.Date == new DateOnly(2026, 5, 31) &&
                shift.ShiftKind == ShiftKind.Night);

        Assert.Contains(
            maorSubmission.ShiftSubmissions,
            shift =>
                shift.Date == new DateOnly(2026, 6, 1) &&
                shift.ShiftKind == ShiftKind.Afternoon);

        var amirSubmission = result.WorkerSubmissions.Single(
            submission => submission.ResourceId == resources[2].Id);

        var amirShift = Assert.Single(amirSubmission.ShiftSubmissions);

        Assert.Equal(new DateOnly(2026, 6, 2), amirShift.Date);
        Assert.Equal(ShiftKind.Morning, amirShift.ShiftKind);
    }

    [Fact]
    public void Import_ShouldReturnFatalErrors_AndSkipPipeline_WhenScopeDiscoveryFails()
    {
        var importer = new GoogleFormsWorkerSubmissionImporter();

        var result = importer.Import(new GoogleFormsWorkerSubmissionImportRequest(
            Rows: [],
            SchedulePeriod: CreateSchedulePeriod(),
            SubmittedAtFrom: new DateOnly(2026, 5, 25),
            SubmittedAtTo: new DateOnly(2026, 5, 28),
            Resources: CreateResources()));

        Assert.Empty(result.WorkerSubmissions);
        Assert.Empty(result.Warnings);

        var fatalError = Assert.Single(result.FatalErrors);

        Assert.Equal(GoogleFormsImportFatalErrorType.EmptyTable, fatalError.Type);
    }

    [Fact]
    public void Import_ShouldAggregateWarnings_FromPipelineStages()
    {
        var resources = CreateResources();

        var rows = CreateTable(
            CreateRow("5/25/2026 08:00:00", "Worker16 אלדר"),
            CreateRow("5/25/2026 09:00:00", "עובד לא מוכר"));

        rows[1][2] = "בוקר, לא יודע";
        rows[2][2] = "בוקר";

        var importer = new GoogleFormsWorkerSubmissionImporter();

        var result = importer.Import(new GoogleFormsWorkerSubmissionImportRequest(
            rows,
            CreateSchedulePeriod(),
            SubmittedAtFrom: new DateOnly(2026, 5, 25),
            SubmittedAtTo: new DateOnly(2026, 5, 28),
            resources));

        Assert.Empty(result.FatalErrors);

        Assert.Single(result.WorkerSubmissions);

        Assert.Contains(
            result.Warnings,
            warning =>
                warning.Type == GoogleFormsImportWarningType.InvalidShiftSelectionToken &&
                warning.RowIndex == 1 &&
                warning.RawValue == "לא יודע");

        Assert.Contains(
            result.Warnings,
            warning =>
                warning.Type == GoogleFormsImportWarningType.UnresolvedWorkerName &&
                warning.RowIndex == 2 &&
                warning.RawValue == "עובד לא מוכר");
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
                "Worker16 אלדר",
                hourlyCost: 100m),
            new Resource(
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                "Worker14",
                hourlyCost: 100m),
            new Resource(
                Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                "אמיר שם טוב",
                hourlyCost: 100m)
        ];
    }

    private static List<string[]> CreateTable(
        params string[][] rows)
    {
        return
        [
            CreateHeaders(),
            .. rows
        ];
    }

    private static string[] CreateHeaders()
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
            .Repeat(string.Empty, CreateHeaders().Length)
            .ToArray();

        cells[0] = timestamp;
        cells[1] = workerName;

        return cells;
    }
}
