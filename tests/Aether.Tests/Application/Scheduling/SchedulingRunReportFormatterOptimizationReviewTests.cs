using Aether.Application.Scheduling.Contracts;
using Aether.Application.Scheduling.Optimization;
using Aether.Application.Scheduling.Reports;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class SchedulingRunReportFormatterOptimizationReviewTests
{
    [Fact]
    public void FormatOptimizationReview_ShouldPrintSingleOptimizationHumanReviewReport()
    {
        var scenario = CreateScenario();

        var candidate = new ScheduleCandidate(
        [
            new Assignment(scenario.Dana.Id, scenario.FirstShift.Id),
            new Assignment(scenario.Yossi.Id, scenario.SecondShift.Id)
        ]);

        var evaluation = CreateEvaluation(
            scoreValue: 900,
            softViolationCount: 1,
            totalPenalty: 100,
            violations:
            [
                new ConstraintViolation(
                    ConstraintViolationType.ResourceRequestedPreferredHoursNotSatisfied,
                    ConstraintViolationSeverity.Soft,
                    "Preferred request was not satisfied.",
                    scenario.Dana.Id,
                    scenario.SecondShift.Id,
                    magnitude: 8)
            ]);

        var result = CreateOptimizationResult(
            scenario.Problem,
            candidate,
            evaluation,
            generationDiagnostics:
            [
                new GeneticGenerationDiagnostic(
                    GenerationIndex: 0,
                    PopulationSize: 10,
                    FeasibleCandidateCount: 2,
                    BestScoreValue: 900,
                    BestTotalPenalty: 100,
                    BestHardViolationCount: 0,
                    BestSoftViolationCount: 1,
                    BestSoFarScoreValue: 900,
                    BestSoFarTotalPenalty: 100,
                    BestSoFarHardViolationCount: 0,
                    BestSoFarSoftViolationCount: 1)
            ]);

        var formatter = new SchedulingRunReportFormatter();

        var report = formatter.FormatOptimizationReview(
            scenario.Problem,
            result);

        Assert.Contains("Schedule Optimization Review", report);
        Assert.Contains("Problem Summary", report);
        Assert.Contains("Resources: 2", report);
        Assert.Contains("Shifts: 2", report);

        Assert.Contains("ScheduleByShift", report);
        Assert.Contains("Morning 2026-06-01 06:30-14:30 UTC | Min=1 Max=1 Assigned=1 | Workers=Dana", report);
        Assert.Contains("Afternoon 2026-06-01 14:30-22:30 UTC | Min=1 Max=1 Assigned=1 | Workers=Yossi", report);

        Assert.Contains("LoadByResource", report);
        Assert.Contains("Dana: 8.0h, assignments=1", report);
        Assert.Contains("Yossi: 8.0h, assignments=1", report);

        Assert.Contains("TargetGapByResource", report);
        Assert.Contains("Dana: Assigned=8.0h, Target=8.0h, Gap=+0.0h", report);
        Assert.Contains("Yossi: Assigned=8.0h, Target=16.0h, Gap=-8.0h", report);

        Assert.Contains("PreferenceFulfillment", report);
        Assert.Contains("PreferredRequests: 2", report);
        Assert.Contains("FulfilledPreferredRequests: 1", report);
        Assert.Contains("UnsatisfiedPreferredRequests: 1", report);
        Assert.Contains("FulfillmentRate: 50.0%", report);

        Assert.Contains("ViolationsByType", report);
        Assert.Contains("ResourceRequestedPreferredHoursNotSatisfied: 1", report);
        Assert.Contains("Violations:", report);
        Assert.Contains("Resource=Dana", report);
        Assert.Contains("Preferred request was not satisfied.", report);

        Assert.Contains("GenerationDiagnostics", report);
        Assert.Contains("Generation 0", report);
        Assert.Contains("BestSoFarTotalPenalty=100", report);
    }

    private static SchedulingRunOptimizationResult CreateOptimizationResult(
        SchedulingProblem problem,
        ScheduleCandidate candidate,
        ScheduleEvaluationResult evaluation,
        IReadOnlyCollection<GeneticGenerationDiagnostic> generationDiagnostics)
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
            generationDiagnostics);
    }

    private static ScheduleEvaluationResult CreateEvaluation(
        int scoreValue,
        int softViolationCount,
        int totalPenalty,
        IReadOnlyCollection<ConstraintViolation> violations)
    {
        var score = new ScheduleScore(
            value: scoreValue,
            hardViolationCount: 0,
            softViolationCount: softViolationCount,
            totalPenalty: totalPenalty);

        return new ScheduleEvaluationResult(score, violations);
    }

    private static Scenario CreateScenario()
    {
        var dana = new Resource(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "Dana",
            hourlyCost: 100m);

        var yossi = new Resource(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            "Yossi",
            hourlyCost: 100m);

        var firstShift = new Shift(
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning,
            minResourceCount: 1,
            maxResourceCount: 1);

        var secondShift = new Shift(
            Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 22, 30, 0, DateTimeKind.Utc),
            ShiftKind.Afternoon,
            minResourceCount: 1,
            maxResourceCount: 1);

        var availabilityWindows = new[]
        {
            new AvailabilityWindow(dana.Id, firstShift.StartUtc, firstShift.EndUtc),
            new AvailabilityWindow(dana.Id, secondShift.StartUtc, secondShift.EndUtc),
            new AvailabilityWindow(yossi.Id, firstShift.StartUtc, firstShift.EndUtc),
            new AvailabilityWindow(yossi.Id, secondShift.StartUtc, secondShift.EndUtc)
        };

        var preferences = new[]
        {
            new ResourcePreference(
                dana.Id,
                firstShift.StartUtc,
                firstShift.EndUtc,
                ResourcePreferenceType.Prefer,
                ResourcePreferencePriority.High),
            new ResourcePreference(
                dana.Id,
                secondShift.StartUtc,
                secondShift.EndUtc,
                ResourcePreferenceType.Prefer,
                ResourcePreferencePriority.High)
        };

        var workloadDemands = new[]
        {
            new ResourceWorkloadDemand(
                dana.Id,
                requestedPreferredHours: 8,
                minimumRequiredHours: 0),
            new ResourceWorkloadDemand(
                yossi.Id,
                requestedPreferredHours: 16,
                minimumRequiredHours: 0)
        };

        var problem = new SchedulingProblem(
            period: new SchedulePeriod(
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc)),
            resources: [dana, yossi],
            shifts: [firstShift, secondShift],
            availabilityWindows: availabilityWindows,
            resourcePreferences: preferences,
            resourceWorkloadDemands: workloadDemands);

        return new Scenario(
            Problem: problem,
            Dana: dana,
            Yossi: yossi,
            FirstShift: firstShift,
            SecondShift: secondShift);
    }

    private sealed record Scenario(
        SchedulingProblem Problem,
        Resource Dana,
        Resource Yossi,
        Shift FirstShift,
        Shift SecondShift);
}
