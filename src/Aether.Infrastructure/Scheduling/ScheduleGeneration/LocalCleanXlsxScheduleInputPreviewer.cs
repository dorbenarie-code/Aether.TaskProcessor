using Aether.Application.Scheduling.Profiles;
using Aether.Application.Scheduling.SubmissionForms;
using Aether.Domain.Optimization;
using Aether.Infrastructure.Forms;

namespace Aether.Infrastructure.Scheduling.ScheduleGeneration;

public sealed record LocalCleanXlsxScheduleInputPreviewRequest(
    string InputWorkbookPath);

public sealed record LocalCleanXlsxScheduleInputPreviewResult(
    bool Succeeded,
    string Message,
    IReadOnlyList<string> WorkerNames,
    IReadOnlyList<DateOnly> ScheduleDates);

public sealed class LocalCleanXlsxScheduleInputPreviewer
{
    private readonly ILocalCleanXlsxWorkbookInputReader workbookInputReader;
    private readonly IFormTableReader formTableReader;

    public LocalCleanXlsxScheduleInputPreviewer()
        : this(
            new LocalCleanXlsxWorkbookInputReaderAdapter(),
            new XlsxFormTableReader())
    {
    }

    public LocalCleanXlsxScheduleInputPreviewer(
        ILocalCleanXlsxWorkbookInputReader workbookInputReader,
        IFormTableReader formTableReader)
    {
        this.workbookInputReader = workbookInputReader ??
            throw new ArgumentNullException(nameof(workbookInputReader));

        this.formTableReader = formTableReader ??
            throw new ArgumentNullException(nameof(formTableReader));
    }

    public LocalCleanXlsxScheduleInputPreviewResult Load(
        LocalCleanXlsxScheduleInputPreviewRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!File.Exists(request.InputWorkbookPath))
        {
            return new LocalCleanXlsxScheduleInputPreviewResult(
                Succeeded: false,
                Message: $"Input workbook was not found: {request.InputWorkbookPath}",
                WorkerNames: [],
                ScheduleDates: []);
        }

        var profile = new LastDorLocalScheduleGenerationProfile();
        var schedulePeriod = profile.CreateSchedulePeriod();
        var resources = profile.CreateResources();

        using var workbookInput = workbookInputReader.Open(
            request.InputWorkbookPath);

        var rows = formTableReader.Read(
            workbookInput.AvailabilityMatrixStream);

        var importResult = new AvailabilityMatrixWorkerSubmissionImporter()
            .Import(new AvailabilityMatrixWorkerSubmissionImportRequest(
                rows,
                schedulePeriod,
                resources));

        if (importResult.FatalErrors.Count > 0)
        {
            return new LocalCleanXlsxScheduleInputPreviewResult(
                Succeeded: false,
                Message: $"Worker requests preview failed with {importResult.FatalErrors.Count} fatal error(s).",
                WorkerNames: [],
                ScheduleDates: []);
        }

        var resourcesById = resources.ToDictionary(resource => resource.Id);

        var workerNames = importResult.WorkerSubmissions
            .Select(submission => resourcesById[submission.ResourceId].Name)
            .ToArray();

        var scheduleDates = profile.CreateShifts()
            .Select(shift => DateOnly.FromDateTime(shift.StartUtc))
            .Distinct()
            .OrderBy(date => date)
            .ToArray();

        return new LocalCleanXlsxScheduleInputPreviewResult(
            Succeeded: true,
            Message: $"Worker requests preview completed: {workerNames.Length} worker(s), {scheduleDates.Length} schedule date(s).",
            WorkerNames: workerNames,
            ScheduleDates: scheduleDates);
    }
}
