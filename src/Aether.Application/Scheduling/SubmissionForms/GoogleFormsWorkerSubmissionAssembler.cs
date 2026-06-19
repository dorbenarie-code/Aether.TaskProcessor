namespace Aether.Application.Scheduling.SubmissionForms;

public sealed class GoogleFormsWorkerSubmissionAssembler
{
    public GoogleFormsWorkerSubmissionAssemblyResult Assemble(
        GoogleFormsWorkerSubmissionAssemblyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var selectionsByRowIndex = request.Selections
            .ToLookup(selection => selection.RowIndex);

        var workerSubmissions = request.AcceptedRows
            .Select(row => CreateWorkerSubmission(
                row,
                selectionsByRowIndex[row.RowIndex]))
            .ToArray();

        return new GoogleFormsWorkerSubmissionAssemblyResult(
            workerSubmissions,
            Warnings: []);
    }

    private static WorkerSubmission CreateWorkerSubmission(
        GoogleFormsResolvedWorkerRow row,
        IEnumerable<GoogleFormsShiftCellSelection> selections)
    {
        var shiftSubmissions = selections
            .Select(selection => new WorkerShiftSubmission(
                selection.Date,
                selection.ShiftKind,
                selection.Choice))
            .ToArray();

        return new WorkerSubmission(
            row.ResourceId,
            shiftSubmissions);
    }
}
