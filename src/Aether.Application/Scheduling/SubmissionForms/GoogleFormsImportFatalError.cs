namespace Aether.Application.Scheduling.SubmissionForms;

public sealed record GoogleFormsImportFatalError(
    GoogleFormsImportFatalErrorType Type,
    DateOnly? Date = null,
    int? ColumnIndex = null,
    string? Header = null);
