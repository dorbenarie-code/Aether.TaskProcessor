using System.Text.RegularExpressions;
using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.SubmissionForms;

public sealed class GoogleFormsWorkerRowResolver
{
    private static readonly Regex WhitespaceRegex = new(
        @"\s+",
        RegexOptions.Compiled);

    public GoogleFormsWorkerRowResolutionResult Resolve(
        GoogleFormsWorkerRowResolutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var resolvedRows = new List<GoogleFormsResolvedWorkerRow>();
        var warnings = new List<GoogleFormsImportWarning>();

        var resourcesByName = request.Resources.ToDictionary(
            resource => NormalizeWorkerName(resource.Name),
            StringComparer.Ordinal);

        var resourcesById = request.Resources.ToDictionary(
            resource => resource.Id);

        var aliasesByWorkerName = CreateNormalizedAliases(
            request.AliasesByWorkerName);

        foreach (var rowIndex in request.Scope.SelectedRowIndexes)
        {
            if (rowIndex < 0 || rowIndex >= request.Rows.Count)
            {
                continue;
            }

            var row = request.Rows[rowIndex];

            var rawWorkerName = GetWorkerNameCellValue(
                row,
                request.Scope.WorkerNameColumnIndex);

            var normalizedWorkerName = NormalizeWorkerName(rawWorkerName);

            if (TryResolveResource(
                    normalizedWorkerName,
                    resourcesByName,
                    resourcesById,
                    aliasesByWorkerName,
                    out var resource))
            {
                resolvedRows.Add(new GoogleFormsResolvedWorkerRow(
                    rowIndex,
                    normalizedWorkerName,
                    resource.Id,
                    resource.Name));

                continue;
            }

            warnings.Add(new GoogleFormsImportWarning(
                GoogleFormsImportWarningType.UnresolvedWorkerName,
                RowIndex: rowIndex,
                ColumnIndex: request.Scope.WorkerNameColumnIndex,
                Header: "שם המאבטח",
                RawValue: rawWorkerName));
        }

        return new GoogleFormsWorkerRowResolutionResult(
            resolvedRows,
            warnings);
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

    private static string GetWorkerNameCellValue(
        IReadOnlyList<string> row,
        int workerNameColumnIndex)
    {
        if (workerNameColumnIndex < 0 ||
            workerNameColumnIndex >= row.Count)
        {
            return string.Empty;
        }

        return row[workerNameColumnIndex] ?? string.Empty;
    }

    private static string NormalizeWorkerName(string value)
    {
        return WhitespaceRegex
            .Replace(value.Trim(), " ");
    }
}
