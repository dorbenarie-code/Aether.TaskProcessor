namespace Aether.Domain.Optimization;

public sealed class Shift
{
    public Guid Id { get; }
    public DateTime StartUtc { get; }
    public DateTime EndUtc { get; }
    public ShiftKind Kind { get; }
    public NightShiftCategory? NightShiftCategory { get; }
    public int MinResourceCount { get; }
    public int MaxResourceCount { get; }
    public bool RequiresPreferenceToAssign { get; }
    public bool RequiresMinimumWhenPreferenceExists { get; }

    public Shift(
        Guid id,
        DateTime startUtc,
        DateTime endUtc,
        ShiftKind kind,
        int minResourceCount,
        int maxResourceCount,
        bool requiresPreferenceToAssign = false,
        bool requiresMinimumWhenPreferenceExists = false,
        NightShiftCategory? nightShiftCategory = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Shift id cannot be empty.", nameof(id));
        }

        EnsureUtc(startUtc, nameof(startUtc));
        EnsureUtc(endUtc, nameof(endUtc));

        if (endUtc <= startUtc)
        {
            throw new ArgumentException("Shift end time must be later than start time.", nameof(endUtc));
        }

        if (!Enum.IsDefined(typeof(ShiftKind), kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "Shift kind is not supported.");
        }

        if (nightShiftCategory.HasValue &&
            !Enum.IsDefined(typeof(NightShiftCategory), nightShiftCategory.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(nightShiftCategory),
                "Night shift category is not supported.");
        }

        if (nightShiftCategory.HasValue && kind != ShiftKind.Night)
        {
            throw new ArgumentException(
                "Night shift category can only be assigned to night shifts.",
                nameof(nightShiftCategory));
        }

        if (minResourceCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minResourceCount),
                "Minimum resource count cannot be negative.");
        }

        if (maxResourceCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxResourceCount),
                "Maximum resource count must be greater than zero.");
        }

        if (maxResourceCount < minResourceCount)
        {
            throw new ArgumentException(
                "Maximum resource count cannot be lower than minimum resource count.",
                nameof(maxResourceCount));
        }

        Id = id;
        StartUtc = startUtc;
        EndUtc = endUtc;
        Kind = kind;
        NightShiftCategory = nightShiftCategory;
        MinResourceCount = minResourceCount;
        MaxResourceCount = maxResourceCount;
        RequiresPreferenceToAssign = requiresPreferenceToAssign;
        RequiresMinimumWhenPreferenceExists = requiresMinimumWhenPreferenceExists;
    }

    private static void EnsureUtc(DateTime value, string paramName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("DateTime value must be in UTC.", paramName);
        }
    }
}
