using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.SubmissionForms;

public sealed record GoogleFormsShiftCellSelection(
    int RowIndex,
    int ColumnIndex,
    DateOnly Date,
    ShiftKind ShiftKind,
    ShiftSubmissionChoice Choice);
