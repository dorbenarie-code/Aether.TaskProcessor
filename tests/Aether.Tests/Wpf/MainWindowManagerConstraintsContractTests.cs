namespace Aether.Tests.Wpf;

public sealed class MainWindowManagerConstraintsContractTests
{
    [Fact]
    public void MainWindow_ShouldExposeManagerConstraintsDataGrid()
    {
        var repositoryRoot = GetRepositoryRoot();

        var xaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Aether.WpfApp",
            "MainWindow.xaml"));

        Assert.Contains(
            "<DataGrid",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "x:Name=\"ManagerConstraintsGrid\"",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "ItemsSource=\"{Binding ManagerConstraintDraftRows}\"",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "AutoGenerateColumns=\"False\"",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "CanUserAddRows=\"True\"",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "סוג אילוץ",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "עובד",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "תאריך",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "משמרת",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "מינימום",
            xaml,
            StringComparison.Ordinal);

        Assert.Contains(
            "מקסימום",
            xaml,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_ShouldOwnTypedManagerConstraintDraftRows()
    {
        var repositoryRoot = GetRepositoryRoot();

        var codeBehind = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Aether.WpfApp",
            "MainWindow.xaml.cs"));

        Assert.Contains(
            "using System.Collections.ObjectModel;",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "using Aether.Application.Scheduling.ManagerConstraints;",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "using Aether.Domain.Optimization;",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "ObservableCollection<ManagerConstraintDraftRow>",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "ManagerConstraintDraftRows",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "ManagerConstraintDraftType",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "ShiftKind",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "DataContext = this;",
            codeBehind,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateScheduleButtonClick_ShouldBuildManualManagerConstraintRows_AndPassThemToRunner()
    {
        var repositoryRoot = GetRepositoryRoot();

        var codeBehind = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Aether.WpfApp",
            "MainWindow.xaml.cs"));

        var generateHandler = ExtractMethodBlock(
            codeBehind,
            "GenerateScheduleButton_Click");

        Assert.Contains(
            "new ManagerConstraintRowsBuilder()",
            generateHandler,
            StringComparison.Ordinal);

        Assert.Contains(
            ".Build(ManagerConstraintDraftRows)",
            generateHandler,
            StringComparison.Ordinal);

        Assert.Contains(
            "ManualManagerConstraintRows:",
            generateHandler,
            StringComparison.Ordinal);

        Assert.Contains(
            "manualManagerConstraintRows",
            generateHandler,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "ManagerConstraintRowsImporter",
            generateHandler,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "CleanXlsxScheduleGenerationUseCase",
            generateHandler,
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
