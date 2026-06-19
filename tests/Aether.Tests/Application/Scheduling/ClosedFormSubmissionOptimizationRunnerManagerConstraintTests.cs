using Aether.Application.Scheduling.ManagerConstraints;
using Aether.Application.Scheduling.SubmissionForms;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class ClosedFormSubmissionOptimizationRunnerManagerConstraintTests
{
    [Fact]
    public void Run_ShouldPropagateManagerConstraintSet_ToFormProblemBuilder()
    {
        var dana = CreateResource(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "Dana");

        var yossi = CreateResource(
            "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
            "Yossi");

        var date = new DateOnly(2026, 6, 15);

        var morningShift = CreateShift(
            "cccccccc-cccc-cccc-cccc-cccccccccccc",
            date,
            ShiftKind.Morning);

        var afternoonShift = CreateShift(
            "dddddddd-dddd-dddd-dddd-dddddddddddd",
            date,
            ShiftKind.Afternoon);

        var request = new ClosedFormSubmissionOptimizationRequest(
            new SchedulePeriod(
                date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)),
            [dana, yossi],
            [morningShift, afternoonShift],
            [
                new WorkerSubmission(
                    dana.Id,
                    [
                        new WorkerShiftSubmission(
                            date,
                            ShiftKind.Morning,
                            ShiftSubmissionChoice.StrongAvailable)
                    ]),
                new WorkerSubmission(
                    yossi.Id,
                    [
                        new WorkerShiftSubmission(
                            date,
                            ShiftKind.Afternoon,
                            ShiftSubmissionChoice.StrongAvailable)
                    ])
            ],
            TotalEffectiveTargetHours: 8,
            ManagerConstraintSet: new ManagerConstraintSet([
                new ManagerForbiddenAssignment(dana.Id, morningShift.Id)
            ]));

        var result = new ClosedFormSubmissionOptimizationRunner()
            .Run(request);

        Assert.DoesNotContain(
            result.Problem.AvailabilityWindows,
            window =>
                window.ResourceId == dana.Id &&
                window.Covers(morningShift));

        Assert.DoesNotContain(
            result.Problem.ResourcePreferences,
            preference =>
                preference.ResourceId == dana.Id &&
                preference.StartUtc < morningShift.EndUtc &&
                morningShift.StartUtc < preference.EndUtc);

        var danaDemand = result.Problem.ResourceWorkloadDemands.Single(
            demand => demand.ResourceId == dana.Id);

        var yossiDemand = result.Problem.ResourceWorkloadDemands.Single(
            demand => demand.ResourceId == yossi.Id);

        Assert.Equal(0, danaDemand.RequestedPreferredHours);
        Assert.Equal(8, yossiDemand.RequestedPreferredHours);
    }

    private static Resource CreateResource(
        string id,
        string name)
    {
        return new Resource(
            Guid.Parse(id),
            name,
            hourlyCost: 0);
    }

    private static Shift CreateShift(
        string id,
        DateOnly date,
        ShiftKind kind)
    {
        return new Shift(
            Guid.Parse(id),
            GetStartUtc(date, kind),
            GetEndUtc(date, kind),
            kind,
            minResourceCount: 0,
            maxResourceCount: 4);
    }

    private static DateTime GetStartUtc(DateOnly date, ShiftKind kind)
    {
        return kind switch
        {
            ShiftKind.Morning => date.ToDateTime(new TimeOnly(6, 30), DateTimeKind.Utc),
            ShiftKind.Afternoon => date.ToDateTime(new TimeOnly(14, 20), DateTimeKind.Utc),
            ShiftKind.Night => date.ToDateTime(new TimeOnly(22, 40), DateTimeKind.Utc),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), "Shift kind is not supported.")
        };
    }

    private static DateTime GetEndUtc(DateOnly date, ShiftKind kind)
    {
        return kind switch
        {
            ShiftKind.Morning => date.ToDateTime(new TimeOnly(14, 20), DateTimeKind.Utc),
            ShiftKind.Afternoon => date.ToDateTime(new TimeOnly(22, 40), DateTimeKind.Utc),
            ShiftKind.Night => date.AddDays(1).ToDateTime(new TimeOnly(6, 30), DateTimeKind.Utc),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), "Shift kind is not supported.")
        };
    }
}
