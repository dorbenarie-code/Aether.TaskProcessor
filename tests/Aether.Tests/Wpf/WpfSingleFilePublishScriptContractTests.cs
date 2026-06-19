namespace Aether.Tests.Wpf;

public sealed class WpfSingleFilePublishScriptContractTests
{
    [Fact]
    public void Repository_ShouldHaveWindowsWpfSingleFilePublishScript()
    {
        var repoRoot = FindRepositoryRoot();
        var scriptPath = Path.Combine(
            repoRoot.FullName,
            "scripts",
            "windows",
            "publish-wpf-single-file.ps1");

        Assert.True(
            File.Exists(scriptPath),
            $"Expected Windows WPF single-file publish script to exist at: {scriptPath}");

        var script = File.ReadAllText(scriptPath);

        Assert.Contains("dotnet publish", script, StringComparison.Ordinal);
        Assert.Contains("src", script, StringComparison.Ordinal);
        Assert.Contains("Aether.WpfApp", script, StringComparison.Ordinal);
        Assert.Contains("Aether.WpfApp.csproj", script, StringComparison.Ordinal);
        Assert.Contains("PublishProfile=win-x64-single-file", script, StringComparison.Ordinal);

        Assert.DoesNotContain("Aether.Api", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Aether.Console", script, StringComparison.Ordinal);
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
