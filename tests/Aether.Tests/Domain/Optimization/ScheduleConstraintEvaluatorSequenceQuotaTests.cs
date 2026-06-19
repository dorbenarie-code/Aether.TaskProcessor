using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ScheduleConstraintEvaluatorSequenceQuotaTests
{
    [Fact]
    public void Evaluate_allows_two_night_to_afternoon_sequences_in_same_month()
    {
        var resource = CreateResource();
        var shifts = CreateNightToAfternoonSequenceShifts(
            year: 2026,
            month: 1,
            firstDay: 1,
            count: 2);

        var problem = CreateProblem([resource], shifts);
        var candidate = CreateCandidate(resource.Id, shifts);

        var evaluator = new ScheduleConstraintEvaluator();

        var violations = evaluator.Evaluate(problem, candidate);

        Assert.Empty(violations);
    }

    [Fact]
    public void Evaluate_detects_three_night_to_afternoon_sequences_in_same_month()
    {
        var resource = CreateResource();
        var shifts = CreateNightToAfternoonSequenceShifts(
            year: 2026,
            month: 1,
            firstDay: 1,
            count: 3);

        var problem = CreateProblem([resource], shifts);
        var candidate = CreateCandidate(resource.Id, shifts);

        var evaluator = new ScheduleConstraintEvaluator();

        var violation = Assert.Single(evaluator.Evaluate(problem, candidate));

        Assert.Equal(ConstraintViolationType.ShiftSequenceQuotaExceeded, violation.Type);
        Assert.Equal(ConstraintViolationSeverity.Hard, violation.Severity);
        Assert.Equal(resource.Id, violation.ResourceId);
        Assert.Null(violation.ShiftId);
    }

    [Fact]
    public void Evaluate_allows_two_afternoon_to_morning_sequences_in_same_month()
    {
        var resource = CreateResource();
        var shifts = CreateAfternoonToMorningSequenceShifts(
            year: 2026,
            month: 1,
            firstDay: 1,
            count: 2);

        var problem = CreateProblem([resource], shifts);
        var candidate = CreateCandidate(resource.Id, shifts);

        var evaluator = new ScheduleConstraintEvaluator();

        var violations = evaluator.Evaluate(problem, candidate);

        Assert.Empty(violations);
    }

    [Fact]
    public void Evaluate_detects_three_afternoon_to_morning_sequences_in_same_month()
    {
        var resource = CreateResource();
        var shifts = CreateAfternoonToMorningSequenceShifts(
            year: 2026,
            month: 1,
            firstDay: 1,
            count: 3);

        var problem = CreateProblem([resource], shifts);
        var candidate = CreateCandidate(resource.Id, shifts);

        var evaluator = new ScheduleConstraintEvaluator();

        var violation = Assert.Single(evaluator.Evaluate(problem, candidate));

        Assert.Equal(ConstraintViolationType.ShiftSequenceQuotaExceeded, violation.Type);
        Assert.Equal(ConstraintViolationSeverity.Hard, violation.Severity);
        Assert.Equal(resource.Id, violation.ResourceId);
        Assert.Null(violation.ShiftId);
    }

    [Fact]
    public void Evaluate_allows_two_sequences_of_each_type_in_same_month()
    {
        var resource = CreateResource();

        var nightToAfternoonShifts = CreateNightToAfternoonSequenceShifts(
            year: 2026,
            month: 1,
            firstDay: 1,
            count: 2);

        var afternoonToMorningShifts = CreateAfternoonToMorningSequenceShifts(
            year: 2026,
            month: 1,
            firstDay: 15,
            count: 2);

        var shifts = nightToAfternoonShifts
            .Concat(afternoonToMorningShifts)
            .OrderBy(shift => shift.StartUtc)
            .ToArray();

        var problem = CreateProblem([resource], shifts);
        var candidate = CreateCandidate(resource.Id, shifts);

        var evaluator = new ScheduleConstraintEvaluator();

        var violations = evaluator.Evaluate(problem, candidate);

        Assert.Empty(violations);
    }

    [Fact]
    public void Evaluate_counts_sequence_quota_by_calendar_month()
    {
        var resource = CreateResource();

        var januaryShifts = CreateNightToAfternoonSequenceShifts(
            year: 2026,
            month: 1,
            firstDay: 1,
            count: 2);

        var februaryShifts = CreateNightToAfternoonSequenceShifts(
            year: 2026,
            month: 2,
            firstDay: 1,
            count: 2);

        var shifts = januaryShifts
            .Concat(februaryShifts)
            .OrderBy(shift => shift.StartUtc)
            .ToArray();

        var problem = CreateProblem([resource], shifts);
        var candidate = CreateCandidate(resource.Id, shifts);

        var evaluator = new ScheduleConstraintEvaluator();

        var violations = evaluator.Evaluate(problem, candidate);

        Assert.Empty(violations);
    }

    [Fact]
    public void Evaluate_counts_sequences_separately_per_resource()
    {
        var firstResource = CreateResource();
        var secondResource = CreateResource();

        var firstResourceShifts = CreateNightToAfternoonSequenceShifts(
            year: 2026,
            month: 1,
            firstDay: 1,
            count: 2);

        var secondResourceShifts = CreateNightToAfternoonSequenceShifts(
            year: 2026,
            month: 1,
            firstDay: 15,
            count: 2);

        var shifts = firstResourceShifts
            .Concat(secondResourceShifts)
            .OrderBy(shift => shift.StartUtc)
            .ToArray();

        var problem = CreateProblem([firstResource, secondResource], shifts);

        var assignments = firstResourceShifts
            .Select(shift => new Assignment(firstResource.Id, shift.Id))
            .Concat(secondResourceShifts.Select(shift => new Assignment(secondResource.Id, shift.Id)))
            .ToArray();

        var candidate = new ScheduleCandidate(assignments);

        var evaluator = new ScheduleConstraintEvaluator();

        var violations = evaluator.Evaluate(problem, candidate);

        Assert.Empty(violations);
    }

    private static Resource CreateResource()
    {
        return new Resource(
            Guid.NewGuid(),
            "Guard",
            hourlyCost: 50);
    }

    private static SchedulingProblem CreateProblem(
        IReadOnlyCollection<Resource> resources,
        IReadOnlyCollection<Shift> shifts)
    {
        var availabilityWindows = resources
            .Select(resource => new AvailabilityWindow(
                resource.Id,
                new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 3, 31, 23, 59, 0, DateTimeKind.Utc)))
            .ToArray();

        return new SchedulingProblem(
            period: new SchedulePeriod(
                new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)),
            resources: resources,
            shifts: shifts,
            availabilityWindows: availabilityWindows,
            resourcePreferences: []);
    }

    private static ScheduleCandidate CreateCandidate(
        Guid resourceId,
        IReadOnlyCollection<Shift> shifts)
    {
        return new ScheduleCandidate(
            shifts
                .Select(shift => new Assignment(resourceId, shift.Id))
                .ToArray());
    }

    private static IReadOnlyCollection<Shift> CreateNightToAfternoonSequenceShifts(
        int year,
        int month,
        int firstDay,
        int count)
    {
        var shifts = new List<Shift>();

        for (var i = 0; i < count; i++)
        {
            var day = firstDay + (i * 3);

            var nightStartUtc = new DateTime(year, month, day, 22, 30, 0, DateTimeKind.Utc);
            var nightEndUtc = nightStartUtc.AddHours(8).AddMinutes(10);

            var afternoonStartUtc = new DateTime(
                nightEndUtc.Year,
                nightEndUtc.Month,
                nightEndUtc.Day,
                14,
                0,
                0,
                DateTimeKind.Utc);

            var afternoonEndUtc = afternoonStartUtc.AddHours(8);

            shifts.Add(CreateShift(ShiftKind.Night, nightStartUtc, nightEndUtc));
            shifts.Add(CreateShift(ShiftKind.Afternoon, afternoonStartUtc, afternoonEndUtc));
        }

        return shifts;
    }

    private static IReadOnlyCollection<Shift> CreateAfternoonToMorningSequenceShifts(
        int year,
        int month,
        int firstDay,
        int count)
    {
        var shifts = new List<Shift>();

        for (var i = 0; i < count; i++)
        {
            var day = firstDay + (i * 3);

            var afternoonStartUtc = new DateTime(year, month, day, 14, 30, 0, DateTimeKind.Utc);
            var afternoonEndUtc = new DateTime(year, month, day, 22, 40, 0, DateTimeKind.Utc);

            var morningStartUtc = afternoonStartUtc
                .Date
                .AddDays(1)
                .AddHours(6)
                .AddMinutes(20);

            var morningEndUtc = morningStartUtc.AddHours(8).AddMinutes(10);

            shifts.Add(CreateShift(ShiftKind.Afternoon, afternoonStartUtc, afternoonEndUtc));
            shifts.Add(CreateShift(ShiftKind.Morning, morningStartUtc, morningEndUtc));
        }

        return shifts;
    }

    private static Shift CreateShift(
        ShiftKind kind,
        DateTime startUtc,
        DateTime endUtc)
    {
        return new Shift(
            Guid.NewGuid(),
            startUtc,
            endUtc,
            kind,
            minResourceCount: 1,
            maxResourceCount: 1);
    }
}
