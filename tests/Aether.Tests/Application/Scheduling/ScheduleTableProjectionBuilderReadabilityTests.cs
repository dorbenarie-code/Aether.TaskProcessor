using Aether.Application.Scheduling.Contracts;
using Aether.Application.Scheduling.Reports;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class ScheduleTableProjectionBuilderReadabilityTests
{
    [Fact]
    public void Build_ShouldExposeShiftTimeRanges_FromShiftModel()
    {
        var resource = new Resource(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "Dana",
            hourlyCost: 100m);

        var date = new DateOnly(2026, 6, 1);

        var morningShift = CreateShift(
            "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
            date,
            ShiftKind.Morning,
            new TimeOnly(6, 35),
            date,
            new TimeOnly(14, 25));

        var afternoonShift = CreateShift(
            "cccccccc-cccc-cccc-cccc-cccccccccccc",
            date,
            ShiftKind.Afternoon,
            new TimeOnly(14, 25),
            date,
            new TimeOnly(22, 45));

        var nightShift = CreateShift(
            "dddddddd-dddd-dddd-dddd-dddddddddddd",
            date,
            ShiftKind.Night,
            new TimeOnly(22, 45),
            date.AddDays(1),
            new TimeOnly(6, 35));

        var shifts = new[]
        {
            morningShift,
            afternoonShift,
            nightShift
        };

        var problem = new SchedulingProblem(
            period: new SchedulePeriod(
                date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                date.AddDays(2).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)),
            resources: [resource],
            shifts: shifts,
            availabilityWindows:
            [
                new AvailabilityWindow(resource.Id, morningShift.StartUtc, morningShift.EndUtc),
                new AvailabilityWindow(resource.Id, afternoonShift.StartUtc, afternoonShift.EndUtc),
                new AvailabilityWindow(resource.Id, nightShift.StartUtc, nightShift.EndUtc)
            ],
            resourcePreferences: []);

        var result = CreateOptimizationResult(
            problem,
            new ScheduleCandidate([]));

        var projection = new ScheduleTableProjectionBuilder()
            .Build(problem, result);

        Assert.Equal("06:35-14:25", projection.MorningTimeRangeText);
        Assert.Equal("14:25-22:45", projection.AfternoonTimeRangeText);
        Assert.Equal("22:45-06:35", projection.NightTimeRangeText);
    }

    private static SchedulingRunOptimizationResult CreateOptimizationResult(
        SchedulingProblem problem,
        ScheduleCandidate candidate)
    {
        var evaluation = new ScheduleEvaluator()
            .Evaluate(problem, candidate);

        var loadByResource = problem.Resources
            .Select(resource => new ResourceLoadSummary(
                resource.Id,
                resource.Name,
                AssignedHours: 0,
                AssignmentCount: 0))
            .ToArray();

        var violationsByType = evaluation.Violations
            .GroupBy(violation => violation.Type)
            .ToDictionary(
                group => group.Key,
                group => group.Count());

        return new SchedulingRunOptimizationResult(
            candidate,
            evaluation,
            loadByResource,
            violationsByType,
            GenerationDiagnostics: []);
    }

    private static Shift CreateShift(
        string id,
        DateOnly startDate,
        ShiftKind kind,
        TimeOnly startTime,
        DateOnly endDate,
        TimeOnly endTime)
    {
        return new Shift(
            Guid.Parse(id),
            startDate.ToDateTime(startTime, DateTimeKind.Utc),
            endDate.ToDateTime(endTime, DateTimeKind.Utc),
            kind,
            minResourceCount: 0,
            maxResourceCount: 3);
    }
}
