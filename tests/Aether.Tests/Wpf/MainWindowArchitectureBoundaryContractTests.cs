namespace Aether.Tests.Wpf;

public sealed class MainWindowArchitectureBoundaryContractTests
{
    [Fact]
    public void MainWindow_ShouldRemainThin_AndNotKnowSchedulingInternals()
    {
        var repositoryRoot = GetRepositoryRoot();

        var codeBehind = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Aether.WpfApp",
            "MainWindow.xaml.cs"));

        var forbiddenReferences = new[]
        {
            "ManagerConstraintRowsImporter",
            "CleanXlsxScheduleGenerationUseCase",
            "AvailabilityMatrixStreamOptimizationRunner",
            "AvailabilityMatrixImportedRowsOptimizationRunner",
            "ClosedFormSubmissionOptimizationRunner",
            "XlsxAvailabilityMatrixWorkbookInputReader",
            "XlsxFormTableReader",
            "ScheduleTableXlsxExporter",
            "XlsxWorkbookSheetTableReader",
            "XlsxWorksheetTableReadResult",
            "XLWorkbook",
            "IXLWorkbook",
            "ClosedXML",
            "Aether.Infrastructure.Forms",
            "Aether.Infrastructure.Scheduling.Reports"
        };

        foreach (var forbiddenReference in forbiddenReferences)
        {
            Assert.DoesNotContain(
                forbiddenReference,
                codeBehind,
                StringComparison.Ordinal);
        }

        Assert.Contains(
            "LocalCleanXlsxScheduleGenerationRunner",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "ManagerConstraintRowsBuilder",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "LocalCleanXlsxScheduleGenerationRequest",
            codeBehind,
            StringComparison.Ordinal);
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "Aether.WpfApp",
                "MainWindow.xaml.cs");

            if (File.Exists(candidate))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}
