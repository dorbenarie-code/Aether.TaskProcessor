namespace Aether.Domain.Optimization;

public sealed class ScheduleEvaluator
{
    private readonly ScheduleConstraintEvaluator _constraintEvaluator;
    private readonly ScheduleScoreCalculator _scoreCalculator;

    public ScheduleEvaluator()
        : this(ScheduleScoringWeights.CreateDefault())
    {
    }

    public ScheduleEvaluator(ScheduleScoringWeights scoringWeights)
    {
        ArgumentNullException.ThrowIfNull(scoringWeights);

        _constraintEvaluator = new ScheduleConstraintEvaluator();
        _scoreCalculator = new ScheduleScoreCalculator(scoringWeights);
    }

    public ScheduleEvaluationResult Evaluate(
        SchedulingProblem problem,
        ScheduleCandidate candidate)
    {
        var violations = _constraintEvaluator.Evaluate(problem, candidate);
        var score = _scoreCalculator.Calculate(violations);

        return new ScheduleEvaluationResult(score, violations);
    }
}
