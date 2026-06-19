using System.Xml.Linq;

namespace Aether.Tests.Wpf;

public sealed class WpfSingleFilePublishOutputContractTests
{
    [Fact]
    public void PublishProfile_ShouldBundleNativeLibraries_ForTrueSingleFileOutput()
    {
        var repoRoot = FindRepositoryRoot();
        var profilePath = Path.Combine(
            repoRoot.FullName,
            "src",
            "Aether.WpfApp",
            "Properties",
            "PublishProfiles",
            "win-x64-single-file.pubxml");

        Assert.True(
            File.Exists(profilePath),
            $"Expected publish profile to exist at: {profilePath}");

        var document = XDocument.Load(profilePath);
        var properties = document
            .Descendants()
            .Where(element => element.Parent?.Name.LocalName == "PropertyGroup")
            .ToDictionary(
                element => element.Name.LocalName,
                element => element.Value,
                StringComparer.Ordinal);

        Assert.Equal("true", properties["IncludeNativeLibrariesForSelfExtract"]);
        Assert.Equal("false", properties["CopyOutputSymbolsToPublishDirectory"]);
    }

    [Fact]
    public void WindowsPublishScript_ShouldFail_WhenPublishOutputIsNotExactlyOneExe()
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

        Assert.Contains("Expected exactly one EXE", script, StringComparison.Ordinal);
        Assert.Contains("Unexpected publish output files", script, StringComparison.Ordinal);
        Assert.Contains("$unexpectedFiles", script, StringComparison.Ordinal);
        Assert.Contains("$_.Extension -in", script, StringComparison.Ordinal);
        Assert.Contains("\".pdb\"", script, StringComparison.Ordinal);
        Assert.Contains("\".dll\"", script, StringComparison.Ordinal);
        Assert.Contains("\".json\"", script, StringComparison.Ordinal);
        Assert.Contains("\".deps\"", script, StringComparison.Ordinal);
        Assert.Contains("\".runtimeconfig\"", script, StringComparison.Ordinal);
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
