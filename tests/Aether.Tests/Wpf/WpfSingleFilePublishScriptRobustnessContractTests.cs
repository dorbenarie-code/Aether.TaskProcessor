namespace Aether.Tests.Wpf;

public sealed class WpfSingleFilePublishScriptRobustnessContractTests
{
    [Fact]
    public void WindowsPublishScript_ShouldUseProviderPath_AndFailWhenDotnetPublishFails()
    {
        var repoRoot = FindRepositoryRoot();
        var scriptPath = Path.Combine(
            repoRoot.FullName,
            "scripts",
            "windows",
            "publish-wpf-single-file.ps1");

        Assert.True(
            File.Exists(scriptPath),
            $"Expected publish script to exist at: {scriptPath}");

        var script = File.ReadAllText(scriptPath);

        Assert.Contains(".ProviderPath", script, StringComparison.Ordinal);
        Assert.Contains("$LASTEXITCODE", script, StringComparison.Ordinal);
        Assert.Contains("dotnet publish failed", script, StringComparison.Ordinal);
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var srcPath = Path.Combine(directory.FullName, "src");
            var testsPath = Path.Combine(directory.FullName, "tests");

            if (Directory.Exists(srcPath) && Directory.Exists(testsPath))
            {
                return directory;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be found.");
    }
}
