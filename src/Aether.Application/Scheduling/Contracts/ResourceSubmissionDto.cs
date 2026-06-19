namespace Aether.Application.Scheduling.Contracts;

public sealed record ResourceSubmissionDto(
    string ResourceName,
    IReadOnlyCollection<ShiftSelectionDto> ShiftSelections,
    string? RawSpecialRequestNote = null);
