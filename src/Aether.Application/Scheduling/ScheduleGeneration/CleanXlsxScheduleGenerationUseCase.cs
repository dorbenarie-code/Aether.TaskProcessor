using Aether.Application.Scheduling.Diagnostics;
using Aether.Application.Scheduling.ManagerConstraints;
using Aether.Application.Scheduling.Reports;
using Aether.Application.Scheduling.Reports.Exporting;
using Aether.Application.Scheduling.SubmissionForms;
using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.ScheduleGeneration;

public sealed class CleanXlsxScheduleGenerationUseCase
{
    private readonly IAvailabilityMatrixStreamOptimizationRunner runner;
    private readonly IScheduleTableXlsxExporter xlsxExporter;

    public CleanXlsxScheduleGenerationUseCase(
        IAvailabilityMatrixStreamOptimizationRunner runner,
        IScheduleTableXlsxExporter xlsxExporter)
    {
        this.runner = runner ??
            throw new ArgumentNullException(nameof(runner));

        this.xlsxExporter = xlsxExporter ??
            throw new ArgumentNullException(nameof(xlsxExporter));
    }

    public CleanXlsxScheduleGenerationResult Run(
        CleanXlsxScheduleGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var optimizationFlowResult = runner.Run(new AvailabilityMatrixStreamOptimizationRequest(
            request.AvailabilityMatrixStream,
            request.SchedulePeriod,
            request.Resources,
            request.Shifts,
            request.TotalEffectiveTargetHours,
            request.MaximumAssignedHoursDeviationFromAverageHours,
            request.Seed,
            request.AliasesByWorkerName,
            request.ResourceMonthlyNightShiftHistories,
            request.ManagerConstraintSet,
            request.ManagerConstraintRows,
            request.ApplyPostRunLocalAddImprovement));

        if (optimizationFlowResult.ImportFatalErrors.Count > 0 ||
            optimizationFlowResult.ManagerConstraintImportFatalErrors.Count > 0 ||
            optimizationFlowResult.OptimizationResult is null)
        {
            return CreateFailedResult(optimizationFlowResult);
        }

        var optimizationResult = optimizationFlowResult.OptimizationResult;

        var reviewText = new SchedulingRunReportFormatter()
            .FormatOptimizationReview(
                optimizationResult.Problem,
                optimizationResult.GeneticResult);

        var scheduleTableCsv = new ScheduleTableCsvFormatter()
            .Format(
                optimizationResult.Problem,
                optimizationResult.GeneticResult);

        var projection = new ScheduleTableProjectionBuilder()
            .Build(
                optimizationResult.Problem,
                optimizationResult.GeneticResult);

        var scheduleTableXlsxBytes = xlsxExporter.ExportToXlsx(projection);

        var targetGapDiagnosticsText = request.IncludeTargetGapDiagnostics
            ? CreateTargetGapDiagnosticsText(optimizationResult)
            : null;

        return new CleanXlsxScheduleGenerationResult(
            Succeeded: true,
            ImportWarnings: optimizationFlowResult.ImportWarnings,
            ImportFatalErrors: optimizationFlowResult.ImportFatalErrors,
            ManagerConstraintImportWarnings: optimizationFlowResult.ManagerConstraintImportWarnings,
            ManagerConstraintImportFatalErrors: optimizationFlowResult.ManagerConstraintImportFatalErrors,
            ManagerConstraintImportSummary: optimizationFlowResult.ManagerConstraintImportSummary,
            ReviewText: reviewText,
            ScheduleTableCsv: scheduleTableCsv,
            ScheduleTableXlsxBytes: scheduleTableXlsxBytes,
            TargetGapDiagnosticsText: targetGapDiagnosticsText,
            Summary: CreateSummary(
                optimizationFlowResult,
                optimizationResult));
    }

    private static ScheduleGenerationRunSummary CreateSummary(
        AvailabilityMatrixImportedRowsOptimizationResult optimizationFlowResult,
        ClosedFormSubmissionOptimizationResult optimizationResult)
    {
        var geneticResult = optimizationResult.GeneticResult;
        var evaluation = geneticResult.Evaluation;
        var postRunLocalAddImprovementResult =
            optimizationResult.PostRunLocalAddImprovementResult;

        var submittedShiftSelectionCount = optimizationFlowResult.ImportedWorkerSubmissions
            .Sum(submission => submission.ShiftSubmissions.Count);

        int? postRunLocalAddImprovementPenaltyDelta =
            postRunLocalAddImprovementResult is null
                ? null
                : postRunLocalAddImprovementResult.InitialTotalPenalty -
                  postRunLocalAddImprovementResult.FinalTotalPenalty;

        return new ScheduleGenerationRunSummary(
            ImportedWorkerSubmissionCount: optimizationFlowResult.ImportedWorkerSubmissions.Count,
            SubmittedShiftSelectionCount: submittedShiftSelectionCount,
            ResourceCount: optimizationResult.Problem.Resources.Count,
            ShiftCount: optimizationResult.Problem.Shifts.Count,
            ResourceWorkloadDemandCount: optimizationResult.Problem.ResourceWorkloadDemands.Count,
            AssignmentCount: geneticResult.Candidate.Assignments.Count,
            IsFeasible: evaluation.IsFeasible,
            HardViolationCount: evaluation.Score.HardViolationCount,
            SoftViolationCount: evaluation.Score.SoftViolationCount,
            TotalPenalty: evaluation.Score.TotalPenalty,
            GenerationDiagnosticCount: geneticResult.GenerationDiagnostics.Count,
            PostRunLocalAddImprovementApplied: postRunLocalAddImprovementResult is not null,
            PostRunLocalAddImprovementAcceptedAddMoveCount: postRunLocalAddImprovementResult?.AcceptedAddMoveCount,
            PostRunLocalAddImprovementInitialTotalPenalty: postRunLocalAddImprovementResult?.InitialTotalPenalty,
            PostRunLocalAddImprovementFinalTotalPenalty: postRunLocalAddImprovementResult?.FinalTotalPenalty,
            PostRunLocalAddImprovementPenaltyDelta: postRunLocalAddImprovementPenaltyDelta);
    }


    private static CleanXlsxScheduleGenerationResult CreateFailedResult(
        AvailabilityMatrixImportedRowsOptimizationResult optimizationFlowResult)
    {
        return new CleanXlsxScheduleGenerationResult(
            Succeeded: false,
            ImportWarnings: optimizationFlowResult.ImportWarnings,
            ImportFatalErrors: optimizationFlowResult.ImportFatalErrors,
            ManagerConstraintImportWarnings: optimizationFlowResult.ManagerConstraintImportWarnings,
            ManagerConstraintImportFatalErrors: optimizationFlowResult.ManagerConstraintImportFatalErrors,
            ManagerConstraintImportSummary: optimizationFlowResult.ManagerConstraintImportSummary,
            FailureType: ResolveFailureType(optimizationFlowResult));
    }

    private static ScheduleGenerationFailureType ResolveFailureType(
        AvailabilityMatrixImportedRowsOptimizationResult optimizationFlowResult)
    {
        if (optimizationFlowResult.ImportFatalErrors.Count > 0)
        {
            return ScheduleGenerationFailureType.AvailabilityMatrixImportFailed;
        }

        if (optimizationFlowResult.ManagerConstraintImportFatalErrors.Count > 0)
        {
            return ScheduleGenerationFailureType.ManagerConstraintImportFailed;
        }

        return ScheduleGenerationFailureType.OptimizationResultMissing;
    }

    private static string CreateTargetGapDiagnosticsText(
        ClosedFormSubmissionOptimizationResult optimizationResult)
    {
        var scoringWeights = ScheduleScoringWeights.CreateDefault();

        var diagnostic = new CleanGaTargetGapExplainabilityAnalyzer()
            .Analyze(
                optimizationResult.Problem,
                optimizationResult.GeneticResult.Candidate,
                optimizationResult.GeneticResult.Evaluation,
                scoringWeights);

        return new CleanGaTargetGapExplainabilityDiagnosticFormatter()
            .Format(diagnostic);
    }
}
