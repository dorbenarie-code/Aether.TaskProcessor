using System.Text.RegularExpressions;

namespace Aether.Tests.Wpf;

public sealed class WpfSingleFilePathSafetyContractTests
{
    [Fact]
    public void ProductionCode_ShouldNotUseAssemblyOrCurrentDirectoryApis_ThatAreUnsafeForSingleFilePublish()
    {
        var repoRoot = FindRepositoryRoot();
        var srcDirectory = Path.Combine(repoRoot.FullName, "src");

        var forbiddenPatterns = new[]
        {
            @"Assembly\.Location",
            @"GetExecutingAssembly\s*\(",
            @"GetEntryAssembly\s*\(",
            @"CodeBase",
            @"Assembly\.GetFile\s*\(",
            @"Directory\.GetCurrentDirectory\s*\(",
            @"Environment\.CurrentDirectory"
        };

        var violations = Directory
            .EnumerateFiles(srcDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path =>
                !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(path => FindViolations(path, forbiddenPatterns))
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "Production code contains path APIs that are risky for single-file publish:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, violations));
    }

    private static IEnumerable<string> FindViolations(
        string path,
        IReadOnlyList<string> forbiddenPatterns)
    {
        var lines = File.ReadAllLines(path);

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];

            foreach (var pattern in forbiddenPatterns)
            {
                if (Regex.IsMatch(line, pattern, RegexOptions.CultureInvariant))
                {
                    yield return $"{path}:{index + 1}: {line.Trim()}";
                }
            }
        }
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
