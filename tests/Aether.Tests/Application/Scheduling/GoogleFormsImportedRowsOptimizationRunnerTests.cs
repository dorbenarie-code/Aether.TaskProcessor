using Aether.Application.Scheduling.ManagerConstraints;
using Aether.Application.Scheduling.SubmissionForms;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class GoogleFormsImportedRowsOptimizationRunnerTests
{
    private const int Seed = 20260603;

    [Fact]
    public void Run_ShouldImportRows_AndOptimizeImportedWorkerSubmissions()
    {
        var resources = CreateResources();
        var shifts = CreateShifts();
        var rows = CreateImportRows();

        var runner = new GoogleFormsImportedRowsOptimizationRunner();

        var result = runner.Run(new GoogleFormsImportedRowsOptimizationRequest(
            rows,
            CreateSchedulePeriod(),
            SubmittedAtFrom: new DateOnly(2026, 5, 25),
            SubmittedAtTo: new DateOnly(2026, 5, 28),
            resources,
            shifts,
            TotalEffectiveTargetHours: 16,
            MaximumAssignedHoursDeviationFromAverageHours: 8,
            Seed: Seed));

        Assert.Empty(result.ImportFatalErrors);

        Assert.Contains(
            result.ImportWarnings,
            warning =>
                warning.Type == GoogleFormsImportWarningType.DateColumnOutsideSchedulePeriod &&
                warning.ColumnIndex == 3);

        Assert.Equal(2, result.ImportedWorkerSubmissions.Count);

        Assert.Contains(
            result.ImportedWorkerSubmissions,
            submission =>
                submission.ResourceId == resources[0].Id &&
                submission.ShiftSubmissions.Single().Date == new DateOnly(2026, 5, 31) &&
                submission.ShiftSubmissions.Single().ShiftKind == ShiftKind.Morning);

        Assert.Contains(
            result.ImportedWorkerSubmissions,
            submission =>
                submission.ResourceId == resources[1].Id &&
                submission.ShiftSubmissions.Single().Date == new DateOnly(2026, 6, 1) &&
                submission.ShiftSubmissions.Single().ShiftKind == ShiftKind.Night);

        Assert.NotNull(result.OptimizationResult);

        var optimizationResult = result.OptimizationResult!;

        Assert.Empty(optimizationResult.Warnings);
        Assert.Equal(resources.Count, optimizationResult.Problem.Resources.Count);
        Assert.Equal(shifts.Count, optimizationResult.Problem.Shifts.Count);
        Assert.Equal(2, optimizationResult.Problem.ResourceWorkloadDemands.Count);

        Assert.NotNull(optimizationResult.GeneticResult.Candidate);
        Assert.NotNull(optimizationResult.GeneticResult.Evaluation);
        Assert.NotEmpty(optimizationResult.GeneticResult.GenerationDiagnostics);
    }

    [Fact]
    public void Run_ShouldPropagateManagerConstraintSet_ToClosedFormOptimizationRunner()
    {
        var resources = CreateResources();
        var shifts = CreateShifts();
        var rows = CreateImportRows();

        var forbiddenResource = resources[0];

        var forbiddenShift = shifts.Single(shift =>
            shift.Kind == ShiftKind.Morning &&
            DateOnly.FromDateTime(shift.StartUtc) == new DateOnly(2026, 5, 31));

        var runner = new GoogleFormsImportedRowsOptimizationRunner();

        var result = runner.Run(new GoogleFormsImportedRowsOptimizationRequest(
            rows,
            CreateSchedulePeriod(),
            SubmittedAtFrom: new DateOnly(2026, 5, 25),
            SubmittedAtTo: new DateOnly(2026, 5, 28),
            resources,
            shifts,
            TotalEffectiveTargetHours: 16,
            MaximumAssignedHoursDeviationFromAverageHours: 8,
            Seed: Seed,
            ManagerConstraintSet: new ManagerConstraintSet([
                new ManagerForbiddenAssignment(
                    forbiddenResource.Id,
                    forbiddenShift.Id)
            ])));

        Assert.Empty(result.ImportFatalErrors);
        Assert.NotNull(result.OptimizationResult);

        var problem = result.OptimizationResult!.Problem;

        Assert.DoesNotContain(
            problem.AvailabilityWindows,
            window =>
                window.ResourceId == forbiddenResource.Id &&
                window.Covers(forbiddenShift));

        Assert.DoesNotContain(
            problem.ResourcePreferences,
            preference =>
                preference.ResourceId == forbiddenResource.Id &&
                preference.StartUtc < forbiddenShift.EndUtc &&
                forbiddenShift.StartUtc < preference.EndUtc);

        var forbiddenDemand = problem.ResourceWorkloadDemands.Single(
            demand => demand.ResourceId == forbiddenResource.Id);

        var remainingDemand = problem.ResourceWorkloadDemands.Single(
            demand => demand.ResourceId == resources[1].Id);

        Assert.Equal(0, forbiddenDemand.RequestedPreferredHours);
        Assert.Equal(16, remainingDemand.RequestedPreferredHours);
    }

    [Fact]
    public void Run_ShouldReturnImportFatalErrors_AndSkipOptimization()
    {
        var runner = new GoogleFormsImportedRowsOptimizationRunner();

        var result = runner.Run(new GoogleFormsImportedRowsOptimizationRequest(
            Rows: [],
            SchedulePeriod: CreateSchedulePeriod(),
            SubmittedAtFrom: new DateOnly(2026, 5, 25),
            SubmittedAtTo: new DateOnly(2026, 5, 28),
            Resources: CreateResources(),
            Shifts: CreateShifts(),
            TotalEffectiveTargetHours: 16));

        Assert.Empty(result.ImportedWorkerSubmissions);
        Assert.Empty(result.ImportWarnings);

        var fatalError = Assert.Single(result.ImportFatalErrors);

        Assert.Equal(GoogleFormsImportFatalErrorType.EmptyTable, fatalError.Type);
        Assert.Null(result.OptimizationResult);
    }


    [Fact]
    public void Run_ShouldOptimizeRealisticImportedRows_AndPreserveImportContract()
    {
        var resources = CreateAcceptanceResources();
        var shifts = CreateAcceptanceShifts();
        var rows = CreateAcceptanceRows();

        var aliases = new Dictionary<string, Guid>(StringComparer.Ordinal)
        {
            ["Worker11"] = resources[2].Id
        };

        var runner = new GoogleFormsImportedRowsOptimizationRunner();

        var result = runner.Run(new GoogleFormsImportedRowsOptimizationRequest(
            rows,
            CreateSchedulePeriod(),
            SubmittedAtFrom: new DateOnly(2026, 5, 25),
            SubmittedAtTo: new DateOnly(2026, 5, 28),
            resources,
            shifts,
            TotalEffectiveTargetHours: 40,
            MaximumAssignedHoursDeviationFromAverageHours: 8,
            Seed: Seed,
            AliasesByWorkerName: aliases));

        Assert.Empty(result.ImportFatalErrors);

        Assert.Contains(
            result.ImportWarnings,
            warning =>
                warning.Type == GoogleFormsImportWarningType.DateColumnOutsideSchedulePeriod &&
                warning.ColumnIndex == 3);

        Assert.Contains(
            result.ImportWarnings,
            warning =>
                warning.Type == GoogleFormsImportWarningType.DuplicateWorkerSubmissionIgnored &&
                warning.RowIndex == 1 &&
                warning.ResourceId == resources[0].Id);

        Assert.Equal(3, result.ImportedWorkerSubmissions.Count);

        var zivSubmission = result.ImportedWorkerSubmissions.Single(
            submission => submission.ResourceId == resources[0].Id);

        var zivShift = Assert.Single(zivSubmission.ShiftSubmissions);

        Assert.Equal(new DateOnly(2026, 6, 1), zivShift.Date);
        Assert.Equal(ShiftKind.Night, zivShift.ShiftKind);

        Assert.DoesNotContain(
            zivSubmission.ShiftSubmissions,
            shift => shift.Date == new DateOnly(2026, 5, 31));

        var maorSubmission = result.ImportedWorkerSubmissions.Single(
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

        var amirSubmission = result.ImportedWorkerSubmissions.Single(
            submission => submission.ResourceId == resources[2].Id);

        Assert.Equal(2, amirSubmission.ShiftSubmissions.Count);

        Assert.Contains(
            amirSubmission.ShiftSubmissions,
            shift =>
                shift.Date == new DateOnly(2026, 6, 2) &&
                shift.ShiftKind == ShiftKind.Morning);

        Assert.Contains(
            amirSubmission.ShiftSubmissions,
            shift =>
                shift.Date == new DateOnly(2026, 6, 13) &&
                shift.ShiftKind == ShiftKind.Night);




        Assert.NotNull(result.OptimizationResult);

        var optimizationResult = result.OptimizationResult!;

        Assert.Empty(optimizationResult.Warnings);
        Assert.Equal(resources.Count, optimizationResult.Problem.Resources.Count);
        Assert.Equal(shifts.Count, optimizationResult.Problem.Shifts.Count);
        Assert.NotEmpty(optimizationResult.Problem.AvailabilityWindows);
        Assert.NotEmpty(optimizationResult.Problem.ResourcePreferences);
        Assert.Equal(resources.Count, optimizationResult.Problem.ResourceWorkloadDemands.Count);
        Assert.Equal(8, optimizationResult.Problem.MaximumAssignedHoursDeviationFromAverageHours);

        Assert.NotNull(optimizationResult.GeneticResult.Candidate);
        Assert.NotNull(optimizationResult.GeneticResult.Evaluation);
        Assert.NotEmpty(optimizationResult.GeneticResult.GenerationDiagnostics);
    }

    private static IReadOnlyList<Resource> CreateAcceptanceResources()
    {
        return
        [
            new Resource(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                "Worker16 אלדר",
                hourlyCost: 100m),
            new Resource(
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                "Worker14",
                hourlyCost: 100m),
            new Resource(
                Guid.Parse("33333333-3333-3333-3333-333333333333"),
                "אמיר שם טוב",
                hourlyCost: 100m)
        ];
    }


    private static IReadOnlyList<Shift> CreateAcceptanceShifts()
    {
        var shifts = new List<Shift>();

        for (var dayOffset = 0; dayOffset < 14; dayOffset++)
        {
            var date = new DateOnly(2026, 5, 31).AddDays(dayOffset);

            shifts.Add(CreateAcceptanceShift(date, ShiftKind.Morning));
            shifts.Add(CreateAcceptanceShift(date, ShiftKind.Afternoon));
            shifts.Add(CreateAcceptanceShift(date, ShiftKind.Night));
        }

        return shifts
            .OrderBy(shift => shift.StartUtc)
            .ToArray();
    }


    private static Shift CreateAcceptanceShift(
        DateOnly date,
        ShiftKind kind)
    {
        var startUtc = kind switch
        {
            ShiftKind.Morning => date.ToDateTime(new TimeOnly(6, 30), DateTimeKind.Utc),
            ShiftKind.Afternoon => date.ToDateTime(new TimeOnly(14, 30), DateTimeKind.Utc),
            ShiftKind.Night => date.ToDateTime(new TimeOnly(22, 30), DateTimeKind.Utc),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        var endUtc = kind == ShiftKind.Night
            ? date.AddDays(1).ToDateTime(new TimeOnly(6, 30), DateTimeKind.Utc)
            : startUtc.AddHours(8);

        return new Shift(
            CreateAcceptanceShiftId(date, kind),
            startUtc,
            endUtc,
            kind,
            minResourceCount: 0,
            maxResourceCount: 1,
            requiresPreferenceToAssign: true);
    }


    private static Guid CreateAcceptanceShiftId(
        DateOnly date,
        ShiftKind kind)
    {
        var dayOffset = date.DayNumber - new DateOnly(2026, 5, 31).DayNumber;
        var kindOffset = kind switch
        {
            ShiftKind.Morning => 1,
            ShiftKind.Afternoon => 2,
            ShiftKind.Night => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        return Guid.Parse($"aaaaaaaa-aaaa-aaaa-aaaa-{dayOffset + 1:000000000}{kindOffset:000}");
    }


    private static List<string[]> CreateAcceptanceRows()
    {
        var oldZivRow = CreateRow("5/25/2026 08:00:00", "Worker16 אלדר");
        oldZivRow[2] = "בוקר";
        oldZivRow[9] = "הערה חופשית שלא אמורה להיקלט";

        var latestZivRow = CreateRow("5/26/2026 09:00:00", "Worker16 אלדר");
        latestZivRow[18] = "ערב";

        var maorRow = CreateRow("5/26/2026 10:00:00", "Worker14");
        maorRow[2] = "בוקר, צהריים, ערב";
        maorRow[17] = "עוד הערה חופשית";
        maorRow[18] = "צהריים";

        var aliasRow = CreateRow("5/27/2026 11:00:00", "Worker11");
        aliasRow[4] = "בוקר";
        aliasRow[16] = "ערב";

        return
        [
            CreateHeaders(),
            oldZivRow,
            latestZivRow,
            maorRow,
            aliasRow
        ];
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
                hourlyCost: 100m)
        ];
    }

    private static IReadOnlyList<Shift> CreateShifts()
    {
        return
        [
            new Shift(
                Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                new DateTime(2026, 5, 31, 6, 30, 0, DateTimeKind.Utc),
                new DateTime(2026, 5, 31, 14, 30, 0, DateTimeKind.Utc),
                ShiftKind.Morning,
                minResourceCount: 1,
                maxResourceCount: 1,
                requiresPreferenceToAssign: true),
            new Shift(
                Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                new DateTime(2026, 6, 1, 22, 30, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 2, 6, 30, 0, DateTimeKind.Utc),
                ShiftKind.Night,
                minResourceCount: 1,
                maxResourceCount: 1,
                requiresPreferenceToAssign: true)
        ];
    }

    private static List<string[]> CreateImportRows()
    {
        var firstRow = CreateRow("5/25/2026 08:00:00", "Worker16 אלדר");
        firstRow[2] = "בוקר";

        var secondRow = CreateRow("5/25/2026 09:00:00", "Worker14");
        secondRow[18] = "ערב";

        return
        [
            CreateHeaders(),
            firstRow,
            secondRow
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
