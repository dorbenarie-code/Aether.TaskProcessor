namespace Aether.Tests.Wpf;

public sealed class MainWindowManagerConstraintPickerOptionsContractTests
{
    [Fact]
    public void MainWindow_ShouldExposeManagerConstraintPickerOptionCollections()
    {
        var repositoryRoot = GetRepositoryRoot();

        var codeBehind = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Aether.WpfApp",
            "MainWindow.xaml.cs"));

        Assert.Contains(
            "ObservableCollection<string>",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "WorkerNameOptions",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "ObservableCollection<DateTime>",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "ScheduleDateOptions",
            codeBehind,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_ShouldUseComboBoxColumns_ForManagerConstraintWorkerAndDate()
    {
        var repositoryRoot = GetRepositoryRoot();

        var xaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Aether.WpfApp",
            "MainWindow.xaml"));

        var gridBlock = ExtractElementBlock(
            xaml,
            "ManagerConstraintsGrid",
            "</DataGrid>");

        Assert.DoesNotContain(
            "<DataGridTextColumn Header=\"עובד\"",
            gridBlock,
            StringComparison.Ordinal);

        Assert.Contains(
            "WorkerNameOptions",
            gridBlock,
            StringComparison.Ordinal);

        Assert.Contains(
            "SelectedItem=\"{Binding WorkerName",
            gridBlock,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "<DatePicker",
            gridBlock,
            StringComparison.Ordinal);

        Assert.Contains(
            "ScheduleDateOptions",
            gridBlock,
            StringComparison.Ordinal);

        Assert.Contains(
            "SelectedItem=\"{Binding Date",
            gridBlock,
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
        string elementName,
        string closingElement)
    {
        var nameToken = $"x:Name=\"{elementName}\"";
        var nameIndex = source.IndexOf(nameToken, StringComparison.Ordinal);

        if (nameIndex < 0)
        {
            throw new InvalidOperationException(
                $"Element '{elementName}' was not found.");
        }

        var elementStartIndex = source.LastIndexOf(
            '<',
            nameIndex);

        var elementEndIndex = source.IndexOf(
            closingElement,
            nameIndex,
            StringComparison.Ordinal);

        if (elementStartIndex < 0 || elementEndIndex < 0)
        {
            throw new InvalidOperationException(
                $"Element '{elementName}' block could not be extracted.");
        }

        return source.Substring(
            elementStartIndex,
            elementEndIndex - elementStartIndex + closingElement.Length);
    }
}
