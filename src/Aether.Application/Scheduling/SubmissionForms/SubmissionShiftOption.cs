using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.SubmissionForms;

public sealed record SubmissionShiftOption(
    DateOnly Date,
    ShiftKind ShiftKind,
    ShiftSubmissionChoice DefaultChoice);
