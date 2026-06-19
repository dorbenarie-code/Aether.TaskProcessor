namespace Aether.Application.Scheduling.ManagerConstraints;

public sealed record ManagerConstraintRowsImportResult
{
    public ManagerConstraintSet ConstraintSet { get; }
    public IReadOnlyList<ManagerConstraintRowsImportWarning> Warnings { get; }
    public IReadOnlyList<ManagerConstraintRowsImportFatalError> FatalErrors { get; }
    public ManagerConstraintImportSummary Summary { get; }

    public ManagerConstraintRowsImportResult(
        ManagerConstraintSet constraintSet,
        IReadOnlyList<ManagerConstraintRowsImportWarning> warnings,
        IReadOnlyList<ManagerConstraintRowsImportFatalError> fatalErrors,
        ManagerConstraintImportSummary? summary = null)
    {
        ArgumentNullException.ThrowIfNull(constraintSet);
        ArgumentNullException.ThrowIfNull(warnings);
        ArgumentNullException.ThrowIfNull(fatalErrors);

        ConstraintSet = constraintSet;
        Warnings = warnings.ToArray();
        FatalErrors = fatalErrors.ToArray();
        Summary = summary ?? ManagerConstraintImportSummary.Empty;
    }
}
