namespace Aether.Application.Scheduling.SubmissionForms;

public enum WorkerSubmissionAggregationWarningType
{
    UnknownResource = 1,
    DuplicateWorkerSubmission = 2,
    DuplicateShiftSubmission = 3,
    DateOutsidePeriod = 4,
    NoMatchingShift = 5
}
