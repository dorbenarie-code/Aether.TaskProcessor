using System.Text;
using Aether.Application.Scheduling.Contracts;
using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.Reports;

public sealed class SchedulingRunReportFormatter
{
    private readonly ScheduleEvaluationResultRanker _ranker = new();

    public string Format(SchedulingRunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var bestName = GetBestResultName(result);
        var bestResult = bestName == "Genetic"
            ? result.GeneticResult
            : result.DeterministicResult;

        var builder = new StringBuilder();

        AppendHeader(builder);
        AppendInputSummary(builder, result);
        AppendComparison(builder, result, bestName);
        AppendOptimizationResult(builder, "Best Result", bestResult, result.Problem);
        AppendAssignmentsByShift(builder, result.Problem, bestResult);
        AppendGenerationDiagnostics(builder, result.GeneticResult);

        return builder.ToString();
    }

    public string FormatOptimizationReview(
        SchedulingProblem problem,
        SchedulingRunOptimizationResult result)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(result);

        var builder = new StringBuilder();

        AppendOptimizationReviewHeader(builder);
        AppendProblemSummary(builder, problem);
        AppendScheduleByShift(builder, problem, result);
        AppendOptimizationResult(builder, "Optimization Result", result, problem);
        AppendTargetGapByResource(builder, problem, result);
        AppendPreferenceFulfillment(builder, problem, result);
        AppendGenerationDiagnostics(builder, result);

        return builder.ToString();
    }

    private string GetBestResultName(SchedulingRunResult result)
    {
        return _ranker.IsBetterThan(
            result.GeneticResult.Evaluation,
            result.DeterministicResult.Evaluation)
            ? "Genetic"
            : "Deterministic";
    }

    private static void AppendHeader(StringBuilder builder)
    {
        builder.AppendLine("Scheduling Run Report");
        builder.AppendLine("=====================");
        builder.AppendLine();
    }

    private static void AppendInputSummary(
        StringBuilder builder,
        SchedulingRunResult result)
    {
        var problem = result.Problem;

        builder.AppendLine("Input Summary");
        builder.AppendLine("-------------");
        builder.AppendLine($"Period: {problem.Period.StartUtc:yyyy-MM-dd HH:mm} UTC -> {problem.Period.EndUtc:yyyy-MM-dd HH:mm} UTC");
        builder.AppendLine($"Resources: {problem.Resources.Count}");
        builder.AppendLine($"Shifts: {problem.Shifts.Count}");
        builder.AppendLine($"AvailabilityWindows: {problem.AvailabilityWindows.Count}");
        builder.AppendLine($"ResourcePreferences: {problem.ResourcePreferences.Count}");
        builder.AppendLine($"MinimumAssignedHoursPerResource: {problem.MinimumAssignedHoursPerResource}");
        builder.AppendLine($"MinimumMorningShiftsPerResourcePerFullWeek: {problem.MinimumMorningShiftsPerResourcePerFullWeek}");
        builder.AppendLine($"MinimumAfternoonShiftsPerResourcePerFullWeek: {problem.MinimumAfternoonShiftsPerResourcePerFullWeek}");
        builder.AppendLine($"Warnings: {result.Warnings.Count}");
        builder.AppendLine();

        builder.AppendLine("Resources:");
        foreach (var resource in problem.Resources.OrderBy(resource => resource.Name))
        {
            builder.AppendLine($"- {resource.Name} ({resource.Id})");
        }

        builder.AppendLine();

        builder.AppendLine("Shifts:");
        foreach (var shift in problem.Shifts.OrderBy(shift => shift.StartUtc))
        {
            builder.AppendLine(
                $"- {FormatShift(shift)}, Min={shift.MinResourceCount}, Max={shift.MaxResourceCount}, " +
                $"RequiresPreferenceToAssign={shift.RequiresPreferenceToAssign}, " +
                $"RequiresMinimumWhenPreferenceExists={shift.RequiresMinimumWhenPreferenceExists}");
        }

        builder.AppendLine();

        if (result.Warnings.Count == 0)
        {
            builder.AppendLine("WarningsByBuilder:");
            builder.AppendLine("- none");
            builder.AppendLine();
            return;
        }

        builder.AppendLine("WarningsByBuilder:");
        foreach (var warning in result.Warnings)
        {
            builder.AppendLine($"- {warning.Type}: {warning.Message}");
        }

        builder.AppendLine();
    }

    private static void AppendComparison(
        StringBuilder builder,
        SchedulingRunResult result,
        string bestName)
    {
        var comparison = result.Comparison;

        builder.AppendLine("Comparison");
        builder.AppendLine("----------");
        builder.AppendLine($"BestResult: {bestName}");
        builder.AppendLine($"GeneticRankedBetter: {comparison.GeneticRankedBetter}");
        builder.AppendLine($"HardViolations: {comparison.DeterministicHardViolationCount} -> {comparison.GeneticHardViolationCount}");
        builder.AppendLine($"TotalPenalty: {comparison.DeterministicTotalPenalty} -> {comparison.GeneticTotalPenalty}");
        builder.AppendLine($"IgnoredAvoidPreference: {comparison.DeterministicIgnoredAvoidPreferenceViolations} -> {comparison.GeneticIgnoredAvoidPreferenceViolations}");
        builder.AppendLine($"ShiftSequenceQuotaExceeded: {comparison.DeterministicShiftSequenceQuotaViolations} -> {comparison.GeneticShiftSequenceQuotaViolations}");
        builder.AppendLine();
    }

    private static void AppendOptimizationResult(
        StringBuilder builder,
        string title,
        SchedulingRunOptimizationResult result,
        SchedulingProblem problem)
    {
        var evaluation = result.Evaluation;

        builder.AppendLine(title);
        builder.AppendLine("-----------");
        builder.AppendLine($"IsFeasible: {evaluation.IsFeasible}");
        builder.AppendLine($"Score.Value: {evaluation.Score.Value}");
        builder.AppendLine($"TotalPenalty: {evaluation.Score.TotalPenalty}");
        builder.AppendLine($"HardViolationCount: {evaluation.Score.HardViolationCount}");
        builder.AppendLine($"SoftViolationCount: {evaluation.Score.SoftViolationCount}");
        builder.AppendLine($"Assignments.Count: {result.Candidate.Assignments.Count}");
        builder.AppendLine();

        builder.AppendLine("LoadByResource:");
        foreach (var load in result.LoadByResource.OrderBy(load => load.ResourceName))
        {
            builder.AppendLine($"- {load.ResourceName}: {load.AssignedHours:0.0}h, assignments={load.AssignmentCount}");
        }

        builder.AppendLine();

        AppendLoadBalance(builder, result);

        builder.AppendLine("ViolationsByType:");
        if (result.ViolationsByType.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            foreach (var violationGroup in result.ViolationsByType.OrderBy(item => item.Key.ToString()))
            {
                builder.AppendLine($"- {violationGroup.Key}: {violationGroup.Value}");
            }
        }

        builder.AppendLine();

        builder.AppendLine("Violations:");
        if (evaluation.Violations.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            AppendViolationDetails(builder, evaluation, problem);
        }

        builder.AppendLine();
    }


    private static void AppendLoadBalance(
        StringBuilder builder,
        SchedulingRunOptimizationResult result)
    {
        var loads = result.LoadByResource.ToArray();

        builder.AppendLine("LoadBalance:");

        if (loads.Length == 0)
        {
            builder.AppendLine("- none");
            builder.AppendLine();
            return;
        }

        var totalAssignedHours = loads.Sum(load => load.AssignedHours);
        var averageAssignedHours = totalAssignedHours / loads.Length;
        var minimumAssignedHours = loads.Min(load => load.AssignedHours);
        var maximumAssignedHours = loads.Max(load => load.AssignedHours);
        var loadSpreadHours = maximumAssignedHours - minimumAssignedHours;

        var lowestLoadedResources = loads
            .Where(load => Math.Abs(load.AssignedHours - minimumAssignedHours) < 0.001)
            .Select(load => load.ResourceName)
            .OrderBy(name => name)
            .ToArray();

        var highestLoadedResources = loads
            .Where(load => Math.Abs(load.AssignedHours - maximumAssignedHours) < 0.001)
            .Select(load => load.ResourceName)
            .OrderBy(name => name)
            .ToArray();

        builder.AppendLine($"- TotalAssignedHours: {totalAssignedHours:0.0}");
        builder.AppendLine($"- AverageAssignedHours: {averageAssignedHours:0.0}");
        builder.AppendLine($"- MinimumAssignedHours: {minimumAssignedHours:0.0}");
        builder.AppendLine($"- MaximumAssignedHours: {maximumAssignedHours:0.0}");
        builder.AppendLine($"- LoadSpreadHours: {loadSpreadHours:0.0}");
        builder.AppendLine($"- LowestLoadedResources: {string.Join(", ", lowestLoadedResources)}");
        builder.AppendLine($"- HighestLoadedResources: {string.Join(", ", highestLoadedResources)}");
        builder.AppendLine();
    }

    private static void AppendViolationDetails(
        StringBuilder builder,
        ScheduleEvaluationResult evaluation,
        SchedulingProblem problem)
    {
        var resourcesById = problem.Resources.ToDictionary(resource => resource.Id);
        var shiftsById = problem.Shifts.ToDictionary(shift => shift.Id);

        foreach (var violation in evaluation.Violations
                     .OrderBy(violation => violation.Type.ToString())
                     .ThenBy(violation => violation.ResourceId?.ToString())
                     .ThenBy(violation => violation.ShiftId?.ToString()))
        {
            var resourceName = violation.ResourceId.HasValue &&
                               resourcesById.TryGetValue(violation.ResourceId.Value, out var resource)
                ? resource.Name
                : "-";

            var shiftText = violation.ShiftId.HasValue &&
                            shiftsById.TryGetValue(violation.ShiftId.Value, out var shift)
                ? FormatShift(shift)
                : "-";

            builder.AppendLine(
                $"- {violation.Type} ({violation.Severity}): Resource={resourceName}, Shift={shiftText}, Message={violation.Message}");
        }
    }

    private static void AppendAssignmentsByShift(
        StringBuilder builder,
        SchedulingProblem problem,
        SchedulingRunOptimizationResult result)
    {
        var resourcesById = problem.Resources.ToDictionary(resource => resource.Id);

        var assignmentsByShiftId = result.Candidate.Assignments
            .GroupBy(assignment => assignment.ShiftId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(assignment => resourcesById.TryGetValue(assignment.ResourceId, out var resource)
                        ? resource.Name
                        : assignment.ResourceId.ToString())
                    .OrderBy(name => name)
                    .ToArray());

        builder.AppendLine("AssignmentsByShift");
        builder.AppendLine("------------------");

        foreach (var shift in problem.Shifts.OrderBy(shift => shift.StartUtc))
        {
            assignmentsByShiftId.TryGetValue(shift.Id, out var assignedResources);

            var assignedText = assignedResources is null || assignedResources.Length == 0
                ? "none"
                : string.Join(", ", assignedResources);

            builder.AppendLine($"- {FormatShift(shift)} -> {assignedText}");
        }

        builder.AppendLine();
    }

    private static void AppendGenerationDiagnostics(
        StringBuilder builder,
        SchedulingRunOptimizationResult result)
    {
        if (result.GenerationDiagnostics.Count == 0)
        {
            return;
        }

        builder.AppendLine("GenerationDiagnostics");
        builder.AppendLine("---------------------");

        foreach (var diagnostic in result.GenerationDiagnostics)
        {
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

        builder.AppendLine();
    }

    private static void AppendOptimizationReviewHeader(StringBuilder builder)
    {
        builder.AppendLine("Schedule Optimization Review");
        builder.AppendLine("============================");
        builder.AppendLine();
    }

    private static void AppendProblemSummary(
        StringBuilder builder,
        SchedulingProblem problem)
    {
        builder.AppendLine("Problem Summary");
        builder.AppendLine("---------------");
        builder.AppendLine($"Period: {problem.Period.StartUtc:yyyy-MM-dd HH:mm} UTC -> {problem.Period.EndUtc:yyyy-MM-dd HH:mm} UTC");
        builder.AppendLine($"Resources: {problem.Resources.Count}");
        builder.AppendLine($"Shifts: {problem.Shifts.Count}");
        builder.AppendLine($"AvailabilityWindows: {problem.AvailabilityWindows.Count}");
        builder.AppendLine($"ResourcePreferences: {problem.ResourcePreferences.Count}");
        builder.AppendLine($"ResourceWorkloadDemands: {problem.ResourceWorkloadDemands.Count}");
        builder.AppendLine($"MinimumAssignedHoursPerResource: {problem.MinimumAssignedHoursPerResource}");
        builder.AppendLine($"MinimumMorningShiftsPerResourcePerFullWeek: {problem.MinimumMorningShiftsPerResourcePerFullWeek}");
        builder.AppendLine($"MinimumAfternoonShiftsPerResourcePerFullWeek: {problem.MinimumAfternoonShiftsPerResourcePerFullWeek}");
        builder.AppendLine($"MaximumAssignedHoursDeviationFromAverageHours: {problem.MaximumAssignedHoursDeviationFromAverageHours?.ToString("0.0") ?? "none"}");
        builder.AppendLine();
    }

    private static void AppendScheduleByShift(
        StringBuilder builder,
        SchedulingProblem problem,
        SchedulingRunOptimizationResult result)
    {
        var resourcesById = problem.Resources.ToDictionary(resource => resource.Id);

        var assignmentsByShiftId = result.Candidate.Assignments
            .GroupBy(assignment => assignment.ShiftId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(assignment => resourcesById.TryGetValue(assignment.ResourceId, out var resource)
                        ? resource.Name
                        : assignment.ResourceId.ToString())
                    .OrderBy(name => name)
                    .ToArray());

        builder.AppendLine("ScheduleByShift");
        builder.AppendLine("---------------");

        foreach (var shift in problem.Shifts.OrderBy(shift => shift.StartUtc))
        {
            assignmentsByShiftId.TryGetValue(shift.Id, out var assignedResources);

            var assignedCount = assignedResources?.Length ?? 0;

            var assignedText = assignedResources is null || assignedResources.Length == 0
                ? "none"
                : string.Join(", ", assignedResources);

            builder.AppendLine(
                $"- {FormatShiftForHumanReview(shift)} | Min={shift.MinResourceCount} Max={shift.MaxResourceCount} Assigned={assignedCount} | Workers={assignedText}");
        }

        builder.AppendLine();
    }

    private static void AppendTargetGapByResource(
        StringBuilder builder,
        SchedulingProblem problem,
        SchedulingRunOptimizationResult result)
    {
        var loadsByResourceId = result.LoadByResource.ToDictionary(load => load.ResourceId);

        var demandsByResourceId = problem.ResourceWorkloadDemands.ToDictionary(
            demand => demand.ResourceId);

        builder.AppendLine("TargetGapByResource");
        builder.AppendLine("-------------------");

        foreach (var resource in problem.Resources.OrderBy(resource => resource.Name))
        {
            loadsByResourceId.TryGetValue(resource.Id, out var load);
            demandsByResourceId.TryGetValue(resource.Id, out var demand);

            var assignedHours = load?.AssignedHours ?? 0.0;
            var targetHours = demand?.EffectiveTargetHours ?? 0.0;
            var gapHours = assignedHours - targetHours;

            builder.AppendLine(
                $"- {resource.Name}: Assigned={assignedHours:0.0}h, Target={targetHours:0.0}h, Gap={FormatSignedHours(gapHours)}h");
        }

        builder.AppendLine();
    }

    private static void AppendPreferenceFulfillment(
        StringBuilder builder,
        SchedulingProblem problem,
        SchedulingRunOptimizationResult result)
    {
        var shiftsById = problem.Shifts.ToDictionary(shift => shift.Id);

        var assignedPreferenceKeys = new HashSet<(Guid ResourceId, DateTime StartUtc, DateTime EndUtc)>();

        foreach (var assignment in result.Candidate.Assignments)
        {
            if (!shiftsById.TryGetValue(assignment.ShiftId, out var shift))
            {
                continue;
            }

            assignedPreferenceKeys.Add(CreatePreferenceKey(
                assignment.ResourceId,
                shift.StartUtc,
                shift.EndUtc));
        }

        var preferredRequests = problem.ResourcePreferences
            .Where(preference => preference.Type == ResourcePreferenceType.Prefer)
            .ToArray();

        var fulfilledPreferredRequests = preferredRequests.Count(preference =>
            assignedPreferenceKeys.Contains(CreatePreferenceKey(
                preference.ResourceId,
                preference.StartUtc,
                preference.EndUtc)));

        var unsatisfiedPreferredRequests =
            preferredRequests.Length - fulfilledPreferredRequests;

        var fulfillmentRate = preferredRequests.Length == 0
            ? 0.0
            : fulfilledPreferredRequests * 100.0 / preferredRequests.Length;

        builder.AppendLine("PreferenceFulfillment");
        builder.AppendLine("---------------------");
        builder.AppendLine($"- PreferredRequests: {preferredRequests.Length}");
        builder.AppendLine($"- FulfilledPreferredRequests: {fulfilledPreferredRequests}");
        builder.AppendLine($"- UnsatisfiedPreferredRequests: {unsatisfiedPreferredRequests}");
        builder.AppendLine($"- FulfillmentRate: {fulfillmentRate:0.0}%");
        builder.AppendLine();
    }

    private static (Guid ResourceId, DateTime StartUtc, DateTime EndUtc) CreatePreferenceKey(
        Guid resourceId,
        DateTime startUtc,
        DateTime endUtc)
    {
        return (resourceId, startUtc, endUtc);
    }

    private static string FormatShiftForHumanReview(Shift shift)
    {
        if (shift.StartUtc.Date == shift.EndUtc.Date)
        {
            return $"{shift.Kind} {shift.StartUtc:yyyy-MM-dd HH:mm}-{shift.EndUtc:HH:mm} UTC";
        }

        return $"{shift.Kind} {shift.StartUtc:yyyy-MM-dd HH:mm}-{shift.EndUtc:yyyy-MM-dd HH:mm} UTC";
    }

    private static string FormatSignedHours(double hours)
    {
        return hours.ToString("+0.0;-0.0;+0.0");
    }

    private static string FormatShift(Shift shift)
    {
        return $"{shift.Kind} {shift.StartUtc:yyyy-MM-dd HH:mm}-{shift.EndUtc:yyyy-MM-dd HH:mm} UTC";
    }
}
