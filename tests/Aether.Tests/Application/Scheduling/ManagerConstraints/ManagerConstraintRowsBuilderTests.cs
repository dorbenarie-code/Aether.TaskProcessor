using Aether.Application.Scheduling.ManagerConstraints;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling.ManagerConstraints;

public sealed class ManagerConstraintRowsBuilderTests
{
    [Fact]
    public void Build_ShouldReturnNull_WhenAllDraftRowsAreEmpty()
    {
        var builder = new ManagerConstraintRowsBuilder();

        var rows = builder.Build(
        [
            new ManagerConstraintDraftRow(),
            new ManagerConstraintDraftRow
            {
                WorkerName = "   "
            }
        ]);

        Assert.Null(rows);
    }

    [Fact]
    public void Build_ShouldCreateRows_ForSupportedManagerConstraintDraftRows()
    {
        var builder = new ManagerConstraintRowsBuilder();

        var rows = builder.Build(
        [
            new ManagerConstraintDraftRow
            {
                Type = ManagerConstraintDraftType.ForbidAssignment,
                WorkerName = "  Worker09  ",
                Date = new DateTime(2026, 6, 22),
                ShiftKind = ShiftKind.Morning
            },
            new ManagerConstraintDraftRow
            {
                Type = ManagerConstraintDraftType.AvoidAssignment,
                WorkerName = "Worker14",
                Date = new DateTime(2026, 6, 25),
                ShiftKind = ShiftKind.Morning
            },
            new ManagerConstraintDraftRow
            {
                Type = ManagerConstraintDraftType.ShiftCapacityOverride,
                Date = new DateTime(2026, 6, 19),
                ShiftKind = ShiftKind.Morning,
                MinResourceCount = 3,
                MaxResourceCount = 3
            }
        ]);

        Assert.NotNull(rows);

        var actualRows = rows!;

        Assert.Equal(4, actualRows.Count);

        Assert.Equal(
            new[]
            {
                "Type",
                "WorkerName",
                "Date",
                "ShiftKind",
                "MinResourceCount",
                "MaxResourceCount"
            },
            actualRows[0]);

        Assert.Equal(
            new[]
            {
                "ForbidAssignment",
                "Worker09",
                "2026-06-22",
                "Morning",
                "",
                ""
            },
            actualRows[1]);

        Assert.Equal(
            new[]
            {
                "AvoidAssignment",
                "Worker14",
                "2026-06-25",
                "Morning",
                "",
                ""
            },
            actualRows[2]);

        Assert.Equal(
            new[]
            {
                "ShiftCapacityOverride",
                "",
                "2026-06-19",
                "Morning",
                "3",
                "3"
            },
            actualRows[3]);
    }

    [Fact]
    public void Build_ShouldKeepPartiallyFilledRows_ForExistingImporterValidation()
    {
        var builder = new ManagerConstraintRowsBuilder();

        var rows = builder.Build(
        [
            new ManagerConstraintDraftRow
            {
                Type = ManagerConstraintDraftType.ForbidAssignment
            },
            new ManagerConstraintDraftRow
            {
                WorkerName = "Worker14"
            }
        ]);

        Assert.NotNull(rows);

        var actualRows = rows!;

        Assert.Equal(3, actualRows.Count);

        Assert.Equal(
            new[]
            {
                "Type",
                "WorkerName",
                "Date",
                "ShiftKind",
                "MinResourceCount",
                "MaxResourceCount"
            },
            actualRows[0]);

        Assert.Equal(
            new[]
            {
                "ForbidAssignment",
                "",
                "",
                "",
                "",
                ""
            },
            actualRows[1]);

        Assert.Equal(
            new[]
            {
                "",
                "Worker14",
                "",
                "",
                "",
                ""
            },
            actualRows[2]);
    }
}
