using System.Text;
using Aether.Application.Scheduling.Optimization;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class IgnoredAvoidBurdenCleanGeneticDiagnosticTests
{
    private const int PopulationSize = 120;
    private const int GenerationCount = 30;
    private const int Seed = 20260603;

    [Fact]
    [Trait("Category", "SchedulingExperiment")]
    public void CleanGeneticOptimizer_ShouldPrintIgnoredAvoidBurdenPenaltyWeightDiagnostic()
    {
        var problem = CreateScientificIgnoredAvoidPressureProblem();

        var variants = new[]
        {
            CreateVariant("AvoidBurden0", 0),
            CreateVariant("AvoidBurden10", 10),
            CreateVariant("AvoidBurden25", 25),
            CreateVariant("AvoidBurden50", 50)
        };

        var report = new StringBuilder();

        report.AppendLine("Stage 7.7F.2 Clean GA Ignored Avoid Burden Penalty Weight Diagnostic");
        report.AppendLine("Scenario: Scientific ignored-avoid pressure scenario");
        report.AppendLine("Mode: Clean GA");
        report.AppendLine("Variant: Variant C default scoring policy");
        report.AppendLine($"Seed: {Seed}");
        report.AppendLine($"PopulationSize: {PopulationSize}");
        report.AppendLine($"GenerationCount: {GenerationCount}");
        report.AppendLine($"ResourceCount: {problem.Resources.Count}");
        report.AppendLine($"ShiftCount: {problem.Shifts.Count}");
        report.AppendLine($"TotalMinimumCapacityHours: {CalculateTotalMinimumCapacityHours(problem):0.##}");
        report.AppendLine($"TotalMaximumCapacityHours: {CalculateTotalMaximumCapacityHours(problem):0.##}");
        report.AppendLine($"TotalEffectiveTargetHours: {problem.ResourceWorkloadDemands.Sum(demand => demand.EffectiveTargetHours):0.##}");
        report.AppendLine();

        foreach (var variant in variants)
        {
            var diagnosticsSink = new CollectingGeneticOptimizerDiagnosticsSink();

            var optimizer = new GeneticScheduleOptimizer(
                populationSize: PopulationSize,
                seed: Seed,
                generationCount: GenerationCount,
                eliteCount: 1,
                tournamentSize: 3,
                diagnosticsSink: diagnosticsSink,
                evolutionMode: GeneticEvolutionMode.Clean,
                scoringWeights: variant.Weights);

            var result = optimizer.Optimize(problem);

            Assert.True(result.Evaluation.IsFeasible);
            Assert.Equal(GenerationCount + 1, diagnosticsSink.Diagnostics.Count);
            Assert.True(
                diagnosticsSink.Diagnostics[^1].BestSoFarTotalPenalty <=
                diagnosticsSink.Diagnostics[0].BestSoFarTotalPenalty);

            var assignedHours = CalculateAssignedHoursByResource(problem, result.Candidate);
            var targetGap = CalculateTargetGap(problem, assignedHours);
            var ignoredAvoidCounts = CalculateIgnoredAvoidAssignmentCountsByResource(problem, result.Candidate);
            var burdenViolations = result.Evaluation.Violations
                .Where(violation => violation.Type == ConstraintViolationType.ResourceIgnoredAvoidPreferenceBurden)
                .ToArray();

            var ignoredAvoidViolationCount = CountViolations(
                result.Evaluation,
                ConstraintViolationType.IgnoredAvoidPreference);

            var burdenPenalty = new ScheduleScoreCalculator(variant.Weights)
                .Calculate(burdenViolations)
                .TotalPenalty;

            Assert.Equal(
                ignoredAvoidCounts.Count(pair => pair.Value > 0),
                burdenViolations.Length);

            report.AppendLine("IgnoredAvoidBurdenVariantSummary:");
            report.AppendLine($"VariantName: {variant.Name}");
            report.AppendLine($"AvoidBurdenPenaltyPerHour: {variant.Weights.ResourceIgnoredAvoidPreferenceBurdenPenaltyPerHour}");
            report.AppendLine($"IsFeasible: {result.Evaluation.IsFeasible}");
            report.AppendLine($"TotalPenalty: {result.Evaluation.Score.TotalPenalty}");
            report.AppendLine($"HardViolationCount: {result.Evaluation.Score.HardViolationCount}");
            report.AppendLine($"SoftViolationCount: {result.Evaluation.Score.SoftViolationCount}");
            report.AppendLine($"Assignments.Count: {result.Candidate.Assignments.Count}");
            report.AppendLine($"TotalAssignedHours: {assignedHours.Values.Sum():0.##}");
            report.AppendLine($"TotalOverTargetHours: {targetGap.TotalOverTargetHours:0.##}");
            report.AppendLine($"TotalUnderTargetHours: {targetGap.TotalUnderTargetHours:0.##}");
            report.AppendLine($"IgnoredAvoidPreferenceViolationCount: {ignoredAvoidViolationCount}");
            report.AppendLine($"EstimatedIgnoredAvoidPreferencePenalty: {ignoredAvoidViolationCount * variant.Weights.IgnoredAvoidPreferencePenalty}");
            report.AppendLine($"TotalIgnoredAvoidAssignments: {ignoredAvoidCounts.Values.Sum()}");
            report.AppendLine($"MaxIgnoredAvoidAssignmentsForSingleResource: {ignoredAvoidCounts.Values.Max()}");
            report.AppendLine($"AverageIgnoredAvoidAssignmentsPerResource: {ignoredAvoidCounts.Values.Average():0.##}");
            report.AppendLine($"ResourcesWithIgnoredAvoidAssignmentsCount: {ignoredAvoidCounts.Count(pair => pair.Value > 0)}");
            report.AppendLine($"ResourceIgnoredAvoidPreferenceBurdenViolationCount: {burdenViolations.Length}");
            report.AppendLine($"TotalIgnoredAvoidBurdenMagnitude: {burdenViolations.Sum(violation => violation.Magnitude ?? 0):0.##}");
            report.AppendLine($"MaxIgnoredAvoidBurdenMagnitudeForSingleResource: {burdenViolations.Max(violation => violation.Magnitude ?? 0):0.##}");
            report.AppendLine($"EstimatedIgnoredAvoidBurdenPenalty: {burdenPenalty}");
            report.AppendLine($"FirstBestSoFarTotalPenalty: {diagnosticsSink.Diagnostics[0].BestSoFarTotalPenalty}");
            report.AppendLine($"LastBestSoFarTotalPenalty: {diagnosticsSink.Diagnostics[^1].BestSoFarTotalPenalty}");
            report.AppendLine($"Improvement: {diagnosticsSink.Diagnostics[0].BestSoFarTotalPenalty - diagnosticsSink.Diagnostics[^1].BestSoFarTotalPenalty}");
            report.AppendLine($"FirstFeasibleCandidateCount: {diagnosticsSink.Diagnostics[0].FeasibleCandidateCount}");
            report.AppendLine($"LastFeasibleCandidateCount: {diagnosticsSink.Diagnostics[^1].FeasibleCandidateCount}");
            report.AppendLine();
        }

        var output = report.ToString();

        System.Console.WriteLine(output);

        Assert.Contains("Stage 7.7F.2 Clean GA Ignored Avoid Burden Penalty Weight Diagnostic", output);
        Assert.Contains("AvoidBurden0", output);
        Assert.Contains("AvoidBurden10", output);
        Assert.Contains("AvoidBurden25", output);
        Assert.Contains("AvoidBurden50", output);
        Assert.Contains("ResourceIgnoredAvoidPreferenceBurdenViolationCount:", output);
        Assert.Contains("MaxIgnoredAvoidAssignmentsForSingleResource:", output);
        Assert.Contains("EstimatedIgnoredAvoidBurdenPenalty:", output);
    }


    [Fact]
    [Trait("Category", "SchedulingExperiment")]
    public void CleanGeneticOptimizer_ShouldPrintIgnoredAvoidBurdenPenaltyMultiSeedDiagnostic()
    {
        var problem = CreateScientificIgnoredAvoidPressureProblem();

        var seeds = new[]
        {
            20260603,
            20260604,
            20260605,
            20260606,
            20260607
        };

        var variants = new[]
        {
            CreateVariant("AvoidBurden0", 0),
            CreateVariant("AvoidBurden25", 25),
            CreateVariant("AvoidBurden50", 50)
        };

        var summaries = new List<MultiSeedSummary>();
        var report = new StringBuilder();

        report.AppendLine("Stage 7.7F.3 Clean GA Ignored Avoid Burden Multi-Seed Stability Diagnostic");
        report.AppendLine("Scenario: Scientific ignored-avoid pressure scenario");
        report.AppendLine("Mode: Clean GA");
        report.AppendLine("Variant: Variant C default scoring policy");
        report.AppendLine($"PopulationSize: {PopulationSize}");
        report.AppendLine($"GenerationCount: {GenerationCount}");
        report.AppendLine($"SeedCount: {seeds.Length}");
        report.AppendLine($"VariantCount: {variants.Length}");
        report.AppendLine($"ResourceCount: {problem.Resources.Count}");
        report.AppendLine($"ShiftCount: {problem.Shifts.Count}");
        report.AppendLine($"TotalMinimumCapacityHours: {CalculateTotalMinimumCapacityHours(problem):0.##}");
        report.AppendLine($"TotalMaximumCapacityHours: {CalculateTotalMaximumCapacityHours(problem):0.##}");
        report.AppendLine($"TotalEffectiveTargetHours: {problem.ResourceWorkloadDemands.Sum(demand => demand.EffectiveTargetHours):0.##}");
        report.AppendLine();

        foreach (var variant in variants)
        {
            foreach (var seed in seeds)
            {
                var diagnosticsSink = new CollectingGeneticOptimizerDiagnosticsSink();

                var optimizer = new GeneticScheduleOptimizer(
                    populationSize: PopulationSize,
                    seed: seed,
                    generationCount: GenerationCount,
                    eliteCount: 1,
                    tournamentSize: 3,
                    diagnosticsSink: diagnosticsSink,
                    evolutionMode: GeneticEvolutionMode.Clean,
                    scoringWeights: variant.Weights);

                var result = optimizer.Optimize(problem);

                Assert.True(result.Evaluation.IsFeasible);
                Assert.Equal(GenerationCount + 1, diagnosticsSink.Diagnostics.Count);
                Assert.True(
                    diagnosticsSink.Diagnostics[^1].BestSoFarTotalPenalty <=
                    diagnosticsSink.Diagnostics[0].BestSoFarTotalPenalty);

                var assignedHours = CalculateAssignedHoursByResource(problem, result.Candidate);
                var targetGap = CalculateTargetGap(problem, assignedHours);
                var ignoredAvoidCounts = CalculateIgnoredAvoidAssignmentCountsByResource(problem, result.Candidate);
                var burdenViolations = result.Evaluation.Violations
                    .Where(violation => violation.Type == ConstraintViolationType.ResourceIgnoredAvoidPreferenceBurden)
                    .ToArray();

                var burdenPenalty = new ScheduleScoreCalculator(variant.Weights)
                    .Calculate(burdenViolations)
                    .TotalPenalty;

                Assert.Equal(
                    ignoredAvoidCounts.Count(pair => pair.Value > 0),
                    burdenViolations.Length);

                var summary = new MultiSeedSummary(
                    VariantName: variant.Name,
                    Seed: seed,
                    IsFeasible: result.Evaluation.IsFeasible,
                    TotalPenalty: result.Evaluation.Score.TotalPenalty,
                    HardViolationCount: result.Evaluation.Score.HardViolationCount,
                    SoftViolationCount: result.Evaluation.Score.SoftViolationCount,
                    TotalAssignedHours: assignedHours.Values.Sum(),
                    TotalOverTargetHours: targetGap.TotalOverTargetHours,
                    TotalUnderTargetHours: targetGap.TotalUnderTargetHours,
                    IgnoredAvoidPreferenceViolationCount: CountViolations(
                        result.Evaluation,
                        ConstraintViolationType.IgnoredAvoidPreference),
                    TotalIgnoredAvoidAssignments: ignoredAvoidCounts.Values.Sum(),
                    MaxIgnoredAvoidAssignmentsForSingleResource: ignoredAvoidCounts.Values.Max(),
                    AverageIgnoredAvoidAssignmentsPerResource: ignoredAvoidCounts.Values.Average(),
                    ResourcesWithIgnoredAvoidAssignmentsCount: ignoredAvoidCounts.Count(pair => pair.Value > 0),
                    ResourceIgnoredAvoidPreferenceBurdenViolationCount: burdenViolations.Length,
                    TotalIgnoredAvoidBurdenMagnitude: burdenViolations.Sum(violation => violation.Magnitude ?? 0),
                    MaxIgnoredAvoidBurdenMagnitudeForSingleResource: burdenViolations.Length == 0
                        ? 0
                        : burdenViolations.Max(violation => violation.Magnitude ?? 0),
                    EstimatedIgnoredAvoidBurdenPenalty: burdenPenalty,
                    FirstBestSoFarTotalPenalty: diagnosticsSink.Diagnostics[0].BestSoFarTotalPenalty,
                    LastBestSoFarTotalPenalty: diagnosticsSink.Diagnostics[^1].BestSoFarTotalPenalty,
                    Improvement: diagnosticsSink.Diagnostics[0].BestSoFarTotalPenalty -
                                 diagnosticsSink.Diagnostics[^1].BestSoFarTotalPenalty,
                    FirstFeasibleCandidateCount: diagnosticsSink.Diagnostics[0].FeasibleCandidateCount,
                    LastFeasibleCandidateCount: diagnosticsSink.Diagnostics[^1].FeasibleCandidateCount);

                summaries.Add(summary);

                report.AppendLine("IgnoredAvoidBurdenSeedRunSummary:");
                report.AppendLine($"VariantName: {summary.VariantName}");
                report.AppendLine($"Seed: {summary.Seed}");
                report.AppendLine($"AvoidBurdenPenaltyPerHour: {variant.Weights.ResourceIgnoredAvoidPreferenceBurdenPenaltyPerHour}");
                report.AppendLine($"IsFeasible: {summary.IsFeasible}");
                report.AppendLine($"TotalPenalty: {summary.TotalPenalty}");
                report.AppendLine($"HardViolationCount: {summary.HardViolationCount}");
                report.AppendLine($"SoftViolationCount: {summary.SoftViolationCount}");
                report.AppendLine($"TotalAssignedHours: {summary.TotalAssignedHours:0.##}");
                report.AppendLine($"TotalOverTargetHours: {summary.TotalOverTargetHours:0.##}");
                report.AppendLine($"TotalUnderTargetHours: {summary.TotalUnderTargetHours:0.##}");
                report.AppendLine($"IgnoredAvoidPreferenceViolationCount: {summary.IgnoredAvoidPreferenceViolationCount}");
                report.AppendLine($"TotalIgnoredAvoidAssignments: {summary.TotalIgnoredAvoidAssignments}");
                report.AppendLine($"MaxIgnoredAvoidAssignmentsForSingleResource: {summary.MaxIgnoredAvoidAssignmentsForSingleResource}");
                report.AppendLine($"AverageIgnoredAvoidAssignmentsPerResource: {summary.AverageIgnoredAvoidAssignmentsPerResource:0.##}");
                report.AppendLine($"ResourcesWithIgnoredAvoidAssignmentsCount: {summary.ResourcesWithIgnoredAvoidAssignmentsCount}");
                report.AppendLine($"ResourceIgnoredAvoidPreferenceBurdenViolationCount: {summary.ResourceIgnoredAvoidPreferenceBurdenViolationCount}");
                report.AppendLine($"TotalIgnoredAvoidBurdenMagnitude: {summary.TotalIgnoredAvoidBurdenMagnitude:0.##}");
                report.AppendLine($"MaxIgnoredAvoidBurdenMagnitudeForSingleResource: {summary.MaxIgnoredAvoidBurdenMagnitudeForSingleResource:0.##}");
                report.AppendLine($"EstimatedIgnoredAvoidBurdenPenalty: {summary.EstimatedIgnoredAvoidBurdenPenalty}");
                report.AppendLine($"FirstBestSoFarTotalPenalty: {summary.FirstBestSoFarTotalPenalty}");
                report.AppendLine($"LastBestSoFarTotalPenalty: {summary.LastBestSoFarTotalPenalty}");
                report.AppendLine($"Improvement: {summary.Improvement}");
                report.AppendLine($"FirstFeasibleCandidateCount: {summary.FirstFeasibleCandidateCount}");
                report.AppendLine($"LastFeasibleCandidateCount: {summary.LastFeasibleCandidateCount}");
                report.AppendLine();
            }
        }

        report.AppendLine("IgnoredAvoidBurdenMultiSeedAggregateSummary:");
        report.AppendLine($"RunCount: {summaries.Count}");
        report.AppendLine($"AnyRunHasHardViolations: {summaries.Any(summary => summary.HardViolationCount > 0)}");
        report.AppendLine($"AllRunsFeasible: {summaries.All(summary => summary.IsFeasible)}");
        report.AppendLine($"AllRunsImprovedBestSoFar: {summaries.All(summary => summary.Improvement >= 0)}");
        report.AppendLine();

        foreach (var group in summaries.GroupBy(summary => summary.VariantName).OrderBy(group => group.Key))
        {
            report.AppendLine("VariantAggregateSummary:");
            report.AppendLine($"VariantName: {group.Key}");
            report.AppendLine($"SeedCount: {group.Count()}");
            report.AppendLine($"AverageTotalPenalty: {group.Average(summary => summary.TotalPenalty):0.##}");
            report.AppendLine($"MinTotalPenalty: {group.Min(summary => summary.TotalPenalty)}");
            report.AppendLine($"MaxTotalPenalty: {group.Max(summary => summary.TotalPenalty)}");
            report.AppendLine($"AverageTotalAssignedHours: {group.Average(summary => summary.TotalAssignedHours):0.##}");
            report.AppendLine($"AverageTotalOverTargetHours: {group.Average(summary => summary.TotalOverTargetHours):0.##}");
            report.AppendLine($"AverageTotalUnderTargetHours: {group.Average(summary => summary.TotalUnderTargetHours):0.##}");
            report.AppendLine($"AverageIgnoredAvoidPreferenceViolationCount: {group.Average(summary => summary.IgnoredAvoidPreferenceViolationCount):0.##}");
            report.AppendLine($"AverageTotalIgnoredAvoidAssignments: {group.Average(summary => summary.TotalIgnoredAvoidAssignments):0.##}");
            report.AppendLine($"AverageMaxIgnoredAvoidAssignmentsForSingleResource: {group.Average(summary => summary.MaxIgnoredAvoidAssignmentsForSingleResource):0.##}");
            report.AppendLine($"AverageResourcesWithIgnoredAvoidAssignmentsCount: {group.Average(summary => summary.ResourcesWithIgnoredAvoidAssignmentsCount):0.##}");
            report.AppendLine($"AverageTotalIgnoredAvoidBurdenMagnitude: {group.Average(summary => summary.TotalIgnoredAvoidBurdenMagnitude):0.##}");
            report.AppendLine($"AverageMaxIgnoredAvoidBurdenMagnitudeForSingleResource: {group.Average(summary => summary.MaxIgnoredAvoidBurdenMagnitudeForSingleResource):0.##}");
            report.AppendLine($"AverageEstimatedIgnoredAvoidBurdenPenalty: {group.Average(summary => summary.EstimatedIgnoredAvoidBurdenPenalty):0.##}");
            report.AppendLine($"AverageImprovement: {group.Average(summary => summary.Improvement):0.##}");
            report.AppendLine($"MinLastFeasibleCandidateCount: {group.Min(summary => summary.LastFeasibleCandidateCount)}");
            report.AppendLine($"MaxLastFeasibleCandidateCount: {group.Max(summary => summary.LastFeasibleCandidateCount)}");
            report.AppendLine();
        }

        var output = report.ToString();

        System.Console.WriteLine(output);

        Assert.Contains("Stage 7.7F.3 Clean GA Ignored Avoid Burden Multi-Seed Stability Diagnostic", output);
        Assert.Contains("AvoidBurden0", output);
        Assert.Contains("AvoidBurden25", output);
        Assert.Contains("AvoidBurden50", output);
        Assert.Contains("IgnoredAvoidBurdenMultiSeedAggregateSummary:", output);
        Assert.Contains("VariantAggregateSummary:", output);
    }

    private static ScoringVariant CreateVariant(
        string name,
        int ignoredAvoidBurdenPenaltyPerHour)
    {
        return new ScoringVariant(
            name,
            ScheduleScoringWeights.CreateDefault() with
            {
                ResourceIgnoredAvoidPreferenceBurdenPenaltyPerHour = ignoredAvoidBurdenPenaltyPerHour
            });
    }

    private static SchedulingProblem CreateScientificIgnoredAvoidPressureProblem()
    {
        var periodStartUtc = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEndUtc = new DateTime(2026, 6, 15, 6, 30, 0, DateTimeKind.Utc);

        var resources = Enumerable.Range(1, 16)
            .Select(index => new Resource(
                CreateGuid(index),
                $"Guard{index:00}",
                hourlyCost: 0))
            .ToArray();

        var shifts = new List<Shift>();
        var preferences = new List<ResourcePreference>();

        var shiftIndex = 1;

        for (var dayOffset = 0; dayOffset < 14; dayOffset++)
        {
            var day = periodStartUtc.Date.AddDays(dayOffset);

            var morning = new Shift(
                CreateGuid(1000 + shiftIndex++),
                day.AddHours(6.5),
                day.AddHours(14.5),
                ShiftKind.Morning,
                minResourceCount: 3,
                maxResourceCount: 3);

            var afternoon = new Shift(
                CreateGuid(1000 + shiftIndex++),
                day.AddHours(14.5),
                day.AddHours(22.5),
                ShiftKind.Afternoon,
                minResourceCount: 2,
                maxResourceCount: 2);

            var night = new Shift(
                CreateGuid(1000 + shiftIndex++),
                day.AddHours(22.5),
                day.AddDays(1).AddHours(6.5),
                ShiftKind.Night,
                minResourceCount: 1,
                maxResourceCount: 1);

            shifts.Add(morning);
            shifts.Add(afternoon);
            shifts.Add(night);

            var preferredMorningResource = resources[dayOffset % resources.Length];

            preferences.Add(new ResourcePreference(
                preferredMorningResource.Id,
                morning.StartUtc,
                morning.EndUtc,
                ResourcePreferenceType.Prefer,
                ResourcePreferencePriority.Medium));

            foreach (var resource in resources.Where(resource => resource.Id != preferredMorningResource.Id))
            {
                preferences.Add(new ResourcePreference(
                    resource.Id,
                    morning.StartUtc,
                    morning.EndUtc,
                    ResourcePreferenceType.Avoid,
                    ResourcePreferencePriority.Medium));
            }
        }

        var availabilityWindows = resources
            .Select(resource => new AvailabilityWindow(
                resource.Id,
                periodStartUtc,
                periodEndUtc))
            .ToArray();

        var totalMinimumCapacityHours = shifts
            .Sum(shift => (shift.EndUtc - shift.StartUtc).TotalHours * shift.MinResourceCount);

        var targetHoursPerResource = totalMinimumCapacityHours / resources.Length;

        var workloadDemands = resources
            .Select(resource => new ResourceWorkloadDemand(
                resource.Id,
                requestedPreferredHours: targetHoursPerResource,
                minimumRequiredHours: 0))
            .ToArray();

        return new SchedulingProblem(
            new SchedulePeriod(periodStartUtc, periodEndUtc),
            resources,
            shifts.ToArray(),
            availabilityWindows,
            preferences.ToArray(),
            maximumAssignedHoursDeviationFromAverageHours: 5,
            resourceWorkloadDemands: workloadDemands);
    }

    private static IReadOnlyDictionary<Guid, double> CalculateAssignedHoursByResource(
        SchedulingProblem problem,
        ScheduleCandidate candidate)
    {
        var shiftsById = problem.Shifts.ToDictionary(shift => shift.Id);

        return problem.Resources.ToDictionary(
            resource => resource.Id,
            resource => candidate.Assignments
                .Where(assignment => assignment.ResourceId == resource.Id)
                .Sum(assignment =>
                {
                    var shift = shiftsById[assignment.ShiftId];

                    return (shift.EndUtc - shift.StartUtc).TotalHours;
                }));
    }

    private static TargetGapMetrics CalculateTargetGap(
        SchedulingProblem problem,
        IReadOnlyDictionary<Guid, double> assignedHoursByResourceId)
    {
        var demandsByResourceId = problem.ResourceWorkloadDemands
            .ToDictionary(demand => demand.ResourceId);

        var overTargetHours = 0.0;
        var underTargetHours = 0.0;

        foreach (var resource in problem.Resources)
        {
            var assignedHours = assignedHoursByResourceId[resource.Id];

            if (!demandsByResourceId.TryGetValue(resource.Id, out var demand))
            {
                continue;
            }

            if (assignedHours > demand.EffectiveTargetHours)
            {
                overTargetHours += assignedHours - demand.EffectiveTargetHours;
            }

            if (assignedHours < demand.EffectiveTargetHours)
            {
                underTargetHours += demand.EffectiveTargetHours - assignedHours;
            }
        }

        return new TargetGapMetrics(overTargetHours, underTargetHours);
    }

    private static IReadOnlyDictionary<Guid, int> CalculateIgnoredAvoidAssignmentCountsByResource(
        SchedulingProblem problem,
        ScheduleCandidate candidate)
    {
        var shiftsById = problem.Shifts.ToDictionary(shift => shift.Id);

        var result = problem.Resources.ToDictionary(
            resource => resource.Id,
            _ => 0);

        foreach (var assignment in candidate.Assignments)
        {
            var shift = shiftsById[assignment.ShiftId];

            var hasIgnoredAvoid = problem.ResourcePreferences
                .Where(preference => preference.ResourceId == assignment.ResourceId)
                .Where(preference => preference.Type == ResourcePreferenceType.Avoid)
                .Any(preference => Overlaps(shift, preference));

            if (!hasIgnoredAvoid)
            {
                continue;
            }

            result[assignment.ResourceId]++;
        }

        return result;
    }

    private static int CountViolations(
        ScheduleEvaluationResult evaluation,
        ConstraintViolationType type)
    {
        return evaluation.Violations.Count(violation => violation.Type == type);
    }

    private static double CalculateTotalMinimumCapacityHours(SchedulingProblem problem)
    {
        return problem.Shifts.Sum(shift =>
            (shift.EndUtc - shift.StartUtc).TotalHours * shift.MinResourceCount);
    }

    private static double CalculateTotalMaximumCapacityHours(SchedulingProblem problem)
    {
        return problem.Shifts.Sum(shift =>
            (shift.EndUtc - shift.StartUtc).TotalHours * shift.MaxResourceCount);
    }

    private static bool Overlaps(
        Shift shift,
        ResourcePreference preference)
    {
        return preference.StartUtc < shift.EndUtc &&
               shift.StartUtc < preference.EndUtc;
    }

    private static Guid CreateGuid(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }


    private sealed record MultiSeedSummary(
        string VariantName,
        int Seed,
        bool IsFeasible,
        int TotalPenalty,
        int HardViolationCount,
        int SoftViolationCount,
        double TotalAssignedHours,
        double TotalOverTargetHours,
        double TotalUnderTargetHours,
        int IgnoredAvoidPreferenceViolationCount,
        int TotalIgnoredAvoidAssignments,
        int MaxIgnoredAvoidAssignmentsForSingleResource,
        double AverageIgnoredAvoidAssignmentsPerResource,
        int ResourcesWithIgnoredAvoidAssignmentsCount,
        int ResourceIgnoredAvoidPreferenceBurdenViolationCount,
        double TotalIgnoredAvoidBurdenMagnitude,
        double MaxIgnoredAvoidBurdenMagnitudeForSingleResource,
        int EstimatedIgnoredAvoidBurdenPenalty,
        int FirstBestSoFarTotalPenalty,
        int LastBestSoFarTotalPenalty,
        int Improvement,
        int FirstFeasibleCandidateCount,
        int LastFeasibleCandidateCount);


    private sealed record ScoringVariant(
        string Name,
        ScheduleScoringWeights Weights);

    private sealed record TargetGapMetrics(
        double TotalOverTargetHours,
        double TotalUnderTargetHours);
}
