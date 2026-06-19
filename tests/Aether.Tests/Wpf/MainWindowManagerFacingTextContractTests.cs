namespace Aether.Tests.Wpf;

public sealed class MainWindowManagerFacingTextContractTests
{
    [Fact]
    public void MainWindowXaml_ShouldUseManagerFacingHebrewTitleAndHeader()
    {
        var xaml = ReadMainWindowXaml();

        Assert.Contains("Title=\"מחולל סידור עבודה\"", xaml);
        Assert.Contains("Text=\"מחולל סידור עבודה\"", xaml);
        Assert.Contains("Text=\"יצירת סידור עבודה מקובץ בקשות עובדים\"", xaml);

        Assert.DoesNotContain("Aether Schedule Generator", xaml);
    }

    [Fact]
    public void MainWindowXaml_ShouldNotExposeInternalPostRunImprovementTerm()
    {
        var xaml = ReadMainWindowXaml();

        Assert.Contains("Content=\"הפעל שיפור אוטומטי לאחר החישוב\"", xaml);

        Assert.DoesNotContain("Post Run Local Add Improvement", xaml);
    }

    private static string ReadMainWindowXaml()
    {
        var repositoryRoot = FindRepositoryRoot();

        return File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "Aether.WpfApp",
                "MainWindow.xaml"));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "Aether.WpfApp",
                "MainWindow.xaml");

            if (File.Exists(candidate))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }
}
