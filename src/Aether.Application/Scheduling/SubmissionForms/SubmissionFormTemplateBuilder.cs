using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.SubmissionForms;

public sealed class SubmissionFormTemplateBuilder
{
    private const int DaysInSubmissionPeriod = 14;

    public SubmissionFormTemplate Build(DateOnly startDate)
    {
        if (startDate.DayOfWeek != DayOfWeek.Sunday)
        {
            throw new ArgumentException(
                "Submission form start date must be a Sunday.",
                nameof(startDate));
        }

        var days = Enumerable.Range(0, DaysInSubmissionPeriod)
            .Select(offset => CreateDay(startDate.AddDays(offset)))
            .ToArray();

        return new SubmissionFormTemplate(
            StartDate: startDate,
            EndDate: startDate.AddDays(DaysInSubmissionPeriod - 1),
            NextStartDate: startDate.AddDays(DaysInSubmissionPeriod),
            Days: days);
    }

    private static SubmissionFormDay CreateDay(DateOnly date)
    {
        var shiftOptions = new[]
        {
            CreateShiftOption(date, ShiftKind.Morning),
            CreateShiftOption(date, ShiftKind.Afternoon),
            CreateShiftOption(date, ShiftKind.Night)
        };

        return new SubmissionFormDay(
            Date: date,
            DayOfWeek: date.DayOfWeek,
            ShiftOptions: shiftOptions);
    }

    private static SubmissionShiftOption CreateShiftOption(
        DateOnly date,
        ShiftKind shiftKind)
    {
        return new SubmissionShiftOption(
            Date: date,
            ShiftKind: shiftKind,
            DefaultChoice: ShiftSubmissionChoice.Unavailable);
    }
}
