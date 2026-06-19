using Aether.Application.Scheduling.SubmissionForms;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class AvailabilityMatrixWorkerSubmissionImporterTests
{
    [Fact]
    public void Import_ShouldCreateWorkerSubmissions_FromCleanAvailabilityMatrixRows()
    {
        var resources = CreateResources();
        var rows = CreateCleanMatrixRows();

        var importer = new AvailabilityMatrixWorkerSubmissionImporter();

        var result = importer.Import(new AvailabilityMatrixWorkerSubmissionImportRequest(
            rows,
            CreateSchedulePeriod(),
            resources));

        Assert.Empty(result.FatalErrors);
        Assert.Empty(result.Warnings);

        Assert.Equal(2, result.WorkerSubmissions.Count);

        var zivSubmission = result.WorkerSubmissions.Single(
            submission => submission.ResourceId == resources[0].Id);

        Assert.Equal(2, zivSubmission.ShiftSubmissions.Count);

        Assert.Contains(
            zivSubmission.ShiftSubmissions,
            shift =>
                shift.Date == new DateOnly(2026, 6, 14) &&
                shift.ShiftKind == ShiftKind.Morning &&
                shift.Choice == ShiftSubmissionChoice.StrongAvailable);

        Assert.Contains(
            zivSubmission.ShiftSubmissions,
            shift =>
                shift.Date == new DateOnly(2026, 6, 14) &&
                shift.ShiftKind == ShiftKind.Night &&
                shift.Choice == ShiftSubmissionChoice.StrongAvailable);

        var maorSubmission = result.WorkerSubmissions.Single(
            submission => submission.ResourceId == resources[1].Id);

        var maorShift = Assert.Single(maorSubmission.ShiftSubmissions);

        Assert.Equal(new DateOnly(2026, 6, 15), maorShift.Date);
        Assert.Equal(ShiftKind.Afternoon, maorShift.ShiftKind);
        Assert.Equal(ShiftSubmissionChoice.StrongAvailable, maorShift.Choice);
    }

    [Fact]
    public void Import_ShouldReturnFatalError_WhenDuplicateWorkerRowsExist()
    {
        var resources = CreateResources();

        IReadOnlyList<IReadOnlyList<string>> rows =
        [
            CreateHeaders(),
            CreateRow("Worker16 אלדר", "בוקר", string.Empty, string.Empty),
            CreateRow("Worker16 אלדר", string.Empty, "צהריים", string.Empty)
        ];

        var importer = new AvailabilityMatrixWorkerSubmissionImporter();

        var result = importer.Import(new AvailabilityMatrixWorkerSubmissionImportRequest(
            rows,
            CreateSchedulePeriod(),
            resources));

        Assert.Empty(result.WorkerSubmissions);

        var fatalError = Assert.Single(result.FatalErrors);

        Assert.Equal(AvailabilityMatrixImportFatalErrorType.DuplicateWorkerRow, fatalError.Type);
        Assert.Equal(2, fatalError.RowIndex);
        Assert.Equal(0, fatalError.ColumnIndex);
        Assert.Equal("שם המאבטח", fatalError.Header);
        Assert.Equal("Worker16 אלדר", fatalError.RawValue);
        Assert.Equal(resources[0].Id, fatalError.ResourceId);
        Assert.Equal(resources[0].Name, fatalError.ResourceName);
    }

    [Fact]
    public void Import_ShouldReturnFatalError_WhenWorkerNameCannotBeResolved()
    {
        var resources = CreateResources();

        IReadOnlyList<IReadOnlyList<string>> rows =
        [
            CreateHeaders(),
            CreateRow("עובד לא מוכר", "בוקר", string.Empty, string.Empty)
        ];

        var importer = new AvailabilityMatrixWorkerSubmissionImporter();

        var result = importer.Import(new AvailabilityMatrixWorkerSubmissionImportRequest(
            rows,
            CreateSchedulePeriod(),
            resources));

        Assert.Empty(result.WorkerSubmissions);

        var fatalError = Assert.Single(result.FatalErrors);

        Assert.Equal(AvailabilityMatrixImportFatalErrorType.UnresolvedWorkerName, fatalError.Type);
        Assert.Equal(1, fatalError.RowIndex);
        Assert.Equal(0, fatalError.ColumnIndex);
        Assert.Equal("שם המאבטח", fatalError.Header);
        Assert.Equal("עובד לא מוכר", fatalError.RawValue);
    }

    [Fact]
    public void Import_ShouldReturnFatalError_WhenRequiredScheduleDateColumnIsMissing()
    {
        var resources = CreateResources();

        IReadOnlyList<IReadOnlyList<string>> rows =
        [
            ["שם המאבטח", "ראשון - 14/06", "בקשות מיוחדות"],
            ["Worker16 אלדר", "בוקר", string.Empty]
        ];

        var importer = new AvailabilityMatrixWorkerSubmissionImporter();

        var result = importer.Import(new AvailabilityMatrixWorkerSubmissionImportRequest(
            rows,
            CreateSchedulePeriod(),
            resources));

        Assert.Empty(result.WorkerSubmissions);

        Assert.Contains(
            result.FatalErrors,
            error =>
                error.Type == AvailabilityMatrixImportFatalErrorType.MissingScheduleDateColumn &&
                error.Date == new DateOnly(2026, 6, 15));
    }

    [Fact]
    public void Import_ShouldReturnFatalError_WhenScheduleDateColumnIsDuplicated()
    {
        var resources = CreateResources();

        IReadOnlyList<IReadOnlyList<string>> rows =
        [
            ["שם המאבטח", "ראשון - 14/06", "ראשון נוסף - 14/06", "שני - 15/06"],
            ["Worker16 אלדר", "בוקר", "ערב", "צהריים"]
        ];

        var importer = new AvailabilityMatrixWorkerSubmissionImporter();

        var result = importer.Import(new AvailabilityMatrixWorkerSubmissionImportRequest(
            rows,
            CreateSchedulePeriod(),
            resources));

        Assert.Empty(result.WorkerSubmissions);

        Assert.Contains(
            result.FatalErrors,
            error =>
                error.Type == AvailabilityMatrixImportFatalErrorType.DuplicateScheduleDateColumn &&
                error.Date == new DateOnly(2026, 6, 14) &&
                error.ColumnIndex == 2);
    }

    [Fact]
    public void Import_ShouldWarnAndIgnoreDateColumnOutsideSchedulePeriod()
    {
        var resources = CreateResources();

        IReadOnlyList<IReadOnlyList<string>> rows =
        [
            ["שם המאבטח", "ראשון - 14/06", "שני - 06/10 - ערב סוכות", "שני - 15/06", "בקשות מיוחדות"],
            ["Worker16 אלדר", "בוקר", "ערב", "צהריים", "טקסט חופשי"]
        ];

        var importer = new AvailabilityMatrixWorkerSubmissionImporter();

        var result = importer.Import(new AvailabilityMatrixWorkerSubmissionImportRequest(
            rows,
            CreateSchedulePeriod(),
            resources));

        Assert.Empty(result.FatalErrors);

        var warning = Assert.Single(result.Warnings);

        Assert.Equal(AvailabilityMatrixImportWarningType.DateColumnOutsideSchedulePeriod, warning.Type);
        Assert.Equal(2, warning.ColumnIndex);
        Assert.Equal("שני - 06/10 - ערב סוכות", warning.Header);

        var submission = Assert.Single(result.WorkerSubmissions);

        Assert.Equal(2, submission.ShiftSubmissions.Count);

        Assert.Contains(
            submission.ShiftSubmissions,
            shift =>
                shift.Date == new DateOnly(2026, 6, 14) &&
                shift.ShiftKind == ShiftKind.Morning);

        Assert.Contains(
            submission.ShiftSubmissions,
            shift =>
                shift.Date == new DateOnly(2026, 6, 15) &&
                shift.ShiftKind == ShiftKind.Afternoon);

        Assert.DoesNotContain(
            submission.ShiftSubmissions,
            shift => shift.ShiftKind == ShiftKind.Night);
    }

    [Fact]
    public void Import_ShouldReturnWarning_AndSkipUnknownShiftToken()
    {
        var resources = CreateResources();

        IReadOnlyList<IReadOnlyList<string>> rows =
        [
            CreateHeaders(),
            CreateRow("Worker16 אלדר", "בוקר, לא יודע, ערב", string.Empty, string.Empty)
        ];

        var importer = new AvailabilityMatrixWorkerSubmissionImporter();

        var result = importer.Import(new AvailabilityMatrixWorkerSubmissionImportRequest(
            rows,
            CreateSchedulePeriod(),
            resources));

        Assert.Empty(result.FatalErrors);

        var warning = Assert.Single(result.Warnings);

        Assert.Equal(AvailabilityMatrixImportWarningType.InvalidShiftSelectionToken, warning.Type);
        Assert.Equal(1, warning.RowIndex);
        Assert.Equal(1, warning.ColumnIndex);
        Assert.Equal("לא יודע", warning.RawValue);
        Assert.Equal(new DateOnly(2026, 6, 14), warning.Date);

        var submission = Assert.Single(result.WorkerSubmissions);

        Assert.Equal(2, submission.ShiftSubmissions.Count);

        Assert.Contains(
            submission.ShiftSubmissions,
            shift => shift.ShiftKind == ShiftKind.Morning);

        Assert.Contains(
            submission.ShiftSubmissions,
            shift => shift.ShiftKind == ShiftKind.Night);
    }

    [Fact]
    public void Import_ShouldReturnFatalError_WhenMissingWorkerNameRowContainsScheduleCell()
    {
        var resources = CreateResources();

        IReadOnlyList<IReadOnlyList<string>> rows =
        [
            CreateHeaders(),
            CreateRow(string.Empty, "בוקר", string.Empty, "בקשה חופשית")
        ];

        var importer = new AvailabilityMatrixWorkerSubmissionImporter();

        var result = importer.Import(new AvailabilityMatrixWorkerSubmissionImportRequest(
            rows,
            CreateSchedulePeriod(),
            resources));

        Assert.Empty(result.WorkerSubmissions);

        var fatalError = Assert.Single(result.FatalErrors);

        Assert.Equal(AvailabilityMatrixImportFatalErrorType.MissingWorkerNameForNonEmptyRow, fatalError.Type);
        Assert.Equal(1, fatalError.RowIndex);
        Assert.Equal(0, fatalError.ColumnIndex);
        Assert.Equal("שם המאבטח", fatalError.Header);
    }

    [Fact]
    public void Import_ShouldResolveExplicitAliases_WithoutGuessingNames()
    {
        var resources = CreateResources();

        IReadOnlyList<IReadOnlyList<string>> rows =
        [
            CreateHeaders(),
            CreateRow("Worker16", "בוקר", string.Empty, string.Empty)
        ];

        var aliases = new Dictionary<string, Guid>(StringComparer.Ordinal)
        {
            ["Worker16"] = resources[0].Id
        };

        var importer = new AvailabilityMatrixWorkerSubmissionImporter();

        var result = importer.Import(new AvailabilityMatrixWorkerSubmissionImportRequest(
            rows,
            CreateSchedulePeriod(),
            resources,
            aliases));

        Assert.Empty(result.FatalErrors);
        Assert.Empty(result.Warnings);

        var submission = Assert.Single(result.WorkerSubmissions);

        Assert.Equal(resources[0].Id, submission.ResourceId);

        var shift = Assert.Single(submission.ShiftSubmissions);

        Assert.Equal(new DateOnly(2026, 6, 14), shift.Date);
        Assert.Equal(ShiftKind.Morning, shift.ShiftKind);
    }

    [Fact]
    public void Import_ShouldImportRealisticCleanAvailabilityMatrix()
    {
        var resources = CreateRealisticResources();
        var rows = CreateRealisticCleanMatrixRows();

        var importer = new AvailabilityMatrixWorkerSubmissionImporter();

        var result = importer.Import(new AvailabilityMatrixWorkerSubmissionImportRequest(
            rows,
            CreateRealisticSchedulePeriod(),
            resources));

        Assert.Empty(result.FatalErrors);

        var warning = Assert.Single(result.Warnings);

        Assert.Equal(AvailabilityMatrixImportWarningType.DateColumnOutsideSchedulePeriod, warning.Type);
        Assert.Equal(3, warning.ColumnIndex);
        Assert.Equal("שבוע ראשון [שני - 06/10 - ערב סוכות]", warning.Header);

        Assert.Equal(19, result.WorkerSubmissions.Count);

        Assert.Equal(
            205,
            result.WorkerSubmissions.Sum(submission => submission.ShiftSubmissions.Count));

        Assert.DoesNotContain(
            result.Warnings,
            warning => warning.Type == AvailabilityMatrixImportWarningType.InvalidShiftSelectionToken);

        var hamoSubmission = result.WorkerSubmissions.Single(
            submission => submission.ResourceId == resources[1].Id);

        Assert.Equal(4, hamoSubmission.ShiftSubmissions.Count);

        Assert.Contains(
            hamoSubmission.ShiftSubmissions,
            shift =>
                shift.Date == new DateOnly(2026, 6, 14) &&
                shift.ShiftKind == ShiftKind.Morning);

        Assert.Contains(
            hamoSubmission.ShiftSubmissions,
            shift =>
                shift.Date == new DateOnly(2026, 6, 25) &&
                shift.ShiftKind == ShiftKind.Afternoon);

        var hilaSubmission = result.WorkerSubmissions.Single(
            submission => submission.ResourceId == resources[3].Id);

        Assert.Equal(10, hilaSubmission.ShiftSubmissions.Count);

        Assert.Contains(
            hilaSubmission.ShiftSubmissions,
            shift =>
                shift.Date == new DateOnly(2026, 6, 23) &&
                shift.ShiftKind == ShiftKind.Morning);

        Assert.Contains(
            hilaSubmission.ShiftSubmissions,
            shift =>
                shift.Date == new DateOnly(2026, 6, 23) &&
                shift.ShiftKind == ShiftKind.Night);

        var zivSubmission = result.WorkerSubmissions.Single(
            submission => submission.ResourceId == resources[15].Id);

        Assert.Equal(23, zivSubmission.ShiftSubmissions.Count);

        Assert.Contains(
            zivSubmission.ShiftSubmissions,
            shift =>
                shift.Date == new DateOnly(2026, 6, 25) &&
                shift.ShiftKind == ShiftKind.Morning);

        Assert.Contains(
            zivSubmission.ShiftSubmissions,
            shift =>
                shift.Date == new DateOnly(2026, 6, 25) &&
                shift.ShiftKind == ShiftKind.Afternoon);

        Assert.Contains(
            zivSubmission.ShiftSubmissions,
            shift =>
                shift.Date == new DateOnly(2026, 6, 25) &&
                shift.ShiftKind == ShiftKind.Night);

        var ishaiSubmission = result.WorkerSubmissions.Single(
            submission => submission.ResourceId == resources[18].Id);

        Assert.Equal(23, ishaiSubmission.ShiftSubmissions.Count);

        Assert.Contains(
            ishaiSubmission.ShiftSubmissions,
            shift =>
                shift.Date == new DateOnly(2026, 6, 14) &&
                shift.ShiftKind == ShiftKind.Morning);

        Assert.Contains(
            ishaiSubmission.ShiftSubmissions,
            shift =>
                shift.Date == new DateOnly(2026, 6, 27) &&
                shift.ShiftKind == ShiftKind.Night);
    }

    private static SchedulePeriod CreateRealisticSchedulePeriod()
    {
        return new SchedulePeriod(
            new DateTime(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 28, 0, 0, 0, DateTimeKind.Utc));
    }

    private static IReadOnlyList<Resource> CreateRealisticResources()
    {
        return CreateRealisticWorkerNames()
            .Select((name, index) => new Resource(
                Guid.Parse($"99999999-9999-9999-9999-{index + 1:000000000000}"),
                name,
                hourlyCost: 100m))
            .ToArray();
    }

    private static IReadOnlyList<string> CreateRealisticWorkerNames()
    {
        return
        [
            "עדנה ישראלי",
            "Worker02 ישראלי",
            "Worker03 ישראלי",
            "Worker04 ישראלי",
            "Worker05 ישראלי",
            "Worker06 ישראלי",
            "Worker07 ישראלי",
            "Worker08 ישראלי",
            "Worker09 ישראלי",
            "Worker10 ישראלי",
            "אמיר ישראלי",
            "Worker12 ישראלי",
            "Worker13 ישראלי",
            "Worker14 ישראלי",
            "Worker15 ישראלי",
            "Worker16 ישראלי",
            "Worker17 ישראלי",
            "Worker18 ישראלי",
            "Worker19 ישראלי"
        ];
    }

    private static IReadOnlyList<IReadOnlyList<string>> CreateRealisticCleanMatrixRows()
    {
        return
        [
            CreateRealisticHeaders(),
            ["עדנה ישראלי", "", "", "", "", "ערב", "צהריים", "", "", "", "צהריים", "", "", "", "", "", "", ""],
            ["Worker02 ישראלי", "בוקר, צהריים", "", "", "", "", "", "", "", "בקשה חופשית שלא אמורה להיקלט", "", "", "", "", "בוקר, צהריים", "", "", "בקשה חופשית שלא אמורה להיקלט"],
            ["Worker03 ישראלי", "", "", "", "", "", "", "בוקר", "", "בקשה חופשית שלא אמורה להיקלט", "", "", "", "", "", "בוקר", "", "בקשה חופשית שלא אמורה להיקלט"],
            ["Worker04 ישראלי", "צהריים", "", "", "בוקר", "צהריים", "בוקר", "", "", "בקשה חופשית שלא אמורה להיקלט", "בוקר", "בוקר", "בוקר, ערב", "צהריים", "בוקר", "", "", "בקשה חופשית שלא אמורה להיקלט"],
            ["Worker05 ישראלי", "", "צהריים", "", "צהריים", "בוקר, ערב", "צהריים", "בוקר", "", "", "צהריים", "צהריים", "", "בוקר, ערב", "צהריים", "בוקר", "", ""],
            ["Worker06 ישראלי", "", "", "", "בוקר", "", "צהריים", "בוקר", "", "", "ערב", "", "בוקר", "", "", "בוקר", "", ""],
            ["Worker07 ישראלי", "בוקר", "בוקר", "", "בוקר", "בוקר", "בוקר", "", "", "", "בוקר", "", "", "", "", "", "", "בקשה חופשית שלא אמורה להיקלט"],
            ["Worker08 ישראלי", "בוקר", "", "", "צהריים", "בוקר", "", "", "", "בקשה חופשית שלא אמורה להיקלט", "בוקר", "", "צהריים", "בוקר", "", "", "", "בקשה חופשית שלא אמורה להיקלט"],
            ["Worker09 ישראלי", "בוקר, ערב", "צהריים", "", "", "", "", "", "", "", "ערב", "בוקר, צהריים, ערב", "בוקר, ערב", "בוקר, צהריים, ערב", "בוקר, צהריים, ערב", "", "", ""],
            ["Worker10 ישראלי", "צהריים", "צהריים", "", "בוקר", "צהריים", "בוקר", "", "", "", "בוקר", "צהריים", "צהריים", "בוקר", "", "", "", ""],
            ["אמיר ישראלי", "צהריים, ערב", "", "", "בוקר, צהריים", "ערב", "ערב", "בוקר", "ערב", "", "צהריים, ערב", "ערב", "בוקר, צהריים", "ערב", "ערב", "", "", ""],
            ["Worker12 ישראלי", "בוקר", "צהריים, ערב", "", "צהריים", "בוקר", "ערב", "", "", "בקשה חופשית שלא אמורה להיקלט", "צהריים", "צהריים, ערב", "בוקר, ערב", "ערב", "ערב", "", "", "בקשה חופשית שלא אמורה להיקלט"],
            ["Worker13 ישראלי", "בוקר", "בוקר", "", "", "ערב", "צהריים", "", "", "בקשה חופשית שלא אמורה להיקלט", "בוקר", "בוקר", "", "ערב", "צהריים", "", "", ""],
            ["Worker14 ישראלי", "צהריים", "", "", "בוקר", "צהריים", "בוקר", "", "", "", "בוקר", "בוקר", "צהריים", "צהריים", "בוקר", "בוקר", "", ""],
            ["Worker15 ישראלי", "צהריים", "בוקר, צהריים, ערב", "", "בוקר, צהריים, ערב", "בוקר, צהריים", "בוקר", "", "", "בקשה חופשית שלא אמורה להיקלט", "צהריים", "בוקר, ערב", "בוקר, צהריים, ערב", "בוקר, צהריים", "בוקר", "", "", "בקשה חופשית שלא אמורה להיקלט"],
            ["Worker16 ישראלי", "צהריים, ערב", "צהריים, ערב", "", "בוקר", "בוקר", "צהריים, ערב", "בוקר", "ערב", "", "צהריים, ערב", "צהריים, ערב", "צהריים, ערב", "בוקר, ערב", "בוקר, צהריים, ערב", "בוקר", "ערב", ""],
            ["Worker17 ישראלי", "", "בוקר", "", "", "צהריים", "", "", "", "", "", "בוקר", "", "צהריים", "", "", "", ""],
            ["Worker18 ישראלי", "ערב", "צהריים", "", "צהריים", "", "בוקר, ערב", "בוקר", "", "בקשה חופשית שלא אמורה להיקלט", "בוקר, צהריים, ערב", "בוקר, צהריים, ערב", "צהריים", "", "בוקר, צהריים, ערב", "בוקר", "", "בקשה חופשית שלא אמורה להיקלט"],
            ["Worker19 ישראלי", "בוקר, צהריים, ערב", "צהריים, ערב", "", "בוקר, צהריים, ערב", "צהריים, ערב", "צהריים, ערב", "בוקר", "ערב", "", "בוקר, צהריים, ערב", "", "ערב", "בוקר, ערב", "ערב", "בוקר", "ערב", ""]
        ];
    }

    private static IReadOnlyList<string> CreateRealisticHeaders()
    {
        return
        [
            "שם המאבטח",
            "שבוע ראשון [ראשון - 14/06]",
            "שבוע ראשון [שני -15/06]",
            "שבוע ראשון [שני - 06/10 - ערב סוכות]",
            "שבוע ראשון [שלWorker19 -16/06]",
            "שבוע ראשון [רביעי - 17/06]",
            "שבוע ראשון [חמWorker19 - 18/06]",
            "שבוע ראשון [שWorker19 - 19/06]",
            "שבוע ראשון [שבת -20/06]",
            "בקשות מיוחדות שבוע ראשון",
            "שבוע שני [ראשון - 21/06]",
            "שבוע שני [שני -22/06]",
            "שבוע שני [שלWorker19 - 23/06]",
            "שבוע שני [רביעי - 24/06]",
            "שבוע שני [חמWorker19 - 25/06]",
            "שבוע שני [שWorker19 - 26/06]",
            "שבוע שני [שבת -27/06]",
            "בקשות מיוחדות שבוע שני"
        ];
    }

    private static SchedulePeriod CreateSchedulePeriod()
    {
        return new SchedulePeriod(
            new DateTime(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 16, 0, 0, 0, DateTimeKind.Utc));
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

    private static IReadOnlyList<IReadOnlyList<string>> CreateCleanMatrixRows()
    {
        return
        [
            CreateHeaders(),
            CreateRow("Worker16 אלדר", "בוקר, ערב", string.Empty, "בקשה חופשית שלא אמורה להיקלט"),
            CreateRow("Worker14", string.Empty, "צהריים", "עוד טקסט חופשי")
        ];
    }

    private static IReadOnlyList<string> CreateHeaders()
    {
        return
        [
            "שם המאבטח",
            "ראשון - 14/06",
            "שני - 15/06",
            "בקשות מיוחדות"
        ];
    }

    private static IReadOnlyList<string> CreateRow(
        string workerName,
        string firstDateCell,
        string secondDateCell,
        string freeTextNote)
    {
        return
        [
            workerName,
            firstDateCell,
            secondDateCell,
            freeTextNote
        ];
    }
}
