namespace Aether.Application.Scheduling.SubmissionForms;

public sealed record GoogleFormsWorkerSubmissionAssemblyRequest(
    IReadOnlyList<GoogleFormsResolvedWorkerRow> AcceptedRows,
    IReadOnlyList<GoogleFormsShiftCellSelection> Selections);
