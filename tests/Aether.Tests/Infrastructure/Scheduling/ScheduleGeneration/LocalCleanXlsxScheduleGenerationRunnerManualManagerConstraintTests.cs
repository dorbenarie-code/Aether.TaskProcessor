using System.Text;
using Aether.Application.Scheduling.ManagerConstraints;
using Aether.Application.Scheduling.ScheduleGeneration;
using Aether.Infrastructure.Scheduling.ScheduleGeneration;

namespace Aether.Tests.Infrastructure.Scheduling.ScheduleGeneration;

public sealed class LocalCleanXlsxScheduleGenerationRunnerManualManagerConstraintTests
{
    [Fact]
    public void Run_ShouldPassManualManagerConstraintRows_WhenWorkbookDoesNotContainManagerConstraintRows()
    {
        var rootDirectoryPath = CreateTemporaryDirectoryPath();
        var outputDirectoryPath = Path.Combine(rootDirectoryPath, "output");
        var inputWorkbookPath = Path.Combine(rootDirectoryPath, "input.xlsx");

        Directory.CreateDirectory(rootDirectoryPath);
        File.WriteAllText(inputWorkbookPath, "fake workbook placeholder");

        var manualManagerConstraintRows = CreateManualManagerConstraintRows();

        var workbookInputReader = new FakeWorkbookInputReader(
            managerConstraintRows: null);

        var scheduleGenerator = new FakeScheduleGenerator(
            CreateSuccessfulGenerationResult());

        var runner = new LocalCleanXlsxScheduleGenerationRunner(
            workbookInputReader,
            scheduleGenerator);

        var result = runner.Run(new LocalCleanXlsxScheduleGenerationRequest(
            InputWorkbookPath: inputWorkbookPath,
            OutputDirectoryPath: outputDirectoryPath,
            ApplyPostRunLocalAddImprovement: false,
            ManualManagerConstraintRows: manualManagerConstraintRows));

        Assert.True(result.Succeeded, result.Message);

        var generationRequest = Assert.Single(scheduleGenerator.Requests);

        Assert.Same(
            manualManagerConstraintRows,
            generationRequest.ManagerConstraintRows);
    }

    [Fact]
    public void Run_ShouldAppendManualManagerConstraintRows_ToWorkbookManagerConstraintRows()
    {
        var rootDirectoryPath = CreateTemporaryDirectoryPath();
        var outputDirectoryPath = Path.Combine(rootDirectoryPath, "output");
        var inputWorkbookPath = Path.Combine(rootDirectoryPath, "input.xlsx");

        Directory.CreateDirectory(rootDirectoryPath);
        File.WriteAllText(inputWorkbookPath, "fake workbook placeholder");

        var workbookManagerConstraintRows = CreateWorkbookManagerConstraintRows();
        var manualManagerConstraintRows = CreateManualManagerConstraintRows();

        var workbookInputReader = new FakeWorkbookInputReader(
            workbookManagerConstraintRows);

        var scheduleGenerator = new FakeScheduleGenerator(
            CreateSuccessfulGenerationResult());

        var runner = new LocalCleanXlsxScheduleGenerationRunner(
            workbookInputReader,
            scheduleGenerator);

        var result = runner.Run(new LocalCleanXlsxScheduleGenerationRequest(
            InputWorkbookPath: inputWorkbookPath,
            OutputDirectoryPath: outputDirectoryPath,
            ApplyPostRunLocalAddImprovement: false,
            ManualManagerConstraintRows: manualManagerConstraintRows));

        Assert.True(result.Succeeded, result.Message);

        var generationRequest = Assert.Single(scheduleGenerator.Requests);

        Assert.NotNull(generationRequest.ManagerConstraintRows);

        var rows = generationRequest.ManagerConstraintRows!;

        Assert.Equal(3, rows.Count);

        Assert.Equal(
            new[]
            {
                "Type",
                "WorkerName",
                "Date",
                "ShiftKind",
                "MinResourceCount",
                "MaxResourceCount"
            },
            rows[0]);

        Assert.Equal(
            new[]
            {
                "AvoidAssignment",
                "Worker14",
                "2026-06-15",
                "Morning",
                "",
                ""
            },
            rows[1]);

        Assert.Equal(
            new[]
            {
                "ShiftCapacityOverride",
                "",
                "2026-06-19",
                "Morning",
                "3",
                "3"
            },
            rows[2]);
    }

    private static IReadOnlyList<IReadOnlyList<string>> CreateWorkbookManagerConstraintRows()
    {
        return
        [
            [
                "Type",
                "WorkerName",
                "Date",
                "ShiftKind",
                "MinResourceCount",
                "MaxResourceCount"
            ],
            [
                "AvoidAssignment",
                "Worker14",
                "2026-06-15",
                "Morning",
                "",
                ""
            ]
        ];
    }

    private static IReadOnlyList<IReadOnlyList<string>> CreateManualManagerConstraintRows()
    {
        return
        [
            [
                "Type",
                "WorkerName",
                "Date",
                "ShiftKind",
                "MinResourceCount",
                "MaxResourceCount"
            ],
            [
                "ShiftCapacityOverride",
                "",
                "2026-06-19",
                "Morning",
                "3",
                "3"
            ]
        ];
    }

    private static CleanXlsxScheduleGenerationResult CreateSuccessfulGenerationResult()
    {
        return new CleanXlsxScheduleGenerationResult(
            Succeeded: true,
            ImportWarnings: [],
            ImportFatalErrors: [],
            ManagerConstraintImportWarnings: [],
            ManagerConstraintImportFatalErrors: [],
            ManagerConstraintImportSummary: new ManagerConstraintImportSummary(
                ImportedForbiddenAssignmentCount: 0,
                ImportedAvoidAssignmentCount: 0,
                ImportedShiftCapacityOverrideCount: 0),
            ScheduleTableXlsxBytes: Encoding.UTF8.GetBytes("fake xlsx bytes"));
    }

    private static string CreateTemporaryDirectoryPath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "aether-local-clean-xlsx-runner-manual-manager-constraint-tests",
            Guid.NewGuid().ToString("N"));
    }

    private sealed class FakeWorkbookInputReader : ILocalCleanXlsxWorkbookInputReader
    {
        private readonly IReadOnlyList<IReadOnlyList<string>>? managerConstraintRows;

        public FakeWorkbookInputReader(
            IReadOnlyList<IReadOnlyList<string>>? managerConstraintRows)
        {
            this.managerConstraintRows = managerConstraintRows;
        }

        public MemoryStream AvailabilityMatrixStream { get; } = new([1, 2, 3]);

        public LocalCleanXlsxWorkbookInput Open(string workbookPath)
        {
            return new LocalCleanXlsxWorkbookInput(
                AvailabilityMatrixStream,
                managerConstraintRows);
        }
    }

    private sealed class FakeScheduleGenerator : ILocalCleanXlsxScheduleGenerator
    {
        private readonly CleanXlsxScheduleGenerationResult result;

        public FakeScheduleGenerator(
            CleanXlsxScheduleGenerationResult result)
        {
            this.result = result;
        }

        public List<CleanXlsxScheduleGenerationRequest> Requests { get; } = [];

        public CleanXlsxScheduleGenerationResult Run(
            CleanXlsxScheduleGenerationRequest request)
        {
            Requests.Add(request);

            return result;
        }
    }
}
