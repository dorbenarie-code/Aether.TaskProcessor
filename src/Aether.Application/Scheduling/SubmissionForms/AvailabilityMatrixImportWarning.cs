namespace Aether.Application.Scheduling.SubmissionForms;

public sealed record AvailabilityMatrixImportWarning(
    AvailabilityMatrixImportWarningType Type,
    int? RowIndex = null,
    int? ColumnIndex = null,
    string? Header = null,
    string? RawValue = null,
    DateOnly? Date = null,
    Guid? ResourceId = null,
    string? ResourceName = null);
