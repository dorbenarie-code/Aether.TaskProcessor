namespace Aether.Tests.Wpf;

public sealed class MainWindowWorkbookPreviewContractTests
{
    [Fact]
    public void MainWindow_ShouldOwnLocalScheduleInputPreviewer()
    {
        var repositoryRoot = GetRepositoryRoot();

        var codeBehind = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Aether.WpfApp",
            "MainWindow.xaml.cs"));

        Assert.Contains(
            "private readonly LocalCleanXlsxScheduleInputPreviewer scheduleInputPreviewer;",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "new LocalCleanXlsxScheduleInputPreviewer()",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "LocalCleanXlsxScheduleInputPreviewer scheduleInputPreviewer",
            codeBehind,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "XlsxFormTableReader",
            codeBehind,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "AvailabilityMatrixWorkerSubmissionImporter",
            codeBehind,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SelectInputWorkbookButtonClick_ShouldLoadManagerConstraintPickerOptions()
    {
        var repositoryRoot = GetRepositoryRoot();

        var codeBehind = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Aether.WpfApp",
            "MainWindow.xaml.cs"));

        var selectInputHandler = ExtractMethodBlock(
            codeBehind,
            "SelectInputWorkbookButton_Click");

        Assert.Contains(
            "scheduleInputPreviewer.Load(",
            selectInputHandler,
            StringComparison.Ordinal);

        Assert.Contains(
            "new LocalCleanXlsxScheduleInputPreviewRequest(selectedInputWorkbookPath)",
            selectInputHandler,
            StringComparison.Ordinal);

        Assert.Contains(
            "RefreshManagerConstraintPickerOptions(",
            selectInputHandler,
            StringComparison.Ordinal);

        Assert.Contains(
            "ClearManagerConstraintPickerOptions();",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "private void RefreshManagerConstraintPickerOptions(",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "WorkerNameOptions.Clear();",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "ScheduleDateOptions.Clear();",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "WorkerNameOptions.Add(workerName);",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "ScheduleDateOptions.Add(scheduleDate.ToDateTime(TimeOnly.MinValue));",
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
