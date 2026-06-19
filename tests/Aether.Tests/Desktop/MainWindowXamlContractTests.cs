namespace Aether.Tests.Desktop;

public sealed class MainWindowXamlContractTests
{
    [Fact]
    public void MainWindow_ShouldExposeMinimalScheduleGenerationControls()
    {
        var repositoryRoot = FindRepositoryRoot();

        var xamlPath = Path.Combine(
            repositoryRoot,
            "src",
            "Aether.WpfApp",
            "MainWindow.xaml");

        Assert.True(
            File.Exists(xamlPath),
            $"Expected MainWindow.xaml to exist: {xamlPath}");

        var xaml = File.ReadAllText(xamlPath);

        Assert.Contains("SelectInputWorkbookButton", xaml);
        Assert.Contains("InputWorkbookPathTextBlock", xaml);

        Assert.Contains("SelectOutputDirectoryButton", xaml);
        Assert.Contains("OutputDirectoryPathTextBlock", xaml);

        Assert.Contains("ApplyPostRunLocalAddImprovementCheckBox", xaml);
        Assert.Contains("GenerateScheduleButton", xaml);

        Assert.Contains("StatusTextBlock", xaml);

        Assert.Contains("OpenScheduleTableXlsxButton", xaml);
        Assert.Contains("OpenOutputDirectoryButton", xaml);
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
