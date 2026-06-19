namespace Aether.Application.Scheduling.SubmissionForms;

public sealed class GoogleFormsWorkerSubmissionImporter
{
    public GoogleFormsWorkerSubmissionImportResult Import(
        GoogleFormsWorkerSubmissionImportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var warnings = new List<GoogleFormsImportWarning>();

        var scopeResult = new GoogleFormsImportScopeDiscoverer()
            .Discover(new GoogleFormsImportScopeDiscoveryRequest(
                request.Rows,
                request.SchedulePeriod,
                request.SubmittedAtFrom,
                request.SubmittedAtTo));

        warnings.AddRange(scopeResult.Warnings);

        if (scopeResult.FatalErrors.Count > 0)
        {
            return new GoogleFormsWorkerSubmissionImportResult(
                WorkerSubmissions: [],
                Warnings: warnings,
                FatalErrors: scopeResult.FatalErrors);
        }

        var shiftCellParsingResult = new GoogleFormsShiftCellParser()
            .Parse(new GoogleFormsShiftCellParsingRequest(
                request.Rows,
                scopeResult));

        warnings.AddRange(shiftCellParsingResult.Warnings);

        var workerRowResolutionResult = new GoogleFormsWorkerRowResolver()
            .Resolve(new GoogleFormsWorkerRowResolutionRequest(
                request.Rows,
                scopeResult,
                request.Resources,
                request.AliasesByWorkerName));

        warnings.AddRange(workerRowResolutionResult.Warnings);

        var latestRowSelectionResult = new GoogleFormsLatestWorkerSubmissionRowSelector()
            .Select(new GoogleFormsLatestWorkerSubmissionRowSelectionRequest(
                request.Rows,
                scopeResult,
                workerRowResolutionResult.ResolvedRows));

        warnings.AddRange(latestRowSelectionResult.Warnings);

        var assemblyResult = new GoogleFormsWorkerSubmissionAssembler()
            .Assemble(new GoogleFormsWorkerSubmissionAssemblyRequest(
                latestRowSelectionResult.AcceptedRows,
                shiftCellParsingResult.Selections));

        warnings.AddRange(assemblyResult.Warnings);

        return new GoogleFormsWorkerSubmissionImportResult(
            assemblyResult.WorkerSubmissions,
            warnings,
            FatalErrors: []);
    }
}
