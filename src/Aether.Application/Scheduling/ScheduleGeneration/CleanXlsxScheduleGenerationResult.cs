using Aether.Application.Scheduling.ManagerConstraints;
using Aether.Application.Scheduling.SubmissionForms;

namespace Aether.Application.Scheduling.ScheduleGeneration;

public sealed record CleanXlsxScheduleGenerationResult(
    bool Succeeded,
    IReadOnlyList<AvailabilityMatrixImportWarning> ImportWarnings,
    IReadOnlyList<AvailabilityMatrixImportFatalError> ImportFatalErrors,
    IReadOnlyList<ManagerConstraintRowsImportWarning> ManagerConstraintImportWarnings,
    IReadOnlyList<ManagerConstraintRowsImportFatalError> ManagerConstraintImportFatalErrors,
    ManagerConstraintImportSummary ManagerConstraintImportSummary,
    string? ReviewText = null,
    string? ScheduleTableCsv = null,
    byte[]? ScheduleTableXlsxBytes = null,
    string? TargetGapDiagnosticsText = null,
    ScheduleGenerationRunSummary? Summary = null,
    ScheduleGenerationFailureType FailureType = ScheduleGenerationFailureType.None);
