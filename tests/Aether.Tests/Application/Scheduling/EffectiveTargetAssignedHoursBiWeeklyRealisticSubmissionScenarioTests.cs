using System.Diagnostics;
using System.Text;
using Aether.Application.Scheduling.Contracts;
using Aether.Application.Scheduling.Interfaces;
using Aether.Application.Scheduling.Optimization;
using Aether.Domain.Optimization;
using Xunit.Abstractions;

namespace Aether.Tests.Application.Scheduling;

public sealed class EffectiveTargetAssignedHoursBiWeeklyRealisticSubmissionScenarioTests
{
    private const int ResourceCount = 16;
    private const int DaysInSchedule = 14;
    private const int PopulationSize = 180;
    private const int GenerationCount = 120;
    private const int Seed = 20260615;
    private const double ExpectedTotalEffectiveTargetHours = 736.0;
    private const double HoursTolerance = 0.000001;

    private readonly ITestOutputHelper _output;

    public EffectiveTargetAssignedHoursBiWeeklyRealisticSubmissionScenarioTests(
        ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    [Trait("Category", "SchedulingExperiment")]
    public void Run_ShouldImproveEffectiveTargetAssignedHoursPressure_InBiWeeklyRealisticSubmissionScenario()
    {
        var scenario = CreateScenario();

        Assert.Equal(ResourceCount, scenario.Problem.Resources.Count);
        Assert.Equal(DaysInSchedule * 3, scenario.Problem.Shifts.Count);
        Assert.Equal(ResourceCount, scenario.Problem.ResourceWorkloadDemands.Count);

        AssertRealisticShiftCapacityRules(scenario.Problem);
        AssertTotalCapacityCanSatisfyEffectiveTargets(scenario.Problem);
        AssertEveryShiftHasEnoughSubmittedResources(scenario.Problem);
        AssertMotzeiShabbatSubmissionPoolsAreSeparated(scenario.Problem);

        var deterministic = RunOptimizer(
            "Deterministic",
            new DeterministicScheduleOptimizer(),
            scenario.Problem);

        var initialOnlyDiagnosticsSink = new CollectingDiagnosticsSink();

        var initialOnly = RunOptimizer(
            "Genetic initial only",
            new GeneticScheduleOptimizer(
                populationSize: PopulationSize,
                seed: Seed,
                generationCount: 0,
                eliteCount: 1,
                tournamentSize: 3,
                diagnosticsSink: initialOnlyDiagnosticsSink),
            scenario.Problem,
            initialOnlyDiagnosticsSink.Diagnostics);

        var evolvedDiagnosticsSink = new CollectingDiagnosticsSink();

        var evolved = RunOptimizer(
            "Genetic evolved",
            new GeneticScheduleOptimizer(
                populationSize: PopulationSize,
                seed: Seed,
                generationCount: GenerationCount,
                eliteCount: 1,
                tournamentSize: 3,
                diagnosticsSink: evolvedDiagnosticsSink),
            scenario.Problem,
            evolvedDiagnosticsSink.Diagnostics);

        var deterministicMetrics = GetRunMetrics(scenario, deterministic);
        var initialOnlyMetrics = GetRunMetrics(scenario, initialOnly);
        var evolvedMetrics = GetRunMetrics(scenario, evolved);

        var report = FormatReport(
            scenario,
            deterministic,
            initialOnly,
            evolved);

        _output.WriteLine(report);
        System.Console.WriteLine(report);

        Assert.Contains("Effective Target Assigned Hours Biweekly Realistic Submission Scenario", report);
        Assert.Contains("ResourceTargetSummary:", report);
        Assert.Contains("RequestedPreferredHours", report);
        Assert.Contains("MinimumRequiredHours", report);
        Assert.Contains("EffectiveTargetHours", report);
        Assert.Contains("AssignedHours", report);
        Assert.Contains("GapToTarget", report);
        Assert.Contains("GenerationDiagnostics:", report);
        Assert.Contains("- Generation 0:", report);
        Assert.Contains($"- Generation {GenerationCount}:", report);

        Assert.Equal(1, initialOnly.Diagnostics.Count);
        Assert.Equal(GenerationCount + 1, evolved.Diagnostics.Count);

        Assert.Equal(0, CountViolations(evolved, ConstraintViolationType.ResourceUnavailable));
        Assert.Equal(0, CountViolations(evolved, ConstraintViolationType.ResourceAssignedToOverlappingShifts));
        Assert.Equal(0, CountViolations(evolved, ConstraintViolationType.ShiftUnderstaffed));
        Assert.Equal(0, CountViolations(evolved, ConstraintViolationType.ShiftOverstaffed));
        Assert.Equal(0, CountViolations(evolved, ConstraintViolationType.ShiftSequenceQuotaExceeded));
        Assert.Equal(0, CountViolations(evolved, ConstraintViolationType.ResourceMonthlyNightShiftQuotaExceeded));

        Assert.True(
            evolved.Result.Evaluation.Score.HardViolationCount <=
            initialOnly.Result.Evaluation.Score.HardViolationCount,
            report);

        Assert.True(
            evolved.Result.Evaluation.Score.TotalPenalty <=
            initialOnly.Result.Evaluation.Score.TotalPenalty,
            report);

        Assert.True(
            evolvedMetrics.EffectiveTargetPenalty <=
            initialOnlyMetrics.EffectiveTargetPenalty,
            report);

        Assert.True(
            evolvedMetrics.TotalTargetGapHours <= deterministicMetrics.TotalTargetGapHours,
            report);

        Assert.True(
            CountEffectiveTargetViolations(evolved) <=
            CountEffectiveTargetViolations(initialOnly),
            report);

        var ranker = new ScheduleEvaluationResultRanker();

        Assert.False(
            ranker.IsBetterThan(
                initialOnly.Result.Evaluation,
                evolved.Result.Evaluation),
            report);
    }

    private static ExperimentScenario CreateScenario()
    {
        var resources = Enumerable
            .Range(1, ResourceCount)
            .Select(index => CreateResource($"Guard{index:00}"))
            .ToArray();

        var shifts = CreateBiWeeklyRealisticShifts();

        var availabilityWindows = new List<AvailabilityWindow>();
        var preferences = new List<ResourcePreference>();

        foreach (var resource in resources)
        {
            foreach (var shift in shifts)
            {
                if (!ShouldSubmit(resource, resources, shift))
                {
                    continue;
                }

                AddSubmission(
                    resource,
                    shift,
                    availabilityWindows,
                    preferences);
            }
        }

        var workloadDemands = CreateWorkloadDemands(resources);

        var problem = new SchedulingProblem(
            period: CreateBiWeeklyPeriod(),
            resources: resources,
            shifts: shifts,
            availabilityWindows: availabilityWindows,
            resourcePreferences: preferences,
            minimumAssignedHoursPerResource: 0,
            minimumMorningShiftsPerResourcePerFullWeek: 0,
            minimumAfternoonShiftsPerResourcePerFullWeek: 0,
            resourceWorkloadDemands: workloadDemands);

        var resourceNames = resources.ToDictionary(
            resource => resource.Id,
            resource => resource.Name);

        return new ExperimentScenario(
            Problem: problem,
            Resources: resources,
            Shifts: shifts,
            ResourceNames: resourceNames);
    }

    private static IReadOnlyCollection<ResourceWorkloadDemand> CreateWorkloadDemands(
        IReadOnlyList<Resource> resources)
    {
        return resources
            .Select((resource, index) =>
            {
                var resourceNumber = index + 1;

                var targetHours = resourceNumber switch
                {
                    1 => 72,
                    2 => 64,
                    3 => 64,
                    4 => 64,
                    5 => 56,
                    6 => 48,
                    7 => 64,
                    8 => 56,
                    9 => 56,
                    10 => 48,
                    >= 11 and <= 16 => 24,
                    _ => throw new ArgumentOutOfRangeException(nameof(resourceNumber))
                };

                var minimumHours = resourceNumber switch
                {
                    >= 1 and <= 4 => 40,
                    5 or 6 => 32,
                    >= 7 and <= 9 => 32,
                    10 => 24,
                    >= 11 and <= 16 => 16,
                    _ => throw new ArgumentOutOfRangeException(nameof(resourceNumber))
                };

                return new ResourceWorkloadDemand(
                    resource.Id,
                    requestedPreferredHours: targetHours,
                    minimumRequiredHours: minimumHours);
            })
            .ToArray();
    }

    private static IReadOnlyList<Shift> CreateBiWeeklyRealisticShifts()
    {
        var shifts = new List<Shift>();

        for (var dayOffset = 0; dayOffset < DaysInSchedule; dayOffset++)
        {
            var date = new DateOnly(2026, 5, 31).AddDays(dayOffset);

            shifts.Add(CreateShift(date, ShiftKind.Morning));
            shifts.Add(CreateShift(date, ShiftKind.Afternoon));
            shifts.Add(CreateShift(date, ShiftKind.Night));
        }

        return shifts
            .OrderBy(shift => shift.StartUtc)
            .ToArray();
    }

    private static Shift CreateShift(
        DateOnly date,
        ShiftKind kind)
    {
        var startUtc = GetStartUtc(date, kind);
        var endUtc = GetEndUtc(date, kind);

        var (minResourceCount, maxResourceCount) = GetCapacityRule(date, kind);

        return new Shift(
            Guid.NewGuid(),
            startUtc,
            endUtc,
            kind,
            minResourceCount: minResourceCount,
            maxResourceCount: maxResourceCount,
            requiresPreferenceToAssign: true,
            requiresMinimumWhenPreferenceExists: false,
            nightShiftCategory: GetNightShiftCategory(date, kind));
    }

    private static (int MinResourceCount, int MaxResourceCount) GetCapacityRule(
        DateOnly date,
        ShiftKind kind)
    {
        if (date.DayOfWeek == DayOfWeek.Friday)
        {
            return kind switch
            {
                ShiftKind.Morning => (2, 2),
                ShiftKind.Afternoon => (1, 1),
                ShiftKind.Night => (1, 1),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
        }

        if (date.DayOfWeek == DayOfWeek.Saturday)
        {
            return kind switch
            {
                ShiftKind.Morning => (1, 1),
                ShiftKind.Afternoon => (1, 1),
                ShiftKind.Night => (3, 3),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
        }

        return kind switch
        {
            ShiftKind.Morning => (4, 6),
            ShiftKind.Afternoon => (2, 4),
            ShiftKind.Night => (1, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private static DateTime GetStartUtc(
        DateOnly date,
        ShiftKind kind)
    {
        return kind switch
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
    }

    private static DateTime GetEndUtc(
        DateOnly date,
        ShiftKind kind)
    {
        return kind switch
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
    }

    private static NightShiftCategory? GetNightShiftCategory(
        DateOnly date,
        ShiftKind kind)
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

    private static bool ShouldSubmit(
        Resource resource,
        IReadOnlyList<Resource> resources,
        Shift shift)
    {
        var resourceNumber = Array.FindIndex(
            resources.ToArray(),
            candidate => candidate.Id == resource.Id) + 1;

        if (resourceNumber <= 0)
        {
            return false;
        }

        if (shift.Kind == ShiftKind.Morning)
        {
            return resourceNumber is >= 1 and <= 6;
        }

        if (shift.Kind == ShiftKind.Afternoon)
        {
            return resourceNumber is >= 7 and <= 10;
        }

        if (shift.Kind == ShiftKind.Night &&
            shift.NightShiftCategory == NightShiftCategory.MotzeiShabbatNight)
        {
            return ShouldSubmitMotzeiShabbatNight(resourceNumber, shift);
        }

        if (shift.Kind == ShiftKind.Night)
        {
            return resourceNumber is >= 11 and <= 16;
        }

        return false;
    }

    private static bool ShouldSubmitMotzeiShabbatNight(
        int resourceNumber,
        Shift shift)
    {
        var firstMotzeiShabbatStartUtc = new DateTime(
            2026,
            6,
            6,
            22,
            30,
            0,
            DateTimeKind.Utc);

        if (shift.StartUtc == firstMotzeiShabbatStartUtc)
        {
            return resourceNumber is >= 11 and <= 13;
        }

        return resourceNumber is >= 14 and <= 16;
    }

    private static SchedulePeriod CreateBiWeeklyPeriod()
    {
        return new SchedulePeriod(
            new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc));
    }

    private static Resource CreateResource(string name)
    {
        return new Resource(
            Guid.NewGuid(),
            name,
            hourlyCost: 100m);
    }

    private static void AddSubmission(
        Resource resource,
        Shift shift,
        ICollection<AvailabilityWindow> availabilityWindows,
        ICollection<ResourcePreference> preferences)
    {
        availabilityWindows.Add(new AvailabilityWindow(
            resource.Id,
            shift.StartUtc,
            shift.EndUtc));

        preferences.Add(new ResourcePreference(
            resource.Id,
            shift.StartUtc,
            shift.EndUtc,
            ResourcePreferenceType.Prefer,
            ResourcePreferencePriority.High));
    }

    private static ExperimentRun RunOptimizer(
        string name,
        IScheduleOptimizer optimizer,
        SchedulingProblem problem,
        IReadOnlyList<GeneticGenerationDiagnostic>? diagnostics = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = optimizer.Optimize(problem);
        stopwatch.Stop();

        return new ExperimentRun(
            Name: name,
            Result: result,
            Elapsed: stopwatch.Elapsed,
            Diagnostics: diagnostics ?? []);
    }

    private static void AssertRealisticShiftCapacityRules(
        SchedulingProblem problem)
    {
        Assert.All(problem.Shifts, shift =>
        {
            var date = DateOnly.FromDateTime(shift.StartUtc);

            if (date.DayOfWeek == DayOfWeek.Friday)
            {
                if (shift.Kind == ShiftKind.Morning)
                {
                    Assert.Equal(2, shift.MinResourceCount);
                    Assert.Equal(2, shift.MaxResourceCount);
                }
                else if (shift.Kind == ShiftKind.Afternoon)
                {
                    Assert.Equal(1, shift.MinResourceCount);
                    Assert.Equal(1, shift.MaxResourceCount);
                }
                else if (shift.Kind == ShiftKind.Night)
                {
                    Assert.Equal(1, shift.MinResourceCount);
                    Assert.Equal(1, shift.MaxResourceCount);
                    Assert.Equal(NightShiftCategory.FridayNight, shift.NightShiftCategory);
                }

                return;
            }

            if (date.DayOfWeek == DayOfWeek.Saturday)
            {
                if (shift.Kind == ShiftKind.Morning)
                {
                    Assert.Equal(1, shift.MinResourceCount);
                    Assert.Equal(1, shift.MaxResourceCount);
                }
                else if (shift.Kind == ShiftKind.Afternoon)
                {
                    Assert.Equal(1, shift.MinResourceCount);
                    Assert.Equal(1, shift.MaxResourceCount);
                }
                else if (shift.Kind == ShiftKind.Night)
                {
                    Assert.Equal(3, shift.MinResourceCount);
                    Assert.Equal(3, shift.MaxResourceCount);
                    Assert.Equal(NightShiftCategory.MotzeiShabbatNight, shift.NightShiftCategory);
                }

                return;
            }

            if (shift.Kind == ShiftKind.Morning)
            {
                Assert.Equal(4, shift.MinResourceCount);
                Assert.Equal(6, shift.MaxResourceCount);
            }
            else if (shift.Kind == ShiftKind.Afternoon)
            {
                Assert.Equal(2, shift.MinResourceCount);
                Assert.Equal(4, shift.MaxResourceCount);
            }
            else if (shift.Kind == ShiftKind.Night)
            {
                Assert.Equal(1, shift.MinResourceCount);
                Assert.Equal(1, shift.MaxResourceCount);
                Assert.Equal(NightShiftCategory.Regular, shift.NightShiftCategory);
            }
        });
    }

    private static void AssertTotalCapacityCanSatisfyEffectiveTargets(
        SchedulingProblem problem)
    {
        var totalEffectiveTargetHours = problem.ResourceWorkloadDemands
            .Sum(demand => demand.EffectiveTargetHours);

        var totalMinimumCapacityHours = problem.Shifts
            .Sum(shift => GetShiftHours(shift) * shift.MinResourceCount);

        var totalMaximumCapacityHours = problem.Shifts
            .Sum(shift => GetShiftHours(shift) * shift.MaxResourceCount);

        Assert.Equal(ExpectedTotalEffectiveTargetHours, totalEffectiveTargetHours);

        Assert.True(
            totalMinimumCapacityHours <= totalEffectiveTargetHours,
            $"Minimum capacity {totalMinimumCapacityHours:0.##}h is above target {totalEffectiveTargetHours:0.##}h.");

        Assert.True(
            totalMaximumCapacityHours >= totalEffectiveTargetHours,
            $"Maximum capacity {totalMaximumCapacityHours:0.##}h is below target {totalEffectiveTargetHours:0.##}h.");
    }

    private static void AssertEveryShiftHasEnoughSubmittedResources(
        SchedulingProblem problem)
    {
        Assert.All(problem.Shifts, shift =>
        {
            var submittedResources = problem.ResourcePreferences
                .Where(preference => preference.Type == ResourcePreferenceType.Prefer)
                .Where(preference => Overlaps(
                    preference.StartUtc,
                    preference.EndUtc,
                    shift.StartUtc,
                    shift.EndUtc))
                .Select(preference => preference.ResourceId)
                .Distinct()
                .Count();

            Assert.True(
                submittedResources >= shift.MinResourceCount,
                $"Shift {FormatShift(shift)} has only {submittedResources} submitted resources, but minimum is {shift.MinResourceCount}.");

            Assert.True(
                submittedResources >= shift.MaxResourceCount,
                $"Shift {FormatShift(shift)} has only {submittedResources} submitted resources, but maximum is {shift.MaxResourceCount}.");
        });
    }

    private static void AssertMotzeiShabbatSubmissionPoolsAreSeparated(
        SchedulingProblem problem)
    {
        var motzeiShabbatShifts = problem.Shifts
            .Where(shift => shift.NightShiftCategory == NightShiftCategory.MotzeiShabbatNight)
            .OrderBy(shift => shift.StartUtc)
            .ToArray();

        Assert.Equal(2, motzeiShabbatShifts.Length);

        var firstPool = GetSubmittedResourceIds(problem, motzeiShabbatShifts[0]);
        var secondPool = GetSubmittedResourceIds(problem, motzeiShabbatShifts[1]);

        Assert.Equal(3, firstPool.Count);
        Assert.Equal(3, secondPool.Count);
        Assert.Empty(firstPool.Intersect(secondPool));
    }

    private static IReadOnlySet<Guid> GetSubmittedResourceIds(
        SchedulingProblem problem,
        Shift shift)
    {
        return problem.ResourcePreferences
            .Where(preference => preference.Type == ResourcePreferenceType.Prefer)
            .Where(preference => Overlaps(
                preference.StartUtc,
                preference.EndUtc,
                shift.StartUtc,
                shift.EndUtc))
            .Select(preference => preference.ResourceId)
            .ToHashSet();
    }

    private static RunMetrics GetRunMetrics(
        ExperimentScenario scenario,
        ExperimentRun run)
    {
        var summaries = CreateResourceTargetSummaries(
            scenario,
            run.Result.Candidate);

        var totalTargetGapHours = summaries.Sum(summary =>
            Math.Abs(summary.GapToTarget));

        var totalAssignedHours = summaries.Sum(summary =>
            summary.AssignedHours);

        var totalEffectiveTargetHours = summaries.Sum(summary =>
            summary.EffectiveTargetHours);

        var effectiveTargetPenalty = run.Result.Evaluation.Violations
            .Where(violation =>
                violation.Type == ConstraintViolationType.ResourceEffectiveTargetAssignedHoursBelowTarget ||
                violation.Type == ConstraintViolationType.ResourceEffectiveTargetAssignedHoursAboveTarget)
            .Sum(violation =>
            {
                var weight = violation.Type == ConstraintViolationType.ResourceEffectiveTargetAssignedHoursBelowTarget
                    ? 10
                    : 5;

                return (int)Math.Ceiling((violation.Magnitude ?? 0) * weight);
            });

        return new RunMetrics(
            TotalAssignedHours: totalAssignedHours,
            TotalEffectiveTargetHours: totalEffectiveTargetHours,
            TotalTargetGapHours: totalTargetGapHours,
            EffectiveTargetPenalty: effectiveTargetPenalty);
    }

    private static IReadOnlyList<ResourceTargetSummary> CreateResourceTargetSummaries(
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        var shiftsById = scenario.Shifts.ToDictionary(shift => shift.Id);
        var demandByResourceId = scenario.Problem.ResourceWorkloadDemands
            .ToDictionary(demand => demand.ResourceId);

        return scenario.Resources
            .Select(resource =>
            {
                var assignments = candidate.Assignments
                    .Where(assignment => assignment.ResourceId == resource.Id)
                    .ToArray();

                var assignedShifts = assignments
                    .Select(assignment => shiftsById[assignment.ShiftId])
                    .ToArray();

                var assignedHours = assignedShifts.Sum(GetShiftHours);
                var demand = demandByResourceId[resource.Id];
                var gapToTarget = assignedHours - demand.EffectiveTargetHours;

                return new ResourceTargetSummary(
                    ResourceName: resource.Name,
                    RequestedPreferredHours: demand.RequestedPreferredHours,
                    MinimumRequiredHours: demand.MinimumRequiredHours,
                    EffectiveTargetHours: demand.EffectiveTargetHours,
                    AssignedHours: assignedHours,
                    GapToTarget: gapToTarget,
                    BelowTarget: assignedHours + HoursTolerance < demand.EffectiveTargetHours,
                    AboveTarget: assignedHours > demand.EffectiveTargetHours + HoursTolerance,
                    AssignmentCount: assignments.Length,
                    MorningAssignments: assignedShifts.Count(shift => shift.Kind == ShiftKind.Morning),
                    AfternoonAssignments: assignedShifts.Count(shift => shift.Kind == ShiftKind.Afternoon),
                    NightAssignments: assignedShifts.Count(shift => shift.Kind == ShiftKind.Night),
                    FridayMorningAssignments: assignedShifts.Count(shift =>
                        shift.StartUtc.DayOfWeek == DayOfWeek.Friday &&
                        shift.Kind == ShiftKind.Morning),
                    FridayAfternoonAssignments: assignedShifts.Count(shift =>
                        shift.StartUtc.DayOfWeek == DayOfWeek.Friday &&
                        shift.Kind == ShiftKind.Afternoon),
                    FridayNightAssignments: assignedShifts.Count(shift =>
                        shift.StartUtc.DayOfWeek == DayOfWeek.Friday &&
                        shift.Kind == ShiftKind.Night),
                    SaturdayMorningAssignments: assignedShifts.Count(shift =>
                        shift.StartUtc.DayOfWeek == DayOfWeek.Saturday &&
                        shift.Kind == ShiftKind.Morning),
                    SaturdayAfternoonAssignments: assignedShifts.Count(shift =>
                        shift.StartUtc.DayOfWeek == DayOfWeek.Saturday &&
                        shift.Kind == ShiftKind.Afternoon),
                    MotzeiShabbatNightAssignments: assignedShifts.Count(shift =>
                        shift.NightShiftCategory == NightShiftCategory.MotzeiShabbatNight));
            })
            .OrderBy(summary => summary.ResourceName)
            .ToArray();
    }

    private static string FormatReport(
        ExperimentScenario scenario,
        params ExperimentRun[] runs)
    {
        var builder = new StringBuilder();

        builder.AppendLine("Effective Target Assigned Hours Biweekly Realistic Submission Scenario");
        builder.AppendLine($"Resources: {scenario.Resources.Count}");
        builder.AppendLine($"Days: {DaysInSchedule}");
        builder.AppendLine($"Shifts: {scenario.Shifts.Count}");
        builder.AppendLine("Scenario rules:");
        builder.AppendLine("- Regular morning: min=4, max=6");
        builder.AppendLine("- Regular afternoon: min=2, max=4");
        builder.AppendLine("- Regular night: min=1, max=1");
        builder.AppendLine("- Friday morning with demand: min=2, max=2");
        builder.AppendLine("- Friday afternoon with demand: min=1, max=1");
        builder.AppendLine("- Friday night: min=1, max=1");
        builder.AppendLine("- Saturday morning: min=1, max=1");
        builder.AppendLine("- Saturday afternoon: min=1, max=1");
        builder.AppendLine("- Motzei Shabbat night: min=3, max=3");
        builder.AppendLine();

        AppendScenarioCapacitySummary(builder, scenario);
        builder.AppendLine();

        foreach (var run in runs)
        {
            AppendRun(builder, scenario, run);
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static void AppendScenarioCapacitySummary(
        StringBuilder builder,
        ExperimentScenario scenario)
    {
        var totalMinimumCapacityHours = scenario.Shifts
            .Sum(shift => GetShiftHours(shift) * shift.MinResourceCount);

        var totalMaximumCapacityHours = scenario.Shifts
            .Sum(shift => GetShiftHours(shift) * shift.MaxResourceCount);

        var totalEffectiveTargetHours = scenario.Problem.ResourceWorkloadDemands
            .Sum(demand => demand.EffectiveTargetHours);

        builder.AppendLine("ScenarioCapacity:");
        builder.AppendLine($"- TotalMinimumCapacityHours: {totalMinimumCapacityHours:0.##}");
        builder.AppendLine($"- TotalMaximumCapacityHours: {totalMaximumCapacityHours:0.##}");
        builder.AppendLine($"- TotalEffectiveTargetHours: {totalEffectiveTargetHours:0.##}");
    }

    private static void AppendRun(
        StringBuilder builder,
        ExperimentScenario scenario,
        ExperimentRun run)
    {
        var evaluation = run.Result.Evaluation;
        var metrics = GetRunMetrics(scenario, run);

        builder.AppendLine(run.Name);
        builder.AppendLine($"RuntimeMs: {run.Elapsed.TotalMilliseconds:0.00}");
        builder.AppendLine($"IsFeasible: {evaluation.IsFeasible}");
        builder.AppendLine($"Score: {evaluation.Score.Value}");
        builder.AppendLine($"TotalPenalty: {evaluation.Score.TotalPenalty}");
        builder.AppendLine($"HardViolationCount: {evaluation.Score.HardViolationCount}");
        builder.AppendLine($"SoftViolationCount: {evaluation.Score.SoftViolationCount}");
        builder.AppendLine($"Assignments.Count: {run.Result.Candidate.Assignments.Count}");
        builder.AppendLine($"TotalAssignedHours: {metrics.TotalAssignedHours:0.##}");
        builder.AppendLine($"TotalEffectiveTargetHours: {metrics.TotalEffectiveTargetHours:0.##}");
        builder.AppendLine($"TotalTargetGapHours: {metrics.TotalTargetGapHours:0.##}");
        builder.AppendLine($"EffectiveTargetPenalty: {metrics.EffectiveTargetPenalty}");

        builder.AppendLine($"ResourceMinimumAssignedHoursNotMetCount: {CountViolations(run, ConstraintViolationType.ResourceMinimumAssignedHoursNotMet)}");
        builder.AppendLine($"EffectiveBelowTargetViolationCount: {CountViolations(run, ConstraintViolationType.ResourceEffectiveTargetAssignedHoursBelowTarget)}");
        builder.AppendLine($"EffectiveAboveTargetViolationCount: {CountViolations(run, ConstraintViolationType.ResourceEffectiveTargetAssignedHoursAboveTarget)}");
        builder.AppendLine($"ResourceUnavailableCount: {CountViolations(run, ConstraintViolationType.ResourceUnavailable)}");
        builder.AppendLine($"ResourceAssignedToOverlappingShiftsCount: {CountViolations(run, ConstraintViolationType.ResourceAssignedToOverlappingShifts)}");
        builder.AppendLine($"ShiftUnderstaffedCount: {CountViolations(run, ConstraintViolationType.ShiftUnderstaffed)}");
        builder.AppendLine($"ShiftOverstaffedCount: {CountViolations(run, ConstraintViolationType.ShiftOverstaffed)}");
        builder.AppendLine($"ShiftSequenceQuotaExceededCount: {CountViolations(run, ConstraintViolationType.ShiftSequenceQuotaExceeded)}");
        builder.AppendLine($"ResourceMonthlyNightShiftQuotaExceededCount: {CountViolations(run, ConstraintViolationType.ResourceMonthlyNightShiftQuotaExceeded)}");

        AppendViolations(builder, evaluation);
        AppendResourceTargetSummary(builder, scenario, run.Result.Candidate);
        AppendAssignmentsByShift(builder, scenario, run.Result.Candidate);
        AppendGenerationDiagnostics(builder, run);
    }

    private static void AppendViolations(
        StringBuilder builder,
        ScheduleEvaluationResult evaluation)
    {
        builder.AppendLine("ViolationsByType:");

        var groups = evaluation.Violations
            .GroupBy(violation => violation.Type)
            .OrderBy(group => group.Key.ToString())
            .ToArray();

        if (groups.Length == 0)
        {
            builder.AppendLine("- none");
            return;
        }

        foreach (var group in groups)
        {
            builder.AppendLine($"- {group.Key}: {group.Count()}");
        }
    }

    private static void AppendResourceTargetSummary(
        StringBuilder builder,
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        builder.AppendLine("ResourceTargetSummary:");

        foreach (var summary in CreateResourceTargetSummaries(scenario, candidate))
        {
            builder.AppendLine(
                $"- {summary.ResourceName}: " +
                $"RequestedPreferredHours={summary.RequestedPreferredHours:0.##}, " +
                $"MinimumRequiredHours={summary.MinimumRequiredHours:0.##}, " +
                $"EffectiveTargetHours={summary.EffectiveTargetHours:0.##}, " +
                $"AssignedHours={summary.AssignedHours:0.##}, " +
                $"GapToTarget={summary.GapToTarget:0.##}, " +
                $"BelowTarget={summary.BelowTarget}, " +
                $"AboveTarget={summary.AboveTarget}, " +
                $"Assignments={summary.AssignmentCount}, " +
                $"Morning={summary.MorningAssignments}, " +
                $"Afternoon={summary.AfternoonAssignments}, " +
                $"Night={summary.NightAssignments}, " +
                $"FridayMorning={summary.FridayMorningAssignments}, " +
                $"FridayAfternoon={summary.FridayAfternoonAssignments}, " +
                $"FridayNight={summary.FridayNightAssignments}, " +
                $"SaturdayMorning={summary.SaturdayMorningAssignments}, " +
                $"SaturdayAfternoon={summary.SaturdayAfternoonAssignments}, " +
                $"MotzeiShabbatNight={summary.MotzeiShabbatNightAssignments}");
        }
    }

    private static void AppendAssignmentsByShift(
        StringBuilder builder,
        ExperimentScenario scenario,
        ScheduleCandidate candidate)
    {
        builder.AppendLine("AssignmentsByShift:");

        foreach (var shift in scenario.Shifts.OrderBy(shift => shift.StartUtc))
        {
            var assignments = candidate.Assignments
                .Where(assignment => assignment.ShiftId == shift.Id)
                .Select(assignment => scenario.ResourceNames[assignment.ResourceId])
                .OrderBy(name => name)
                .ToArray();

            var assignedText = assignments.Length == 0
                ? "unassigned"
                : string.Join(", ", assignments);

            builder.AppendLine(
                $"- {FormatShift(shift)} min={shift.MinResourceCount}, max={shift.MaxResourceCount} -> {assignedText}");
        }
    }

    private static void AppendGenerationDiagnostics(
        StringBuilder builder,
        ExperimentRun run)
    {
        if (run.Diagnostics.Count == 0)
        {
            return;
        }

        builder.AppendLine("GenerationDiagnostics:");

        foreach (var diagnostic in run.Diagnostics)
        {
            if (diagnostic.GenerationIndex != 0 &&
                diagnostic.GenerationIndex % 20 != 0 &&
                diagnostic.GenerationIndex != GenerationCount)
            {
                continue;
            }

            builder.AppendLine(
                $"- Generation {diagnostic.GenerationIndex}: " +
                $"PopulationSize={diagnostic.PopulationSize}, " +
                $"FeasibleCandidates={diagnostic.FeasibleCandidateCount}, " +
                $"GenerationBestScoreValue={diagnostic.BestScoreValue}, " +
                $"GenerationBestTotalPenalty={diagnostic.BestTotalPenalty}, " +
                $"GenerationBestHardViolationCount={diagnostic.BestHardViolationCount}, " +
                $"GenerationBestSoftViolationCount={diagnostic.BestSoftViolationCount}, " +
                $"BestSoFarScoreValue={diagnostic.BestSoFarScoreValue}, " +
                $"BestSoFarTotalPenalty={diagnostic.BestSoFarTotalPenalty}, " +
                $"BestSoFarHardViolationCount={diagnostic.BestSoFarHardViolationCount}, " +
                $"BestSoFarSoftViolationCount={diagnostic.BestSoFarSoftViolationCount}");
        }
    }

    private static int CountViolations(
        ExperimentRun run,
        ConstraintViolationType type)
    {
        return run.Result.Evaluation.Violations.Count(
            violation => violation.Type == type);
    }

    private static int CountEffectiveTargetViolations(
        ExperimentRun run)
    {
        return run.Result.Evaluation.Violations.Count(violation =>
            violation.Type == ConstraintViolationType.ResourceEffectiveTargetAssignedHoursBelowTarget ||
            violation.Type == ConstraintViolationType.ResourceEffectiveTargetAssignedHoursAboveTarget);
    }

    private static double GetShiftHours(Shift shift)
    {
        return (shift.EndUtc - shift.StartUtc).TotalHours;
    }

    private static bool Overlaps(
        DateTime firstStartUtc,
        DateTime firstEndUtc,
        DateTime secondStartUtc,
        DateTime secondEndUtc)
    {
        return firstStartUtc < secondEndUtc &&
               secondStartUtc < firstEndUtc;
    }

    private static string FormatShift(Shift shift)
    {
        var category = shift.NightShiftCategory is null
            ? ""
            : $" {shift.NightShiftCategory}";

        return $"{shift.Kind}{category} {shift.StartUtc:yyyy-MM-dd HH:mm}-{shift.EndUtc:yyyy-MM-dd HH:mm} UTC";
    }

    private sealed record ExperimentScenario(
        SchedulingProblem Problem,
        IReadOnlyList<Resource> Resources,
        IReadOnlyList<Shift> Shifts,
        IReadOnlyDictionary<Guid, string> ResourceNames);

    private sealed record ExperimentRun(
        string Name,
        ScheduleOptimizationResult Result,
        TimeSpan Elapsed,
        IReadOnlyList<GeneticGenerationDiagnostic> Diagnostics);

    private sealed record RunMetrics(
        double TotalAssignedHours,
        double TotalEffectiveTargetHours,
        double TotalTargetGapHours,
        int EffectiveTargetPenalty);

    private sealed record ResourceTargetSummary(
        string ResourceName,
        double RequestedPreferredHours,
        double MinimumRequiredHours,
        double EffectiveTargetHours,
        double AssignedHours,
        double GapToTarget,
        bool BelowTarget,
        bool AboveTarget,
        int AssignmentCount,
        int MorningAssignments,
        int AfternoonAssignments,
        int NightAssignments,
        int FridayMorningAssignments,
        int FridayAfternoonAssignments,
        int FridayNightAssignments,
        int SaturdayMorningAssignments,
        int SaturdayAfternoonAssignments,
        int MotzeiShabbatNightAssignments);
}
