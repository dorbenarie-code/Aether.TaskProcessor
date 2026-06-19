using Aether.Application.Scheduling.SubmissionForms;
using Aether.Domain.Optimization;

namespace Aether.Api.Scheduling;

internal static class ClosedFormOptimizationApiMapper
{
    public static ClosedFormSubmissionOptimizationRequest ToApplicationRequest(
        this ClosedFormOptimizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var period = new SchedulePeriod(
            request.PeriodStartUtc,
            request.PeriodEndUtc);

        return new ClosedFormSubmissionOptimizationRequest(
            period,
            request.Resources.Select(ToDomainResource).ToArray(),
            request.Shifts.Select(ToDomainShift).ToArray(),
            request.WorkerSubmissions.Select(ToWorkerSubmission).ToArray(),
            request.TotalEffectiveTargetHours,
            request.MaximumAssignedHoursDeviationFromAverageHours,
            request.Seed,
            request.ResourceMonthlyNightShiftHistories?
                .Select(ToDomainNightShiftHistory)
                .ToArray());
    }

    public static ClosedFormOptimizationResponse ToResponse(
        this ClosedFormSubmissionOptimizationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var genetic = result.GeneticResult;
        var score = genetic.Evaluation.Score;

        return new ClosedFormOptimizationResponse(
            genetic.Evaluation.IsFeasible,
            score.Value,
            score.HardViolationCount,
            score.SoftViolationCount,
            score.TotalPenalty,
            genetic.Candidate.Assignments.Count,
            genetic.GenerationDiagnostics.Count,
            genetic.Candidate.Assignments
                .Select(assignment => new ClosedFormOptimizationAssignmentResponse(
                    assignment.ResourceId,
                    assignment.ShiftId))
                .ToArray(),
            genetic.LoadByResource
                .Select(load => new ClosedFormOptimizationResourceLoadResponse(
                    load.ResourceId,
                    load.ResourceName,
                    load.AssignedHours,
                    load.AssignmentCount))
                .ToArray(),
            genetic.ViolationsByType.ToDictionary(
                pair => pair.Key.ToString(),
                pair => pair.Value),
            result.Warnings
                .Select(warning => new ClosedFormOptimizationWarningResponse(
                    warning.Type.ToString(),
                    warning.ResourceId,
                    warning.Date,
                    warning.ShiftKind?.ToString()))
                .ToArray());
    }

    private static Resource ToDomainResource(
        ClosedFormOptimizationResourceRequest request)
    {
        return new Resource(
            request.Id,
            request.Name,
            request.HourlyCost,
            ParseEnum<ResourceWorkloadCategory>(
                request.WorkloadCategory,
                nameof(request.WorkloadCategory)));
    }

    private static Shift ToDomainShift(
        ClosedFormOptimizationShiftRequest request)
    {
        return new Shift(
            request.Id,
            request.StartUtc,
            request.EndUtc,
            ParseEnum<ShiftKind>(request.Kind, nameof(request.Kind)),
            request.MinResourceCount,
            request.MaxResourceCount,
            request.RequiresPreferenceToAssign,
            request.RequiresMinimumWhenPreferenceExists,
            ParseNullableEnum<NightShiftCategory>(
                request.NightShiftCategory,
                nameof(request.NightShiftCategory)));
    }

    private static WorkerSubmission ToWorkerSubmission(
        ClosedFormOptimizationWorkerSubmissionRequest request)
    {
        return new WorkerSubmission(
            request.ResourceId,
            request.ShiftSubmissions
                .Select(ToWorkerShiftSubmission)
                .ToArray());
    }

    private static WorkerShiftSubmission ToWorkerShiftSubmission(
        ClosedFormOptimizationWorkerShiftSubmissionRequest request)
    {
        return new WorkerShiftSubmission(
            request.Date,
            ParseEnum<ShiftKind>(request.ShiftKind, nameof(request.ShiftKind)),
            ParseEnum<ShiftSubmissionChoice>(request.Choice, nameof(request.Choice)));
    }

    private static ResourceMonthlyNightShiftHistory ToDomainNightShiftHistory(
        ClosedFormOptimizationResourceMonthlyNightShiftHistoryRequest request)
    {
        return new ResourceMonthlyNightShiftHistory(
            request.ResourceId,
            request.Year,
            request.Month,
            ParseEnum<NightShiftCategory>(
                request.NightShiftCategory,
                nameof(request.NightShiftCategory)),
            request.AssignedCount);
    }

    private static TEnum ParseEnum<TEnum>(
        string value,
        string fieldName)
        where TEnum : struct, Enum
    {
        if (Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) &&
            Enum.IsDefined(parsed))
        {
            return parsed;
        }

        throw new ArgumentException(
            $"Unsupported {fieldName} value '{value}'.",
            fieldName);
    }

    private static TEnum? ParseNullableEnum<TEnum>(
        string? value,
        string fieldName)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return ParseEnum<TEnum>(value, fieldName);
    }
}
