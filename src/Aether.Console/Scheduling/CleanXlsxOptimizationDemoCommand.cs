using Aether.Application.Scheduling.Profiles;
using Aether.Application.Scheduling.ScheduleGeneration;
using Aether.Application.Scheduling.SubmissionForms;
using Aether.Infrastructure.Forms;
using Aether.Infrastructure.Scheduling.Reports;

namespace Aether.Console.Scheduling;

public static class CleanXlsxOptimizationDemoCommand
{
    private const string ApplyPostRunLocalAddImprovementFlag = "--apply-post-run-local-add-improvement";

    public static int Run(string[] args)
    {
        var applyPostRunLocalAddImprovement = args.Contains(
            ApplyPostRunLocalAddImprovementFlag,
            StringComparer.Ordinal);

        var unknownOption = args
            .Where(arg => arg.StartsWith("--", StringComparison.Ordinal))
            .FirstOrDefault(arg => !string.Equals(
                arg,
                ApplyPostRunLocalAddImprovementFlag,
                StringComparison.Ordinal));

        if (unknownOption is not null)
        {
            System.Console.Error.WriteLine($"Unknown option: {unknownOption}");
            PrintUsage();

            return 1;
        }

        var positionalArgs = args
            .Where(arg => !string.Equals(
                arg,
                ApplyPostRunLocalAddImprovementFlag,
                StringComparison.Ordinal))
            .ToArray();

        if (positionalArgs.Length is not (2 or 3 or 4 or 5))
        {
            PrintUsage();

            return 1;
        }

        var inputPath = positionalArgs[0];
        var outputPath = positionalArgs[1];
        var scheduleTableOutputPath = positionalArgs.Length >= 3
            ? positionalArgs[2]
            : CreateDefaultScheduleTableOutputPath(outputPath);
        var scheduleTableXlsxOutputPath = positionalArgs.Length >= 4
            ? positionalArgs[3]
            : null;

        var targetGapDiagnosticsOutputPath = positionalArgs.Length == 5
            ? positionalArgs[4]
            : null;

        if (!File.Exists(inputPath))
        {
            System.Console.Error.WriteLine($"Input XLSX file was not found: {inputPath}");

            return 2;
        }

        try
        {
            return RunOptimization(
                inputPath,
                outputPath,
                scheduleTableOutputPath,
                scheduleTableXlsxOutputPath,
                targetGapDiagnosticsOutputPath,
                applyPostRunLocalAddImprovement);
        }
        catch (Exception exception)
        {
            System.Console.Error.WriteLine("Clean XLSX optimization failed.");
            System.Console.Error.WriteLine(exception);

            return 99;
        }
    }

    private static int RunOptimization(
        string inputPath,
        string outputPath,
        string scheduleTableOutputPath,
        string? scheduleTableXlsxOutputPath,
        string? targetGapDiagnosticsOutputPath,
        bool applyPostRunLocalAddImprovement)
    {
        using var workbookInput = new XlsxAvailabilityMatrixWorkbookInputReader()
            .Open(inputPath);

        var profile = new LastDorLocalScheduleGenerationProfile();

        var resources = profile.CreateResources();
        var shifts = profile.CreateShifts();

        var useCase = new CleanXlsxScheduleGenerationUseCase(
            new AvailabilityMatrixStreamOptimizationRunner(
                new XlsxFormTableReader()),
            new ScheduleTableXlsxExporter());

        var result = useCase.Run(new CleanXlsxScheduleGenerationRequest(
            workbookInput.AvailabilityMatrixStream,
            profile.CreateSchedulePeriod(),
            resources,
            shifts,
            profile.TotalEffectiveTargetHours,
            MaximumAssignedHoursDeviationFromAverageHours:
                profile.MaximumAssignedHoursDeviationFromAverageHours,
            Seed: profile.Seed,
            ManagerConstraintRows: workbookInput.ManagerConstraintRows,
            ApplyPostRunLocalAddImprovement: applyPostRunLocalAddImprovement,
            IncludeTargetGapDiagnostics: !string.IsNullOrWhiteSpace(targetGapDiagnosticsOutputPath)));

        if (!result.Succeeded)
        {
            return PrintFailure(result);
        }

        WriteTextFile(
            outputPath,
            RequireText(
                result.ReviewText,
                nameof(result.ReviewText)));

        WriteTextFile(
            scheduleTableOutputPath,
            RequireText(
                result.ScheduleTableCsv,
                nameof(result.ScheduleTableCsv)));

        if (!string.IsNullOrWhiteSpace(scheduleTableXlsxOutputPath))
        {
            WriteBytesFile(
                scheduleTableXlsxOutputPath,
                RequireBytes(
                    result.ScheduleTableXlsxBytes,
                    nameof(result.ScheduleTableXlsxBytes)));
        }

        if (!string.IsNullOrWhiteSpace(targetGapDiagnosticsOutputPath))
        {
            WriteTextFile(
                targetGapDiagnosticsOutputPath,
                RequireText(
                    result.TargetGapDiagnosticsText,
                    nameof(result.TargetGapDiagnosticsText)));
        }

        PrintSummary(
            inputPath,
            outputPath,
            scheduleTableOutputPath,
            scheduleTableXlsxOutputPath,
            targetGapDiagnosticsOutputPath,
            result);

        return 0;
    }

    private static int PrintFailure(
        CleanXlsxScheduleGenerationResult result)
    {
        return result.FailureType switch
        {
            ScheduleGenerationFailureType.AvailabilityMatrixImportFailed =>
                PrintImportFatalErrors(result),

            ScheduleGenerationFailureType.ManagerConstraintImportFailed =>
                PrintManagerConstraintImportFatalErrors(result),

            _ => PrintMissingOptimizationResult()
        };
    }

    private static int PrintImportFatalErrors(
        CleanXlsxScheduleGenerationResult result)
    {
        System.Console.Error.WriteLine("Import failed with fatal errors.");

        foreach (var fatalError in result.ImportFatalErrors)
        {
            System.Console.Error.WriteLine(
                $"- {fatalError.Type}: Row={fatalError.RowIndex?.ToString() ?? "-"}, " +
                $"Column={fatalError.ColumnIndex?.ToString() ?? "-"}, " +
                $"Header={fatalError.Header ?? "-"}, RawValue={fatalError.RawValue ?? "-"}");
        }

        return 3;
    }

    private static int PrintManagerConstraintImportFatalErrors(
        CleanXlsxScheduleGenerationResult result)
    {
        System.Console.Error.WriteLine("Manager constraint import failed with fatal errors.");

        foreach (var fatalError in result.ManagerConstraintImportFatalErrors)
        {
            System.Console.Error.WriteLine(
                $"- {fatalError.Type}: Row={fatalError.RowIndex?.ToString() ?? "-"}, " +
                $"Column={fatalError.ColumnIndex?.ToString() ?? "-"}, " +
                $"Header={fatalError.Header ?? "-"}, RawValue={fatalError.RawValue ?? "-"}");
        }

        return 5;
    }

    private static int PrintMissingOptimizationResult()
    {
        System.Console.Error.WriteLine("Optimization result was not created.");

        return 4;
    }

    private static void WriteTextFile(
        string path,
        string content)
    {
        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(
            path,
            content);
    }

    private static void WriteBytesFile(
        string path,
        byte[] content)
    {
        var directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(
            path,
            content);
    }

    private static string RequireText(
        string? value,
        string name)
    {
        return value ??
            throw new InvalidOperationException($"{name} was not generated.");
    }

    private static byte[] RequireBytes(
        byte[]? value,
        string name)
    {
        return value ??
            throw new InvalidOperationException($"{name} was not generated.");
    }

    private static void PrintSummary(
        string inputPath,
        string outputPath,
        string scheduleTableOutputPath,
        string? scheduleTableXlsxOutputPath,
        string? targetGapDiagnosticsOutputPath,
        CleanXlsxScheduleGenerationResult result)
    {
        var summary = result.Summary ??
            throw new InvalidOperationException("Schedule generation summary is required.");

        System.Console.WriteLine("Clean XLSX optimization completed.");
        System.Console.WriteLine($"Input: {inputPath}");
        System.Console.WriteLine($"Report: {outputPath}");
        System.Console.WriteLine($"ScheduleTableCsv: {scheduleTableOutputPath}");

        if (!string.IsNullOrWhiteSpace(scheduleTableXlsxOutputPath))
        {
            System.Console.WriteLine($"ScheduleTableXlsx: {scheduleTableXlsxOutputPath}");
        }

        if (!string.IsNullOrWhiteSpace(targetGapDiagnosticsOutputPath))
        {
            System.Console.WriteLine($"TargetGapDiagnostics: {targetGapDiagnosticsOutputPath}");
        }

        if (summary.PostRunLocalAddImprovementApplied)
        {
            System.Console.WriteLine("PostRunLocalAddImprovement: Applied");
            System.Console.WriteLine($"PostRunLocalAddImprovementAcceptedAddMoves: {summary.PostRunLocalAddImprovementAcceptedAddMoveCount}");
            System.Console.WriteLine($"PostRunLocalAddImprovementInitialTotalPenalty: {summary.PostRunLocalAddImprovementInitialTotalPenalty}");
            System.Console.WriteLine($"PostRunLocalAddImprovementFinalTotalPenalty: {summary.PostRunLocalAddImprovementFinalTotalPenalty}");
            System.Console.WriteLine($"PostRunLocalAddImprovementPenaltyDelta: {summary.PostRunLocalAddImprovementPenaltyDelta}");
        }

        System.Console.WriteLine($"ImportWarnings: {result.ImportWarnings.Count}");

        foreach (var warning in result.ImportWarnings)
        {
            System.Console.WriteLine(
                $"- Warning {warning.Type}: Header={warning.Header ?? "-"}, RawValue={warning.RawValue ?? "-"}");
        }

        var managerConstraintSummary = result.ManagerConstraintImportSummary;
        var importedManagerConstraintCount =
            managerConstraintSummary.ImportedForbiddenAssignmentCount +
            managerConstraintSummary.ImportedAvoidAssignmentCount +
            managerConstraintSummary.ImportedShiftCapacityOverrideCount;

        System.Console.WriteLine("Manager constraints:");
        System.Console.WriteLine($"  Import warnings: {result.ManagerConstraintImportWarnings.Count}");
        System.Console.WriteLine($"  Fatal errors: {result.ManagerConstraintImportFatalErrors.Count}");
        System.Console.WriteLine($"  Imported constraints: {importedManagerConstraintCount}");
        System.Console.WriteLine($"    - ForbidAssignment: {managerConstraintSummary.ImportedForbiddenAssignmentCount}");
        System.Console.WriteLine($"    - AvoidAssignment: {managerConstraintSummary.ImportedAvoidAssignmentCount}");
        System.Console.WriteLine($"    - ShiftCapacityOverride: {managerConstraintSummary.ImportedShiftCapacityOverrideCount}");

        System.Console.WriteLine($"ImportedWorkerSubmissions: {summary.ImportedWorkerSubmissionCount}");
        System.Console.WriteLine($"SubmittedShiftSelections: {summary.SubmittedShiftSelectionCount}");
        System.Console.WriteLine($"Resources: {summary.ResourceCount}");
        System.Console.WriteLine($"Shifts: {summary.ShiftCount}");
        System.Console.WriteLine($"ResourceWorkloadDemands: {summary.ResourceWorkloadDemandCount}");
        System.Console.WriteLine($"Assignments: {summary.AssignmentCount}");
        System.Console.WriteLine($"IsFeasible: {summary.IsFeasible}");
        System.Console.WriteLine($"HardViolationCount: {summary.HardViolationCount}");
        System.Console.WriteLine($"SoftViolationCount: {summary.SoftViolationCount}");
        System.Console.WriteLine($"TotalPenalty: {summary.TotalPenalty}");
        System.Console.WriteLine($"GenerationDiagnostics: {summary.GenerationDiagnosticCount}");
    }

    private static string CreateDefaultScheduleTableOutputPath(string outputPath)
    {
        var directory = Path.GetDirectoryName(outputPath);
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(outputPath);

        var tableFileName = fileNameWithoutExtension.EndsWith(
            "-review",
            StringComparison.OrdinalIgnoreCase)
            ? $"{fileNameWithoutExtension[..^"-review".Length]}-schedule-table.csv"
            : $"{fileNameWithoutExtension}-schedule-table.csv";

        return string.IsNullOrWhiteSpace(directory)
            ? tableFileName
            : Path.Combine(directory, tableFileName);
    }

    private static void PrintUsage()
    {
        System.Console.WriteLine("Usage:");
        System.Console.WriteLine("  dotnet run --project src/Aether.Console -- optimize-clean-xlsx <input.xlsx> <output.txt> [schedule-table.csv] [schedule-table.xlsx] [target-gap-diagnostics.txt] [--apply-post-run-local-add-improvement]");
    }
}
