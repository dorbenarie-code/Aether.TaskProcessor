using Aether.Application.Scheduling.ManagerConstraints;
using Aether.Application.Scheduling.ScheduleGeneration;
using Aether.Infrastructure.Scheduling.ScheduleGeneration;

namespace Aether.Tests.Infrastructure.Scheduling.ScheduleGeneration;

public sealed class LocalCleanXlsxScheduleGenerationRunnerManagerConstraintFailureMessageTests
{
    [Fact]
    public void Run_ShouldReturnHebrewManagerFacingMessage_WithFatalErrorDetails_WhenManagerConstraintImportFails()
    {
        var rootDirectoryPath = CreateTemporaryDirectoryPath();
        var outputDirectoryPath = Path.Combine(rootDirectoryPath, "output");
        var inputWorkbookPath = Path.Combine(rootDirectoryPath, "input.xlsx");

        Directory.CreateDirectory(rootDirectoryPath);
        File.WriteAllText(inputWorkbookPath, "fake workbook placeholder");

        var failedGenerationResult = CreateFailedManagerConstraintGenerationResult(
        [
            new ManagerConstraintRowsImportFatalError(
                ManagerConstraintRowsImportFatalErrorType.UnexpectedWorkerName,
                RowIndex: 1,
                ColumnIndex: 1,
                Header: "WorkerName",
                RawValue: "Worker03")
        ]);

        var runner = new LocalCleanXlsxScheduleGenerationRunner(
            new FakeWorkbookInputReader(),
            new FakeScheduleGenerator(failedGenerationResult));

        var result = runner.Run(new LocalCleanXlsxScheduleGenerationRequest(
            InputWorkbookPath: inputWorkbookPath,
            OutputDirectoryPath: outputDirectoryPath,
            ApplyPostRunLocalAddImprovement: false,
            ManualManagerConstraintRows: CreateManualCapacityOverrideRowsWithUnexpectedWorkerName()));

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

        Assert.Contains(
            "שורה 1",
            result.Message,
            StringComparison.Ordinal);

        Assert.Contains(
            "Worker03",
            result.Message,
            StringComparison.Ordinal);

        Assert.Contains(
            "אין לבחור עובד",
            result.Message,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "Schedule generation failed",
            result.Message,
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            "Please fix",
            result.Message,
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            "FailureType",
            result.Message,
            StringComparison.Ordinal);

        Assert.True(Directory.Exists(outputDirectoryPath));
        Assert.Empty(Directory.GetFiles(outputDirectoryPath));
    }

    [Fact]
    public void Run_ShouldReturnHebrewManagerFacingMessage_ForIncompleteManagerConstraintRow()
    {
        var rootDirectoryPath = CreateTemporaryDirectoryPath();
        var outputDirectoryPath = Path.Combine(rootDirectoryPath, "output");
        var inputWorkbookPath = Path.Combine(rootDirectoryPath, "input.xlsx");

        Directory.CreateDirectory(rootDirectoryPath);
        File.WriteAllText(inputWorkbookPath, "fake workbook placeholder");

        var failedGenerationResult = CreateFailedManagerConstraintGenerationResult(
        [
            new ManagerConstraintRowsImportFatalError(
                ManagerConstraintRowsImportFatalErrorType.MissingWorkerName,
                RowIndex: 2,
                ColumnIndex: 1,
                Header: "WorkerName",
                RawValue: "")
        ]);

        var runner = new LocalCleanXlsxScheduleGenerationRunner(
            new FakeWorkbookInputReader(),
            new FakeScheduleGenerator(failedGenerationResult));

        var result = runner.Run(new LocalCleanXlsxScheduleGenerationRequest(
            InputWorkbookPath: inputWorkbookPath,
            OutputDirectoryPath: outputDirectoryPath,
            ApplyPostRunLocalAddImprovement: false,
            ManualManagerConstraintRows: CreateIncompleteManualAssignmentRows()));

        Assert.False(result.Succeeded);

        Assert.Contains(
            "לא ניתן לייצר סידור עבודה",
            result.Message,
            StringComparison.Ordinal);

        Assert.Contains(
            "אילוצי המנהל",
            result.Message,
            StringComparison.Ordinal);

        Assert.Contains(
            "שורה 2",
            result.Message,
            StringComparison.Ordinal);

        Assert.Contains(
            "חסר עובד",
            result.Message,
            StringComparison.Ordinal);

        Assert.Contains(
            "להשלים את שדה העובד",
            result.Message,
            StringComparison.Ordinal);

        Assert.Contains(
            "למחוק את השורה",
            result.Message,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            nameof(ScheduleGenerationFailureType.ManagerConstraintImportFailed),
            result.Message,
            StringComparison.Ordinal);

        Assert.True(Directory.Exists(outputDirectoryPath));
        Assert.Empty(Directory.GetFiles(outputDirectoryPath));
    }

    private static CleanXlsxScheduleGenerationResult CreateFailedManagerConstraintGenerationResult(
        IReadOnlyList<ManagerConstraintRowsImportFatalError> fatalErrors)
    {
        return new CleanXlsxScheduleGenerationResult(
            Succeeded: false,
            ImportWarnings: [],
            ImportFatalErrors: [],
            ManagerConstraintImportWarnings: [],
            ManagerConstraintImportFatalErrors: fatalErrors,
            ManagerConstraintImportSummary: new ManagerConstraintImportSummary(
                ImportedForbiddenAssignmentCount: 0,
                ImportedAvoidAssignmentCount: 0,
                ImportedShiftCapacityOverrideCount: 0),
            FailureType: ScheduleGenerationFailureType.ManagerConstraintImportFailed);
    }

    private static IReadOnlyList<IReadOnlyList<string>> CreateManualCapacityOverrideRowsWithUnexpectedWorkerName()
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
                "Worker03",
                "2026-06-19",
                "Morning",
                "3",
                "3"
            ]
        ];
    }

    private static IReadOnlyList<IReadOnlyList<string>> CreateIncompleteManualAssignmentRows()
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
                "ForbidAssignment",
                "",
                "2026-06-19",
                "Morning",
                "",
                ""
            ]
        ];
    }

    private static string CreateTemporaryDirectoryPath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "aether-local-clean-xlsx-runner-manager-constraint-message-tests",
            Guid.NewGuid().ToString("N"));
    }

    private sealed class FakeWorkbookInputReader : ILocalCleanXlsxWorkbookInputReader
    {
        public LocalCleanXlsxWorkbookInput Open(string workbookPath)
        {
            return new LocalCleanXlsxWorkbookInput(
                new MemoryStream([1, 2, 3]),
                managerConstraintRows: null);
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

        public CleanXlsxScheduleGenerationResult Run(
            CleanXlsxScheduleGenerationRequest request)
        {
            return result;
        }
    }
}
