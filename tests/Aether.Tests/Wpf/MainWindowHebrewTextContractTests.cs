namespace Aether.Tests.Wpf;

public sealed class MainWindowHebrewTextContractTests
{
    [Fact]
    public void MainWindowCodeBehind_ShouldNotContainMojibakeHebrewText()
    {
        var codeBehind = ReadMainWindowCodeBehind();

        Assert.DoesNotContain("׳", codeBehind);
    }

    [Fact]
    public void MainWindowCodeBehind_ShouldUseReadableHebrew_ForFolderSelectionAndRequiredInputMessages()
    {
        var codeBehind = ReadMainWindowCodeBehind();

        Assert.Contains("בחר תיקיית שמירה", codeBehind);
        Assert.Contains("תיקיית פלט נבחרה.", codeBehind);
        Assert.Contains("נדרש לבחור קובץ בקשות עובדים.", codeBehind);
        Assert.Contains("נדרש לבחור תיקיית שמירה.", codeBehind);
    }

    [Fact]
    public void MainWindowCodeBehind_ShouldUseReadableHebrew_WhenWorkbookPreviewFails()
    {
        var codeBehind = ReadMainWindowCodeBehind();

        Assert.Contains("נכשלה טעינת תצוגה מקדימה של קובץ הבקשות", codeBehind);
        Assert.DoesNotContain("Workbook preview failed", codeBehind);
    }

    private static string ReadMainWindowCodeBehind()
    {
        var repositoryRoot = FindRepositoryRoot();

        return File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "Aether.WpfApp",
                "MainWindow.xaml.cs"));
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
                "MainWindow.xaml.cs");

            if (File.Exists(candidate))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }
}
