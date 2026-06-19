using Aether.Application.Scheduling.SubmissionForms;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class AvailabilityMatrixImportedRowsOptimizationRunnerTests
{
    private const int Seed = 20260612;

    [Fact]
    public void Run_ShouldImportRows_AndOptimizeImportedWorkerSubmissions()
    {
        var resources = CreateResources();
        var shifts = CreateShifts();
        var rows = CreateImportRows();

        var runner = new AvailabilityMatrixImportedRowsOptimizationRunner();

        var result = runner.Run(new AvailabilityMatrixImportedRowsOptimizationRequest(
            rows,
            CreateSchedulePeriod(),
            resources,
            shifts,
            TotalEffectiveTargetHours: 16,
            MaximumAssignedHoursDeviationFromAverageHours: 8,
            Seed: Seed));

        Assert.Empty(result.ImportFatalErrors);
        Assert.Empty(result.ImportWarnings);

        Assert.Equal(2, result.ImportedWorkerSubmissions.Count);

        Assert.Contains(
            result.ImportedWorkerSubmissions,
            submission =>
                submission.ResourceId == resources[0].Id &&
                submission.ShiftSubmissions.Single().Date == new DateOnly(2026, 6, 14) &&
                submission.ShiftSubmissions.Single().ShiftKind == ShiftKind.Morning);

        Assert.Contains(
            result.ImportedWorkerSubmissions,
            submission =>
                submission.ResourceId == resources[1].Id &&
                submission.ShiftSubmissions.Single().Date == new DateOnly(2026, 6, 15) &&
                submission.ShiftSubmissions.Single().ShiftKind == ShiftKind.Night);

        Assert.NotNull(result.OptimizationResult);

        var optimizationResult = result.OptimizationResult!;

        Assert.Empty(optimizationResult.Warnings);
        Assert.Equal(resources.Count, optimizationResult.Problem.Resources.Count);
        Assert.Equal(shifts.Count, optimizationResult.Problem.Shifts.Count);
        Assert.Equal(2, optimizationResult.Problem.ResourceWorkloadDemands.Count);
        Assert.Equal(8, optimizationResult.Problem.MaximumAssignedHoursDeviationFromAverageHours);

        Assert.NotNull(optimizationResult.GeneticResult.Candidate);
        Assert.NotNull(optimizationResult.GeneticResult.Evaluation);
        Assert.NotEmpty(optimizationResult.GeneticResult.GenerationDiagnostics);
    }

    [Fact]
    public void Run_ShouldReturnImportFatalErrors_AndSkipOptimization()
    {
        var runner = new AvailabilityMatrixImportedRowsOptimizationRunner();

        var result = runner.Run(new AvailabilityMatrixImportedRowsOptimizationRequest(
            Rows: [],
            SchedulePeriod: CreateSchedulePeriod(),
            Resources: CreateResources(),
            Shifts: CreateShifts(),
            TotalEffectiveTargetHours: 16));

        Assert.Empty(result.ImportedWorkerSubmissions);
        Assert.Empty(result.ImportWarnings);

        var fatalError = Assert.Single(result.ImportFatalErrors);

        Assert.Equal(AvailabilityMatrixImportFatalErrorType.EmptyTable, fatalError.Type);
        Assert.Null(result.OptimizationResult);
    }

    private static SchedulePeriod CreateSchedulePeriod()
    {
        return new SchedulePeriod(
            new DateTime(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 16, 0, 0, 0, DateTimeKind.Utc));
    }

    private static IReadOnlyList<Resource> CreateResources()
    {
        return
        [
            new Resource(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                "Worker16 אלדר",
                hourlyCost: 100m),
            new Resource(
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                "Worker14",
                hourlyCost: 100m)
        ];
    }

    private static IReadOnlyList<Shift> CreateShifts()
    {
        return
        [
            new Shift(
                Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                new DateTime(2026, 6, 14, 6, 30, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 14, 14, 30, 0, DateTimeKind.Utc),
                ShiftKind.Morning,
                minResourceCount: 1,
                maxResourceCount: 1,
                requiresPreferenceToAssign: true),
            new Shift(
                Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                new DateTime(2026, 6, 15, 22, 30, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 16, 6, 30, 0, DateTimeKind.Utc),
                ShiftKind.Night,
                minResourceCount: 1,
                maxResourceCount: 1,
                requiresPreferenceToAssign: true)
        ];
    }

    private static IReadOnlyList<IReadOnlyList<string>> CreateImportRows()
    {
        return
        [
            CreateHeaders(),
            ["Worker16 אלדר", "בוקר", string.Empty],
            ["Worker14", string.Empty, "ערב"]
        ];
    }

    private static IReadOnlyList<string> CreateHeaders()
    {
        return
        [
            "שם המאבטח",
            "ראשון - 14/06",
            "שני - 15/06"
        ];
    }
}
