using System.Globalization;
using System.Text.RegularExpressions;

namespace Aether.Application.Scheduling.SubmissionForms;

public sealed class GoogleFormsImportScopeDiscoverer
{
    private static readonly Regex DateTokenRegex = new(
        @"(?<!\d)(\d{1,2})/(\d{1,2})(?!\d)",
        RegexOptions.Compiled);

    public GoogleFormsImportScopeDiscoveryResult Discover(
        GoogleFormsImportScopeDiscoveryRequest request)
    {
        var warnings = new List<GoogleFormsImportWarning>();
        var fatalErrors = new List<GoogleFormsImportFatalError>();

        if (request.Rows.Count == 0)
        {
            fatalErrors.Add(new GoogleFormsImportFatalError(
                GoogleFormsImportFatalErrorType.EmptyTable));

            return CreateResult(
                timestampColumnIndex: -1,
                workerNameColumnIndex: -1,
                scheduleDateColumns: [],
                selectedRowIndexes: [],
                warnings,
                fatalErrors);
        }

        var headers = request.Rows[0];

        var timestampColumnIndex = FindHeaderIndex(
            headers,
            "חותמת זמן");

        var workerNameColumnIndex = FindHeaderIndex(
            headers,
            "שם המאבטח");

        if (timestampColumnIndex < 0)
        {
            fatalErrors.Add(new GoogleFormsImportFatalError(
                GoogleFormsImportFatalErrorType.MissingTimestampColumn));
        }

        if (workerNameColumnIndex < 0)
        {
            fatalErrors.Add(new GoogleFormsImportFatalError(
                GoogleFormsImportFatalErrorType.MissingWorkerNameColumn));
        }

        var scheduleDates = CreateScheduleDates(request);
        var scheduleDatesByDayMonth = scheduleDates.ToDictionary(
            date => (date.Day, date.Month));

        var scheduleDateColumnsByDate = new Dictionary<DateOnly, ScheduleDateColumnMapping>();

        for (var columnIndex = 0; columnIndex < headers.Count; columnIndex++)
        {
            var header = headers[columnIndex] ?? string.Empty;
            var matches = DateTokenRegex.Matches(header);

            foreach (Match match in matches)
            {
                var day = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                var month = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);

                if (!scheduleDatesByDayMonth.TryGetValue((day, month), out var date))
                {
                    warnings.Add(new GoogleFormsImportWarning(
                        GoogleFormsImportWarningType.DateColumnOutsideSchedulePeriod,
                        ColumnIndex: columnIndex,
                        Header: header));

                    continue;
                }

                if (scheduleDateColumnsByDate.ContainsKey(date))
                {
                    fatalErrors.Add(new GoogleFormsImportFatalError(
                        GoogleFormsImportFatalErrorType.DuplicateScheduleDateColumn,
                        Date: date,
                        ColumnIndex: columnIndex,
                        Header: header));

                    continue;
                }

                scheduleDateColumnsByDate[date] = new ScheduleDateColumnMapping(
                    date,
                    columnIndex,
                    header);
            }
        }

        foreach (var date in scheduleDates)
        {
            if (!scheduleDateColumnsByDate.ContainsKey(date))
            {
                fatalErrors.Add(new GoogleFormsImportFatalError(
                    GoogleFormsImportFatalErrorType.MissingScheduleDateColumn,
                    Date: date));
            }
        }

        var selectedRowIndexes = new List<int>();

        if (timestampColumnIndex >= 0)
        {
            for (var rowIndex = 1; rowIndex < request.Rows.Count; rowIndex++)
            {
                var row = request.Rows[rowIndex];

                if (timestampColumnIndex >= row.Count)
                {
                    warnings.Add(new GoogleFormsImportWarning(
                        GoogleFormsImportWarningType.InvalidTimestamp,
                        RowIndex: rowIndex,
                        ColumnIndex: timestampColumnIndex));

                    continue;
                }

                var rawTimestamp = row[timestampColumnIndex];

                if (!TryParseTimestampDate(rawTimestamp, out var submittedAt))
                {
                    warnings.Add(new GoogleFormsImportWarning(
                        GoogleFormsImportWarningType.InvalidTimestamp,
                        RowIndex: rowIndex,
                        ColumnIndex: timestampColumnIndex,
                        RawValue: rawTimestamp));

                    continue;
                }

                if (submittedAt >= request.SubmittedAtFrom &&
                    submittedAt <= request.SubmittedAtTo)
                {
                    selectedRowIndexes.Add(rowIndex);
                }
            }

            if (selectedRowIndexes.Count == 0)
            {
                fatalErrors.Add(new GoogleFormsImportFatalError(
                    GoogleFormsImportFatalErrorType.NoRowsInsideSubmittedAtWindow));
            }
        }

        return CreateResult(
            timestampColumnIndex,
            workerNameColumnIndex,
            scheduleDateColumnsByDate
                .Values
                .OrderBy(column => column.Date)
                .ToArray(),
            selectedRowIndexes,
            warnings,
            fatalErrors);
    }

    private static int FindHeaderIndex(
        IReadOnlyList<string> headers,
        string expectedText)
    {
        for (var index = 0; index < headers.Count; index++)
        {
            if ((headers[index] ?? string.Empty).Contains(
                    expectedText,
                    StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static IReadOnlyList<DateOnly> CreateScheduleDates(
        GoogleFormsImportScopeDiscoveryRequest request)
    {
        var startDate = DateOnly.FromDateTime(request.SchedulePeriod.StartUtc);
        var endDateExclusive = DateOnly.FromDateTime(request.SchedulePeriod.EndUtc);

        var dates = new List<DateOnly>();

        for (var date = startDate; date < endDateExclusive; date = date.AddDays(1))
        {
            dates.Add(date);
        }

        return dates;
    }

    private static bool TryParseTimestampDate(
        string? value,
        out DateOnly date)
    {
        date = default;

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
            date = DateOnly.FromDateTime(exactDateTime);
            return true;
        }

        if (DateTime.TryParse(
                value.Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsedDateTime))
        {
            date = DateOnly.FromDateTime(parsedDateTime);
            return true;
        }

        return false;
    }

    private static GoogleFormsImportScopeDiscoveryResult CreateResult(
        int timestampColumnIndex,
        int workerNameColumnIndex,
        IReadOnlyList<ScheduleDateColumnMapping> scheduleDateColumns,
        IReadOnlyList<int> selectedRowIndexes,
        IReadOnlyList<GoogleFormsImportWarning> warnings,
        IReadOnlyList<GoogleFormsImportFatalError> fatalErrors)
    {
        return new GoogleFormsImportScopeDiscoveryResult(
            timestampColumnIndex,
            workerNameColumnIndex,
            scheduleDateColumns,
            selectedRowIndexes,
            warnings,
            fatalErrors);
    }
}
