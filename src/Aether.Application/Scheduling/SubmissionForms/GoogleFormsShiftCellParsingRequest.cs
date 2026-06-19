namespace Aether.Application.Scheduling.SubmissionForms;

public sealed record GoogleFormsShiftCellParsingRequest(
    IReadOnlyList<IReadOnlyList<string>> Rows,
    GoogleFormsImportScopeDiscoveryResult Scope);
