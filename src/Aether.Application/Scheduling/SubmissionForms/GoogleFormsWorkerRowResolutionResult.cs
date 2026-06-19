namespace Aether.Application.Scheduling.SubmissionForms;

public sealed record GoogleFormsWorkerRowResolutionResult(
    IReadOnlyList<GoogleFormsResolvedWorkerRow> ResolvedRows,
    IReadOnlyList<GoogleFormsImportWarning> Warnings);
