using Aether.Console.Scheduling;
using ClosedXML.Excel;

namespace Aether.Tests.Console.Scheduling;

public sealed class CleanXlsxOptimizationDemoCommandTests
{
    [Fact]
    [Trait("Category", "SchedulingExperiment")]
    public void Run_ShouldReturnManagerConstraintImportFatalErrors_WhenManagerConstraintsSheetContainsInvalidRows()
    {
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            $"aether-clean-xlsx-command-{Guid.NewGuid():N}");

        Directory.CreateDirectory(tempDirectory);

        var inputPath = Path.Combine(tempDirectory, "input.xlsx");
        var reportPath = Path.Combine(tempDirectory, "review.txt");
        var scheduleTablePath = Path.Combine(tempDirectory, "schedule-table.csv");

        CreateWorkbookWithInvalidManagerConstraintSheet(inputPath);

        var originalError = System.Console.Error;
        var originalOut = System.Console.Out;

        using var errorWriter = new StringWriter();
        using var outputWriter = new StringWriter();

        try
        {
            System.Console.SetError(errorWriter);
            System.Console.SetOut(outputWriter);

            var exitCode = CleanXlsxOptimizationDemoCommand.Run(
            [
                inputPath,
                reportPath,
                scheduleTablePath
            ]);

            Assert.Equal(5, exitCode);

            var errorOutput = errorWriter.ToString();

            Assert.Contains("Manager constraint import failed with fatal errors.", errorOutput);
            Assert.Contains("UnresolvedWorkerName", errorOutput);
            Assert.Contains("WorkerName", errorOutput);
            Assert.Contains("עובד לא מוכר", errorOutput);

            Assert.False(File.Exists(reportPath));
            Assert.False(File.Exists(scheduleTablePath));
        }
        finally
        {
            System.Console.SetError(originalError);
            System.Console.SetOut(originalOut);

            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }


    [Fact]
    [Trait("Category", "SchedulingExperiment")]
    public void Run_ShouldPrintManagerConstraintImportSummary_WhenManagerConstraintsSheetContainsValidRows()
    {
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            $"aether-clean-xlsx-command-{Guid.NewGuid():N}");

        Directory.CreateDirectory(tempDirectory);

        var inputPath = Path.Combine(tempDirectory, "input.xlsx");
        var reportPath = Path.Combine(tempDirectory, "review.txt");
        var scheduleTablePath = Path.Combine(tempDirectory, "schedule-table.csv");

        CreateWorkbookWithValidManagerConstraintSheet(inputPath);

        var originalError = System.Console.Error;
        var originalOut = System.Console.Out;

        using var errorWriter = new StringWriter();
        using var outputWriter = new StringWriter();

        try
        {
            System.Console.SetError(errorWriter);
            System.Console.SetOut(outputWriter);

            var exitCode = CleanXlsxOptimizationDemoCommand.Run(
            [
                inputPath,
                reportPath,
                scheduleTablePath
            ]);

            Assert.Equal(0, exitCode);

            var output = outputWriter.ToString();

            Assert.Contains("Manager constraints:", output);
            Assert.Contains("  Import warnings: 0", output);
            Assert.Contains("  Fatal errors: 0", output);
            Assert.Contains("  Imported constraints: 3", output);
            Assert.Contains("    - ForbidAssignment: 1", output);
            Assert.Contains("    - AvoidAssignment: 1", output);
            Assert.Contains("    - ShiftCapacityOverride: 1", output);

            Assert.DoesNotContain("ManagerConstraintImportWarnings", output);
            Assert.DoesNotContain("ManagerConstraintImportFatalErrors", output);
            Assert.DoesNotContain("ImportedManagerForbiddenAssignments", output);
            Assert.DoesNotContain("ImportedManagerAvoidAssignments", output);
            Assert.DoesNotContain("ImportedManagerShiftCapacityOverrides", output);

            Assert.True(File.Exists(reportPath));
            Assert.True(File.Exists(scheduleTablePath));
        }
        finally
        {
            System.Console.SetError(originalError);
            System.Console.SetOut(originalOut);

            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }


    [Fact]
    [Trait("Category", "SchedulingExperiment")]
    public void Run_ShouldWriteScheduleTableXlsx_WhenScheduleTableXlsxPathIsProvided()
    {
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            $"aether-clean-xlsx-command-{Guid.NewGuid():N}");

        Directory.CreateDirectory(tempDirectory);

        var inputPath = Path.Combine(tempDirectory, "input.xlsx");
        var reportPath = Path.Combine(tempDirectory, "review.txt");
        var scheduleTablePath = Path.Combine(tempDirectory, "schedule-table.csv");
        var scheduleTableXlsxPath = Path.Combine(tempDirectory, "schedule-table.xlsx");

        CreateWorkbookWithValidManagerConstraintSheet(inputPath);

        var originalError = System.Console.Error;
        var originalOut = System.Console.Out;

        using var errorWriter = new StringWriter();
        using var outputWriter = new StringWriter();

        try
        {
            System.Console.SetError(errorWriter);
            System.Console.SetOut(outputWriter);

            var exitCode = CleanXlsxOptimizationDemoCommand.Run(
            [
                inputPath,
                reportPath,
                scheduleTablePath,
                scheduleTableXlsxPath
            ]);

            Assert.Equal(0, exitCode);

            var output = outputWriter.ToString();

            Assert.Contains("ScheduleTableXlsx:", output);
            Assert.Contains(scheduleTableXlsxPath, output);

            Assert.True(File.Exists(reportPath));
            Assert.True(File.Exists(scheduleTablePath));
            Assert.True(File.Exists(scheduleTableXlsxPath));

            using var workbook = new XLWorkbook(scheduleTableXlsxPath);

            Assert.Contains(
                workbook.Worksheets,
                worksheet => worksheet.Name == "סידור");

            Assert.DoesNotContain(
                workbook.Worksheets,
                worksheet => worksheet.Name == "Schedule");

            var worksheet = workbook.Worksheet("סידור");

            Assert.True(worksheet.RightToLeft);
            Assert.Equal("משמרת", worksheet.Cell(1, 1).GetString());
            Assert.Equal("יום", worksheet.Cell(2, 1).GetString());
            Assert.Equal("ראשון", worksheet.Cell(2, 2).GetString());
        }

        finally
        {
            System.Console.SetError(originalError);
            System.Console.SetOut(originalOut);

            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }




    [Fact]
    [Trait("Category", "SchedulingExperiment")]
    public void Run_ShouldWriteTargetGapDiagnostics_WhenTargetGapDiagnosticsPathIsProvided()
    {
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            $"aether-clean-xlsx-command-{Guid.NewGuid():N}");

        Directory.CreateDirectory(tempDirectory);

        var inputPath = Path.Combine(tempDirectory, "input.xlsx");
        var reportPath = Path.Combine(tempDirectory, "review.txt");
        var scheduleTablePath = Path.Combine(tempDirectory, "schedule-table.csv");
        var scheduleTableXlsxPath = Path.Combine(tempDirectory, "schedule-table.xlsx");
        var targetGapDiagnosticsPath = Path.Combine(tempDirectory, "target-gap-diagnostics.txt");

        CreateWorkbookWithValidManagerConstraintSheet(inputPath);

        var originalError = System.Console.Error;
        var originalOut = System.Console.Out;

        using var errorWriter = new StringWriter();
        using var outputWriter = new StringWriter();

        try
        {
            System.Console.SetError(errorWriter);
            System.Console.SetOut(outputWriter);

            var exitCode = CleanXlsxOptimizationDemoCommand.Run(
            [
                inputPath,
                reportPath,
                scheduleTablePath,
                scheduleTableXlsxPath,
                targetGapDiagnosticsPath
            ]);

            Assert.Equal(0, exitCode);

            var output = outputWriter.ToString();

            Assert.Contains("TargetGapDiagnostics:", output);
            Assert.Contains(targetGapDiagnosticsPath, output);

            Assert.True(File.Exists(reportPath));
            Assert.True(File.Exists(scheduleTablePath));
            Assert.True(File.Exists(scheduleTableXlsxPath));
            Assert.True(File.Exists(targetGapDiagnosticsPath));

            var diagnosticsReport = File.ReadAllText(targetGapDiagnosticsPath);

            Assert.Contains("Clean GA Target Gap Explainability Diagnostics", diagnosticsReport);
            Assert.Contains("WorkerTargetGaps:", diagnosticsReport);
            Assert.Contains("AddMoveDiagnostics:", diagnosticsReport);
            Assert.Contains("RejectedAddMoves:", diagnosticsReport);
            Assert.Contains("RejectedHardViolationByType:", diagnosticsReport);
            Assert.Contains("TransferMoveDiagnostics:", diagnosticsReport);
        }
        finally
        {
            System.Console.SetError(originalError);
            System.Console.SetOut(originalOut);

            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }
    [Fact]
    [Trait("Category", "SchedulingExperiment")]
    public void Run_ShouldApplyPostRunLocalAddImprovement_WhenFlagIsProvided()
    {
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            $"aether-clean-xlsx-command-{Guid.NewGuid():N}");

        Directory.CreateDirectory(tempDirectory);

        var inputPath = Path.Combine(tempDirectory, "input.xlsx");
        var reportPath = Path.Combine(tempDirectory, "review.txt");
        var scheduleTablePath = Path.Combine(tempDirectory, "schedule-table.csv");
        var scheduleTableXlsxPath = Path.Combine(tempDirectory, "schedule-table.xlsx");
        var targetGapDiagnosticsPath = Path.Combine(tempDirectory, "target-gap-diagnostics.txt");

        CreateWorkbookWithValidManagerConstraintSheet(inputPath);

        var originalError = System.Console.Error;
        var originalOut = System.Console.Out;

        using var errorWriter = new StringWriter();
        using var outputWriter = new StringWriter();

        try
        {
            System.Console.SetError(errorWriter);
            System.Console.SetOut(outputWriter);

            var exitCode = CleanXlsxOptimizationDemoCommand.Run(
            [
                inputPath,
                reportPath,
                scheduleTablePath,
                scheduleTableXlsxPath,
                targetGapDiagnosticsPath,
                "--apply-post-run-local-add-improvement"
            ]);

            Assert.Equal(0, exitCode);

            var output = outputWriter.ToString();

            Assert.Contains("TargetGapDiagnostics:", output);
            Assert.Contains(targetGapDiagnosticsPath, output);
            Assert.Contains("PostRunLocalAddImprovement: Applied", output);
            Assert.Contains("PostRunLocalAddImprovementAcceptedAddMoves:", output);
            Assert.Contains("PostRunLocalAddImprovementInitialTotalPenalty:", output);
            Assert.Contains("PostRunLocalAddImprovementFinalTotalPenalty:", output);
            Assert.Contains("PostRunLocalAddImprovementPenaltyDelta:", output);

            Assert.True(File.Exists(reportPath));
            Assert.True(File.Exists(scheduleTablePath));
            Assert.True(File.Exists(scheduleTableXlsxPath));
            Assert.True(File.Exists(targetGapDiagnosticsPath));

            var diagnosticsReport = File.ReadAllText(targetGapDiagnosticsPath);

            Assert.Contains("Clean GA Target Gap Explainability Diagnostics", diagnosticsReport);
            Assert.Contains("WorkerTargetGaps:", diagnosticsReport);
            Assert.Contains("AddMoveDiagnostics:", diagnosticsReport);
            Assert.Contains("RejectedAddMoves:", diagnosticsReport);
        }
        finally
        {
            System.Console.SetError(originalError);
            System.Console.SetOut(originalOut);

            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    private static void CreateWorkbookWithValidManagerConstraintSheet(
        string outputPath)
    {
        using var workbook = new XLWorkbook(FindFixturePath());

        var existingWorksheet = workbook.Worksheets.FirstOrDefault(worksheet =>
            string.Equals(
                worksheet.Name,
                "ManagerConstraints",
                StringComparison.Ordinal));

        existingWorksheet?.Delete();

        var worksheet = workbook.Worksheets.Add("ManagerConstraints");

        worksheet.Cell(1, 1).Value = "Type";
        worksheet.Cell(1, 2).Value = "WorkerName";
        worksheet.Cell(1, 3).Value = "Date";
        worksheet.Cell(1, 4).Value = "ShiftKind";
        worksheet.Cell(1, 5).Value = "MinResourceCount";
        worksheet.Cell(1, 6).Value = "MaxResourceCount";

        worksheet.Cell(2, 1).Value = "ForbidAssignment";
        worksheet.Cell(2, 2).Value = "Worker16";
        worksheet.Cell(2, 3).Value = "2026-06-14";
        worksheet.Cell(2, 4).Value = "Morning";

        worksheet.Cell(3, 1).Value = "AvoidAssignment";
        worksheet.Cell(3, 2).Value = "Worker14";
        worksheet.Cell(3, 3).Value = "2026-06-15";
        worksheet.Cell(3, 4).Value = "Morning";

        worksheet.Cell(4, 1).Value = "ShiftCapacityOverride";
        worksheet.Cell(4, 3).Value = "2026-06-14";
        worksheet.Cell(4, 4).Value = "Morning";
        worksheet.Cell(4, 5).Value = "4";
        worksheet.Cell(4, 6).Value = "6";

        workbook.SaveAs(outputPath);
    }

    private static void CreateWorkbookWithInvalidManagerConstraintSheet(
        string outputPath)
    {
        using var workbook = new XLWorkbook(FindFixturePath());

        var existingWorksheet = workbook.Worksheets.FirstOrDefault(worksheet =>
            string.Equals(
                worksheet.Name,
                "ManagerConstraints",
                StringComparison.Ordinal));

        existingWorksheet?.Delete();

        var worksheet = workbook.Worksheets.Add("ManagerConstraints");

        worksheet.Cell(1, 1).Value = "Type";
        worksheet.Cell(1, 2).Value = "WorkerName";
        worksheet.Cell(1, 3).Value = "Date";
        worksheet.Cell(1, 4).Value = "ShiftKind";
        worksheet.Cell(1, 5).Value = "MinResourceCount";
        worksheet.Cell(1, 6).Value = "MaxResourceCount";

        worksheet.Cell(2, 1).Value = "ForbidAssignment";
        worksheet.Cell(2, 2).Value = "עובד לא מוכר";
        worksheet.Cell(2, 3).Value = "2026-06-14";
        worksheet.Cell(2, 4).Value = "Morning";

        workbook.SaveAs(outputPath);
    }

    private static string FindFixturePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidatePath = Path.Combine(
                directory.FullName,
                "TestData",
                "Forms",
                "last-dor-clean-availability-matrix.xlsx");

            if (File.Exists(candidatePath))
            {
                return candidatePath;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "Could not find test fixture 'last-dor-clean-availability-matrix.xlsx'.");
    }
}
