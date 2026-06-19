using System.Globalization;
using Aether.Application.Scheduling.SubmissionForms;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class ClosedFormSubmissionOptimizationRunnerTests
{
    private const int ResourceCount = 19;
    private const int DaysInSchedule = 14;
    private const int ExpectedShiftCount = DaysInSchedule * 3;
    private const double ExpectedTotalEffectiveTargetHours = 736.0;
    private const double BalanceToleranceHours = 5.0;
    private const int AcceptedPopulationSize = 120;
    private const int AcceptedGenerationCount = 100;
    private const int Seed = 20260603;

    [Fact]
    [Trait("Category", "SchedulingExperiment")]
    public void Run_ShouldOptimizeRealSubmittedSchedule_FromClosedFormSubmissions()
    {
        var scenario = CreateRealSubmittedFormScenario();

        var request = new ClosedFormSubmissionOptimizationRequest(
            scenario.Period,
            scenario.Resources,
            scenario.Shifts,
            scenario.WorkerSubmissions,
            TotalEffectiveTargetHours: ExpectedTotalEffectiveTargetHours,
            MaximumAssignedHoursDeviationFromAverageHours: BalanceToleranceHours,
            Seed: Seed);

        var runner = new ClosedFormSubmissionOptimizationRunner();

        var result = runner.Run(request);

        Assert.Empty(result.Warnings);

        Assert.Equal(ResourceCount, result.Problem.Resources.Count);
        Assert.Equal(ExpectedShiftCount, result.Problem.Shifts.Count);
        Assert.Equal(ResourceCount, result.Problem.ResourceWorkloadDemands.Count);
        Assert.Equal(
            ExpectedTotalEffectiveTargetHours,
            result.Problem.ResourceWorkloadDemands.Sum(demand => demand.EffectiveTargetHours),
            0.000001);

        Assert.Equal(
            BalanceToleranceHours,
            result.Problem.MaximumAssignedHoursDeviationFromAverageHours);

        AssertMandatoryShiftPolicyShape(result.Problem);

        Assert.NotNull(result.GeneticResult.Candidate);
        Assert.NotNull(result.GeneticResult.Evaluation);
        Assert.NotEmpty(result.GeneticResult.Candidate.Assignments);

        var generationDiagnostics = result.GeneticResult.GenerationDiagnostics.ToArray();

        Assert.Equal(
            AcceptedGenerationCount + 1,
            generationDiagnostics.Length);

        Assert.Equal(
            Enumerable.Range(0, AcceptedGenerationCount + 1).ToArray(),
            generationDiagnostics
                .Select(diagnostic => diagnostic.GenerationIndex)
                .ToArray());

        Assert.All(generationDiagnostics, diagnostic =>
        {
            Assert.Equal(AcceptedPopulationSize, diagnostic.PopulationSize);
            Assert.InRange(diagnostic.FeasibleCandidateCount, 0, AcceptedPopulationSize);
        });

        Assert.True(
            generationDiagnostics[^1].BestSoFarTotalPenalty <=
            generationDiagnostics[0].BestSoFarTotalPenalty,
            "Accepted Clean GA run should not return a best-so-far penalty worse than generation 0.");

        AssertCandidateReferencesKnownProblemEntities(
            result.Problem,
            result.GeneticResult.Candidate);

        AssertNoBasicStructuralViolations(result.GeneticResult.Evaluation);

        Assert.True(
            result.GeneticResult.Evaluation.IsFeasible,
            $"Expected real closed-form submitted schedule run to be feasible, but got {result.GeneticResult.Evaluation.Score.HardViolationCount} hard violations.");
    }

    [Fact]
    public void Run_ShouldNotApplyPostRunLocalAddImprovement_ByDefault()
    {
        var scenario = CreatePostRunLocalAddIntegrationScenario();

        var result = new ClosedFormSubmissionOptimizationRunner()
            .Run(new ClosedFormSubmissionOptimizationRequest(
                scenario.Period,
                scenario.Resources,
                scenario.Shifts,
                scenario.WorkerSubmissions,
                TotalEffectiveTargetHours: 8,
                MaximumAssignedHoursDeviationFromAverageHours: null,
                Seed: 20260615));

        Assert.Empty(result.Warnings);
        Assert.NotNull(result.GeneticResult);
        Assert.Null(result.PostRunLocalAddImprovementResult);
        Assert.True(result.GeneticResult.Evaluation.IsFeasible);

        AssertCandidateReferencesKnownProblemEntities(
            result.Problem,
            result.GeneticResult.Candidate);

        AssertNoBasicStructuralViolations(result.GeneticResult.Evaluation);
    }

    [Fact]
    public void Run_ShouldApplyPostRunLocalAddImprovement_WhenRequested()
    {
        var scenario = CreatePostRunLocalAddIntegrationScenario();

        var result = new ClosedFormSubmissionOptimizationRunner()
            .Run(new ClosedFormSubmissionOptimizationRequest(
                scenario.Period,
                scenario.Resources,
                scenario.Shifts,
                scenario.WorkerSubmissions,
                TotalEffectiveTargetHours: 8,
                MaximumAssignedHoursDeviationFromAverageHours: null,
                Seed: 20260615,
                ApplyPostRunLocalAddImprovement: true));

        Assert.Empty(result.Warnings);
        Assert.NotNull(result.GeneticResult);
        Assert.NotNull(result.PostRunLocalAddImprovementResult);
        Assert.True(result.GeneticResult.Evaluation.IsFeasible);

        var localImprovementResult = result.PostRunLocalAddImprovementResult;

        Assert.True(
            localImprovementResult.FinalTotalPenalty <=
            localImprovementResult.InitialTotalPenalty);

        Assert.Equal(
            localImprovementResult.FinalTotalPenalty,
            result.GeneticResult.Evaluation.Score.TotalPenalty);

        Assert.Equal(
            localImprovementResult.Candidate.Assignments.Count,
            result.GeneticResult.Candidate.Assignments.Count);

        AssertCandidateReferencesKnownProblemEntities(
            result.Problem,
            result.GeneticResult.Candidate);

        AssertNoBasicStructuralViolations(result.GeneticResult.Evaluation);
    }

    private static PostRunLocalAddIntegrationScenario CreatePostRunLocalAddIntegrationScenario()
    {
        var resource = new Resource(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "Ziv",
            hourlyCost: 100m);

        var date = new DateOnly(2026, 6, 14);

        var shift = new Shift(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            date.ToDateTime(new TimeOnly(6, 30), DateTimeKind.Utc),
            date.ToDateTime(new TimeOnly(14, 30), DateTimeKind.Utc),
            ShiftKind.Morning,
            minResourceCount: 0,
            maxResourceCount: 1,
            requiresPreferenceToAssign: false);

        return new PostRunLocalAddIntegrationScenario(
            Period: new SchedulePeriod(
                date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)),
            Resources: [resource],
            Shifts: [shift],
            WorkerSubmissions:
            [
                new WorkerSubmission(
                    resource.Id,
                    [
                        new WorkerShiftSubmission(
                            date,
                            ShiftKind.Morning,
                            ShiftSubmissionChoice.StrongAvailable)
                    ])
            ]);
    }

    private static RealSubmittedFormScenario CreateRealSubmittedFormScenario()
    {
        var resources = CreateResources();
        var shifts = CreateBiWeeklySequencePressureShifts();
        var workerSubmissions = CreateWorkerSubmissions(resources);

        return new RealSubmittedFormScenario(
            Period: new SchedulePeriod(
                new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc)),
            Resources: resources,
            Shifts: shifts,
            WorkerSubmissions: workerSubmissions);
    }

    private static IReadOnlyList<Resource> CreateResources()
    {
        return Enumerable
            .Range(1, ResourceCount)
            .Select(index => new Resource(
                CreateGuid(index),
                $"עובד {index:00}",
                hourlyCost: 100m))
            .ToArray();
    }

    private static IReadOnlyList<WorkerSubmission> CreateWorkerSubmissions(
        IReadOnlyList<Resource> resources)
    {
        var submissionsByResourceId = resources.ToDictionary(
            resource => resource.Id,
            _ => new List<WorkerShiftSubmission>());

        AddSubmissions(submissionsByResourceId, resources[0], "2026-05-31:A;2026-06-01:M;2026-06-02:N;2026-06-03:A;2026-06-04:M;2026-06-07:A;2026-06-08:M;2026-06-09:A;2026-06-10:A;2026-06-11:M");
        AddSubmissions(submissionsByResourceId, resources[1], "2026-05-31:M,A;2026-06-07:M,A");
        AddSubmissions(submissionsByResourceId, resources[2], "2026-05-31:N;2026-06-04:M,A;2026-06-05:M;2026-06-07:N;2026-06-08:N;2026-06-09:M");
        AddSubmissions(submissionsByResourceId, resources[3], "2026-05-31:M;2026-06-01:M;2026-06-02:M;2026-06-03:M;2026-06-04:M;2026-06-07:M;2026-06-08:M;2026-06-09:M;2026-06-10:M;2026-06-11:M");
        AddSubmissions(submissionsByResourceId, resources[4], "2026-05-31:A,N;2026-06-02:M,A;2026-06-04:N;2026-06-05:M;2026-06-06:N;2026-06-07:A,N;2026-06-09:M,A;2026-06-11:N;2026-06-12:M;2026-06-13:N");
        AddSubmissions(submissionsByResourceId, resources[5], "2026-05-31:A;2026-06-03:M;2026-06-05:M;2026-06-07:A;2026-06-10:M;2026-06-12:M");
        AddSubmissions(submissionsByResourceId, resources[6], "2026-05-31:M,A;2026-06-01:A;2026-06-02:A;2026-06-03:M;2026-06-07:A;2026-06-08:M;2026-06-09:M;2026-06-10:A;2026-06-11:A");
        AddSubmissions(submissionsByResourceId, resources[7], "2026-05-31:N;2026-06-01:M,A;2026-06-02:N;2026-06-03:A,N;2026-06-04:M,A;2026-06-07:M,A,N;2026-06-08:M,A;2026-06-10:A,N;2026-06-11:M,A");
        AddSubmissions(submissionsByResourceId, resources[8], "2026-06-01:M;2026-06-02:N;2026-06-03:A;2026-06-04:M;2026-06-07:M;2026-06-08:M;2026-06-09:A;2026-06-10:N;2026-06-11:A");
        AddSubmissions(submissionsByResourceId, resources[9], "2026-06-01:N;2026-06-02:M,N;2026-06-03:M,A,N;2026-06-04:M,A,N;2026-06-07:M,N;2026-06-08:M,A,N;2026-06-09:M,N;2026-06-10:A,N;2026-06-11:M,A,N");
        AddSubmissions(submissionsByResourceId, resources[10], "2026-06-01:M;2026-06-02:M;2026-06-03:A;2026-06-04:M;2026-06-07:M;2026-06-08:M;2026-06-09:M;2026-06-10:A;2026-06-11:M");
        AddSubmissions(submissionsByResourceId, resources[11], "2026-05-31:A;2026-06-01:A;2026-06-02:M,N;2026-06-03:A;2026-06-04:M;2026-06-07:M,N;2026-06-08:A;2026-06-09:M;2026-06-11:A;2026-06-12:M");
        AddSubmissions(submissionsByResourceId, resources[12], "2026-05-31:M;2026-06-02:A;2026-06-03:A;2026-06-07:M;2026-06-09:A;2026-06-10:M");
        AddSubmissions(submissionsByResourceId, resources[13], "2026-06-03:A;2026-06-04:M;2026-06-06:N;2026-06-07:A;2026-06-08:M");
        AddSubmissions(submissionsByResourceId, resources[14], "2026-05-31:M,N;2026-06-01:A,N;2026-06-02:A;2026-06-03:N;2026-06-04:A,N;2026-06-05:M;2026-06-06:N;2026-06-07:M,N;2026-06-08:A,N;2026-06-10:A,N;2026-06-11:M,A,N;2026-06-12:M;2026-06-13:N");
        AddSubmissions(submissionsByResourceId, resources[15], "2026-05-31:M,A,N;2026-06-01:N;2026-06-02:N;2026-06-03:M,A,N;2026-06-04:N;2026-06-05:M;2026-06-06:N;2026-06-07:M,A,N;2026-06-08:N;2026-06-09:N;2026-06-10:M,N;2026-06-11:N;2026-06-12:M;2026-06-13:N");
        AddSubmissions(submissionsByResourceId, resources[16], "2026-06-03:M;2026-06-05:N;2026-06-06:M;2026-06-10:M;2026-06-12:N;2026-06-13:M");
        AddSubmissions(submissionsByResourceId, resources[17], "2026-05-31:M,N;2026-06-01:A;2026-06-02:M");
        AddSubmissions(submissionsByResourceId, resources[18], "2026-05-31:A;2026-06-01:N;2026-06-02:M;2026-06-04:A,N;2026-06-05:M;2026-06-07:M;2026-06-09:N;2026-06-11:M,A,N;2026-06-12:M");

        return resources
            .Select(resource => new WorkerSubmission(
                resource.Id,
                submissionsByResourceId[resource.Id]
                    .Distinct()
                    .ToArray()))
            .ToArray();
    }

    private static void AddSubmissions(
        IReadOnlyDictionary<Guid, List<WorkerShiftSubmission>> submissionsByResourceId,
        Resource resource,
        string encoded)
    {
        foreach (var block in encoded.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = block.Split(':', StringSplitOptions.TrimEntries);
            var date = DateOnly.ParseExact(parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture);

            foreach (var token in parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                submissionsByResourceId[resource.Id].Add(new WorkerShiftSubmission(
                    date,
                    token switch
                    {
                        "M" => ShiftKind.Morning,
                        "A" => ShiftKind.Afternoon,
                        "N" => ShiftKind.Night,
                        _ => throw new ArgumentOutOfRangeException(nameof(token), token, "Unsupported token.")
                    },
                    ShiftSubmissionChoice.StrongAvailable));
            }
        }
    }

    private static IReadOnlyList<Shift> CreateBiWeeklySequencePressureShifts()
    {
        var shifts = new List<Shift>();

        for (var dayOffset = 0; dayOffset < DaysInSchedule; dayOffset++)
        {
            var date = new DateOnly(2026, 5, 31).AddDays(dayOffset);

            shifts.Add(CreateShift(date, ShiftKind.Morning));
            shifts.Add(CreateShift(date, ShiftKind.Afternoon));
            shifts.Add(CreateShift(date, ShiftKind.Night));
        }

        return shifts.OrderBy(shift => shift.StartUtc).ToArray();
    }

    private static Shift CreateShift(DateOnly date, ShiftKind kind)
    {
        var capacity = GetCapacityRule(date, kind);

        return new Shift(
            CreateShiftGuid(date, kind),
            GetStartUtc(date, kind),
            GetEndUtc(date, kind),
            kind,
            capacity.Min,
            capacity.Max,
            requiresPreferenceToAssign: capacity.RequiresPreference,
            requiresMinimumWhenPreferenceExists: false,
            nightShiftCategory: GetNightShiftCategory(date, kind));
    }

    private static CapacityRule GetCapacityRule(DateOnly date, ShiftKind kind)
    {
        if (date.DayOfWeek == DayOfWeek.Friday)
        {
            return kind switch
            {
                ShiftKind.Morning => new CapacityRule(0, 2, true),
                ShiftKind.Afternoon => new CapacityRule(0, 1, true),
                ShiftKind.Night => new CapacityRule(0, 1, true),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
        }

        if (date.DayOfWeek == DayOfWeek.Saturday)
        {
            return kind switch
            {
                ShiftKind.Morning => new CapacityRule(0, 1, true),
                ShiftKind.Afternoon => new CapacityRule(0, 1, true),
                ShiftKind.Night => new CapacityRule(3, 3, false),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
        }

        return kind switch
        {
            ShiftKind.Morning => new CapacityRule(3, 6, false),
            ShiftKind.Afternoon => new CapacityRule(2, 4, true),
            ShiftKind.Night => new CapacityRule(0, 1, true),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private static DateTime GetStartUtc(DateOnly date, ShiftKind kind)
    {
        var time = kind switch
        {
            ShiftKind.Morning => new TimeOnly(6, 30),
            ShiftKind.Afternoon => new TimeOnly(14, 20),
            ShiftKind.Night => new TimeOnly(22, 40),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        return date.ToDateTime(time, DateTimeKind.Utc);
    }

    private static DateTime GetEndUtc(DateOnly date, ShiftKind kind)
    {
        var endDate = kind == ShiftKind.Night ? date.AddDays(1) : date;

        var time = kind switch
        {
            ShiftKind.Morning => new TimeOnly(14, 20),
            ShiftKind.Afternoon => new TimeOnly(22, 40),
            ShiftKind.Night => new TimeOnly(6, 30),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        return endDate.ToDateTime(time, DateTimeKind.Utc);
    }

    private static NightShiftCategory? GetNightShiftCategory(DateOnly date, ShiftKind kind)
    {
        if (kind != ShiftKind.Night)
        {
            return null;
        }

        return date.DayOfWeek switch
        {
            DayOfWeek.Friday => NightShiftCategory.FridayNight,
            DayOfWeek.Saturday => NightShiftCategory.MotzeiShabbatNight,
            _ => NightShiftCategory.Regular
        };
    }

    private static void AssertMandatoryShiftPolicyShape(SchedulingProblem problem)
    {
        var weekdayMornings = problem.Shifts.Where(IsWeekdayMorning).ToArray();
        Assert.Equal(10, weekdayMornings.Length);

        Assert.All(weekdayMornings, shift =>
        {
            Assert.False(shift.RequiresPreferenceToAssign);
            Assert.Equal(ResourceCount, CountAvailable(problem, shift));
            Assert.True(CountPrefer(problem, shift) >= shift.MinResourceCount);
            Assert.Equal(ResourceCount, CountPrefer(problem, shift) + CountAvoid(problem, shift));
        });

        var motzeiShabbatShifts = problem.Shifts
            .Where(shift => shift.NightShiftCategory == NightShiftCategory.MotzeiShabbatNight)
            .ToArray();

        Assert.Equal(2, motzeiShabbatShifts.Length);

        Assert.All(motzeiShabbatShifts, shift =>
        {
            Assert.False(shift.RequiresPreferenceToAssign);
            Assert.Equal(3, shift.MinResourceCount);
            Assert.Equal(3, shift.MaxResourceCount);
            Assert.Equal(ResourceCount, CountAvailable(problem, shift));
            Assert.True(CountPrefer(problem, shift) >= shift.MinResourceCount);
            Assert.Equal(ResourceCount, CountPrefer(problem, shift) + CountAvoid(problem, shift));
        });
    }

    private static int CountAvailable(SchedulingProblem problem, Shift shift)
    {
        return problem.AvailabilityWindows.Count(window => window.Covers(shift));
    }

    private static int CountPrefer(SchedulingProblem problem, Shift shift)
    {
        return problem.ResourcePreferences
            .Where(preference => preference.Type == ResourcePreferenceType.Prefer)
            .Count(preference => Overlaps(
                shift.StartUtc,
                shift.EndUtc,
                preference.StartUtc,
                preference.EndUtc));
    }

    private static int CountAvoid(SchedulingProblem problem, Shift shift)
    {
        return problem.ResourcePreferences
            .Where(preference => preference.Type == ResourcePreferenceType.Avoid)
            .Count(preference => Overlaps(
                shift.StartUtc,
                shift.EndUtc,
                preference.StartUtc,
                preference.EndUtc));
    }

    private static void AssertCandidateReferencesKnownProblemEntities(
        SchedulingProblem problem,
        ScheduleCandidate candidate)
    {
        var resourceIds = problem.Resources.Select(resource => resource.Id).ToHashSet();
        var shiftIds = problem.Shifts.Select(shift => shift.Id).ToHashSet();

        Assert.All(candidate.Assignments, assignment =>
        {
            Assert.Contains(assignment.ResourceId, resourceIds);
            Assert.Contains(assignment.ShiftId, shiftIds);
        });

        Assert.Equal(
            candidate.Assignments.Count,
            candidate.Assignments
                .Select(assignment => $"{assignment.ResourceId}:{assignment.ShiftId}")
                .Distinct()
                .Count());
    }

    private static void AssertNoBasicStructuralViolations(
        ScheduleEvaluationResult evaluation)
    {
        Assert.Equal(0, CountViolations(evaluation, ConstraintViolationType.ResourceUnavailable));
        Assert.Equal(0, CountViolations(evaluation, ConstraintViolationType.ResourceAssignedToOverlappingShifts));
        Assert.Equal(0, CountViolations(evaluation, ConstraintViolationType.AssignedWithoutRequiredPreference));
        Assert.Equal(0, CountViolations(evaluation, ConstraintViolationType.ShiftUnderstaffed));
        Assert.Equal(0, CountViolations(evaluation, ConstraintViolationType.ShiftOverstaffed));
        Assert.Equal(0, CountViolations(evaluation, ConstraintViolationType.ResourceMonthlyNightShiftQuotaExceeded));
        Assert.Equal(0, CountViolations(evaluation, ConstraintViolationType.ShiftSequenceQuotaExceeded));
        Assert.Equal(0, evaluation.Score.HardViolationCount);
    }

    private static int CountViolations(
        ScheduleEvaluationResult evaluation,
        ConstraintViolationType type)
    {
        return evaluation.Violations.Count(violation => violation.Type == type);
    }

    private static bool IsWeekdayMorning(Shift shift)
    {
        var day = DateOnly.FromDateTime(shift.StartUtc).DayOfWeek;

        return shift.Kind == ShiftKind.Morning &&
               day is not DayOfWeek.Friday and not DayOfWeek.Saturday;
    }

    private static bool Overlaps(
        DateTime firstStart,
        DateTime firstEnd,
        DateTime secondStart,
        DateTime secondEnd)
    {
        return firstStart < secondEnd &&
               secondStart < firstEnd;
    }

    private static Guid CreateGuid(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }

    private static Guid CreateShiftGuid(DateOnly date, ShiftKind kind)
    {
        var kindValue = kind switch
        {
            ShiftKind.Morning => 1,
            ShiftKind.Afternoon => 2,
            ShiftKind.Night => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        return CreateGuid((date.DayNumber * 10) + kindValue);
    }

    private sealed record PostRunLocalAddIntegrationScenario(
        SchedulePeriod Period,
        IReadOnlyList<Resource> Resources,
        IReadOnlyList<Shift> Shifts,
        IReadOnlyCollection<WorkerSubmission> WorkerSubmissions);

    private sealed record RealSubmittedFormScenario(
        SchedulePeriod Period,
        IReadOnlyList<Resource> Resources,
        IReadOnlyList<Shift> Shifts,
        IReadOnlyCollection<WorkerSubmission> WorkerSubmissions);

    private sealed record CapacityRule(int Min, int Max, bool RequiresPreference);
}
