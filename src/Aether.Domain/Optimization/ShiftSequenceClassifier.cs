namespace Aether.Domain.Optimization;

public sealed class ShiftSequenceClassifier
{
    private static readonly TimeSpan MinimumRest = TimeSpan.FromHours(8);

    public ShiftSequenceType? Classify(
        Shift previousShift,
        Shift nextShift)
    {
        ArgumentNullException.ThrowIfNull(previousShift);
        ArgumentNullException.ThrowIfNull(nextShift);

        var restGap = nextShift.StartUtc - previousShift.EndUtc;

        if (restGap < TimeSpan.Zero)
        {
            return null;
        }

        if (restGap >= MinimumRest)
        {
            return null;
        }

        return (previousShift.Kind, nextShift.Kind) switch
        {
            (ShiftKind.Night, ShiftKind.Afternoon) => ShiftSequenceType.NightToAfternoon,
            (ShiftKind.Afternoon, ShiftKind.Morning) => ShiftSequenceType.AfternoonToMorning,
            _ => null
        };
    }
}
