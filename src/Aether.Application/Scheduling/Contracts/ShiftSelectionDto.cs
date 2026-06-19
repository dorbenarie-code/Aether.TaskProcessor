using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.Contracts;

public sealed record ShiftSelectionDto(
    DateOnly Date,
    ShiftKind ShiftKind,
    bool IsSelected);
