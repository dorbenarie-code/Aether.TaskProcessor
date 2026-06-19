using Aether.Application.Scheduling.ManagerConstraints;
using Aether.Application.Scheduling.ScheduleGeneration;
using Aether.Application.Scheduling.SubmissionForms;
using Aether.Infrastructure.Scheduling.ScheduleGeneration;

namespace Aether.Tests.Infrastructure.Scheduling.ScheduleGeneration;

public sealed class LocalCleanXlsxScheduleGenerationRunnerWorkerRequestsFailureMessageTests
{
    [Fact]
    public void Run_ShouldReturnHebrewManagerFacingMessage_WithFatalErrorDetails_WhenWorkerRequestsImportFails()
    {
        var rootDirectoryPath = CreateTemporaryDirectoryPath();
        var outputDirectoryPath = Path.Combine(rootDirectoryPath, "output");
        var inputWorkbookPath = Path.Combine(rootDirectoryPath, "input.xlsx");

        Directory.CreateDirectory(rootDirectoryPath);
        File.WriteAllText(inputWorkbookPath, "fake workbook placeholder");

        var failedGenerationResult = CreateFailedWorkerRequestsGenerationResult(
        [
            new AvailabilityMatrixImportFatalError(
                AvailabilityMatrixImportFatalErrorType.UnresolvedWorkerName,
                RowIndex: 4,
                ColumnIndex: 1,
                Header: "שם המאבטח",
                RawValue: "Worker05")
        ]);

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
            "קובץ בקשות העובדים",
            result.Message,
            StringComparison.Ordinal);

        Assert.Contains(
            "שורה 4",
            result.Message,
            StringComparison.Ordinal);

        Assert.Contains(
            "שם המאבטח",
            result.Message,
            StringComparison.Ordinal);

        Assert.Contains(
            "Worker05",
            result.Message,
            StringComparison.Ordinal);

        Assert.Contains(
            "עובד לא נמצא",
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

        Assert.DoesNotContain(
            nameof(ScheduleGenerationFailureType.AvailabilityMatrixImportFailed),
            result.Message,
            StringComparison.Ordinal);

        Assert.True(Directory.Exists(outputDirectoryPath));
        Assert.Empty(Directory.GetFiles(outputDirectoryPath));
    }

    private static CleanXlsxScheduleGenerationResult CreateFailedWorkerRequestsGenerationResult(
        IReadOnlyList<AvailabilityMatrixImportFatalError> fatalErrors)
    {
        return new CleanXlsxScheduleGenerationResult(
            Succeeded: false,
            ImportWarnings: [],
            ImportFatalErrors: fatalErrors,
            ManagerConstraintImportWarnings: [],
            ManagerConstraintImportFatalErrors: [],
            ManagerConstraintImportSummary: new ManagerConstraintImportSummary(
                ImportedForbiddenAssignmentCount: 0,
                ImportedAvoidAssignmentCount: 0,
                ImportedShiftCapacityOverrideCount: 0),
            FailureType: ScheduleGenerationFailureType.AvailabilityMatrixImportFailed);
    }

    private static string CreateTemporaryDirectoryPath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "aether-local-clean-xlsx-runner-worker-requests-message-tests",
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
