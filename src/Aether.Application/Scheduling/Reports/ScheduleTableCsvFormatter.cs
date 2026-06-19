using System.Text;
using Aether.Application.Scheduling.Contracts;
using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.Reports;

public sealed class ScheduleTableCsvFormatter
{
    public string Format(
        SchedulingProblem problem,
        SchedulingRunOptimizationResult result)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(result);

        var projection = new ScheduleTableProjectionBuilder()
            .Build(problem, result);

        var builder = new StringBuilder();

        builder.AppendLine("Date,DayOfWeek,Morning,Afternoon,Night");

        foreach (var day in projection.Days)
        {
            builder.AppendLine(string.Join(
                ",",
                FormatCsvValue(day.Date.ToString("yyyy-MM-dd")),
                FormatCsvValue(day.DayOfWeek.ToString()),
                FormatCsvValue(GetCellValue(day.MorningWorkerNames)),
                FormatCsvValue(GetCellValue(day.AfternoonWorkerNames)),
                FormatCsvValue(GetCellValue(day.NightWorkerNames))));
        }

        return builder.ToString();
    }

    private static string GetCellValue(
        IReadOnlyList<string> assignedNames)
    {
        if (assignedNames.Count == 0)
        {
            return "-";
        }

        return string.Join("; ", assignedNames);
    }

    private static string FormatCsvValue(string value)
    {
        if (!RequiresQuoting(value))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static bool RequiresQuoting(string value)
    {
        return value.Contains(',') ||
               value.Contains(';') ||
               value.Contains('"') ||
               value.Contains('\n') ||
               value.Contains('\r');
    }
}
