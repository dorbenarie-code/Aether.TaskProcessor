using System.Text;
using Aether.Application.Scheduling.ManagerConstraints;
using Aether.Application.Scheduling.ScheduleGeneration;
using Aether.Infrastructure.Scheduling.ScheduleGeneration;

namespace Aether.Tests.Infrastructure.Scheduling.ScheduleGeneration;

public sealed class LocalCleanXlsxScheduleGenerationRunnerTests
{
    [Fact]
    public void Run_ShouldReturnInputWorkbookNotFound_WhenInputWorkbookDoesNotExist()
    {
        var outputDirectoryPath = CreateTemporaryDirectoryPath();
        Directory.CreateDirectory(outputDirectoryPath);

        var missingInputWorkbookPath = Path.Combine(
            outputDirectoryPath,
            "missing-input.xlsx");

        var runner = new LocalCleanXlsxScheduleGenerationRunner();

        var result = runner.Run(new LocalCleanXlsxScheduleGenerationRequest(
            InputWorkbookPath: missingInputWorkbookPath,
            OutputDirectoryPath: outputDirectoryPath,
            ApplyPostRunLocalAddImprovement: false));

        Assert.False(result.Succeeded);
        Assert.Equal(
            LocalCleanXlsxScheduleGenerationFailureType.InputWorkbookNotFound,
            result.FailureType);

        Assert.Null(result.ScheduleTableXlsxPath);
        Assert.Contains(missingInputWorkbookPath, result.Message);

        Assert.Empty(Directory.GetFiles(outputDirectoryPath));
    }

    [Fact]
    public void Run_ShouldReturnGenerationFailed_AndNotWriteXlsx_WhenGenerationFails()
    {
        var rootDirectoryPath = CreateTemporaryDirectoryPath();
        var outputDirectoryPath = Path.Combine(rootDirectoryPath, "output");
        var inputWorkbookPath = Path.Combine(rootDirectoryPath, "input.xlsx");

        Directory.CreateDirectory(rootDirectoryPath);
        File.WriteAllText(inputWorkbookPath, "fake workbook placeholder");

        var failedGenerationResult = new CleanXlsxScheduleGenerationResult(
            Succeeded: false,
            ImportWarnings: [],
            ImportFatalErrors: [],
            ManagerConstraintImportWarnings: [],
            ManagerConstraintImportFatalErrors: [],
            ManagerConstraintImportSummary: new ManagerConstraintImportSummary(
                ImportedForbiddenAssignmentCount: 0,
                ImportedAvoidAssignmentCount: 0,
                ImportedShiftCapacityOverrideCount: 0),
            FailureType: ScheduleGenerationFailureType.ManagerConstraintImportFailed);

        var runner = new LocalCleanXlsxScheduleGenerationRunner(
            new FakeWorkbookInputReader(),
            new FakeScheduleGenerator(failedGenerationResult));

        var result = runner.Run(new LocalCleanXlsxScheduleGenerationRequest(
            InputWorkbookPath: inputWorkbookPath,
            OutputDirectoryPath: outputDirectoryPath,
            ApplyPostRunLocalAddImprovement: false));

        Assert.False(result.Succeeded);
        Assert.Equal(
            LocalCleanXlsxScheduleGenerationFailureType.GenerationFailed,
            result.FailureType);

        Assert.Null(result.ScheduleTableXlsxPath);
        Assert.Contains(
            "לא ניתן לייצר סידור עבודה",
            result.Message,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "FailureType",
            result.Message,
            StringComparison.Ordinal);

        Assert.True(Directory.Exists(outputDirectoryPath));
        Assert.Empty(Directory.GetFiles(outputDirectoryPath));
    }

    [Fact]
    public void Run_ShouldWriteScheduleTableXlsx_WhenGenerationSucceeds()
    {
        var rootDirectoryPath = CreateTemporaryDirectoryPath();
        var outputDirectoryPath = Path.Combine(rootDirectoryPath, "output");
        var inputWorkbookPath = Path.Combine(rootDirectoryPath, "input.xlsx");

        Directory.CreateDirectory(rootDirectoryPath);
        File.WriteAllText(inputWorkbookPath, "fake workbook placeholder");

        var expectedXlsxBytes = Encoding.UTF8.GetBytes("fake xlsx bytes");

        var workbookInputReader = new FakeWorkbookInputReader();
        var scheduleGenerator = new FakeScheduleGenerator(
            CreateSuccessfulGenerationResult(expectedXlsxBytes));

        var runner = new LocalCleanXlsxScheduleGenerationRunner(
            workbookInputReader,
            scheduleGenerator);

        var result = runner.Run(new LocalCleanXlsxScheduleGenerationRequest(
            InputWorkbookPath: inputWorkbookPath,
            OutputDirectoryPath: outputDirectoryPath,
            ApplyPostRunLocalAddImprovement: true));

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(
            LocalCleanXlsxScheduleGenerationFailureType.None,
            result.FailureType);

        Assert.False(string.IsNullOrWhiteSpace(result.ScheduleTableXlsxPath));

        var scheduleTableXlsxPath = result.ScheduleTableXlsxPath!;

        Assert.True(File.Exists(scheduleTableXlsxPath));
        Assert.Equal(expectedXlsxBytes, File.ReadAllBytes(scheduleTableXlsxPath));
        Assert.StartsWith(outputDirectoryPath, scheduleTableXlsxPath);
        Assert.EndsWith(".xlsx", scheduleTableXlsxPath);
        Assert.Contains("completed", result.Message, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(inputWorkbookPath, workbookInputReader.OpenedWorkbookPath);

        var generationRequest = Assert.Single(scheduleGenerator.Requests);

        Assert.Same(
            workbookInputReader.AvailabilityMatrixStream,
            generationRequest.AvailabilityMatrixStream);

        Assert.Same(
            workbookInputReader.ManagerConstraintRows,
            generationRequest.ManagerConstraintRows);

        Assert.True(generationRequest.ApplyPostRunLocalAddImprovement);
        Assert.False(generationRequest.IncludeTargetGapDiagnostics);

        Assert.Equal(736.0, generationRequest.TotalEffectiveTargetHours);
        Assert.Equal(5.0, generationRequest.MaximumAssignedHoursDeviationFromAverageHours);
        Assert.Equal(20260603, generationRequest.Seed);
        Assert.Equal(19, generationRequest.Resources.Count);
        Assert.Equal(42, generationRequest.Shifts.Count);
    }


    [Fact]
    public void Run_ShouldWriteScheduleTableXlsx_WithSchedulePeriodFilename_WhenGenerationSucceeds()
    {
        var rootDirectoryPath = CreateTemporaryDirectoryPath();
        var outputDirectoryPath = Path.Combine(rootDirectoryPath, "output");
        var inputWorkbookPath = Path.Combine(rootDirectoryPath, "input.xlsx");

        Directory.CreateDirectory(rootDirectoryPath);
        File.WriteAllText(inputWorkbookPath, "fake workbook placeholder");

        var expectedXlsxBytes = Encoding.UTF8.GetBytes("fake xlsx bytes");

        var runner = new LocalCleanXlsxScheduleGenerationRunner(
            new FakeWorkbookInputReader(),
            new FakeScheduleGenerator(CreateSuccessfulGenerationResult(expectedXlsxBytes)));

        var result = runner.Run(new LocalCleanXlsxScheduleGenerationRequest(
            InputWorkbookPath: inputWorkbookPath,
            OutputDirectoryPath: outputDirectoryPath,
            ApplyPostRunLocalAddImprovement: false));

        Assert.True(result.Succeeded, result.Message);
        Assert.False(string.IsNullOrWhiteSpace(result.ScheduleTableXlsxPath));

        var scheduleTableXlsxPath = result.ScheduleTableXlsxPath!;

        Assert.Equal(
            "last-dor-schedule-2026-06-14-to-2026-06-28.xlsx",
            Path.GetFileName(scheduleTableXlsxPath));

        Assert.Equal(
            outputDirectoryPath,
            Path.GetDirectoryName(scheduleTableXlsxPath));

        Assert.True(File.Exists(scheduleTableXlsxPath));
        Assert.Equal(expectedXlsxBytes, File.ReadAllBytes(scheduleTableXlsxPath));
        Assert.Contains(Path.GetFileName(scheduleTableXlsxPath), result.Message);
    }


    [Fact]
    public void Run_ShouldReturnClearFailureMessage_WhenManagerConstraintImportFails()
    {
        var rootDirectoryPath = CreateTemporaryDirectoryPath();
        var outputDirectoryPath = Path.Combine(rootDirectoryPath, "output");
        var inputWorkbookPath = Path.Combine(rootDirectoryPath, "input.xlsx");

        Directory.CreateDirectory(rootDirectoryPath);
        File.WriteAllText(inputWorkbookPath, "fake workbook placeholder");

        var failedGenerationResult = new CleanXlsxScheduleGenerationResult(
            Succeeded: false,
            ImportWarnings: [],
            ImportFatalErrors: [],
            ManagerConstraintImportWarnings: [],
            ManagerConstraintImportFatalErrors: [],
            ManagerConstraintImportSummary: new ManagerConstraintImportSummary(
                ImportedForbiddenAssignmentCount: 0,
                ImportedAvoidAssignmentCount: 0,
                ImportedShiftCapacityOverrideCount: 0),
            FailureType: ScheduleGenerationFailureType.ManagerConstraintImportFailed);

        var runner = new LocalCleanXlsxScheduleGenerationRunner(
            new FakeWorkbookInputReader(),
            new FakeScheduleGenerator(failedGenerationResult));

        var result = runner.Run(new LocalCleanXlsxScheduleGenerationRequest(
            InputWorkbookPath: inputWorkbookPath,
            OutputDirectoryPath: outputDirectoryPath,
            ApplyPostRunLocalAddImprovement: false));

        Assert.False(result.Succeeded);
        Assert.Equal(
            LocalCleanXlsxScheduleGenerationFailureType.GenerationFailed,
            result.FailureType);

        Assert.Null(result.ScheduleTableXlsxPath);

        Assert.Contains(
            "לא ניתן לייצר סידור עבודה",
            result.Message,
            StringComparison.Ordinal);

        Assert.Contains(
            "אילוצי המנהל",
            result.Message,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "Schedule generation failed",
            result.Message,
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            "FailureType",
            result.Message,
            StringComparison.Ordinal);

        Assert.True(Directory.Exists(outputDirectoryPath));
        Assert.Empty(Directory.GetFiles(outputDirectoryPath));
    }

    private static CleanXlsxScheduleGenerationResult CreateSuccessfulGenerationResult(
        byte[] scheduleTableXlsxBytes)
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
            ScheduleTableXlsxBytes: scheduleTableXlsxBytes);
    }

    private static string CreateTemporaryDirectoryPath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "aether-local-clean-xlsx-runner-tests",
            Guid.NewGuid().ToString("N"));
    }

    private sealed class FakeWorkbookInputReader : ILocalCleanXlsxWorkbookInputReader
    {
        public string? OpenedWorkbookPath { get; private set; }

        public MemoryStream AvailabilityMatrixStream { get; } = new([1, 2, 3]);

        public IReadOnlyList<IReadOnlyList<string>> ManagerConstraintRows { get; } =
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

        public LocalCleanXlsxWorkbookInput Open(string workbookPath)
        {
            OpenedWorkbookPath = workbookPath;

            return new LocalCleanXlsxWorkbookInput(
                AvailabilityMatrixStream,
                ManagerConstraintRows);
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
