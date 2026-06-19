namespace Aether.Application.Scheduling.SubmissionForms;

public sealed record AvailabilityMatrixImportFatalError(
    AvailabilityMatrixImportFatalErrorType Type,
    DateOnly? Date = null,
    int? RowIndex = null,
    int? ColumnIndex = null,
    string? Header = null,
    string? RawValue = null,
    Guid? ResourceId = null,
    string? ResourceName = null);
