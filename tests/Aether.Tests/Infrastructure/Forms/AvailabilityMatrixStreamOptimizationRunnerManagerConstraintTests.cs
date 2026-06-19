using Aether.Application.Scheduling.ManagerConstraints;
using Aether.Application.Scheduling.SubmissionForms;
using Aether.Domain.Optimization;
using Aether.Infrastructure.Forms;
using ClosedXML.Excel;

namespace Aether.Tests.Infrastructure.Forms;

public sealed class AvailabilityMatrixStreamOptimizationRunnerManagerConstraintTests
{
    [Fact]
    public void Run_ShouldPropagateManagerConstraintSet_ToImportedRowsOptimizationRunner()
    {
        using var stream = CreateWorkbookStream(worksheet =>
        {
            worksheet.Cell(1, 1).Value = "שם המאבטח";
            worksheet.Cell(1, 2).Value = "ראשון - 14/06";
            worksheet.Cell(1, 3).Value = "שני - 15/06";

            worksheet.Cell(2, 1).Value = "Worker16 אלדר";
            worksheet.Cell(2, 2).Value = "בוקר";

            worksheet.Cell(3, 1).Value = "Worker14";
            worksheet.Cell(3, 3).Value = "ערב";
        });

        var resources = CreateResources();
        var shifts = CreateShifts();

        var forbiddenResource = resources[0];
        var forbiddenShift = shifts[0];

        var result = new AvailabilityMatrixStreamOptimizationRunner(
                new XlsxFormTableReader())
            .Run(new AvailabilityMatrixStreamOptimizationRequest(
                stream,
                CreateSchedulePeriod(),
                resources,
                shifts,
                TotalEffectiveTargetHours: 16,
                MaximumAssignedHoursDeviationFromAverageHours: 8,
                Seed: 20260612,
                ManagerConstraintSet: new ManagerConstraintSet([
                    new ManagerForbiddenAssignment(
                        forbiddenResource.Id,
                        forbiddenShift.Id)
                ])));

        Assert.Empty(result.ImportFatalErrors);
        Assert.Empty(result.ImportWarnings);
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
    public void Run_ShouldImportManagerConstraintRows_AndApplyThemBeforeOptimization()
    {
        using var stream = CreateWorkbookStream(worksheet =>
        {
            worksheet.Cell(1, 1).Value = "שם המאבטח";
            worksheet.Cell(1, 2).Value = "ראשון - 14/06";
            worksheet.Cell(1, 3).Value = "שני - 15/06";

            worksheet.Cell(2, 1).Value = "Worker16 אלדר";
            worksheet.Cell(2, 2).Value = "בוקר";

            worksheet.Cell(3, 1).Value = "Worker14";
            worksheet.Cell(3, 3).Value = "ערב";
        });

        var resources = CreateResources();
        var shifts = CreateShifts();

        var forbiddenResource = resources[0];
        var forbiddenShift = shifts[0];

        var result = new AvailabilityMatrixStreamOptimizationRunner(
                new XlsxFormTableReader())
            .Run(new AvailabilityMatrixStreamOptimizationRequest(
                stream,
                CreateSchedulePeriod(),
                resources,
                shifts,
                TotalEffectiveTargetHours: 16,
                MaximumAssignedHoursDeviationFromAverageHours: 8,
                Seed: 20260612,
                ManagerConstraintRows: CreateManagerConstraintRows()));

        Assert.Empty(result.ImportFatalErrors);
        Assert.Empty(result.ImportWarnings);
        Assert.Empty(result.ManagerConstraintImportFatalErrors);
        Assert.Empty(result.ManagerConstraintImportWarnings);
        Assert.Equal(1, result.ManagerConstraintImportSummary.ImportedForbiddenAssignmentCount);
        Assert.Equal(0, result.ManagerConstraintImportSummary.ImportedAvoidAssignmentCount);
        Assert.Equal(0, result.ManagerConstraintImportSummary.ImportedShiftCapacityOverrideCount);
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
    public void Run_ShouldReturnManagerConstraintImportFatalErrors_AndSkipOptimization()
    {
        using var stream = CreateWorkbookStream(worksheet =>
        {
            worksheet.Cell(1, 1).Value = "שם המאבטח";
            worksheet.Cell(1, 2).Value = "ראשון - 14/06";
            worksheet.Cell(1, 3).Value = "שני - 15/06";

            worksheet.Cell(2, 1).Value = "Worker16 אלדר";
            worksheet.Cell(2, 2).Value = "בוקר";

            worksheet.Cell(3, 1).Value = "Worker14";
            worksheet.Cell(3, 3).Value = "ערב";
        });

        var result = new AvailabilityMatrixStreamOptimizationRunner(
                new XlsxFormTableReader())
            .Run(new AvailabilityMatrixStreamOptimizationRequest(
                stream,
                CreateSchedulePeriod(),
                CreateResources(),
                CreateShifts(),
                TotalEffectiveTargetHours: 16,
                MaximumAssignedHoursDeviationFromAverageHours: 8,
                Seed: 20260612,
                ManagerConstraintRows:
                [
                    CreateManagerConstraintHeaders(),
                    [
                        "ForbidAssignment",
                        "עובד לא מוכר",
                        "2026-06-14",
                        "Morning",
                        string.Empty,
                        string.Empty
                    ]
                ]));

        Assert.Empty(result.ImportFatalErrors);
        Assert.Empty(result.ImportWarnings);
        Assert.NotEmpty(result.ImportedWorkerSubmissions);

        var fatalError = Assert.Single(result.ManagerConstraintImportFatalErrors);

        Assert.Equal(ManagerConstraintRowsImportFatalErrorType.UnresolvedWorkerName, fatalError.Type);
        Assert.Equal(1, fatalError.RowIndex);
        Assert.Equal("WorkerName", fatalError.Header);
        Assert.Equal("עובד לא מוכר", fatalError.RawValue);

        Assert.Empty(result.ManagerConstraintImportWarnings);
        Assert.Null(result.OptimizationResult);
    }

    private static IReadOnlyList<IReadOnlyList<string>> CreateManagerConstraintRows()
    {
        return
        [
            CreateManagerConstraintHeaders(),
            [
                "ForbidAssignment",
                "Worker16 אלדר",
                "2026-06-14",
                "Morning",
                string.Empty,
                string.Empty
            ]
        ];
    }

    private static IReadOnlyList<string> CreateManagerConstraintHeaders()
    {
        return
        [
            "Type",
            "WorkerName",
            "Date",
            "ShiftKind",
            "MinResourceCount",
            "MaxResourceCount"
        ];
    }


    private static MemoryStream CreateWorkbookStream(
        Action<IXLWorksheet> configure)
    {
        var stream = new MemoryStream();

        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Matrix");

            configure(worksheet);

            workbook.SaveAs(stream);
        }

        stream.Position = 0;

        return stream;
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

    private static IReadOnlyList<Shift> CreateShifts()
    {
        return
        [
            new Shift(
                Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                new DateTime(2026, 6, 14, 6, 30, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 14, 14, 30, 0, DateTimeKind.Utc),
                ShiftKind.Morning,
                minResourceCount: 1,
                maxResourceCount: 1,
                requiresPreferenceToAssign: true),
            new Shift(
                Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                new DateTime(2026, 6, 15, 22, 30, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 16, 6, 30, 0, DateTimeKind.Utc),
                ShiftKind.Night,
                minResourceCount: 1,
                maxResourceCount: 1,
                requiresPreferenceToAssign: true)
        ];
    }
}
