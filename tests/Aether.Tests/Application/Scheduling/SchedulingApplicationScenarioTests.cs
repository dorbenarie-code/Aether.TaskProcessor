using Aether.Application.Scheduling.Builders;
using Aether.Application.Scheduling.Contracts;
using Aether.Application.Scheduling.Optimization;
using Aether.Application.Scheduling.Services;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class SchedulingApplicationScenarioTests
{
    [Fact]
    public void Run_ShouldCreateFeasibleSchedule_FromBiweeklyApplicationScenario()
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
            ]);

        var service = CreateService();

        var result = service.Run(request);

        Assert.Empty(result.Warnings);
        Assert.NotNull(result.Problem);
        Assert.NotNull(result.DeterministicResult.Candidate);
        Assert.NotNull(result.DeterministicResult.Evaluation);
        Assert.NotNull(result.GeneticResult.Candidate);
        Assert.NotNull(result.GeneticResult.Evaluation);
        Assert.NotNull(result.Comparison);

        Assert.Equal(shifts.Length, result.DeterministicResult.Candidate.Assignments.Count);
        Assert.True(result.DeterministicResult.Evaluation.IsFeasible);
        Assert.Empty(result.DeterministicResult.Evaluation.Violations);
        Assert.Equal(1000, result.DeterministicResult.Evaluation.Score.Value);
        Assert.NotEmpty(result.GeneticResult.GenerationDiagnostics);

        var resourceIds = resources.Select(resource => resource.Id).ToHashSet();
        var shiftIds = shifts.Select(shift => shift.Id).ToHashSet();

        Assert.All(result.DeterministicResult.Candidate.Assignments, assignment =>
        {
            Assert.Contains(assignment.ResourceId, resourceIds);
            Assert.Contains(assignment.ShiftId, shiftIds);
        });
    }

    [Fact]
    public void Run_ShouldPreserveWarningsAndStillReturnCandidate_WhenScenarioContainsManagerReviewIssues()
    {
        var dana = CreateResource("Dana");
        var yossi = CreateResource("Yossi");

        var resources = new[]
        {
            dana,
            yossi
        };

        var shift = CreateShift(new DateOnly(2026, 6, 7), ShiftKind.Morning);

        var request = new SchedulingProblemBuildRequest(
            Period: CreatePeriod(),
            Resources: resources,
            Shifts: [shift],
            ResourceSubmissions:
            [
                new ResourceSubmissionDto(
                    "Dana",
                    [
                        Select(new DateOnly(2026, 6, 7), ShiftKind.Morning)
                    ],
                    RawSpecialRequestNote: "Exam this week"),
                new ResourceSubmissionDto(
                    "Unknown",
                    [
                        Select(new DateOnly(2026, 6, 7), ShiftKind.Morning)
                    ]),
                new ResourceSubmissionDto(
                    "Yossi",
                    [
                        Select(new DateOnly(2026, 6, 8), ShiftKind.Morning)
                    ])
            ]);

        var service = CreateService();

        var result = service.Run(request);

        Assert.NotNull(result.Problem);
        Assert.NotNull(result.DeterministicResult.Candidate);
        Assert.NotNull(result.DeterministicResult.Evaluation);
        Assert.NotNull(result.GeneticResult.Candidate);
        Assert.NotNull(result.GeneticResult.Evaluation);

        Assert.Equal(3, result.Warnings.Count);

        Assert.Contains(
            result.Warnings,
            warning => warning.Type == SchedulingProblemBuildWarningType.RawSpecialRequestNote);

        Assert.Contains(
            result.Warnings,
            warning => warning.Type == SchedulingProblemBuildWarningType.UnknownResourceName);

        Assert.Contains(
            result.Warnings,
            warning => warning.Type == SchedulingProblemBuildWarningType.NoMatchingShift);

        var assignment = Assert.Single(result.DeterministicResult.Candidate.Assignments);
        Assert.Equal(dana.Id, assignment.ResourceId);
        Assert.Equal(shift.Id, assignment.ShiftId);

        Assert.True(result.DeterministicResult.Evaluation.IsFeasible);
        Assert.Empty(result.DeterministicResult.Evaluation.Violations);
    }

    private static SchedulingRunService CreateService()
    {
        return new SchedulingRunService(
            new SchedulingProblemBuilder(),
            new DeterministicScheduleOptimizer(),
            diagnosticsSink => new GeneticScheduleOptimizer(
                populationSize: 30,
                seed: 20260601,
                generationCount: 10,
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
