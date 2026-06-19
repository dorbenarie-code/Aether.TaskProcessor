using System.Globalization;
using System.Text;

namespace Aether.Application.Scheduling.Diagnostics;

public sealed class CleanGaTargetGapExplainabilityDiagnosticFormatter
{
    public string Format(TargetGapExplainabilityDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        var builder = new StringBuilder();

        builder.AppendLine("Clean GA Target Gap Explainability Diagnostics");
        builder.AppendLine("==============================================");
        builder.AppendLine();

        AppendWorkerTargetGaps(builder, diagnostic);
        AppendAddMoveDiagnostics(builder, diagnostic);
        AppendScoreImprovingAddMoveDetails(builder, diagnostic);
        AppendRejectedAddMoves(builder, diagnostic);
        AppendRejectedHardViolationByType(builder, diagnostic);
        AppendTransferMoveDiagnostics(builder, diagnostic);

        return builder.ToString();
    }

    private static void AppendWorkerTargetGaps(
        StringBuilder builder,
        TargetGapExplainabilityDiagnostic diagnostic)
    {
        builder.AppendLine("WorkerTargetGaps:");

        if (diagnostic.WorkerDiagnostics.Count == 0)
        {
            builder.AppendLine("- none");
            builder.AppendLine();
            return;
        }

        foreach (var worker in diagnostic.WorkerDiagnostics
                     .OrderBy(worker => worker.ResourceName, StringComparer.Ordinal))
        {
            builder.AppendLine(
                $"- {worker.ResourceName}: " +
                $"Assigned={FormatHours(worker.AssignedHours)}h, " +
                $"Target={FormatHours(worker.TargetHours)}h, " +
                $"Gap={FormatSignedHours(worker.GapToTarget)}h, " +
                $"SubmittedPreferred={FormatHours(worker.SubmittedPreferredHours)}h, " +
                $"AssignedPreferred={FormatHours(worker.AssignedPreferredHours)}h, " +
                $"UnsatisfiedPreferred={FormatHours(worker.UnsatisfiedPreferredHours)}h");
        }

        builder.AppendLine();
    }

    private static void AppendAddMoveDiagnostics(
        StringBuilder builder,
        TargetGapExplainabilityDiagnostic diagnostic)
    {
        builder.AppendLine("AddMoveDiagnostics:");
        builder.AppendLine($"UnderTargetWorkerCount: {diagnostic.UnderTargetWorkerCount}");
        builder.AppendLine($"CandidateAddMoveCount: {diagnostic.CandidateAddMoveCount}");
        builder.AppendLine($"ScoreImprovingAddMoveCount: {diagnostic.ScoreImprovingAddMoveCount}");
        builder.AppendLine($"FairnessImprovingButScoreNotImprovingAddMoveCount: {diagnostic.FairnessImprovingButScoreNotImprovingAddMoveCount}");
        builder.AppendLine();
    }

    private static void AppendScoreImprovingAddMoveDetails(
        StringBuilder builder,
        TargetGapExplainabilityDiagnostic diagnostic)
    {
        builder.AppendLine("ScoreImprovingAddMoveDetails:");

        if (diagnostic.ScoreImprovingAddMoveDetails.Count == 0)
        {
            builder.AppendLine("- none");
            builder.AppendLine();
            return;
        }

        foreach (var move in diagnostic.ScoreImprovingAddMoveDetails)
        {
            builder.AppendLine(
                $"- {move.ResourceName} | " +
                $"Shift={move.ShiftKind} {move.ShiftStartUtc:yyyy-MM-dd HH:mm}-{move.ShiftEndUtc:HH:mm} UTC | " +
                $"Penalty={move.BaseTotalPenalty}->{move.NewTotalPenalty} Delta={move.PenaltyDelta} | " +
                $"Hours={FormatHours(move.BaseAssignedHours)}->{FormatHours(move.NewAssignedHours)} Target={FormatHours(move.TargetHours)}");
        }

        builder.AppendLine();
    }

    private static void AppendRejectedAddMoves(
        StringBuilder builder,
        TargetGapExplainabilityDiagnostic diagnostic)
    {
        builder.AppendLine("RejectedAddMoves:");
        builder.AppendLine($"RejectedShiftAtMaxCapacity: {diagnostic.RejectedShiftAtMaxCapacity}");
        builder.AppendLine($"RejectedAlreadyAssignedToShift: {diagnostic.RejectedAlreadyAssignedToShift}");
        builder.AppendLine($"RejectedUnavailable: {diagnostic.RejectedUnavailable}");
        builder.AppendLine($"RejectedMissingRequiredPreference: {diagnostic.RejectedMissingRequiredPreference}");
        builder.AppendLine($"RejectedOverlap: {diagnostic.RejectedOverlap}");
        builder.AppendLine($"RejectedHardViolation: {diagnostic.RejectedHardViolation}");
        builder.AppendLine($"RejectedNotImproving: {diagnostic.RejectedNotImproving}");
        builder.AppendLine();
    }

    private static void AppendRejectedHardViolationByType(
        StringBuilder builder,
        TargetGapExplainabilityDiagnostic diagnostic)
    {
        builder.AppendLine("RejectedHardViolationByType:");

        if (diagnostic.RejectedHardViolationByType.Count == 0)
        {
            builder.AppendLine("- none");
            builder.AppendLine();
            return;
        }

        foreach (var item in diagnostic.RejectedHardViolationByType
                     .OrderBy(item => item.Key.ToString(), StringComparer.Ordinal))
        {
            builder.AppendLine($"- {item.Key}: {item.Value}");
        }

        builder.AppendLine();
    }

    private static void AppendTransferMoveDiagnostics(
        StringBuilder builder,
        TargetGapExplainabilityDiagnostic diagnostic)
    {
        builder.AppendLine("TransferMoveDiagnostics:");
        builder.AppendLine($"CandidateTransferMoveCount: {diagnostic.CandidateTransferMoveCount}");
        builder.AppendLine($"ScoreImprovingTransferMoveCount: {diagnostic.ScoreImprovingTransferMoveCount}");
        builder.AppendLine($"FairnessImprovingButScoreNotImprovingTransferMoveCount: {diagnostic.FairnessImprovingButScoreNotImprovingTransferMoveCount}");
        builder.AppendLine();
    }

    private static string FormatHours(double value)
    {
        return value.ToString("0.0", CultureInfo.InvariantCulture);
    }

    private static string FormatSignedHours(double value)
    {
        return value.ToString("+0.0;-0.0;+0.0", CultureInfo.InvariantCulture);
    }
}
