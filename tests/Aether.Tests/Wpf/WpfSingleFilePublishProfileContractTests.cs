using System.Xml.Linq;

namespace Aether.Tests.Wpf;

public sealed class WpfSingleFilePublishProfileContractTests
{
    [Fact]
    public void WpfApp_ShouldHaveWinX64SingleFileSelfContainedPublishProfile()
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
            $"Expected WPF single-file publish profile to exist at: {profilePath}");

        var document = XDocument.Load(profilePath);
        var properties = document
            .Descendants()
            .Where(element => element.Parent?.Name.LocalName == "PropertyGroup")
            .ToDictionary(
                element => element.Name.LocalName,
                element => element.Value,
                StringComparer.Ordinal);

        Assert.Equal("Release", properties["Configuration"]);
        Assert.Equal("win-x64", properties["RuntimeIdentifier"]);
        Assert.Equal("true", properties["SelfContained"]);
        Assert.Equal("true", properties["PublishSingleFile"]);
        Assert.Equal("true", properties["PublishReadyToRun"]);
        Assert.Equal("true", properties["PublishReadyToRunShowWarnings"]);
        Assert.Equal("false", properties["PublishTrimmed"]);
        Assert.Equal("none", properties["DebugType"]);
        Assert.Equal("false", properties["DebugSymbols"]);
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
