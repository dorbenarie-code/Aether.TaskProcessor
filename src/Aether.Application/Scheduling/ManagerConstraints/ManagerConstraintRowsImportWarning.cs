namespace Aether.Application.Scheduling.ManagerConstraints;

public sealed record ManagerConstraintRowsImportWarning(
    ManagerConstraintRowsImportWarningType Type,
    int? RowIndex = null,
    int? ColumnIndex = null,
    string? Header = null,
    string? RawValue = null);
