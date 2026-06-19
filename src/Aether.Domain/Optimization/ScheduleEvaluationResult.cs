namespace Aether.Domain.Optimization;

public sealed class ScheduleEvaluationResult
{
    public ScheduleScore Score { get; }
    public IReadOnlyCollection<ConstraintViolation> Violations { get; }
    public bool IsFeasible => Score.IsFeasible;

    public ScheduleEvaluationResult(
        ScheduleScore score,
        IReadOnlyCollection<ConstraintViolation> violations)
    {
        ArgumentNullException.ThrowIfNull(score);
        ArgumentNullException.ThrowIfNull(violations);

        Score = score;
        Violations = violations.ToArray();
    }
}
