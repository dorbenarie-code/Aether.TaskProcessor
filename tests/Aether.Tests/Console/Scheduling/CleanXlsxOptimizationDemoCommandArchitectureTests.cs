namespace Aether.Tests.Console.Scheduling;

public sealed class CleanXlsxOptimizationDemoCommandArchitectureTests
{
    [Fact]
    public void Source_ShouldUseApplicationLastDorProfile_InsteadOfConsoleDemoFactory()
    {
        var repositoryRoot = FindRepositoryRoot();

        var commandPath = Path.Combine(
            repositoryRoot,
            "src",
            "Aether.Console",
            "Scheduling",
            "CleanXlsxOptimizationDemoCommand.cs");

        var source = File.ReadAllText(commandPath);

        Assert.Contains(
            "Aether.Application.Scheduling.Profiles",
            source);

        Assert.Contains(
            "LastDorLocalScheduleGenerationProfile",
            source);

        Assert.DoesNotContain(
            "LastDorDemoSchedulingFactory",
            source);

        Assert.DoesNotContain(
            "TotalEffectiveTargetHours = 736.0",
            source);

        Assert.DoesNotContain(
            "BalanceToleranceHours = 5.0",
            source);

        Assert.DoesNotContain(
            "Seed = 20260603",
            source);
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
