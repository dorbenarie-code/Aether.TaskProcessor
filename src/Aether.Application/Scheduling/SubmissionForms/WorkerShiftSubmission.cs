using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.SubmissionForms;

public sealed record WorkerShiftSubmission(
    DateOnly Date,
    ShiftKind ShiftKind,
    ShiftSubmissionChoice Choice);
