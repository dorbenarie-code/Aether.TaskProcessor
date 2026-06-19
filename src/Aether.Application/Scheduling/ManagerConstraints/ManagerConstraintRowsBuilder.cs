using System.Globalization;
using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.ManagerConstraints;

public sealed class ManagerConstraintRowsBuilder
{
    public IReadOnlyList<IReadOnlyList<string>>? Build(
        IReadOnlyCollection<ManagerConstraintDraftRow> draftRows)
    {
        ArgumentNullException.ThrowIfNull(draftRows);

        var rows = draftRows
            .Where(row => !IsEmpty(row))
            .Select(BuildRow)
            .ToList();

        if (rows.Count == 0)
        {
            return null;
        }

        var result = new List<IReadOnlyList<string>>
        {
            CreateHeaderRow()
        };

        result.AddRange(rows);

        return result;
    }

    private static IReadOnlyList<string> CreateHeaderRow()
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

    private static IReadOnlyList<string> BuildRow(
        ManagerConstraintDraftRow row)
    {
        return
        [
            FormatType(row.Type),
            NormalizeText(row.WorkerName),
            FormatDate(row.Date),
            FormatShiftKind(row.ShiftKind),
            FormatInteger(row.MinResourceCount),
            FormatInteger(row.MaxResourceCount)
        ];
    }

    private static bool IsEmpty(
        ManagerConstraintDraftRow row)
    {
        return row.Type is null &&
               string.IsNullOrWhiteSpace(row.WorkerName) &&
               row.Date is null &&
               row.ShiftKind is null &&
               row.MinResourceCount is null &&
               row.MaxResourceCount is null;
    }

    private static string FormatType(
        ManagerConstraintDraftType? type)
    {
        return type?.ToString() ?? string.Empty;
    }

    private static string NormalizeText(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }

    private static string FormatDate(
        DateTime? date)
    {
        return date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ??
               string.Empty;
    }

    private static string FormatShiftKind(
        ShiftKind? shiftKind)
    {
        return shiftKind?.ToString() ?? string.Empty;
    }

    private static string FormatInteger(
        int? value)
    {
        return value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
