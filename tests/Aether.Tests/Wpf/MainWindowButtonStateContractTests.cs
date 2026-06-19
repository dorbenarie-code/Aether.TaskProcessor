namespace Aether.Tests.Wpf;

public sealed class MainWindowButtonStateContractTests
{
    [Fact]
    public void MainWindow_ShouldDisableGenerateButtonUntilInputWorkbookAndOutputDirectoryAreSelected()
    {
        var repositoryRoot = GetRepositoryRoot();

        var xaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Aether.WpfApp",
            "MainWindow.xaml"));

        var codeBehind = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Aether.WpfApp",
            "MainWindow.xaml.cs"));

        var generateButtonBlock = ExtractElementBlock(
            xaml,
            "GenerateScheduleButton");

        Assert.Contains(
            "IsEnabled=\"False\"",
            generateButtonBlock,
            StringComparison.Ordinal);

        Assert.Contains(
            "RefreshGenerateButtonState();",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "private void RefreshGenerateButtonState()",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "GenerateScheduleButton.IsEnabled",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "selectedInputWorkbookPath",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "selectedOutputDirectoryPath",
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
                "MainWindow.xaml");

            if (File.Exists(candidate))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }

    private static string ExtractElementBlock(
        string source,
        string elementName)
    {
        var nameToken = $"x:Name=\"{elementName}\"";
        var startIndex = source.IndexOf(nameToken, StringComparison.Ordinal);

        if (startIndex < 0)
        {
            throw new InvalidOperationException(
                $"Element '{elementName}' was not found.");
        }

        var elementStartIndex = source.LastIndexOf(
            '<',
            startIndex);

        var elementEndIndex = source.IndexOf(
            "/>",
            startIndex,
            StringComparison.Ordinal);

        if (elementStartIndex < 0 || elementEndIndex < 0)
        {
            throw new InvalidOperationException(
                $"Element '{elementName}' block could not be extracted.");
        }

        return source.Substring(
            elementStartIndex,
            elementEndIndex - elementStartIndex + 2);
    }
}
