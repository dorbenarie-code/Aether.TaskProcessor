using System;
using System.IO;
using Xunit;

namespace Aether.Tests.Wpf;

public sealed class MainWindowGenerationProgressContractTests
{
    [Fact]
    public void MainWindowXaml_ShouldContainStyledIndeterminateGenerationProgressBar()
    {
        var xaml = ReadRepositoryTextFile("src/Aether.WpfApp/MainWindow.xaml");

        Assert.Contains("x:Name=\"GenerationProgressBar\"", xaml);
        Assert.Contains("IsIndeterminate=\"True\"", xaml);
        Assert.Contains("Visibility=\"Collapsed\"", xaml);
        Assert.Contains("Height=\"10\"", xaml);
        Assert.Contains("Foreground=\"#2563EB\"", xaml);
        Assert.Contains("Background=\"#E5E7EB\"", xaml);
        Assert.Contains("ToolTip=\"המערכת מחשבת את הסידור ברקע\"", xaml);
    }

    [Fact]
    public void MainWindowCodeBehind_ShouldShowManagerFacingRunningMessageBeforeGeneration()
    {
        var code = ReadRepositoryTextFile("src/Aether.WpfApp/MainWindow.xaml.cs");

        Assert.Contains(
            "מחשב את הסידור האופטימלי... נא לא לסגור את החלון.",
            code);

        Assert.DoesNotContain(
            "StatusTextBlock.Text = \"׳׳—׳©׳‘...\";",
            code);
    }

    [Fact]
    public void MainWindowCodeBehind_ShouldToggleGenerationProgressBarAroundBackgroundGeneration()
    {
        var code = ReadRepositoryTextFile("src/Aether.WpfApp/MainWindow.xaml.cs");

        Assert.Contains(
            "GenerationProgressBar.Visibility = Visibility.Visible;",
            code);

        Assert.Contains(
            "GenerationProgressBar.Visibility = Visibility.Collapsed;",
            code);

        var showProgressIndex = code.IndexOf(
            "GenerationProgressBar.Visibility = Visibility.Visible;",
            StringComparison.Ordinal);

        var taskRunIndex = code.IndexOf(
            "await Task.Run",
            StringComparison.Ordinal);

        var finallyIndex = code.IndexOf(
            "finally",
            StringComparison.Ordinal);

        var hideProgressIndex = code.IndexOf(
            "GenerationProgressBar.Visibility = Visibility.Collapsed;",
            StringComparison.Ordinal);

        Assert.True(
            showProgressIndex >= 0 &&
            taskRunIndex >= 0 &&
            showProgressIndex < taskRunIndex,
            "The progress bar should become visible before the background generation starts.");

        Assert.True(
            finallyIndex >= 0 &&
            hideProgressIndex >= 0 &&
            finallyIndex < hideProgressIndex,
            "The progress bar should be collapsed inside the finally block.");
    }

    [Fact]
    public void MainWindowCodeBehind_ShouldClearGeneratedOutputState_WhenNewGenerationStarts()
    {
        var code = ReadRepositoryTextFile("src/Aether.WpfApp/MainWindow.xaml.cs");

        Assert.Contains("ClearGeneratedOutputState();", code);

        var disableControlsIndex = code.IndexOf(
            "SetRunControlsEnabled(false);",
            StringComparison.Ordinal);

        var clearOutputIndex = code.IndexOf(
            "ClearGeneratedOutputState();",
            StringComparison.Ordinal);

        var taskRunIndex = code.IndexOf(
            "await Task.Run",
            StringComparison.Ordinal);

        Assert.True(
            disableControlsIndex >= 0 &&
            clearOutputIndex >= 0 &&
            taskRunIndex >= 0 &&
            disableControlsIndex < clearOutputIndex &&
            clearOutputIndex < taskRunIndex,
            "Generated output actions should be cleared before starting a new background generation.");
    }

    private static string ReadRepositoryTextFile(string relativePath)
    {
        var repositoryRoot = FindRepositoryRoot();

        return File.ReadAllText(
            Path.Combine(repositoryRoot, relativePath));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not find repository root from test output directory.");
    }
}
