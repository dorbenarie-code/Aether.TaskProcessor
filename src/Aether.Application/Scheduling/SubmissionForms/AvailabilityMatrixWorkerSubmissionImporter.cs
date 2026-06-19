using System.Text.RegularExpressions;
using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.SubmissionForms;

public sealed class AvailabilityMatrixWorkerSubmissionImporter
{
    private const string WorkerNameHeader = "שם המאבטח";

    private static readonly Regex DateTokenRegex = new(
        @"(?<!\d)(\d{1,2})/(\d{1,2})(?!\d)",
        RegexOptions.Compiled);

    private static readonly Regex WhitespaceRegex = new(
        @"\s+",
        RegexOptions.Compiled);

    public AvailabilityMatrixWorkerSubmissionImportResult Import(
        AvailabilityMatrixWorkerSubmissionImportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var warnings = new List<AvailabilityMatrixImportWarning>();
        var fatalErrors = new List<AvailabilityMatrixImportFatalError>();

        if (request.Rows.Count == 0)
        {
            fatalErrors.Add(new AvailabilityMatrixImportFatalError(
                AvailabilityMatrixImportFatalErrorType.EmptyTable));

            return new AvailabilityMatrixWorkerSubmissionImportResult(
                WorkerSubmissions: [],
                warnings,
                fatalErrors);
        }

        var headers = request.Rows[0];

        var workerNameColumnIndex = FindWorkerNameColumnIndex(headers);

        if (workerNameColumnIndex is null)
        {
            fatalErrors.Add(new AvailabilityMatrixImportFatalError(
                AvailabilityMatrixImportFatalErrorType.MissingWorkerNameColumn));

            return new AvailabilityMatrixWorkerSubmissionImportResult(
                WorkerSubmissions: [],
                warnings,
                fatalErrors);
        }

        var dateColumns = DiscoverScheduleDateColumns(
            request,
            headers,
            warnings,
            fatalErrors);

        if (fatalErrors.Count > 0)
        {
            return new AvailabilityMatrixWorkerSubmissionImportResult(
                WorkerSubmissions: [],
                warnings,
                fatalErrors);
        }

        var resourcesByName = request.Resources.ToDictionary(
            resource => NormalizeWorkerName(resource.Name),
            StringComparer.Ordinal);

        var resourcesById = request.Resources.ToDictionary(
            resource => resource.Id);

        var aliasesByWorkerName = CreateNormalizedAliases(
            request.AliasesByWorkerName);

        var seenResourceIds = new HashSet<Guid>();
        var workerSubmissions = new List<WorkerSubmission>();

        for (var rowIndex = 1; rowIndex < request.Rows.Count; rowIndex++)
        {
            var row = request.Rows[rowIndex];

            var rawWorkerName = GetCellValue(
                row,
                workerNameColumnIndex.Value);

            var normalizedWorkerName = NormalizeWorkerName(rawWorkerName);

            if (string.IsNullOrWhiteSpace(normalizedWorkerName))
            {
                if (HasAnyScheduleCellValue(row, dateColumns))
                {
                    fatalErrors.Add(new AvailabilityMatrixImportFatalError(
                        AvailabilityMatrixImportFatalErrorType.MissingWorkerNameForNonEmptyRow,
                        RowIndex: rowIndex,
                        ColumnIndex: workerNameColumnIndex.Value,
                        Header: WorkerNameHeader,
                        RawValue: rawWorkerName));
                }

                continue;
            }

            if (!TryResolveResource(
                    normalizedWorkerName,
                    resourcesByName,
                    resourcesById,
                    aliasesByWorkerName,
                    out var resource))
            {
                fatalErrors.Add(new AvailabilityMatrixImportFatalError(
                    AvailabilityMatrixImportFatalErrorType.UnresolvedWorkerName,
                    RowIndex: rowIndex,
                    ColumnIndex: workerNameColumnIndex.Value,
                    Header: WorkerNameHeader,
                    RawValue: rawWorkerName));

                continue;
            }

            if (!seenResourceIds.Add(resource.Id))
            {
                fatalErrors.Add(new AvailabilityMatrixImportFatalError(
                    AvailabilityMatrixImportFatalErrorType.DuplicateWorkerRow,
                    RowIndex: rowIndex,
                    ColumnIndex: workerNameColumnIndex.Value,
                    Header: WorkerNameHeader,
                    RawValue: rawWorkerName,
                    ResourceId: resource.Id,
                    ResourceName: resource.Name));

                continue;
            }

            var shiftSubmissions = CreateShiftSubmissions(
                row,
                rowIndex,
                dateColumns,
                warnings);

            workerSubmissions.Add(new WorkerSubmission(
                resource.Id,
                shiftSubmissions));
        }

        if (fatalErrors.Count > 0)
        {
            return new AvailabilityMatrixWorkerSubmissionImportResult(
                WorkerSubmissions: [],
                warnings,
                fatalErrors);
        }

        return new AvailabilityMatrixWorkerSubmissionImportResult(
            workerSubmissions,
            warnings,
            FatalErrors: []);
    }

    private static int? FindWorkerNameColumnIndex(
        IReadOnlyList<string> headers)
    {
        for (var columnIndex = 0; columnIndex < headers.Count; columnIndex++)
        {
            if (NormalizeWorkerName(headers[columnIndex])
                .Contains(WorkerNameHeader, StringComparison.Ordinal))
            {
                return columnIndex;
            }
        }

        return null;
    }

    private static IReadOnlyList<AvailabilityMatrixDateColumn> DiscoverScheduleDateColumns(
        AvailabilityMatrixWorkerSubmissionImportRequest request,
        IReadOnlyList<string> headers,
        List<AvailabilityMatrixImportWarning> warnings,
        List<AvailabilityMatrixImportFatalError> fatalErrors)
    {
        var expectedDates = CreateExpectedDates(request.SchedulePeriod);

        var expectedDatesByMonthAndDay = expectedDates.ToDictionary(
            date => (date.Month, date.Day));

        var dateColumns = new List<AvailabilityMatrixDateColumn>();
        var seenDates = new HashSet<DateOnly>();

        for (var columnIndex = 0; columnIndex < headers.Count; columnIndex++)
        {
            var header = headers[columnIndex];

            if (!TryExtractDateToken(header, out var month, out var day))
            {
                continue;
            }

            if (!expectedDatesByMonthAndDay.TryGetValue(
                    (month, day),
                    out var scheduleDate))
            {
                warnings.Add(new AvailabilityMatrixImportWarning(
                    AvailabilityMatrixImportWarningType.DateColumnOutsideSchedulePeriod,
                    ColumnIndex: columnIndex,
                    Header: header,
                    RawValue: header));

                continue;
            }

            if (!seenDates.Add(scheduleDate))
            {
                fatalErrors.Add(new AvailabilityMatrixImportFatalError(
                    AvailabilityMatrixImportFatalErrorType.DuplicateScheduleDateColumn,
                    Date: scheduleDate,
                    ColumnIndex: columnIndex,
                    Header: header));

                continue;
            }

            dateColumns.Add(new AvailabilityMatrixDateColumn(
                columnIndex,
                header,
                scheduleDate));
        }

        foreach (var expectedDate in expectedDates)
        {
            if (!seenDates.Contains(expectedDate))
            {
                fatalErrors.Add(new AvailabilityMatrixImportFatalError(
                    AvailabilityMatrixImportFatalErrorType.MissingScheduleDateColumn,
                    Date: expectedDate));
            }
        }

        return dateColumns
            .OrderBy(column => column.Date)
            .ToArray();
    }

    private static IReadOnlyList<WorkerShiftSubmission> CreateShiftSubmissions(
        IReadOnlyList<string> row,
        int rowIndex,
        IReadOnlyList<AvailabilityMatrixDateColumn> dateColumns,
        List<AvailabilityMatrixImportWarning> warnings)
    {
        var shiftSubmissions = new List<WorkerShiftSubmission>();

        foreach (var dateColumn in dateColumns)
        {
            var rawCellValue = GetCellValue(
                row,
                dateColumn.ColumnIndex);

            if (string.IsNullOrWhiteSpace(rawCellValue))
            {
                continue;
            }

            foreach (var token in SplitTokens(rawCellValue))
            {
                if (!TryMapShiftKind(token, out var shiftKind))
                {
                    warnings.Add(new AvailabilityMatrixImportWarning(
                        AvailabilityMatrixImportWarningType.InvalidShiftSelectionToken,
                        RowIndex: rowIndex,
                        ColumnIndex: dateColumn.ColumnIndex,
                        Header: dateColumn.Header,
                        RawValue: token,
                        Date: dateColumn.Date));

                    continue;
                }

                shiftSubmissions.Add(new WorkerShiftSubmission(
                    dateColumn.Date,
                    shiftKind,
                    ShiftSubmissionChoice.StrongAvailable));
            }
        }

        return shiftSubmissions;
    }

    private static IReadOnlyList<DateOnly> CreateExpectedDates(
        SchedulePeriod schedulePeriod)
    {
        var startDate = DateOnly.FromDateTime(schedulePeriod.StartUtc);
        var endDate = DateOnly.FromDateTime(schedulePeriod.EndUtc);

        var dates = new List<DateOnly>();

        for (var date = startDate; date < endDate; date = date.AddDays(1))
        {
            dates.Add(date);
        }

        return dates;
    }

    private static bool TryExtractDateToken(
        string header,
        out int month,
        out int day)
    {
        month = 0;
        day = 0;

        var match = DateTokenRegex.Match(header);

        if (!match.Success)
        {
            return false;
        }

        if (!int.TryParse(match.Groups[1].Value, out day) ||
            !int.TryParse(match.Groups[2].Value, out month))
        {
            return false;
        }

        return month is >= 1 and <= 12 &&
               day is >= 1 and <= 31;
    }

    private static IEnumerable<string> SplitTokens(
        string rawCellValue)
    {
        return rawCellValue
            .Split(
                new[] { ',', '\n', '\r' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => !string.IsNullOrWhiteSpace(token));
    }

    private static bool TryMapShiftKind(
        string token,
        out ShiftKind shiftKind)
    {
        shiftKind = default;

        switch (token.Trim())
        {
            case "בוקר":
                shiftKind = ShiftKind.Morning;
                return true;

            case "צהריים":
                shiftKind = ShiftKind.Afternoon;
                return true;

            case "ערב":
                shiftKind = ShiftKind.Night;
                return true;

            default:
                return false;
        }
    }

    private static bool TryResolveResource(
        string workerName,
        IReadOnlyDictionary<string, Resource> resourcesByName,
        IReadOnlyDictionary<Guid, Resource> resourcesById,
        IReadOnlyDictionary<string, Guid> aliasesByWorkerName,
        out Resource resource)
    {
        if (resourcesByName.TryGetValue(workerName, out resource!))
        {
            return true;
        }

        if (aliasesByWorkerName.TryGetValue(workerName, out var resourceId) &&
            resourcesById.TryGetValue(resourceId, out resource!))
        {
            return true;
        }

        resource = null!;
        return false;
    }

    private static IReadOnlyDictionary<string, Guid> CreateNormalizedAliases(
        IReadOnlyDictionary<string, Guid>? aliasesByWorkerName)
    {
        if (aliasesByWorkerName is null)
        {
            return new Dictionary<string, Guid>(StringComparer.Ordinal);
        }

        return aliasesByWorkerName.ToDictionary(
            alias => NormalizeWorkerName(alias.Key),
            alias => alias.Value,
            StringComparer.Ordinal);
    }

    private static bool HasAnyScheduleCellValue(
        IReadOnlyList<string> row,
        IReadOnlyList<AvailabilityMatrixDateColumn> dateColumns)
    {
        return dateColumns.Any(
            dateColumn => !string.IsNullOrWhiteSpace(
                GetCellValue(row, dateColumn.ColumnIndex)));
    }

    private static string GetCellValue(
        IReadOnlyList<string> row,
        int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= row.Count)
        {
            return string.Empty;
        }

        return row[columnIndex] ?? string.Empty;
    }

    private static string NormalizeWorkerName(
        string value)
    {
        return WhitespaceRegex
            .Replace(value.Trim(), " ");
    }

    private sealed record AvailabilityMatrixDateColumn(
        int ColumnIndex,
        string Header,
        DateOnly Date);
}
