namespace Aether.Application.Scheduling.SubmissionForms;

public enum GoogleFormsImportWarningType
{
    DateColumnOutsideSchedulePeriod = 1,
    InvalidTimestamp = 2,
    InvalidShiftSelectionToken = 3,
    UnresolvedWorkerName = 4,
    DuplicateWorkerSubmissionIgnored = 5,
    InvalidResolvedWorkerSubmissionTimestamp = 6
}
