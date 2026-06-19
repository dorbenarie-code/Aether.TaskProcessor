namespace Aether.Application.Scheduling.SubmissionForms;

public sealed record WorkerSubmission(
    Guid ResourceId,
    IReadOnlyCollection<WorkerShiftSubmission> ShiftSubmissions);
