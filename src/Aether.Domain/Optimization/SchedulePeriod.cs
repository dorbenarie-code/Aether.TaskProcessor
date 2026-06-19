namespace Aether.Domain.Optimization;

public sealed class SchedulePeriod
{
    public DateTime StartUtc { get; }
    public DateTime EndUtc { get; }

    public SchedulePeriod(
        DateTime startUtc,
        DateTime endUtc)
    {
        EnsureUtc(startUtc, nameof(startUtc));
        EnsureUtc(endUtc, nameof(endUtc));

        if (endUtc <= startUtc)
        {
            throw new ArgumentException(
                "Schedule period end time must be later than start time.",
                nameof(endUtc));
        }

        StartUtc = startUtc;
        EndUtc = endUtc;
    }

    private static void EnsureUtc(DateTime value, string paramName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("DateTime value must be in UTC.", paramName);
        }
    }
}
