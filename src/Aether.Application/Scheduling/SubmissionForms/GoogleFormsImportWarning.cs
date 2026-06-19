namespace Aether.Application.Scheduling.SubmissionForms;

public sealed record GoogleFormsImportWarning(
    GoogleFormsImportWarningType Type,
    int? RowIndex = null,
    int? ColumnIndex = null,
    string? Header = null,
    string? RawValue = null,
    Guid? ResourceId = null,
    string? ResourceName = null);
