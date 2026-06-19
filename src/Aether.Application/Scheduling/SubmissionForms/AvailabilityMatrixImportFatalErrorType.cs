namespace Aether.Application.Scheduling.SubmissionForms;

public enum AvailabilityMatrixImportFatalErrorType
{
    EmptyTable = 1,
    MissingWorkerNameColumn = 2,
    MissingScheduleDateColumn = 3,
    DuplicateScheduleDateColumn = 4,
    DuplicateWorkerRow = 5,
    UnresolvedWorkerName = 6,
    MissingWorkerNameForNonEmptyRow = 7
}
