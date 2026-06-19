namespace Aether.Tests.Wpf;

public sealed class MainWindowOpenOutputFolderContractTests
{
    [Fact]
    public void MainWindow_ShouldOpenGeneratedOutputDirectory_NotOnlyCurrentlySelectedDirectory()
    {
        var repositoryRoot = GetRepositoryRoot();

        var codeBehind = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Aether.WpfApp",
            "MainWindow.xaml.cs"));

        Assert.Contains(
            "private string? generatedOutputDirectoryPath;",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "generatedOutputDirectoryPath = Path.GetDirectoryName(generatedScheduleTableXlsxPath);",
            codeBehind,
            StringComparison.Ordinal);

        var openOutputHandler = ExtractMethodBlock(
            codeBehind,
            "OpenOutputDirectoryButton_Click");

        Assert.Contains(
            "generatedOutputDirectoryPath",
            openOutputHandler,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "selectedOutputDirectoryPath",
            openOutputHandler,
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

    private static string ExtractMethodBlock(
        string source,
        string methodName)
    {
        var methodIndex = source.IndexOf(methodName, StringComparison.Ordinal);

        if (methodIndex < 0)
        {
            throw new InvalidOperationException(
                $"Method '{methodName}' was not found.");
        }

        var methodStartIndex = source.LastIndexOf(
            "private",
            methodIndex,
            StringComparison.Ordinal);

        var nextMethodIndex = source.IndexOf(
            "\n    private",
            methodIndex,
            StringComparison.Ordinal);

        if (methodStartIndex < 0 || nextMethodIndex < 0)
        {
            throw new InvalidOperationException(
                $"Method '{methodName}' block could not be extracted.");
        }

        return source.Substring(
            methodStartIndex,
            nextMethodIndex - methodStartIndex);
    }
}
