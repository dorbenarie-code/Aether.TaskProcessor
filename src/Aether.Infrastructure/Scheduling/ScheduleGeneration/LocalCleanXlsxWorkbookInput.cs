namespace Aether.Infrastructure.Scheduling.ScheduleGeneration;

public sealed class LocalCleanXlsxWorkbookInput : IDisposable
{
    public Stream AvailabilityMatrixStream { get; }

    public IReadOnlyList<IReadOnlyList<string>>? ManagerConstraintRows { get; }

    public LocalCleanXlsxWorkbookInput(
        Stream availabilityMatrixStream,
        IReadOnlyList<IReadOnlyList<string>>? managerConstraintRows)
    {
        AvailabilityMatrixStream = availabilityMatrixStream ??
            throw new ArgumentNullException(nameof(availabilityMatrixStream));

        ManagerConstraintRows = managerConstraintRows;
    }

    public void Dispose()
    {
        AvailabilityMatrixStream.Dispose();
    }
}
