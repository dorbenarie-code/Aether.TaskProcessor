using System.Text;
using Aether.Application.Scheduling.Reports;
using Aether.Application.Scheduling.SubmissionForms;
using Aether.Domain.Optimization;
using Aether.Infrastructure.Forms;

namespace Aether.Console.Scheduling;

public static class MorningCapacitySensitivityDemoCommand
{
    private const double TotalEffectiveTargetHours = 736.0;
    private const double BalanceToleranceHours = 5.0;
    private const int Seed = 20260603;

    private static readonly UTF8Encoding Utf8WithBom = new(
        encoderShouldEmitUTF8Identifier: true);

    public static int Run(string[] args)
    {
        if (args.Length != 2)
        {
            PrintUsage();

            return 1;
        }

        var inputPath = args[0];
        var outputDirectory = args[1];

        if (!File.Exists(inputPath))
        {
            System.Console.Error.WriteLine($"Input XLSX file was not found: {inputPath}");

            return 2;
        }

        try
        {
            return RunSensitivity(
                inputPath,
                outputDirectory);
        }
        catch (Exception exception)
        {
            System.Console.Error.WriteLine("Morning capacity sensitivity failed.");
            System.Console.Error.WriteLine(exception);

            return 99;
        }
    }

    private static int RunSensitivity(
        string inputPath,
        string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        var variants = new[]
        {
            new MorningCapacityVariant(
                Key: "variant-a-current-min3",
                Name: "Variant A — Current",
                WeekdayMorningMinResourceCount: 3),
            new MorningCapacityVariant(
                Key: "variant-b-morning-min4",
                Name: "Variant B — Morning Min 4",
                WeekdayMorningMinResourceCount: 4),
            new MorningCapacityVariant(
                Key: "variant-c-morning-min5",
                Name: "Variant C — Morning Min 5",
                WeekdayMorningMinResourceCount: 5)
        };

        var summaries = new List<MorningCapacityVariantSummary>();

        foreach (var variant in variants)
        {
            var summary = RunVariant(
                inputPath,
                outputDirectory,
                variant);

            summaries.Add(summary);

            PrintVariantSummary(summary);
        }

        var summaryCsv = FormatSummaryCsv(summaries);
        var summaryPath = Path.Combine(
            outputDirectory,
            "morning-capacity-sensitivity-summary.csv");

        File.WriteAllText(
            summaryPath,
            summaryCsv,
            Utf8WithBom);

        System.Console.WriteLine();
        System.Console.WriteLine("Morning capacity sensitivity completed.");
        System.Console.WriteLine($"SummaryCsv: {summaryPath}");

        return summaries.Any(summary => !summary.IsFeasible || summary.HardViolationCount > 0)
            ? 10
            : 0;
    }

    private static MorningCapacityVariantSummary RunVariant(
        string inputPath,
        string outputDirectory,
        MorningCapacityVariant variant)
    {
        using var stream = File.OpenRead(inputPath);

        var resources = LastDorDemoSchedulingFactory.CreateResources();
        var shifts = LastDorDemoSchedulingFactory.CreateBiWeeklySequencePressureShifts(
            variant.WeekdayMorningMinResourceCount);

        var runner = new AvailabilityMatrixStreamOptimizationRunner(
            new XlsxFormTableReader());

        var result = runner.Run(new AvailabilityMatrixStreamOptimizationRequest(
            stream,
            LastDorDemoSchedulingFactory.CreateSchedulePeriod(),
            resources,
            shifts,
            TotalEffectiveTargetHours,
            BalanceToleranceHours,
            Seed));

        if (result.ImportFatalErrors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Variant '{variant.Key}' failed import with {result.ImportFatalErrors.Count} fatal errors.");
        }

        if (result.OptimizationResult is null)
        {
            throw new InvalidOperationException(
                $"Variant '{variant.Key}' did not produce an optimization result.");
        }

        var optimizationResult = result.OptimizationResult;

        var reportPath = Path.Combine(
            outputDirectory,
            $"{variant.Key}-review.txt");

        var tablePath = Path.Combine(
            outputDirectory,
            $"{variant.Key}-schedule-table.csv");

        var reviewReport = new SchedulingRunReportFormatter()
            .FormatOptimizationReview(
                optimizationResult.Problem,
                optimizationResult.GeneticResult);

        File.WriteAllText(
            reportPath,
            reviewReport);

        var scheduleTableCsv = new ScheduleTableCsvFormatter()
            .Format(
                optimizationResult.Problem,
                optimizationResult.GeneticResult);

        File.WriteAllText(
            tablePath,
            scheduleTableCsv,
            Utf8WithBom);

        return CreateSummary(
            variant,
            reportPath,
            tablePath,
            result);
    }

    private static MorningCapacityVariantSummary CreateSummary(
        MorningCapacityVariant variant,
        string reportPath,
        string tablePath,
        AvailabilityMatrixImportedRowsOptimizationResult result)
    {
        var optimizationResult = result.OptimizationResult ??
            throw new InvalidOperationException("Optimization result is required.");

        var problem = optimizationResult.Problem;
        var geneticResult = optimizationResult.GeneticResult;

        var shiftsById = problem.Shifts.ToDictionary(shift => shift.Id);

        var totalAssignedHours = geneticResult.Candidate.Assignments
            .Sum(assignment =>
            {
                var shift = shiftsById[assignment.ShiftId];

                return (shift.EndUtc - shift.StartUtc).TotalHours;
            });

        var weekdayMorningShifts = problem.Shifts
            .Where(IsWeekdayMorning)
            .ToArray();

        var assignmentCountByShiftId = geneticResult.Candidate.Assignments
            .GroupBy(assignment => assignment.ShiftId)
            .ToDictionary(
                group => group.Key,
                group => group.Count());

        var weekdayMorningAssignedCounts = weekdayMorningShifts
            .Select(shift =>
            {
                assignmentCountByShiftId.TryGetValue(
                    shift.Id,
                    out var count);

                return count;
            })
            .ToArray();

        var ignoredAvoidCount = GetViolationCount(
            geneticResult,
            ConstraintViolationType.IgnoredAvoidPreference);

        var sequenceQuotaCount = GetViolationCount(
            geneticResult,
            ConstraintViolationType.ShiftSequenceQuotaExceeded);

        var monthlyNightQuotaCount = GetViolationCount(
            geneticResult,
            ConstraintViolationType.ResourceMonthlyNightShiftQuotaExceeded);

        var submittedShiftSelections = result.ImportedWorkerSubmissions
            .Sum(submission => submission.ShiftSubmissions.Count);

        return new MorningCapacityVariantSummary(
            VariantKey: variant.Key,
            VariantName: variant.Name,
            WeekdayMorningMinResourceCount: variant.WeekdayMorningMinResourceCount,
            ImportedWorkerSubmissionCount: result.ImportedWorkerSubmissions.Count,
            SubmittedShiftSelectionCount: submittedShiftSelections,
            AssignmentCount: geneticResult.Candidate.Assignments.Count,
            TotalAssignedHours: totalAssignedHours,
            IsFeasible: geneticResult.Evaluation.IsFeasible,
            HardViolationCount: geneticResult.Evaluation.Score.HardViolationCount,
            SoftViolationCount: geneticResult.Evaluation.Score.SoftViolationCount,
            TotalPenalty: geneticResult.Evaluation.Score.TotalPenalty,
            IgnoredAvoidPreferenceCount: ignoredAvoidCount,
            SequenceQuotaViolationCount: sequenceQuotaCount,
            MonthlyNightQuotaViolationCount: monthlyNightQuotaCount,
            WeekdayMorningShiftCount: weekdayMorningAssignedCounts.Length,
            MinimumWeekdayMorningAssignedCount: weekdayMorningAssignedCounts.Min(),
            MaximumWeekdayMorningAssignedCount: weekdayMorningAssignedCounts.Max(),
            AverageWeekdayMorningAssignedCount: weekdayMorningAssignedCounts.Average(),
            GenerationDiagnosticCount: geneticResult.GenerationDiagnostics.Count,
            ReportPath: reportPath,
            ScheduleTablePath: tablePath);
    }

    private static int GetViolationCount(
        Aether.Application.Scheduling.Contracts.SchedulingRunOptimizationResult result,
        ConstraintViolationType type)
    {
        return result.ViolationsByType.TryGetValue(type, out var count)
            ? count
            : 0;
    }

    private static bool IsWeekdayMorning(Shift shift)
    {
        var date = DateOnly.FromDateTime(shift.StartUtc);

        return shift.Kind == ShiftKind.Morning &&
               date.DayOfWeek is not DayOfWeek.Friday and not DayOfWeek.Saturday;
    }

    private static void PrintVariantSummary(
        MorningCapacityVariantSummary summary)
    {
        System.Console.WriteLine();
        System.Console.WriteLine(summary.VariantName);
        System.Console.WriteLine($"Key: {summary.VariantKey}");
        System.Console.WriteLine($"WeekdayMorningMinResourceCount: {summary.WeekdayMorningMinResourceCount}");
        System.Console.WriteLine($"Assignments: {summary.AssignmentCount}");
        System.Console.WriteLine($"TotalAssignedHours: {summary.TotalAssignedHours:0.##}");
        System.Console.WriteLine($"IsFeasible: {summary.IsFeasible}");
        System.Console.WriteLine($"HardViolationCount: {summary.HardViolationCount}");
        System.Console.WriteLine($"SoftViolationCount: {summary.SoftViolationCount}");
        System.Console.WriteLine($"TotalPenalty: {summary.TotalPenalty}");
        System.Console.WriteLine($"IgnoredAvoidPreferenceCount: {summary.IgnoredAvoidPreferenceCount}");
        System.Console.WriteLine($"SequenceQuotaViolationCount: {summary.SequenceQuotaViolationCount}");
        System.Console.WriteLine($"MonthlyNightQuotaViolationCount: {summary.MonthlyNightQuotaViolationCount}");
        System.Console.WriteLine($"WeekdayMorningAssignedRange: {summary.MinimumWeekdayMorningAssignedCount}..{summary.MaximumWeekdayMorningAssignedCount}");
        System.Console.WriteLine($"AverageWeekdayMorningAssignedCount: {summary.AverageWeekdayMorningAssignedCount:0.##}");
        System.Console.WriteLine($"Report: {summary.ReportPath}");
        System.Console.WriteLine($"ScheduleTable: {summary.ScheduleTablePath}");
    }

    private static string FormatSummaryCsv(
        IReadOnlyCollection<MorningCapacityVariantSummary> summaries)
    {
        var builder = new StringBuilder();

        builder.AppendLine(
            "VariantKey,VariantName,WeekdayMorningMin,ImportedWorkers,SubmittedShiftSelections,Assignments,TotalAssignedHours,IsFeasible,HardViolationCount,SoftViolationCount,TotalPenalty,IgnoredAvoidPreferenceCount,SequenceQuotaViolationCount,MonthlyNightQuotaViolationCount,WeekdayMorningShiftCount,MinimumWeekdayMorningAssignedCount,MaximumWeekdayMorningAssignedCount,AverageWeekdayMorningAssignedCount,GenerationDiagnosticCount,ReportPath,ScheduleTablePath");

        foreach (var summary in summaries)
        {
            builder.AppendLine(string.Join(
                ",",
                Csv(summary.VariantKey),
                Csv(summary.VariantName),
                summary.WeekdayMorningMinResourceCount,
                summary.ImportedWorkerSubmissionCount,
                summary.SubmittedShiftSelectionCount,
                summary.AssignmentCount,
                summary.TotalAssignedHours.ToString("0.##"),
                summary.IsFeasible,
                summary.HardViolationCount,
                summary.SoftViolationCount,
                summary.TotalPenalty,
                summary.IgnoredAvoidPreferenceCount,
                summary.SequenceQuotaViolationCount,
                summary.MonthlyNightQuotaViolationCount,
                summary.WeekdayMorningShiftCount,
                summary.MinimumWeekdayMorningAssignedCount,
                summary.MaximumWeekdayMorningAssignedCount,
                summary.AverageWeekdayMorningAssignedCount.ToString("0.##"),
                summary.GenerationDiagnosticCount,
                Csv(summary.ReportPath),
                Csv(summary.ScheduleTablePath)));
        }

        return builder.ToString();
    }

    private static string Csv(string value)
    {
        if (!value.Contains(',') &&
            !value.Contains('"') &&
            !value.Contains('\n') &&
            !value.Contains('\r'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static void PrintUsage()
    {
        System.Console.WriteLine("Usage:");
        System.Console.WriteLine("  dotnet run --project src/Aether.Console -- optimize-clean-xlsx-morning-capacity-sensitivity <input.xlsx> <output-directory>");
    }

    private sealed record MorningCapacityVariant(
        string Key,
        string Name,
        int WeekdayMorningMinResourceCount);

    private sealed record MorningCapacityVariantSummary(
        string VariantKey,
        string VariantName,
        int WeekdayMorningMinResourceCount,
        int ImportedWorkerSubmissionCount,
        int SubmittedShiftSelectionCount,
        int AssignmentCount,
        double TotalAssignedHours,
        bool IsFeasible,
        int HardViolationCount,
        int SoftViolationCount,
        int TotalPenalty,
        int IgnoredAvoidPreferenceCount,
        int SequenceQuotaViolationCount,
        int MonthlyNightQuotaViolationCount,
        int WeekdayMorningShiftCount,
        int MinimumWeekdayMorningAssignedCount,
        int MaximumWeekdayMorningAssignedCount,
        double AverageWeekdayMorningAssignedCount,
        int GenerationDiagnosticCount,
        string ReportPath,
        string ScheduleTablePath);
}
