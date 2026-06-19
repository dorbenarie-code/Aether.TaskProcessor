using Aether.Application.Scheduling.ManagerConstraints;
using Aether.Application.Scheduling.ScheduleGeneration;
using Aether.Infrastructure.Scheduling.ScheduleGeneration;

namespace Aether.Tests.Infrastructure.Scheduling.ScheduleGeneration;

public sealed class LocalCleanXlsxScheduleGenerationRunnerOptimizationFailureMessageTests
{
    [Fact]
    public void Run_ShouldReturnHebrewManagerFacingMessage_WhenOptimizationResultIsMissing()
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
            FailureType: ScheduleGenerationFailureType.OptimizationResultMissing);

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
            "המערכת לא הצליחה להרכיב סידור תקין",
            result.Message,
            StringComparison.Ordinal);

        Assert.Contains(
            "קובץ בקשות העובדים",
            result.Message,
            StringComparison.Ordinal);

        Assert.Contains(
            "אילוצי המנהל",
            result.Message,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "המנוע",
            result.Message,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "optimization",
            result.Message,
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            nameof(ScheduleGenerationFailureType.OptimizationResultMissing),
            result.Message,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "FailureType",
            result.Message,
            StringComparison.Ordinal);

        Assert.True(Directory.Exists(outputDirectoryPath));
        Assert.Empty(Directory.GetFiles(outputDirectoryPath));
    }

    private static string CreateTemporaryDirectoryPath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "aether-local-clean-xlsx-runner-optimization-message-tests",
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
