namespace Aether.Application.Scheduling.SubmissionForms;

public sealed record SubmissionFormDay(
    DateOnly Date,
    DayOfWeek DayOfWeek,
    IReadOnlyCollection<SubmissionShiftOption> ShiftOptions);
