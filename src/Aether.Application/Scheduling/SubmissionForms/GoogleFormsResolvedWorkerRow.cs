namespace Aether.Application.Scheduling.SubmissionForms;

public sealed record GoogleFormsResolvedWorkerRow(
    int RowIndex,
    string RawWorkerName,
    Guid ResourceId,
    string ResourceName);
