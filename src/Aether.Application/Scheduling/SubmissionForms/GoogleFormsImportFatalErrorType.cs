namespace Aether.Application.Scheduling.SubmissionForms;

public enum GoogleFormsImportFatalErrorType
{
    EmptyTable = 1,
    MissingTimestampColumn = 2,
    MissingWorkerNameColumn = 3,
    MissingScheduleDateColumn = 4,
    DuplicateScheduleDateColumn = 5,
    NoRowsInsideSubmittedAtWindow = 6
}
