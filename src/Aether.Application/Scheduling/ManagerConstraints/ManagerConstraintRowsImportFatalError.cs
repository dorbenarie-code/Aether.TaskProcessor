using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.ManagerConstraints;

public sealed record ManagerConstraintRowsImportFatalError(
    ManagerConstraintRowsImportFatalErrorType Type,
    int? RowIndex = null,
    int? ColumnIndex = null,
    string? Header = null,
    string? RawValue = null,
    DateOnly? Date = null,
    ShiftKind? ShiftKind = null,
    Guid? ResourceId = null,
    string? ResourceName = null);
