namespace Aether.Tests.Wpf;

public sealed class MainWindowVisualReadabilityContractTests
{
    [Fact]
    public void MainWindow_ShouldDefineSharedVisualStyles_ForManagerShell()
    {
        var xaml = ReadMainWindowXaml();

        Assert.Contains("<Window.Resources>", xaml);

        Assert.Contains("x:Key=\"PrimaryButtonStyle\"", xaml);
        Assert.Contains("x:Key=\"SecondaryButtonStyle\"", xaml);
        Assert.Contains("x:Key=\"OutputActionButtonStyle\"", xaml);
        Assert.Contains("x:Key=\"SectionTitleTextBlockStyle\"", xaml);
        Assert.Contains("x:Key=\"CardBorderStyle\"", xaml);
        Assert.Contains("x:Key=\"ManagerConstraintsDataGridStyle\"", xaml);
    }

    [Fact]
    public void MainWindow_ShouldApplyVisualStyles_ToPrimaryAndSecondaryActions()
    {
        var xaml = ReadMainWindowXaml();

        Assert.Contains("x:Name=\"GenerateScheduleButton\"", xaml);
        Assert.Contains("Style=\"{StaticResource PrimaryButtonStyle}\"", xaml);

        Assert.Contains("x:Name=\"SelectInputWorkbookButton\"", xaml);
        Assert.Contains("x:Name=\"SelectOutputDirectoryButton\"", xaml);
        Assert.Contains("Style=\"{StaticResource SecondaryButtonStyle}\"", xaml);

        Assert.Contains("x:Name=\"OpenScheduleTableXlsxButton\"", xaml);
        Assert.Contains("x:Name=\"OpenOutputDirectoryButton\"", xaml);
        Assert.Contains("Style=\"{StaticResource OutputActionButtonStyle}\"", xaml);
    }

    [Fact]
    public void MainWindow_ShouldStyleManagerConstraintsGrid_ForReadability()
    {
        var xaml = ReadMainWindowXaml();

        Assert.Contains("x:Name=\"ManagerConstraintsGrid\"", xaml);
        Assert.Contains("Style=\"{StaticResource ManagerConstraintsDataGridStyle}\"", xaml);

        Assert.Contains("GridLinesVisibility\" Value=\"Horizontal\"", xaml);
        Assert.Contains("AlternatingRowBackground", xaml);
        Assert.Contains("DataGridColumnHeader", xaml);

        Assert.DoesNotContain("GridLinesVisibility=\"All\"", xaml);
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
