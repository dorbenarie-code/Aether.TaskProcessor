namespace Aether.Tests.Desktop;

public sealed class AetherWpfAppProjectArchitectureTests
{
    [Fact]
    public void Solution_ShouldContainMinimalWpfAppProject_WithExpectedReferences()
    {
        var repositoryRoot = FindRepositoryRoot();

        var solutionPath = Path.Combine(
            repositoryRoot,
            "Aether.TaskProcessor.sln");

        var projectPath = Path.Combine(
            repositoryRoot,
            "src",
            "Aether.WpfApp",
            "Aether.WpfApp.csproj");

        Assert.True(
            File.Exists(projectPath),
            $"Expected WPF project file to exist: {projectPath}");

        var solutionText = File.ReadAllText(solutionPath);
        var projectText = File.ReadAllText(projectPath);

        Assert.Contains(
            "src\\Aether.WpfApp\\Aether.WpfApp.csproj",
            solutionText.Replace("/", "\\"));

        Assert.Contains(
            "<UseWPF>true</UseWPF>",
            projectText);

        Assert.Contains(
            "<TargetFramework>net8.0-windows</TargetFramework>",
            projectText);

        Assert.Contains(
            "Aether.Application.csproj",
            projectText);

        Assert.Contains(
            "Aether.Infrastructure.csproj",
            projectText);

        Assert.DoesNotContain(
            "Aether.Console.csproj",
            projectText);

        Assert.DoesNotContain(
            "Aether.Api.csproj",
            projectText);
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
