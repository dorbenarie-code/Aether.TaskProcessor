using Aether.Application.Scheduling.SubmissionForms;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class GoogleFormsWorkerSubmissionAssemblerTests
{
    [Fact]
    public void Assemble_ShouldCreateWorkerSubmission_ForAcceptedWorkerRowSelections()
    {
        var resources = CreateResources();

        var acceptedRows = new[]
        {
            CreateAcceptedRow(5, resources[0])
        };

        var selections = new[]
        {
            CreateSelection(5, new DateOnly(2026, 5, 31), ShiftKind.Morning),
            CreateSelection(5, new DateOnly(2026, 6, 1), ShiftKind.Night)
        };

        var assembler = new GoogleFormsWorkerSubmissionAssembler();

        var result = assembler.Assemble(new GoogleFormsWorkerSubmissionAssemblyRequest(
            acceptedRows,
            selections));

        Assert.Empty(result.Warnings);

        var submission = Assert.Single(result.WorkerSubmissions);

        Assert.Equal(resources[0].Id, submission.ResourceId);
        Assert.Equal(2, submission.ShiftSubmissions.Count);

        Assert.Contains(
            submission.ShiftSubmissions,
            shift =>
                shift.Date == new DateOnly(2026, 5, 31) &&
                shift.ShiftKind == ShiftKind.Morning &&
                shift.Choice == ShiftSubmissionChoice.StrongAvailable);

        Assert.Contains(
            submission.ShiftSubmissions,
            shift =>
                shift.Date == new DateOnly(2026, 6, 1) &&
                shift.ShiftKind == ShiftKind.Night &&
                shift.Choice == ShiftSubmissionChoice.StrongAvailable);
    }

    [Fact]
    public void Assemble_ShouldIgnoreSelections_FromNonAcceptedRows()
    {
        var resources = CreateResources();

        var acceptedRows = new[]
        {
            CreateAcceptedRow(2, resources[0])
        };

        var selections = new[]
        {
            CreateSelection(1, new DateOnly(2026, 5, 31), ShiftKind.Morning),
            CreateSelection(2, new DateOnly(2026, 6, 1), ShiftKind.Night)
        };

        var assembler = new GoogleFormsWorkerSubmissionAssembler();

        var result = assembler.Assemble(new GoogleFormsWorkerSubmissionAssemblyRequest(
            acceptedRows,
            selections));

        Assert.Empty(result.Warnings);

        var submission = Assert.Single(result.WorkerSubmissions);
        var shiftSubmission = Assert.Single(submission.ShiftSubmissions);

        Assert.Equal(resources[0].Id, submission.ResourceId);
        Assert.Equal(new DateOnly(2026, 6, 1), shiftSubmission.Date);
        Assert.Equal(ShiftKind.Night, shiftSubmission.ShiftKind);
    }

    [Fact]
    public void Assemble_ShouldCreateEmptyWorkerSubmission_WhenAcceptedRowHasNoSelections()
    {
        var resources = CreateResources();

        var acceptedRows = new[]
        {
            CreateAcceptedRow(2, resources[0])
        };

        var selections = new[]
        {
            CreateSelection(1, new DateOnly(2026, 5, 31), ShiftKind.Morning)
        };

        var assembler = new GoogleFormsWorkerSubmissionAssembler();

        var result = assembler.Assemble(new GoogleFormsWorkerSubmissionAssemblyRequest(
            acceptedRows,
            selections));

        Assert.Empty(result.Warnings);

        var submission = Assert.Single(result.WorkerSubmissions);

        Assert.Equal(resources[0].Id, submission.ResourceId);
        Assert.Empty(submission.ShiftSubmissions);
    }

    [Fact]
    public void Assemble_ShouldCreateSubmissions_ForMultipleAcceptedWorkers()
    {
        var resources = CreateResources();

        var acceptedRows = new[]
        {
            CreateAcceptedRow(1, resources[0]),
            CreateAcceptedRow(2, resources[1])
        };

        var selections = new[]
        {
            CreateSelection(1, new DateOnly(2026, 5, 31), ShiftKind.Morning),
            CreateSelection(2, new DateOnly(2026, 6, 1), ShiftKind.Afternoon)
        };

        var assembler = new GoogleFormsWorkerSubmissionAssembler();

        var result = assembler.Assemble(new GoogleFormsWorkerSubmissionAssemblyRequest(
            acceptedRows,
            selections));

        Assert.Empty(result.Warnings);
        Assert.Equal(2, result.WorkerSubmissions.Count);

        Assert.Contains(
            result.WorkerSubmissions,
            submission =>
                submission.ResourceId == resources[0].Id &&
                submission.ShiftSubmissions.Single().ShiftKind == ShiftKind.Morning);

        Assert.Contains(
            result.WorkerSubmissions,
            submission =>
                submission.ResourceId == resources[1].Id &&
                submission.ShiftSubmissions.Single().ShiftKind == ShiftKind.Afternoon);
    }

    [Fact]
    public void Assemble_ShouldPreserveShiftSubmissionChoice()
    {
        var resources = CreateResources();

        var acceptedRows = new[]
        {
            CreateAcceptedRow(1, resources[0])
        };

        var selections = new[]
        {
            CreateSelection(
                1,
                new DateOnly(2026, 5, 31),
                ShiftKind.Morning,
                ShiftSubmissionChoice.Available)
        };

        var assembler = new GoogleFormsWorkerSubmissionAssembler();

        var result = assembler.Assemble(new GoogleFormsWorkerSubmissionAssemblyRequest(
            acceptedRows,
            selections));

        Assert.Empty(result.Warnings);

        var submission = Assert.Single(result.WorkerSubmissions);
        var shiftSubmission = Assert.Single(submission.ShiftSubmissions);

        Assert.Equal(ShiftSubmissionChoice.Available, shiftSubmission.Choice);
    }

    private static IReadOnlyList<Resource> CreateResources()
    {
        return
        [
            new Resource(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                "Worker16 אלדר",
                hourlyCost: 100m),
            new Resource(
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                "Worker14",
                hourlyCost: 100m)
        ];
    }

    private static GoogleFormsResolvedWorkerRow CreateAcceptedRow(
        int rowIndex,
        Resource resource)
    {
        return new GoogleFormsResolvedWorkerRow(
            rowIndex,
            resource.Name,
            resource.Id,
            resource.Name);
    }

    private static GoogleFormsShiftCellSelection CreateSelection(
        int rowIndex,
        DateOnly date,
        ShiftKind shiftKind,
        ShiftSubmissionChoice choice = ShiftSubmissionChoice.StrongAvailable)
    {
        return new GoogleFormsShiftCellSelection(
            RowIndex: rowIndex,
            ColumnIndex: 2,
            Date: date,
            ShiftKind: shiftKind,
            Choice: choice);
    }
}
