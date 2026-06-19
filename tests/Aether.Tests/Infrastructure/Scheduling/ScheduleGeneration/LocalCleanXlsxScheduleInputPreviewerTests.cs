using Aether.Application.Scheduling.SubmissionForms;
using Aether.Infrastructure.Scheduling.ScheduleGeneration;

namespace Aether.Tests.Infrastructure.Scheduling.ScheduleGeneration;

public sealed class LocalCleanXlsxScheduleInputPreviewerTests
{
    [Fact]
    public void Load_ShouldReturnCanonicalWorkerNamesAndScheduleDates_WhenWorkbookImportsSuccessfully()
    {
        var rootDirectoryPath = CreateTemporaryDirectoryPath();
        var inputWorkbookPath = Path.Combine(rootDirectoryPath, "input.xlsx");

        Directory.CreateDirectory(rootDirectoryPath);
        File.WriteAllText(inputWorkbookPath, "fake workbook placeholder");

        var availabilityMatrixStream = new MemoryStream();

        var workbookInputReader = new FakeWorkbookInputReader(
            availabilityMatrixStream);

        var rowsReader = new FakeFormTableReader(CreateAvailabilityMatrixRows());

        var previewer = new LocalCleanXlsxScheduleInputPreviewer(
            workbookInputReader,
            rowsReader);

        var result = previewer.Load(
            new LocalCleanXlsxScheduleInputPreviewRequest(inputWorkbookPath));

        Assert.True(result.Succeeded, result.Message);

        Assert.Equal(
            ["Worker10", "Worker14", "Worker16"],
            result.WorkerNames.ToArray());

        Assert.Equal(14, result.ScheduleDates.Count);
        Assert.Equal(new DateOnly(2026, 6, 14), result.ScheduleDates.First());
        Assert.Equal(new DateOnly(2026, 6, 27), result.ScheduleDates.Last());

        Assert.Equal(inputWorkbookPath, workbookInputReader.OpenedWorkbookPath);

        var readStream = Assert.Single(rowsReader.ReadStreams);
        Assert.Same(availabilityMatrixStream, readStream);
    }

    private static IReadOnlyList<IReadOnlyList<string>> CreateAvailabilityMatrixRows()
    {
        return
        [
            CreateHeaders(),
            CreateWorkerRow("Worker10"),
            CreateWorkerRow("  Worker14  "),
            CreateWorkerRow("Worker16")
        ];
    }

    private static IReadOnlyList<string> CreateHeaders()
    {
        return
        [
            "שם המאבטח",
            "שבוע ראשון [ראשון - 14/06]",
            "שבוע ראשון [שני - 15/06]",
            "שבוע ראשון [שלWorker19 - 16/06]",
            "שבוע ראשון [רביעי - 17/06]",
            "שבוע ראשון [חמWorker19 - 18/06]",
            "שבוע ראשון [שWorker19 - 19/06]",
            "שבוע ראשון [שבת - 20/06]",
            "שבוע שני [ראשון - 21/06]",
            "שבוע שני [שני - 22/06]",
            "שבוע שני [שלWorker19 - 23/06]",
            "שבוע שני [רביעי - 24/06]",
            "שבוע שני [חמWorker19 - 25/06]",
            "שבוע שני [שWorker19 - 26/06]",
            "שבוע שני [שבת - 27/06]"
        ];
    }

    private static IReadOnlyList<string> CreateWorkerRow(string workerName)
    {
        return
        [
            workerName,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty
        ];
    }

    private static string CreateTemporaryDirectoryPath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "aether-tests",
            Guid.NewGuid().ToString("N"));
    }

    private sealed class FakeWorkbookInputReader : ILocalCleanXlsxWorkbookInputReader
    {
        private readonly Stream availabilityMatrixStream;

        public FakeWorkbookInputReader(Stream availabilityMatrixStream)
        {
            this.availabilityMatrixStream = availabilityMatrixStream;
        }

        public string? OpenedWorkbookPath { get; private set; }

        public LocalCleanXlsxWorkbookInput Open(string workbookPath)
        {
            OpenedWorkbookPath = workbookPath;

            return new LocalCleanXlsxWorkbookInput(
                availabilityMatrixStream,
                null);
        }
    }

    private sealed class FakeFormTableReader : IFormTableReader
    {
        private readonly IReadOnlyList<IReadOnlyList<string>> rows;

        public FakeFormTableReader(IReadOnlyList<IReadOnlyList<string>> rows)
        {
            this.rows = rows;
        }

        public List<Stream> ReadStreams { get; } = [];

        public IReadOnlyList<IReadOnlyList<string>> Read(Stream stream)
        {
            ReadStreams.Add(stream);

            return rows;
        }
    }
}
