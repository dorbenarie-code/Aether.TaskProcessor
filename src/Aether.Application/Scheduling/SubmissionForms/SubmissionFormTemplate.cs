namespace Aether.Application.Scheduling.SubmissionForms;

public sealed record SubmissionFormTemplate(
    DateOnly StartDate,
    DateOnly EndDate,
    DateOnly NextStartDate,
    IReadOnlyCollection<SubmissionFormDay> Days);
