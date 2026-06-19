namespace Aether.Tests.Desktop;

public sealed class MainWindowCodeBehindContractTests
{
    [Fact]
    public void MainWindow_ShouldWireControlsToLocalScheduleGenerationRunner_WithoutSchedulingInternals()
    {
        var repositoryRoot = FindRepositoryRoot();

        var xamlPath = Path.Combine(
            repositoryRoot,
            "src",
            "Aether.WpfApp",
            "MainWindow.xaml");

        var codeBehindPath = Path.Combine(
            repositoryRoot,
            "src",
            "Aether.WpfApp",
            "MainWindow.xaml.cs");

        Assert.True(
            File.Exists(xamlPath),
            $"Expected MainWindow.xaml to exist: {xamlPath}");

        Assert.True(
            File.Exists(codeBehindPath),
            $"Expected MainWindow.xaml.cs to exist: {codeBehindPath}");

        var xaml = File.ReadAllText(xamlPath);
        var source = File.ReadAllText(codeBehindPath);

        Assert.Contains("Click=\"SelectInputWorkbookButton_Click\"", xaml);
        Assert.Contains("Click=\"SelectOutputDirectoryButton_Click\"", xaml);
        Assert.Contains("Click=\"GenerateScheduleButton_Click\"", xaml);
        Assert.Contains("Click=\"OpenScheduleTableXlsxButton_Click\"", xaml);
        Assert.Contains("Click=\"OpenOutputDirectoryButton_Click\"", xaml);

        Assert.Contains(
            "using Aether.Infrastructure.Scheduling.ScheduleGeneration;",
            source);

        Assert.Contains(
            "LocalCleanXlsxScheduleGenerationRunner",
            source);

        Assert.Contains(
            "selectedInputWorkbookPath",
            source);

        Assert.Contains(
            "selectedOutputDirectoryPath",
            source);

        Assert.Contains(
            "generatedScheduleTableXlsxPath",
            source);

        Assert.Contains(
            "SelectInputWorkbookButton_Click",
            source);

        Assert.Contains(
            "SelectOutputDirectoryButton_Click",
            source);

        Assert.Contains(
            "GenerateScheduleButton_Click",
            source);

        Assert.Contains(
            "OpenScheduleTableXlsxButton_Click",
            source);

        Assert.Contains(
            "OpenOutputDirectoryButton_Click",
            source);

        Assert.Contains(
            "Task.Run",
            source);

        Assert.DoesNotContain(
            "CleanXlsxScheduleGenerationUseCase",
            source);

        Assert.DoesNotContain(
            "AvailabilityMatrixStreamOptimizationRunner",
            source);

        Assert.DoesNotContain(
            "XlsxFormTableReader",
            source);

        Assert.DoesNotContain(
            "ScheduleTableXlsxExporter",
            source);

        Assert.DoesNotContain(
            "LastDorLocalScheduleGenerationProfile",
            source);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "Aether.TaskProcessor.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ??
            throw new InvalidOperationException("Could not find repository root.");
    }
}
