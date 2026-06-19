using Aether.Application.Scheduling.Builders;
using Aether.Application.Scheduling.Contracts;
using Aether.Application.Scheduling.Optimization;
using Aether.Application.Scheduling.Reports;
using Aether.Application.Scheduling.Services;
using Aether.Domain.Optimization;
using Xunit.Abstractions;

namespace Aether.Tests.Application.Scheduling;

public sealed class SchedulingRunServiceReportScenarioTests
{
    private readonly ITestOutputHelper _output;

    public SchedulingRunServiceReportScenarioTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Run_ShouldPrintFullReport_FromRealSchedulingRunService()
    {
        var resources = new[]
        {
            CreateResource("Yossi"),
            CreateResource("Dana"),
            CreateResource("Noa"),
            CreateResource("Amir"),
            CreateResource("Lior")
        };

        var shifts = new[]
        {
            CreateShift(new DateOnly(2026, 6, 1), ShiftKind.Morning),
            CreateShift(new DateOnly(2026, 6, 1), ShiftKind.Afternoon),
            CreateShift(
                new DateOnly(2026, 6, 2),
                ShiftKind.Night,
                requiresPreferenceToAssign: true),
            CreateShift(new DateOnly(2026, 6, 3), ShiftKind.Morning),
            CreateShift(new DateOnly(2026, 6, 4), ShiftKind.Afternoon),
            CreateShift(new DateOnly(2026, 6, 8), ShiftKind.Morning)
        };

        var request = new SchedulingProblemBuildRequest(
            Period: CreatePeriod(),
            Resources: resources,
            Shifts: shifts,
            ResourceSubmissions:
            [
                new ResourceSubmissionDto(
                    "Yossi",
                    [
                        Select(new DateOnly(2026, 6, 1), ShiftKind.Morning),
                        Select(new DateOnly(2026, 6, 8), ShiftKind.Morning)
                    ]),
                new ResourceSubmissionDto(
                    "Dana",
                    [
                        Select(new DateOnly(2026, 6, 2), ShiftKind.Night)
                    ]),
                new ResourceSubmissionDto(
                    "Noa",
                    [
                        Select(new DateOnly(2026, 6, 1), ShiftKind.Afternoon)
                    ]),
                new ResourceSubmissionDto(
                    "Amir",
                    [
                        Select(new DateOnly(2026, 6, 3), ShiftKind.Morning)
                    ]),
                new ResourceSubmissionDto(
                    "Lior",
                    [
                        Select(new DateOnly(2026, 6, 4), ShiftKind.Afternoon)
                    ])
            ],
            MinimumAssignedHoursPerResource: 8);

        var service = CreateService();

        var runResult = service.Run(request);

        var formatter = new SchedulingRunReportFormatter();
        var report = formatter.Format(runResult);

        _output.WriteLine(report);
        System.Console.WriteLine(report);

        Assert.Empty(runResult.Warnings);
        Assert.Equal(resources.Length, runResult.Problem.Resources.Count);
        Assert.Equal(shifts.Length, runResult.Problem.Shifts.Count);
        Assert.Equal(8, runResult.Problem.MinimumAssignedHoursPerResource);

        Assert.NotNull(runResult.DeterministicResult.Candidate);
        Assert.NotNull(runResult.DeterministicResult.Evaluation);
        Assert.NotNull(runResult.GeneticResult.Candidate);
        Assert.NotNull(runResult.GeneticResult.Evaluation);
        Assert.NotNull(runResult.Comparison);

        Assert.True(runResult.DeterministicResult.Evaluation.IsFeasible);
        Assert.Equal(1000, runResult.DeterministicResult.Evaluation.Score.Value);

        Assert.Equal(16, runResult.GeneticResult.GenerationDiagnostics.Count);

        Assert.Contains("Scheduling Run Report", report);
        Assert.Contains("Input Summary", report);
        Assert.Contains("Resources: 5", report);
        Assert.Contains("Shifts: 6", report);
        Assert.Contains("MinimumAssignedHoursPerResource: 8", report);
        Assert.Contains("Comparison", report);
        Assert.Contains("BestResult:", report);
        Assert.Contains("Best Result", report);
        Assert.Contains("LoadByResource", report);
        Assert.Contains("ViolationsByType", report);
        Assert.Contains("AssignmentsByShift", report);
        Assert.Contains("GenerationDiagnostics", report);
    }

    private static SchedulingRunService CreateService()
    {
        return new SchedulingRunService(
            new SchedulingProblemBuilder(),
            new DeterministicScheduleOptimizer(),
            diagnosticsSink => new GeneticScheduleOptimizer(
                populationSize: 40,
                seed: 20260601,
                generationCount: 15,
                eliteCount: 1,
                tournamentSize: 3,
                diagnosticsSink: diagnosticsSink));
    }

    private static SchedulePeriod CreatePeriod()
    {
        return new SchedulePeriod(
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));
    }

    private static Resource CreateResource(string name)
    {
        return new Resource(
            Guid.NewGuid(),
            name,
            hourlyCost: 100m);
    }

    private static Shift CreateShift(
        DateOnly date,
        ShiftKind kind,
        bool requiresPreferenceToAssign = false)
    {
        var startUtc = kind switch
        {
            ShiftKind.Morning => date.ToDateTime(
                new TimeOnly(6, 30),
                DateTimeKind.Utc),
            ShiftKind.Afternoon => date.ToDateTime(
                new TimeOnly(14, 30),
                DateTimeKind.Utc),
            ShiftKind.Night => date.ToDateTime(
                new TimeOnly(22, 30),
                DateTimeKind.Utc),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        var endUtc = kind switch
        {
            ShiftKind.Morning => date.ToDateTime(
                new TimeOnly(14, 30),
                DateTimeKind.Utc),
            ShiftKind.Afternoon => date.ToDateTime(
                new TimeOnly(22, 30),
                DateTimeKind.Utc),
            ShiftKind.Night => date.AddDays(1).ToDateTime(
                new TimeOnly(6, 30),
                DateTimeKind.Utc),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        return new Shift(
            Guid.NewGuid(),
            startUtc,
            endUtc,
            kind,
            minResourceCount: 1,
            maxResourceCount: 1,
            requiresPreferenceToAssign: requiresPreferenceToAssign);
    }

    private static ShiftSelectionDto Select(
        DateOnly date,
        ShiftKind kind)
    {
        return new ShiftSelectionDto(
            date,
            kind,
            IsSelected: true);
    }
}
