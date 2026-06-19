namespace Aether.Application.Scheduling.Reports;

public sealed record ScheduleTableProjection(
    IReadOnlyList<ScheduleTableDayProjection> Days,
    int MorningSlotCount,
    int AfternoonSlotCount,
    int NightSlotCount)
{
    public string MorningTimeRangeText { get; init; } = string.Empty;
    public string AfternoonTimeRangeText { get; init; } = string.Empty;
    public string NightTimeRangeText { get; init; } = string.Empty;
}

public sealed record ScheduleTableDayProjection(
    DateOnly Date,
    DayOfWeek DayOfWeek,
    IReadOnlyList<string> MorningWorkerNames,
    IReadOnlyList<string> AfternoonWorkerNames,
    IReadOnlyList<string> NightWorkerNames);
