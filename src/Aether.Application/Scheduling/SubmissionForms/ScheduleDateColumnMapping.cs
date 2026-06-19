namespace Aether.Application.Scheduling.SubmissionForms;

public sealed record ScheduleDateColumnMapping(
    DateOnly Date,
    int ColumnIndex,
    string Header);
