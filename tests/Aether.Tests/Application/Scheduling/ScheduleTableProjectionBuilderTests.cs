using Aether.Application.Scheduling.Contracts;
using Aether.Application.Scheduling.Reports;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class ScheduleTableProjectionBuilderTests
{
    [Fact]
    public void Build_ShouldProjectAssignmentsByDateAndShiftKind()
    {
        var scenario = CreateScenario();

        var candidate = new ScheduleCandidate(
        [
            new Assignment(scenario.Yossi.Id, scenario.FirstMorningShift.Id),
            new Assignment(scenario.Dana.Id, scenario.FirstMorningShift.Id),
            new Assignment(scenario.Noa.Id, scenario.FirstAfternoonShift.Id),
            new Assignment(scenario.Dana.Id, scenario.SecondNightShift.Id)
        ]);

        var result = CreateOptimizationResult(
            scenario.Problem,
            candidate);

        var projection = new ScheduleTableProjectionBuilder()
            .Build(
                scenario.Problem,
                result);

        Assert.Equal(3, projection.MorningSlotCount);
        Assert.Equal(3, projection.AfternoonSlotCount);
        Assert.Equal(3, projection.NightSlotCount);

        Assert.Collection(
            projection.Days,
            firstDay =>
            {
                Assert.Equal(new DateOnly(2026, 6, 1), firstDay.Date);
                Assert.Equal(DayOfWeek.Monday, firstDay.DayOfWeek);
                Assert.Equal(["Dana", "Yossi"], firstDay.MorningWorkerNames);
                Assert.Equal(["Noa"], firstDay.AfternoonWorkerNames);
                Assert.Empty(firstDay.NightWorkerNames);
            },
            secondDay =>
            {
                Assert.Equal(new DateOnly(2026, 6, 2), secondDay.Date);
                Assert.Equal(DayOfWeek.Tuesday, secondDay.DayOfWeek);
                Assert.Empty(secondDay.MorningWorkerNames);
                Assert.Empty(secondDay.AfternoonWorkerNames);
                Assert.Equal(["Dana"], secondDay.NightWorkerNames);
            });
    }

    private static SchedulingRunOptimizationResult CreateOptimizationResult(
        SchedulingProblem problem,
        ScheduleCandidate candidate)
    {
        var shiftsById = problem.Shifts.ToDictionary(shift => shift.Id);

        var loadByResource = problem.Resources
            .Select(resource =>
            {
                var resourceAssignments = candidate.Assignments
                    .Where(assignment => assignment.ResourceId == resource.Id)
                    .ToArray();

                var assignedHours = resourceAssignments.Sum(assignment =>
                {
                    var shift = shiftsById[assignment.ShiftId];

                    return (shift.EndUtc - shift.StartUtc).TotalHours;
                });

                return new ResourceLoadSummary(
                    resource.Id,
                    resource.Name,
                    assignedHours,
                    resourceAssignments.Length);
            })
            .ToArray();

        var evaluation = new ScheduleEvaluator()
            .Evaluate(problem, candidate);

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

    private static Scenario CreateScenario()
    {
        var dana = CreateResource("Dana", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var yossi = CreateResource("Yossi", "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var noa = CreateResource("Noa", "cccccccc-cccc-cccc-cccc-cccccccccccc");

        var firstMorningShift = CreateShift(
            "dddddddd-dddd-dddd-dddd-dddddddddddd",
            new DateOnly(2026, 6, 1),
            ShiftKind.Morning);

        var firstAfternoonShift = CreateShift(
            "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee",
            new DateOnly(2026, 6, 1),
            ShiftKind.Afternoon);

        var firstNightShift = CreateShift(
            "ffffffff-ffff-ffff-ffff-ffffffffffff",
            new DateOnly(2026, 6, 1),
            ShiftKind.Night);

        var secondMorningShift = CreateShift(
            "11111111-1111-1111-1111-111111111111",
            new DateOnly(2026, 6, 2),
            ShiftKind.Morning);

        var secondAfternoonShift = CreateShift(
            "22222222-2222-2222-2222-222222222222",
            new DateOnly(2026, 6, 2),
            ShiftKind.Afternoon);

        var secondNightShift = CreateShift(
            "33333333-3333-3333-3333-333333333333",
            new DateOnly(2026, 6, 2),
            ShiftKind.Night);

        var resources = new[] { dana, yossi, noa };

        var shifts = new[]
        {
            firstMorningShift,
            firstAfternoonShift,
            firstNightShift,
            secondMorningShift,
            secondAfternoonShift,
            secondNightShift
        };

        var availabilityWindows = resources
            .SelectMany(resource => shifts.Select(shift =>
                new AvailabilityWindow(
                    resource.Id,
                    shift.StartUtc,
                    shift.EndUtc)))
            .ToArray();

        var problem = new SchedulingProblem(
            period: new SchedulePeriod(
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc)),
            resources: resources,
            shifts: shifts,
            availabilityWindows: availabilityWindows,
            resourcePreferences: []);

        return new Scenario(
            Problem: problem,
            Dana: dana,
            Yossi: yossi,
            Noa: noa,
            FirstMorningShift: firstMorningShift,
            FirstAfternoonShift: firstAfternoonShift,
            SecondNightShift: secondNightShift);
    }

    private static Resource CreateResource(
        string name,
        string id)
    {
        return new Resource(
            Guid.Parse(id),
            name,
            hourlyCost: 100m);
    }

    private static Shift CreateShift(
        string id,
        DateOnly date,
        ShiftKind kind)
    {
        var startUtc = kind switch
        {
            ShiftKind.Morning => date.ToDateTime(new TimeOnly(6, 30), DateTimeKind.Utc),
            ShiftKind.Afternoon => date.ToDateTime(new TimeOnly(14, 30), DateTimeKind.Utc),
            ShiftKind.Night => date.ToDateTime(new TimeOnly(22, 30), DateTimeKind.Utc),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        var endUtc = kind switch
        {
            ShiftKind.Morning => date.ToDateTime(new TimeOnly(14, 30), DateTimeKind.Utc),
            ShiftKind.Afternoon => date.ToDateTime(new TimeOnly(22, 30), DateTimeKind.Utc),
            ShiftKind.Night => date.AddDays(1).ToDateTime(new TimeOnly(6, 30), DateTimeKind.Utc),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        return new Shift(
            Guid.Parse(id),
            startUtc,
            endUtc,
            kind,
            minResourceCount: 0,
            maxResourceCount: 3);
    }

    private sealed record Scenario(
        SchedulingProblem Problem,
        Resource Dana,
        Resource Yossi,
        Resource Noa,
        Shift FirstMorningShift,
        Shift FirstAfternoonShift,
        Shift SecondNightShift);
}
