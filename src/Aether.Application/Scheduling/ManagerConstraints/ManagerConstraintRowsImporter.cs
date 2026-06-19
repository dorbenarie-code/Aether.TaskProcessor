using System.Globalization;
using System.Text.RegularExpressions;
using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.ManagerConstraints;

public sealed class ManagerConstraintRowsImporter
{
    private const string TypeHeader = "Type";
    private const string WorkerNameHeader = "WorkerName";
    private const string DateHeader = "Date";
    private const string ShiftKindHeader = "ShiftKind";
    private const string MinResourceCountHeader = "MinResourceCount";
    private const string MaxResourceCountHeader = "MaxResourceCount";

    private const string ForbidAssignmentType = "ForbidAssignment";
    private const string AvoidAssignmentType = "AvoidAssignment";
    private const string ShiftCapacityOverrideType = "ShiftCapacityOverride";

    private static readonly Regex WhitespaceRegex = new(
        @"\s+",
        RegexOptions.Compiled);

    public ManagerConstraintRowsImportResult Import(
        ManagerConstraintRowsImportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var warnings = new List<ManagerConstraintRowsImportWarning>();
        var fatalErrors = new List<ManagerConstraintRowsImportFatalError>();

        if (request.Rows.Count == 0)
        {
            fatalErrors.Add(new ManagerConstraintRowsImportFatalError(
                ManagerConstraintRowsImportFatalErrorType.EmptyTable));

            return new ManagerConstraintRowsImportResult(
                ManagerConstraintSet.Empty,
                warnings,
                fatalErrors);
        }

        var columns = FindRequiredColumns(
            request.Rows[0],
            fatalErrors);

        if (columns is null)
        {
            return new ManagerConstraintRowsImportResult(
                ManagerConstraintSet.Empty,
                warnings,
                fatalErrors);
        }

        var forbiddenAssignments = new List<ManagerForbiddenAssignment>();
        var avoidAssignments = new List<ManagerAvoidAssignment>();
        var shiftCapacityOverrides = new List<ManagerShiftCapacityOverride>();

        var resourcesByName = request.Resources.ToDictionary(
            resource => NormalizeName(resource.Name),
            StringComparer.Ordinal);

        var resourcesById = request.Resources.ToDictionary(
            resource => resource.Id);

        var aliasesByWorkerName = CreateNormalizedAliases(
            request.AliasesByWorkerName);

        for (var rowIndex = 1; rowIndex < request.Rows.Count; rowIndex++)
        {
            var row = request.Rows[rowIndex];

            if (IsEmptyRow(row))
            {
                continue;
            }

            var rawType = GetCellValue(row, columns.TypeColumnIndex);

            if (!TryResolveShift(
                    request,
                    row,
                    rowIndex,
                    columns,
                    fatalErrors,
                    out var shift))
            {
                continue;
            }

            switch (rawType.Trim())
            {
                case ForbidAssignmentType:
                    if (!TryEnsureNoCapacityValues(
                            row,
                            rowIndex,
                            columns,
                            fatalErrors))
                    {
                        continue;
                    }

                    if (!TryResolveResource(
                            request,
                            row,
                            rowIndex,
                            columns,
                            resourcesByName,
                            resourcesById,
                            aliasesByWorkerName,
                            fatalErrors,
                            out var forbiddenResource))
                    {
                        continue;
                    }

                    forbiddenAssignments.Add(new ManagerForbiddenAssignment(
                        forbiddenResource.Id,
                        shift.Id));
                    break;

                case AvoidAssignmentType:
                    if (!TryEnsureNoCapacityValues(
                            row,
                            rowIndex,
                            columns,
                            fatalErrors))
                    {
                        continue;
                    }

                    if (!TryResolveResource(
                            request,
                            row,
                            rowIndex,
                            columns,
                            resourcesByName,
                            resourcesById,
                            aliasesByWorkerName,
                            fatalErrors,
                            out var avoidedResource))
                    {
                        continue;
                    }

                    avoidAssignments.Add(new ManagerAvoidAssignment(
                        avoidedResource.Id,
                        shift.Id));
                    break;

                case ShiftCapacityOverrideType:
                    if (!TryEnsureNoWorkerName(
                            row,
                            rowIndex,
                            columns,
                            fatalErrors))
                    {
                        continue;
                    }

                    if (!TryResolveCapacity(
                            row,
                            rowIndex,
                            columns,
                            fatalErrors,
                            out var minResourceCount,
                            out var maxResourceCount))
                    {
                        continue;
                    }

                    shiftCapacityOverrides.Add(new ManagerShiftCapacityOverride(
                        shift.Id,
                        minResourceCount,
                        maxResourceCount));
                    break;

                default:
                    fatalErrors.Add(new ManagerConstraintRowsImportFatalError(
                        ManagerConstraintRowsImportFatalErrorType.UnknownConstraintType,
                        RowIndex: rowIndex,
                        ColumnIndex: columns.TypeColumnIndex,
                        Header: TypeHeader,
                        RawValue: rawType));
                    break;
            }
        }

        if (fatalErrors.Count > 0)
        {
            return new ManagerConstraintRowsImportResult(
                ManagerConstraintSet.Empty,
                warnings,
                fatalErrors);
        }

        var constraintSet = new ManagerConstraintSet(
            forbiddenAssignments,
            shiftCapacityOverrides,
            avoidAssignments);

        return new ManagerConstraintRowsImportResult(
            constraintSet,
            warnings,
            fatalErrors: [],
            summary: ManagerConstraintImportSummary.FromConstraintSet(constraintSet));
    }

    private static ManagerConstraintRowsColumnIndexes? FindRequiredColumns(
        IReadOnlyList<string> headers,
        List<ManagerConstraintRowsImportFatalError> fatalErrors)
    {
        var typeColumnIndex = FindColumnIndex(headers, TypeHeader);
        var workerNameColumnIndex = FindColumnIndex(headers, WorkerNameHeader);
        var dateColumnIndex = FindColumnIndex(headers, DateHeader);
        var shiftKindColumnIndex = FindColumnIndex(headers, ShiftKindHeader);
        var minResourceCountColumnIndex = FindColumnIndex(headers, MinResourceCountHeader);
        var maxResourceCountColumnIndex = FindColumnIndex(headers, MaxResourceCountHeader);

        AddMissingColumnFatalError(typeColumnIndex, TypeHeader, fatalErrors);
        AddMissingColumnFatalError(workerNameColumnIndex, WorkerNameHeader, fatalErrors);
        AddMissingColumnFatalError(dateColumnIndex, DateHeader, fatalErrors);
        AddMissingColumnFatalError(shiftKindColumnIndex, ShiftKindHeader, fatalErrors);
        AddMissingColumnFatalError(minResourceCountColumnIndex, MinResourceCountHeader, fatalErrors);
        AddMissingColumnFatalError(maxResourceCountColumnIndex, MaxResourceCountHeader, fatalErrors);

        if (fatalErrors.Count > 0)
        {
            return null;
        }

        return new ManagerConstraintRowsColumnIndexes(
            typeColumnIndex!.Value,
            workerNameColumnIndex!.Value,
            dateColumnIndex!.Value,
            shiftKindColumnIndex!.Value,
            minResourceCountColumnIndex!.Value,
            maxResourceCountColumnIndex!.Value);
    }

    private static int? FindColumnIndex(
        IReadOnlyList<string> headers,
        string expectedHeader)
    {
        for (var columnIndex = 0; columnIndex < headers.Count; columnIndex++)
        {
            if (NormalizeHeader(headers[columnIndex])
                .Equals(expectedHeader, StringComparison.OrdinalIgnoreCase))
            {
                return columnIndex;
            }
        }

        return null;
    }

    private static void AddMissingColumnFatalError(
        int? columnIndex,
        string header,
        List<ManagerConstraintRowsImportFatalError> fatalErrors)
    {
        if (columnIndex is not null)
        {
            return;
        }

        fatalErrors.Add(new ManagerConstraintRowsImportFatalError(
            ManagerConstraintRowsImportFatalErrorType.MissingRequiredColumn,
            Header: header));
    }

    private static bool TryEnsureNoWorkerName(
        IReadOnlyList<string> row,
        int rowIndex,
        ManagerConstraintRowsColumnIndexes columns,
        List<ManagerConstraintRowsImportFatalError> fatalErrors)
    {
        var rawWorkerName = GetCellValue(
            row,
            columns.WorkerNameColumnIndex);

        if (string.IsNullOrWhiteSpace(rawWorkerName))
        {
            return true;
        }

        fatalErrors.Add(new ManagerConstraintRowsImportFatalError(
            ManagerConstraintRowsImportFatalErrorType.UnexpectedWorkerName,
            RowIndex: rowIndex,
            ColumnIndex: columns.WorkerNameColumnIndex,
            Header: WorkerNameHeader,
            RawValue: rawWorkerName));

        return false;
    }

    private static bool TryEnsureNoCapacityValues(
        IReadOnlyList<string> row,
        int rowIndex,
        ManagerConstraintRowsColumnIndexes columns,
        List<ManagerConstraintRowsImportFatalError> fatalErrors)
    {
        var rawMinResourceCount = GetCellValue(
            row,
            columns.MinResourceCountColumnIndex);

        if (!string.IsNullOrWhiteSpace(rawMinResourceCount))
        {
            fatalErrors.Add(new ManagerConstraintRowsImportFatalError(
                ManagerConstraintRowsImportFatalErrorType.UnexpectedCapacityValue,
                RowIndex: rowIndex,
                ColumnIndex: columns.MinResourceCountColumnIndex,
                Header: MinResourceCountHeader,
                RawValue: rawMinResourceCount));

            return false;
        }

        var rawMaxResourceCount = GetCellValue(
            row,
            columns.MaxResourceCountColumnIndex);

        if (!string.IsNullOrWhiteSpace(rawMaxResourceCount))
        {
            fatalErrors.Add(new ManagerConstraintRowsImportFatalError(
                ManagerConstraintRowsImportFatalErrorType.UnexpectedCapacityValue,
                RowIndex: rowIndex,
                ColumnIndex: columns.MaxResourceCountColumnIndex,
                Header: MaxResourceCountHeader,
                RawValue: rawMaxResourceCount));

            return false;
        }

        return true;
    }

    private static bool TryResolveResource(
        ManagerConstraintRowsImportRequest request,
        IReadOnlyList<string> row,
        int rowIndex,
        ManagerConstraintRowsColumnIndexes columns,
        IReadOnlyDictionary<string, Resource> resourcesByName,
        IReadOnlyDictionary<Guid, Resource> resourcesById,
        IReadOnlyDictionary<string, Guid> aliasesByWorkerName,
        List<ManagerConstraintRowsImportFatalError> fatalErrors,
        out Resource resource)
    {
        var rawWorkerName = GetCellValue(
            row,
            columns.WorkerNameColumnIndex);

        var normalizedWorkerName = NormalizeName(rawWorkerName);

        if (string.IsNullOrWhiteSpace(normalizedWorkerName))
        {
            fatalErrors.Add(new ManagerConstraintRowsImportFatalError(
                ManagerConstraintRowsImportFatalErrorType.MissingWorkerName,
                RowIndex: rowIndex,
                ColumnIndex: columns.WorkerNameColumnIndex,
                Header: WorkerNameHeader,
                RawValue: rawWorkerName));

            resource = null!;
            return false;
        }

        if (resourcesByName.TryGetValue(normalizedWorkerName, out resource!))
        {
            return true;
        }

        if (aliasesByWorkerName.TryGetValue(normalizedWorkerName, out var resourceId) &&
            resourcesById.TryGetValue(resourceId, out resource!))
        {
            return true;
        }

        fatalErrors.Add(new ManagerConstraintRowsImportFatalError(
            ManagerConstraintRowsImportFatalErrorType.UnresolvedWorkerName,
            RowIndex: rowIndex,
            ColumnIndex: columns.WorkerNameColumnIndex,
            Header: WorkerNameHeader,
            RawValue: rawWorkerName));

        resource = null!;
        return false;
    }

    private static bool TryResolveShift(
        ManagerConstraintRowsImportRequest request,
        IReadOnlyList<string> row,
        int rowIndex,
        ManagerConstraintRowsColumnIndexes columns,
        List<ManagerConstraintRowsImportFatalError> fatalErrors,
        out Shift shift)
    {
        shift = null!;

        var rawDate = GetCellValue(
            row,
            columns.DateColumnIndex);

        if (!DateOnly.TryParseExact(
                rawDate.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            fatalErrors.Add(new ManagerConstraintRowsImportFatalError(
                ManagerConstraintRowsImportFatalErrorType.InvalidDate,
                RowIndex: rowIndex,
                ColumnIndex: columns.DateColumnIndex,
                Header: DateHeader,
                RawValue: rawDate));

            return false;
        }

        var scheduleStartDate = DateOnly.FromDateTime(
            request.SchedulePeriod.StartUtc);

        var scheduleEndDate = DateOnly.FromDateTime(
            request.SchedulePeriod.EndUtc);

        if (date < scheduleStartDate || date >= scheduleEndDate)
        {
            fatalErrors.Add(new ManagerConstraintRowsImportFatalError(
                ManagerConstraintRowsImportFatalErrorType.DateOutsideSchedulePeriod,
                RowIndex: rowIndex,
                ColumnIndex: columns.DateColumnIndex,
                Header: DateHeader,
                RawValue: rawDate,
                Date: date));

            return false;
        }

        var rawShiftKind = GetCellValue(
            row,
            columns.ShiftKindColumnIndex);

        if (!Enum.TryParse<ShiftKind>(
                rawShiftKind.Trim(),
                ignoreCase: true,
                out var shiftKind) ||
            !Enum.IsDefined(typeof(ShiftKind), shiftKind))
        {
            fatalErrors.Add(new ManagerConstraintRowsImportFatalError(
                ManagerConstraintRowsImportFatalErrorType.InvalidShiftKind,
                RowIndex: rowIndex,
                ColumnIndex: columns.ShiftKindColumnIndex,
                Header: ShiftKindHeader,
                RawValue: rawShiftKind,
                Date: date));

            return false;
        }

        var matchingShifts = request.Shifts
            .Where(candidate =>
                DateOnly.FromDateTime(candidate.StartUtc) == date &&
                candidate.Kind == shiftKind)
            .ToArray();

        if (matchingShifts.Length == 0)
        {
            fatalErrors.Add(new ManagerConstraintRowsImportFatalError(
                ManagerConstraintRowsImportFatalErrorType.ShiftNotFound,
                RowIndex: rowIndex,
                ColumnIndex: columns.ShiftKindColumnIndex,
                Header: ShiftKindHeader,
                RawValue: rawShiftKind,
                Date: date,
                ShiftKind: shiftKind));

            return false;
        }

        if (matchingShifts.Length > 1)
        {
            fatalErrors.Add(new ManagerConstraintRowsImportFatalError(
                ManagerConstraintRowsImportFatalErrorType.AmbiguousShift,
                RowIndex: rowIndex,
                ColumnIndex: columns.ShiftKindColumnIndex,
                Header: ShiftKindHeader,
                RawValue: rawShiftKind,
                Date: date,
                ShiftKind: shiftKind));

            return false;
        }

        shift = matchingShifts[0];
        return true;
    }

    private static bool TryResolveCapacity(
        IReadOnlyList<string> row,
        int rowIndex,
        ManagerConstraintRowsColumnIndexes columns,
        List<ManagerConstraintRowsImportFatalError> fatalErrors,
        out int minResourceCount,
        out int maxResourceCount)
    {
        minResourceCount = 0;
        maxResourceCount = 0;

        var rawMinResourceCount = GetCellValue(
            row,
            columns.MinResourceCountColumnIndex);

        var rawMaxResourceCount = GetCellValue(
            row,
            columns.MaxResourceCountColumnIndex);

        if (string.IsNullOrWhiteSpace(rawMinResourceCount))
        {
            fatalErrors.Add(new ManagerConstraintRowsImportFatalError(
                ManagerConstraintRowsImportFatalErrorType.MissingCapacityValue,
                RowIndex: rowIndex,
                ColumnIndex: columns.MinResourceCountColumnIndex,
                Header: MinResourceCountHeader,
                RawValue: rawMinResourceCount));

            return false;
        }

        if (string.IsNullOrWhiteSpace(rawMaxResourceCount))
        {
            fatalErrors.Add(new ManagerConstraintRowsImportFatalError(
                ManagerConstraintRowsImportFatalErrorType.MissingCapacityValue,
                RowIndex: rowIndex,
                ColumnIndex: columns.MaxResourceCountColumnIndex,
                Header: MaxResourceCountHeader,
                RawValue: rawMaxResourceCount));

            return false;
        }

        if (!int.TryParse(rawMinResourceCount, NumberStyles.Integer, CultureInfo.InvariantCulture, out minResourceCount))
        {
            fatalErrors.Add(new ManagerConstraintRowsImportFatalError(
                ManagerConstraintRowsImportFatalErrorType.InvalidCapacityValue,
                RowIndex: rowIndex,
                ColumnIndex: columns.MinResourceCountColumnIndex,
                Header: MinResourceCountHeader,
                RawValue: rawMinResourceCount));

            return false;
        }

        if (!int.TryParse(rawMaxResourceCount, NumberStyles.Integer, CultureInfo.InvariantCulture, out maxResourceCount))
        {
            fatalErrors.Add(new ManagerConstraintRowsImportFatalError(
                ManagerConstraintRowsImportFatalErrorType.InvalidCapacityValue,
                RowIndex: rowIndex,
                ColumnIndex: columns.MaxResourceCountColumnIndex,
                Header: MaxResourceCountHeader,
                RawValue: rawMaxResourceCount));

            return false;
        }

        if (minResourceCount < 0)
        {
            fatalErrors.Add(new ManagerConstraintRowsImportFatalError(
                ManagerConstraintRowsImportFatalErrorType.InvalidCapacityValue,
                RowIndex: rowIndex,
                ColumnIndex: columns.MinResourceCountColumnIndex,
                Header: MinResourceCountHeader,
                RawValue: rawMinResourceCount));

            return false;
        }

        if (maxResourceCount <= 0)
        {
            fatalErrors.Add(new ManagerConstraintRowsImportFatalError(
                ManagerConstraintRowsImportFatalErrorType.InvalidCapacityValue,
                RowIndex: rowIndex,
                ColumnIndex: columns.MaxResourceCountColumnIndex,
                Header: MaxResourceCountHeader,
                RawValue: rawMaxResourceCount));

            return false;
        }

        if (maxResourceCount < minResourceCount)
        {
            fatalErrors.Add(new ManagerConstraintRowsImportFatalError(
                ManagerConstraintRowsImportFatalErrorType.InvalidCapacityValue,
                RowIndex: rowIndex,
                ColumnIndex: columns.MaxResourceCountColumnIndex,
                Header: MaxResourceCountHeader,
                RawValue: rawMaxResourceCount));

            return false;
        }

        return true;
    }

    private static IReadOnlyDictionary<string, Guid> CreateNormalizedAliases(
        IReadOnlyDictionary<string, Guid>? aliasesByWorkerName)
    {
        if (aliasesByWorkerName is null)
        {
            return new Dictionary<string, Guid>(StringComparer.Ordinal);
        }

        return aliasesByWorkerName.ToDictionary(
            alias => NormalizeName(alias.Key),
            alias => alias.Value,
            StringComparer.Ordinal);
    }

    private static bool IsEmptyRow(
        IReadOnlyList<string> row)
    {
        return row.All(string.IsNullOrWhiteSpace);
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

    private static string NormalizeHeader(
        string value)
    {
        return WhitespaceRegex
            .Replace(value.Trim(), string.Empty);
    }

    private static string NormalizeName(
        string value)
    {
        return WhitespaceRegex
            .Replace(value.Trim(), " ");
    }

    private sealed record ManagerConstraintRowsColumnIndexes(
        int TypeColumnIndex,
        int WorkerNameColumnIndex,
        int DateColumnIndex,
        int ShiftKindColumnIndex,
        int MinResourceCountColumnIndex,
        int MaxResourceCountColumnIndex);
}
