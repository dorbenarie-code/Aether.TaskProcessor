namespace Aether.Application.Scheduling.SubmissionForms;

public sealed record GoogleFormsShiftCellParsingResult(
    IReadOnlyList<GoogleFormsShiftCellSelection> Selections,
    IReadOnlyList<GoogleFormsImportWarning> Warnings);
