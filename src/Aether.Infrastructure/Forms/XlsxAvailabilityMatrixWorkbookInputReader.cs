namespace Aether.Infrastructure.Forms;

public sealed class XlsxAvailabilityMatrixWorkbookInput : IDisposable
{
    public Stream AvailabilityMatrixStream { get; }
    public IReadOnlyList<IReadOnlyList<string>>? ManagerConstraintRows { get; }

    public XlsxAvailabilityMatrixWorkbookInput(
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

public sealed class XlsxAvailabilityMatrixWorkbookInputReader
{
    private const string ManagerConstraintsWorksheetName = "ManagerConstraints";

    public XlsxAvailabilityMatrixWorkbookInput Open(string workbookPath)
    {
        if (string.IsNullOrWhiteSpace(workbookPath))
        {
            throw new ArgumentException(
                "Workbook path is required.",
                nameof(workbookPath));
        }

        var managerConstraintRows = ReadManagerConstraintRows(workbookPath);
        var availabilityMatrixStream = File.OpenRead(workbookPath);

        return new XlsxAvailabilityMatrixWorkbookInput(
            availabilityMatrixStream,
            managerConstraintRows);
    }

    private static IReadOnlyList<IReadOnlyList<string>>? ReadManagerConstraintRows(
        string workbookPath)
    {
        using var stream = File.OpenRead(workbookPath);

        var result = new XlsxWorkbookSheetTableReader()
            .ReadOptionalSheet(
                stream,
                ManagerConstraintsWorksheetName);

        if (!result.SheetFound || result.Rows.Count == 0)
        {
            return null;
        }

        return result.Rows;
    }
}
