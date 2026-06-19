using System.Globalization;

namespace Aether.Application.Scheduling.SubmissionForms;

public sealed class GoogleFormsLatestWorkerSubmissionRowSelector
{
    public GoogleFormsLatestWorkerSubmissionRowSelectionResult Select(
        GoogleFormsLatestWorkerSubmissionRowSelectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var warnings = new List<GoogleFormsImportWarning>();
        var candidates = new List<ResolvedWorkerSubmissionRowCandidate>();

        var selectedRowIndexes = request.Scope
            .SelectedRowIndexes
            .ToHashSet();

        foreach (var resolvedRow in request.ResolvedRows)
        {
            if (!selectedRowIndexes.Contains(resolvedRow.RowIndex))
            {
                continue;
            }

            if (!TryGetTimestamp(
                    request.Rows,
                    request.Scope.TimestampColumnIndex,
                    resolvedRow.RowIndex,
                    out var rawTimestamp,
                    out var submittedAt))
            {
                warnings.Add(CreateInvalidTimestampWarning(
                    request,
                    resolvedRow,
                    rawTimestamp));

                continue;
            }

            candidates.Add(new ResolvedWorkerSubmissionRowCandidate(
                resolvedRow,
                submittedAt));
        }

        var acceptedRows = new List<GoogleFormsResolvedWorkerRow>();

        foreach (var group in candidates.GroupBy(candidate => candidate.Row.ResourceId))
        {
            var orderedRows = group
                .OrderByDescending(candidate => candidate.SubmittedAt)
                .ThenByDescending(candidate => candidate.Row.RowIndex)
                .ToArray();

            acceptedRows.Add(orderedRows[0].Row);

            foreach (var ignoredRow in orderedRows.Skip(1))
            {
                warnings.Add(CreateDuplicateWarning(
                    request,
                    ignoredRow.Row));
            }
        }

        return new GoogleFormsLatestWorkerSubmissionRowSelectionResult(
            acceptedRows
                .OrderBy(row => row.RowIndex)
                .ToArray(),
            warnings);
    }

    private static GoogleFormsImportWarning CreateDuplicateWarning(
        GoogleFormsLatestWorkerSubmissionRowSelectionRequest request,
        GoogleFormsResolvedWorkerRow row)
    {
        return new GoogleFormsImportWarning(
            GoogleFormsImportWarningType.DuplicateWorkerSubmissionIgnored,
            RowIndex: row.RowIndex,
            ColumnIndex: request.Scope.WorkerNameColumnIndex,
            Header: GetHeader(request.Rows, request.Scope.WorkerNameColumnIndex, "שם המאבטח"),
            RawValue: row.RawWorkerName,
            ResourceId: row.ResourceId,
            ResourceName: row.ResourceName);
    }

    private static GoogleFormsImportWarning CreateInvalidTimestampWarning(
        GoogleFormsLatestWorkerSubmissionRowSelectionRequest request,
        GoogleFormsResolvedWorkerRow row,
        string rawTimestamp)
    {
        return new GoogleFormsImportWarning(
            GoogleFormsImportWarningType.InvalidResolvedWorkerSubmissionTimestamp,
            RowIndex: row.RowIndex,
            ColumnIndex: request.Scope.TimestampColumnIndex,
            Header: GetHeader(request.Rows, request.Scope.TimestampColumnIndex, "חותמת זמן"),
            RawValue: rawTimestamp,
            ResourceId: row.ResourceId,
            ResourceName: row.ResourceName);
    }

    private static bool TryGetTimestamp(
        IReadOnlyList<IReadOnlyList<string>> rows,
        int timestampColumnIndex,
        int rowIndex,
        out string rawTimestamp,
        out DateTime submittedAt)
    {
        rawTimestamp = string.Empty;
        submittedAt = default;

        if (rowIndex < 0 ||
            rowIndex >= rows.Count ||
            timestampColumnIndex < 0 ||
            timestampColumnIndex >= rows[rowIndex].Count)
        {
            return false;
        }

        rawTimestamp = rows[rowIndex][timestampColumnIndex] ?? string.Empty;

        return TryParseTimestamp(rawTimestamp, out submittedAt);
    }

    private static bool TryParseTimestamp(
        string? value,
        out DateTime submittedAt)
    {
        submittedAt = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var formats = new[]
        {
            "M/d/yyyy H:mm:ss",
            "M/d/yyyy HH:mm:ss",
            "MM/dd/yyyy H:mm:ss",
            "MM/dd/yyyy HH:mm:ss",
            "d/M/yyyy H:mm:ss",
            "d/M/yyyy HH:mm:ss",
            "dd/MM/yyyy H:mm:ss",
            "dd/MM/yyyy HH:mm:ss"
        };

        if (DateTime.TryParseExact(
                value.Trim(),
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var exactDateTime))
        {
            submittedAt = exactDateTime;
            return true;
        }

        return DateTime.TryParse(
            value.Trim(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out submittedAt);
    }

    private static string GetHeader(
        IReadOnlyList<IReadOnlyList<string>> rows,
        int columnIndex,
        string fallback)
    {
        if (rows.Count == 0 ||
            columnIndex < 0 ||
            columnIndex >= rows[0].Count)
        {
            return fallback;
        }

        return string.IsNullOrWhiteSpace(rows[0][columnIndex])
            ? fallback
            : rows[0][columnIndex];
    }

    private sealed record ResolvedWorkerSubmissionRowCandidate(
        GoogleFormsResolvedWorkerRow Row,
        DateTime SubmittedAt);
}
