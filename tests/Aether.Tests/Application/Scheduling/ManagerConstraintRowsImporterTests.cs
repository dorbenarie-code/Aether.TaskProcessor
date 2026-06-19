using Aether.Application.Scheduling.ManagerConstraints;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class ManagerConstraintRowsImporterTests
{
    [Fact]
    public void Import_ShouldCreateManagerConstraintSet_FromValidRows()
    {
        var resources = CreateResources();
        var shifts = CreateShifts();

        IReadOnlyList<IReadOnlyList<string>> rows =
        [
            CreateHeaders(),
            CreateRow(
                "ForbidAssignment",
                "דור",
                "2026-06-15",
                "Morning",
                string.Empty,
                string.Empty),
            CreateRow(
                "AvoidAssignment",
                "יוסי",
                "2026-06-19",
                "Morning",
                string.Empty,
                string.Empty),
            CreateRow(
                "ShiftCapacityOverride",
                string.Empty,
                "2026-06-18",
                "Night",
                "2",
                "2")
        ];

        var importer = new ManagerConstraintRowsImporter();

        var result = importer.Import(new ManagerConstraintRowsImportRequest(
            rows,
            CreateSchedulePeriod(),
            resources,
            shifts));

        Assert.Empty(result.FatalErrors);
        Assert.Empty(result.Warnings);

        var forbiddenAssignment = Assert.Single(result.ConstraintSet.ForbiddenAssignments);

        Assert.Equal(resources[0].Id, forbiddenAssignment.ResourceId);
        Assert.Equal(shifts[0].Id, forbiddenAssignment.ShiftId);

        var avoidAssignment = Assert.Single(result.ConstraintSet.AvoidAssignments);

        Assert.Equal(resources[1].Id, avoidAssignment.ResourceId);
        Assert.Equal(shifts[2].Id, avoidAssignment.ShiftId);

        var capacityOverride = Assert.Single(result.ConstraintSet.ShiftCapacityOverrides);

        Assert.Equal(shifts[1].Id, capacityOverride.ShiftId);
        Assert.Equal(2, capacityOverride.MinResourceCount);
        Assert.Equal(2, capacityOverride.MaxResourceCount);
    }

    [Fact]
    public void Import_ShouldReturnFatalError_AndEmptyConstraintSet_WhenUnknownConstraintType()
    {
        var result = ImportSingleRow(CreateRow(
            "NotSupported",
            "דור",
            "2026-06-15",
            "Morning",
            string.Empty,
            string.Empty));

        Assert.Empty(result.Warnings);

        var fatalError = Assert.Single(result.FatalErrors);

        Assert.Equal(ManagerConstraintRowsImportFatalErrorType.UnknownConstraintType, fatalError.Type);
        Assert.Equal(1, fatalError.RowIndex);
        Assert.Equal(0, fatalError.ColumnIndex);
        Assert.Equal("Type", fatalError.Header);
        Assert.Equal("NotSupported", fatalError.RawValue);

        AssertEmptyConstraintSet(result.ConstraintSet);
    }

    [Fact]
    public void Import_ShouldReturnFatalError_AndEmptyConstraintSet_WhenWorkerNameCannotBeResolved()
    {
        var result = ImportSingleRow(CreateRow(
            "ForbidAssignment",
            "עובד לא מוכר",
            "2026-06-15",
            "Morning",
            string.Empty,
            string.Empty));

        Assert.Empty(result.Warnings);

        var fatalError = Assert.Single(result.FatalErrors);

        Assert.Equal(ManagerConstraintRowsImportFatalErrorType.UnresolvedWorkerName, fatalError.Type);
        Assert.Equal(1, fatalError.RowIndex);
        Assert.Equal(1, fatalError.ColumnIndex);
        Assert.Equal("WorkerName", fatalError.Header);
        Assert.Equal("עובד לא מוכר", fatalError.RawValue);

        AssertEmptyConstraintSet(result.ConstraintSet);
    }

    [Fact]
    public void Import_ShouldReturnFatalError_AndEmptyConstraintSet_WhenMinCapacityIsNegative()
    {
        var result = ImportSingleRow(CreateRow(
            "ShiftCapacityOverride",
            string.Empty,
            "2026-06-18",
            "Night",
            "-1",
            "2"));

        Assert.Empty(result.Warnings);

        var fatalError = Assert.Single(result.FatalErrors);

        Assert.Equal(ManagerConstraintRowsImportFatalErrorType.InvalidCapacityValue, fatalError.Type);
        Assert.Equal(1, fatalError.RowIndex);
        Assert.Equal(4, fatalError.ColumnIndex);
        Assert.Equal("MinResourceCount", fatalError.Header);
        Assert.Equal("-1", fatalError.RawValue);

        AssertEmptyConstraintSet(result.ConstraintSet);
    }

    [Fact]
    public void Import_ShouldReturnFatalError_AndEmptyConstraintSet_WhenMaxCapacityIsLowerThanMinCapacity()
    {
        var result = ImportSingleRow(CreateRow(
            "ShiftCapacityOverride",
            string.Empty,
            "2026-06-18",
            "Night",
            "3",
            "2"));

        Assert.Empty(result.Warnings);

        var fatalError = Assert.Single(result.FatalErrors);

        Assert.Equal(ManagerConstraintRowsImportFatalErrorType.InvalidCapacityValue, fatalError.Type);
        Assert.Equal(1, fatalError.RowIndex);
        Assert.Equal(5, fatalError.ColumnIndex);
        Assert.Equal("MaxResourceCount", fatalError.Header);
        Assert.Equal("2", fatalError.RawValue);

        AssertEmptyConstraintSet(result.ConstraintSet);
    }

    [Fact]
    public void Import_ShouldReturnFatalError_AndEmptyConstraintSet_WhenDateIsInvalid()
    {
        var result = ImportSingleRow(CreateRow(
            "ForbidAssignment",
            "דור",
            "15/06/2026",
            "Morning",
            string.Empty,
            string.Empty));

        Assert.Empty(result.Warnings);

        var fatalError = Assert.Single(result.FatalErrors);

        Assert.Equal(ManagerConstraintRowsImportFatalErrorType.InvalidDate, fatalError.Type);
        Assert.Equal(1, fatalError.RowIndex);
        Assert.Equal(2, fatalError.ColumnIndex);
        Assert.Equal("Date", fatalError.Header);
        Assert.Equal("15/06/2026", fatalError.RawValue);

        AssertEmptyConstraintSet(result.ConstraintSet);
    }

    [Fact]
    public void Import_ShouldReturnFatalError_AndEmptyConstraintSet_WhenDateIsOutsideSchedulePeriod()
    {
        var result = ImportSingleRow(CreateRow(
            "ForbidAssignment",
            "דור",
            "2026-06-22",
            "Morning",
            string.Empty,
            string.Empty));

        Assert.Empty(result.Warnings);

        var fatalError = Assert.Single(result.FatalErrors);

        Assert.Equal(ManagerConstraintRowsImportFatalErrorType.DateOutsideSchedulePeriod, fatalError.Type);
        Assert.Equal(1, fatalError.RowIndex);
        Assert.Equal(2, fatalError.ColumnIndex);
        Assert.Equal("Date", fatalError.Header);
        Assert.Equal("2026-06-22", fatalError.RawValue);
        Assert.Equal(new DateOnly(2026, 6, 22), fatalError.Date);

        AssertEmptyConstraintSet(result.ConstraintSet);
    }

    [Fact]
    public void Import_ShouldReturnFatalError_AndEmptyConstraintSet_WhenShiftKindNumericValueIsNotDefined()
    {
        var result = ImportSingleRow(CreateRow(
            "ForbidAssignment",
            "דור",
            "2026-06-15",
            "4",
            string.Empty,
            string.Empty));

        Assert.Empty(result.Warnings);

        var fatalError = Assert.Single(result.FatalErrors);

        Assert.Equal(ManagerConstraintRowsImportFatalErrorType.InvalidShiftKind, fatalError.Type);
        Assert.Equal(1, fatalError.RowIndex);
        Assert.Equal(3, fatalError.ColumnIndex);
        Assert.Equal("ShiftKind", fatalError.Header);
        Assert.Equal("4", fatalError.RawValue);
        Assert.Equal(new DateOnly(2026, 6, 15), fatalError.Date);

        AssertEmptyConstraintSet(result.ConstraintSet);
    }

    [Fact]
    public void Import_ShouldReturnFatalError_AndEmptyConstraintSet_WhenShiftCannotBeFound()
    {
        var result = ImportSingleRow(CreateRow(
            "ForbidAssignment",
            "דור",
            "2026-06-16",
            "Morning",
            string.Empty,
            string.Empty));

        Assert.Empty(result.Warnings);

        var fatalError = Assert.Single(result.FatalErrors);

        Assert.Equal(ManagerConstraintRowsImportFatalErrorType.ShiftNotFound, fatalError.Type);
        Assert.Equal(1, fatalError.RowIndex);
        Assert.Equal(3, fatalError.ColumnIndex);
        Assert.Equal("ShiftKind", fatalError.Header);
        Assert.Equal("Morning", fatalError.RawValue);
        Assert.Equal(new DateOnly(2026, 6, 16), fatalError.Date);
        Assert.Equal(ShiftKind.Morning, fatalError.ShiftKind);

        AssertEmptyConstraintSet(result.ConstraintSet);
    }

    [Fact]
    public void Import_ShouldReturnFatalError_AndEmptyConstraintSet_WhenWorkerNameIsMissingForAssignmentConstraint()
    {
        var result = ImportSingleRow(CreateRow(
            "AvoidAssignment",
            string.Empty,
            "2026-06-19",
            "Morning",
            string.Empty,
            string.Empty));

        Assert.Empty(result.Warnings);

        var fatalError = Assert.Single(result.FatalErrors);

        Assert.Equal(ManagerConstraintRowsImportFatalErrorType.MissingWorkerName, fatalError.Type);
        Assert.Equal(1, fatalError.RowIndex);
        Assert.Equal(1, fatalError.ColumnIndex);
        Assert.Equal("WorkerName", fatalError.Header);
        Assert.Equal(string.Empty, fatalError.RawValue);

        AssertEmptyConstraintSet(result.ConstraintSet);
    }

    [Fact]
    public void Import_ShouldReturnFatalError_AndEmptyConstraintSet_WhenMinCapacityValueIsMissing()
    {
        var result = ImportSingleRow(CreateRow(
            "ShiftCapacityOverride",
            string.Empty,
            "2026-06-18",
            "Night",
            string.Empty,
            "2"));

        Assert.Empty(result.Warnings);

        var fatalError = Assert.Single(result.FatalErrors);

        Assert.Equal(ManagerConstraintRowsImportFatalErrorType.MissingCapacityValue, fatalError.Type);
        Assert.Equal(1, fatalError.RowIndex);
        Assert.Equal(4, fatalError.ColumnIndex);
        Assert.Equal("MinResourceCount", fatalError.Header);
        Assert.Equal(string.Empty, fatalError.RawValue);

        AssertEmptyConstraintSet(result.ConstraintSet);
    }


    [Fact]
    public void Import_ShouldReturnFatalError_AndEmptyConstraintSet_WhenTableIsEmpty()
    {
        var importer = new ManagerConstraintRowsImporter();

        var result = importer.Import(new ManagerConstraintRowsImportRequest(
            [],
            CreateSchedulePeriod(),
            CreateResources(),
            CreateShifts()));

        Assert.Empty(result.Warnings);

        var fatalError = Assert.Single(result.FatalErrors);

        Assert.Equal(ManagerConstraintRowsImportFatalErrorType.EmptyTable, fatalError.Type);

        AssertEmptyConstraintSet(result.ConstraintSet);
    }

    [Fact]
    public void Import_ShouldReturnFatalError_AndEmptyConstraintSet_WhenRequiredColumnIsMissing()
    {
        IReadOnlyList<IReadOnlyList<string>> rows =
        [
            [
                "Type",
                "WorkerName",
                "Date",
                "ShiftKind",
                "MinResourceCount"
            ],
            [
                "ShiftCapacityOverride",
                string.Empty,
                "2026-06-18",
                "Night",
                "2"
            ]
        ];

        var importer = new ManagerConstraintRowsImporter();

        var result = importer.Import(new ManagerConstraintRowsImportRequest(
            rows,
            CreateSchedulePeriod(),
            CreateResources(),
            CreateShifts()));

        Assert.Empty(result.Warnings);

        var fatalError = Assert.Single(result.FatalErrors);

        Assert.Equal(ManagerConstraintRowsImportFatalErrorType.MissingRequiredColumn, fatalError.Type);
        Assert.Equal("MaxResourceCount", fatalError.Header);

        AssertEmptyConstraintSet(result.ConstraintSet);
    }

    [Fact]
    public void Import_ShouldReturnFatalError_AndEmptyConstraintSet_WhenShiftResolutionIsAmbiguous()
    {
        var ambiguousShifts = CreateShifts()
            .Concat(
            [
                new Shift(
                    Guid.Parse("44444444-4444-4444-4444-444444444444"),
                    new DateTime(2026, 6, 15, 7, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 6, 15, 15, 0, 0, DateTimeKind.Utc),
                    ShiftKind.Morning,
                    minResourceCount: 1,
                    maxResourceCount: 6)
            ])
            .ToArray();

        var importer = new ManagerConstraintRowsImporter();

        var result = importer.Import(new ManagerConstraintRowsImportRequest(
            [CreateHeaders(), CreateRow(
                "ForbidAssignment",
                "דור",
                "2026-06-15",
                "Morning",
                string.Empty,
                string.Empty)],
            CreateSchedulePeriod(),
            CreateResources(),
            ambiguousShifts));

        Assert.Empty(result.Warnings);

        var fatalError = Assert.Single(result.FatalErrors);

        Assert.Equal(ManagerConstraintRowsImportFatalErrorType.AmbiguousShift, fatalError.Type);
        Assert.Equal(1, fatalError.RowIndex);
        Assert.Equal(3, fatalError.ColumnIndex);
        Assert.Equal("ShiftKind", fatalError.Header);
        Assert.Equal("Morning", fatalError.RawValue);
        Assert.Equal(new DateOnly(2026, 6, 15), fatalError.Date);
        Assert.Equal(ShiftKind.Morning, fatalError.ShiftKind);

        AssertEmptyConstraintSet(result.ConstraintSet);
    }

    [Fact]
    public void Import_ShouldReturnFatalError_AndEmptyConstraintSet_WhenMaxCapacityValueIsMissing()
    {
        var result = ImportSingleRow(CreateRow(
            "ShiftCapacityOverride",
            string.Empty,
            "2026-06-18",
            "Night",
            "2",
            string.Empty));

        Assert.Empty(result.Warnings);

        var fatalError = Assert.Single(result.FatalErrors);

        Assert.Equal(ManagerConstraintRowsImportFatalErrorType.MissingCapacityValue, fatalError.Type);
        Assert.Equal(1, fatalError.RowIndex);
        Assert.Equal(5, fatalError.ColumnIndex);
        Assert.Equal("MaxResourceCount", fatalError.Header);
        Assert.Equal(string.Empty, fatalError.RawValue);

        AssertEmptyConstraintSet(result.ConstraintSet);
    }

    [Fact]
    public void Import_ShouldReturnFatalError_AndEmptyConstraintSet_WhenMaxCapacityValueIsNotANumber()
    {
        var result = ImportSingleRow(CreateRow(
            "ShiftCapacityOverride",
            string.Empty,
            "2026-06-18",
            "Night",
            "2",
            "abc"));

        Assert.Empty(result.Warnings);

        var fatalError = Assert.Single(result.FatalErrors);

        Assert.Equal(ManagerConstraintRowsImportFatalErrorType.InvalidCapacityValue, fatalError.Type);
        Assert.Equal(1, fatalError.RowIndex);
        Assert.Equal(5, fatalError.ColumnIndex);
        Assert.Equal("MaxResourceCount", fatalError.Header);
        Assert.Equal("abc", fatalError.RawValue);

        AssertEmptyConstraintSet(result.ConstraintSet);
    }

    [Fact]
    public void Import_ShouldReturnFatalError_AndEmptyConstraintSet_WhenMaxCapacityIsZero()
    {
        var result = ImportSingleRow(CreateRow(
            "ShiftCapacityOverride",
            string.Empty,
            "2026-06-18",
            "Night",
            "2",
            "0"));

        Assert.Empty(result.Warnings);

        var fatalError = Assert.Single(result.FatalErrors);

        Assert.Equal(ManagerConstraintRowsImportFatalErrorType.InvalidCapacityValue, fatalError.Type);
        Assert.Equal(1, fatalError.RowIndex);
        Assert.Equal(5, fatalError.ColumnIndex);
        Assert.Equal("MaxResourceCount", fatalError.Header);
        Assert.Equal("0", fatalError.RawValue);

        AssertEmptyConstraintSet(result.ConstraintSet);
    }


    [Fact]
    public void Import_ShouldResolveExplicitAliases_WithoutGuessingNames()
    {
        var resources = CreateResources();

        var aliases = new Dictionary<string, Guid>(StringComparer.Ordinal)
        {
            ["דורון"] = resources[0].Id
        };

        var importer = new ManagerConstraintRowsImporter();

        var result = importer.Import(new ManagerConstraintRowsImportRequest(
            [
                CreateHeaders(),
                CreateRow(
                    "ForbidAssignment",
                    "דורון",
                    "2026-06-15",
                    "Morning",
                    string.Empty,
                    string.Empty)
            ],
            CreateSchedulePeriod(),
            resources,
            CreateShifts(),
            aliases));

        Assert.Empty(result.FatalErrors);
        Assert.Empty(result.Warnings);

        var forbiddenAssignment = Assert.Single(result.ConstraintSet.ForbiddenAssignments);

        Assert.Equal(resources[0].Id, forbiddenAssignment.ResourceId);
    }

    [Fact]
    public void Import_ShouldNormalizeHeadersAndWorkerNameWhitespace()
    {
        IReadOnlyList<IReadOnlyList<string>> rows =
        [
            [
                " Type ",
                " Worker Name ",
                " Date ",
                " Shift Kind ",
                " Min Resource Count ",
                " Max Resource Count "
            ],
            [
                " ForbidAssignment ",
                "  דור  ",
                " 2026-06-15 ",
                " morning ",
                string.Empty,
                string.Empty
            ]
        ];

        var importer = new ManagerConstraintRowsImporter();

        var result = importer.Import(new ManagerConstraintRowsImportRequest(
            rows,
            CreateSchedulePeriod(),
            CreateResources(),
            CreateShifts()));

        Assert.Empty(result.FatalErrors);
        Assert.Empty(result.Warnings);

        var forbiddenAssignment = Assert.Single(result.ConstraintSet.ForbiddenAssignments);

        Assert.Equal(CreateResources()[0].Id, forbiddenAssignment.ResourceId);
    }

    [Fact]
    public void Import_ShouldReturnEmptyConstraintSet_WhenAnyRowHasFatalError()
    {
        IReadOnlyList<IReadOnlyList<string>> rows =
        [
            CreateHeaders(),
            CreateRow(
                "ForbidAssignment",
                "דור",
                "2026-06-15",
                "Morning",
                string.Empty,
                string.Empty),
            CreateRow(
                "AvoidAssignment",
                "עובד לא מוכר",
                "2026-06-19",
                "Morning",
                string.Empty,
                string.Empty)
        ];

        var importer = new ManagerConstraintRowsImporter();

        var result = importer.Import(new ManagerConstraintRowsImportRequest(
            rows,
            CreateSchedulePeriod(),
            CreateResources(),
            CreateShifts()));

        Assert.Empty(result.Warnings);

        var fatalError = Assert.Single(result.FatalErrors);

        Assert.Equal(ManagerConstraintRowsImportFatalErrorType.UnresolvedWorkerName, fatalError.Type);

        AssertEmptyConstraintSet(result.ConstraintSet);
    }

    [Fact]
    public void Import_ShouldReturnFatalError_AndEmptyConstraintSet_WhenWorkerNameIsProvidedForCapacityOverride()
    {
        var result = ImportSingleRow(CreateRow(
            "ShiftCapacityOverride",
            "דור",
            "2026-06-18",
            "Night",
            "2",
            "2"));

        Assert.Empty(result.Warnings);

        var fatalError = Assert.Single(result.FatalErrors);

        Assert.Equal(ManagerConstraintRowsImportFatalErrorType.UnexpectedWorkerName, fatalError.Type);
        Assert.Equal(1, fatalError.RowIndex);
        Assert.Equal(1, fatalError.ColumnIndex);
        Assert.Equal("WorkerName", fatalError.Header);
        Assert.Equal("דור", fatalError.RawValue);

        AssertEmptyConstraintSet(result.ConstraintSet);
    }

    [Fact]
    public void Import_ShouldReturnFatalError_AndEmptyConstraintSet_WhenCapacityValueIsProvidedForAssignmentConstraint()
    {
        var result = ImportSingleRow(CreateRow(
            "ForbidAssignment",
            "דור",
            "2026-06-15",
            "Morning",
            "2",
            string.Empty));

        Assert.Empty(result.Warnings);

        var fatalError = Assert.Single(result.FatalErrors);

        Assert.Equal(ManagerConstraintRowsImportFatalErrorType.UnexpectedCapacityValue, fatalError.Type);
        Assert.Equal(1, fatalError.RowIndex);
        Assert.Equal(4, fatalError.ColumnIndex);
        Assert.Equal("MinResourceCount", fatalError.Header);
        Assert.Equal("2", fatalError.RawValue);

        AssertEmptyConstraintSet(result.ConstraintSet);
    }



    [Fact]
    public void Import_ShouldReturnSummary_FromValidRows()
    {
        IReadOnlyList<IReadOnlyList<string>> rows =
        [
            CreateHeaders(),
            CreateRow(
                "ForbidAssignment",
                "דור",
                "2026-06-15",
                "Morning",
                string.Empty,
                string.Empty),
            CreateRow(
                "AvoidAssignment",
                "יוסי",
                "2026-06-19",
                "Morning",
                string.Empty,
                string.Empty),
            CreateRow(
                "ShiftCapacityOverride",
                string.Empty,
                "2026-06-18",
                "Night",
                "2",
                "2")
        ];

        var importer = new ManagerConstraintRowsImporter();

        var result = importer.Import(new ManagerConstraintRowsImportRequest(
            rows,
            CreateSchedulePeriod(),
            CreateResources(),
            CreateShifts()));

        Assert.Empty(result.FatalErrors);
        Assert.Empty(result.Warnings);

        Assert.Equal(1, result.Summary.ImportedForbiddenAssignmentCount);
        Assert.Equal(1, result.Summary.ImportedAvoidAssignmentCount);
        Assert.Equal(1, result.Summary.ImportedShiftCapacityOverrideCount);
    }

    private static ManagerConstraintRowsImportResult ImportSingleRow(
        IReadOnlyList<string> row)
    {
        var importer = new ManagerConstraintRowsImporter();

        return importer.Import(new ManagerConstraintRowsImportRequest(
            [CreateHeaders(), row],
            CreateSchedulePeriod(),
            CreateResources(),
            CreateShifts()));
    }

    private static void AssertEmptyConstraintSet(
        ManagerConstraintSet constraintSet)
    {
        Assert.Empty(constraintSet.ForbiddenAssignments);
        Assert.Empty(constraintSet.AvoidAssignments);
        Assert.Empty(constraintSet.ShiftCapacityOverrides);
    }


    private static SchedulePeriod CreateSchedulePeriod()
    {
        return new SchedulePeriod(
            new DateTime(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 21, 0, 0, 0, DateTimeKind.Utc));
    }

    private static IReadOnlyList<Resource> CreateResources()
    {
        return
        [
            new Resource(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                "דור",
                hourlyCost: 100m),
            new Resource(
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                "יוסי",
                hourlyCost: 100m)
        ];
    }

    private static IReadOnlyList<Shift> CreateShifts()
    {
        return
        [
            new Shift(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                new DateTime(2026, 6, 15, 6, 30, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 15, 14, 20, 0, DateTimeKind.Utc),
                ShiftKind.Morning,
                minResourceCount: 1,
                maxResourceCount: 6),
            new Shift(
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                new DateTime(2026, 6, 18, 22, 40, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 19, 6, 30, 0, DateTimeKind.Utc),
                ShiftKind.Night,
                minResourceCount: 1,
                maxResourceCount: 6),
            new Shift(
                Guid.Parse("33333333-3333-3333-3333-333333333333"),
                new DateTime(2026, 6, 19, 6, 30, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 19, 14, 20, 0, DateTimeKind.Utc),
                ShiftKind.Morning,
                minResourceCount: 1,
                maxResourceCount: 6)
        ];
    }

    private static IReadOnlyList<string> CreateHeaders()
    {
        return
        [
            "Type",
            "WorkerName",
            "Date",
            "ShiftKind",
            "MinResourceCount",
            "MaxResourceCount"
        ];
    }

    private static IReadOnlyList<string> CreateRow(
        string type,
        string workerName,
        string date,
        string shiftKind,
        string minResourceCount,
        string maxResourceCount)
    {
        return
        [
            type,
            workerName,
            date,
            shiftKind,
            minResourceCount,
            maxResourceCount
        ];
    }
}
