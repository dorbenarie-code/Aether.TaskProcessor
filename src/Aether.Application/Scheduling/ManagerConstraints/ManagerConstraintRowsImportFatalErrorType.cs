namespace Aether.Application.Scheduling.ManagerConstraints;

public enum ManagerConstraintRowsImportFatalErrorType
{
    EmptyTable = 1,
    MissingRequiredColumn = 2,
    UnknownConstraintType = 3,
    MissingWorkerName = 4,
    UnresolvedWorkerName = 5,
    InvalidDate = 6,
    DateOutsideSchedulePeriod = 7,
    InvalidShiftKind = 8,
    ShiftNotFound = 9,
    AmbiguousShift = 10,
    MissingCapacityValue = 11,
    InvalidCapacityValue = 12,
    UnexpectedWorkerName = 13,
    UnexpectedCapacityValue = 14
}
