namespace Aether.Domain.Optimization;

public sealed class ScheduleEvaluationResultRanker
{
    public bool IsBetterThan(
        ScheduleEvaluationResult candidate,
        ScheduleEvaluationResult currentBest)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(currentBest);

        if (candidate.IsFeasible != currentBest.IsFeasible)
        {
            return candidate.IsFeasible;
        }

        if (candidate.Score.HardViolationCount != currentBest.Score.HardViolationCount)
        {
            return candidate.Score.HardViolationCount < currentBest.Score.HardViolationCount;
        }

        if (candidate.Score.TotalPenalty != currentBest.Score.TotalPenalty)
        {
            return candidate.Score.TotalPenalty < currentBest.Score.TotalPenalty;
        }

        return candidate.Score.Value > currentBest.Score.Value;
    }
}
