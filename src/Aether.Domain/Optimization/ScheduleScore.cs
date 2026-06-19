namespace Aether.Domain.Optimization;

public sealed class ScheduleScore
{
    public const int MinimumValue = 0;
    public const int MaximumValue = 1000;

    public int Value { get; }
    public int HardViolationCount { get; }
    public int SoftViolationCount { get; }
    public int TotalPenalty { get; }
    public bool IsFeasible => HardViolationCount == 0;

    public ScheduleScore(
        int value,
        int hardViolationCount,
        int softViolationCount,
        int totalPenalty)
    {
        if (value is < MinimumValue or > MaximumValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                $"Score value must be between {MinimumValue} and {MaximumValue}.");
        }

        if (hardViolationCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(hardViolationCount),
                "Hard violation count cannot be negative.");
        }

        if (softViolationCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(softViolationCount),
                "Soft violation count cannot be negative.");
        }

        if (totalPenalty < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalPenalty),
                "Total penalty cannot be negative.");
        }

        Value = value;
        HardViolationCount = hardViolationCount;
        SoftViolationCount = softViolationCount;
        TotalPenalty = totalPenalty;
    }
}
