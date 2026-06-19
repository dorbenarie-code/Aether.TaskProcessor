using Aether.Application.Scheduling.SubmissionForms;
using Aether.Domain.Optimization;
using Aether.Infrastructure.Forms;
using ClosedXML.Excel;

namespace Aether.Tests.Infrastructure.Forms;

public sealed class AvailabilityMatrixStreamOptimizationRunnerTests
{
    private const int Seed = 20260612;

    [Fact]
    public void Run_ShouldReadXlsxCleanAvailabilityMatrix_AndOptimizeImportedRows()
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

        var runner = new AvailabilityMatrixStreamOptimizationRunner(
            new XlsxFormTableReader());

        var result = runner.Run(new AvailabilityMatrixStreamOptimizationRequest(
            stream,
            CreateSchedulePeriod(),
            resources,
            shifts,
            TotalEffectiveTargetHours: 16,
            MaximumAssignedHoursDeviationFromAverageHours: 8,
            Seed: Seed));

        Assert.Empty(result.ImportFatalErrors);
        Assert.Empty(result.ImportWarnings);

        Assert.Equal(2, result.ImportedWorkerSubmissions.Count);

        Assert.Contains(
            result.ImportedWorkerSubmissions,
            submission =>
                submission.ResourceId == resources[0].Id &&
                submission.ShiftSubmissions.Single().Date == new DateOnly(2026, 6, 14) &&
                submission.ShiftSubmissions.Single().ShiftKind == ShiftKind.Morning);

        Assert.Contains(
            result.ImportedWorkerSubmissions,
            submission =>
                submission.ResourceId == resources[1].Id &&
                submission.ShiftSubmissions.Single().Date == new DateOnly(2026, 6, 15) &&
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
    public void Run_ShouldReturnImportFatalErrors_AndSkipOptimization_WhenXlsxWorksheetIsEmpty()
    {
        using var stream = CreateWorkbookStream(_ =>
        {
        });

        var runner = new AvailabilityMatrixStreamOptimizationRunner(
            new XlsxFormTableReader());

        var result = runner.Run(new AvailabilityMatrixStreamOptimizationRequest(
            stream,
            CreateSchedulePeriod(),
            CreateResources(),
            CreateShifts(),
            TotalEffectiveTargetHours: 16));

        Assert.Empty(result.ImportedWorkerSubmissions);
        Assert.Empty(result.ImportWarnings);

        var fatalError = Assert.Single(result.ImportFatalErrors);

        Assert.Equal(AvailabilityMatrixImportFatalErrorType.EmptyTable, fatalError.Type);
        Assert.Null(result.OptimizationResult);
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
