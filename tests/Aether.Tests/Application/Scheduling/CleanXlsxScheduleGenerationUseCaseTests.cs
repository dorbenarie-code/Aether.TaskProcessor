using Aether.Application.Scheduling.ManagerConstraints;
using Aether.Application.Scheduling.Reports;
using Aether.Application.Scheduling.Reports.Exporting;
using Aether.Application.Scheduling.ScheduleGeneration;
using Aether.Application.Scheduling.SubmissionForms;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class CleanXlsxScheduleGenerationUseCaseTests
{
    private const int Seed = 20260603;

    [Fact]
    public void Run_ShouldReturnReviewCsvXlsxBytesDiagnosticsAndSummary_WhenOptimizationSucceeds()
    {
        using var inputStream = new MemoryStream([1, 2, 3]);

        IReadOnlyList<IReadOnlyList<string>> managerConstraintRows =
        [
            [
                "Type",
                "WorkerName",
                "Date",
                "ShiftKind",
                "MinResourceCount",
                "MaxResourceCount"
            ],
            [
                "ForbidAssignment",
                "Dana",
                "2026-06-15",
                "Morning",
                string.Empty,
                string.Empty
            ]
        ];

        var expectedXlsxBytes = new byte[] { 0x50, 0x4B, 0x03, 0x04 };

        var runner = new FakeAvailabilityMatrixStreamOptimizationRunner(
            CreateSuccessfulRunnerResult());

        var xlsxExporter = new FakeScheduleTableXlsxExporter(
            expectedXlsxBytes);

        var useCase = new CleanXlsxScheduleGenerationUseCase(
            runner,
            xlsxExporter);

        var result = useCase.Run(new CleanXlsxScheduleGenerationRequest(
            inputStream,
            CreateSchedulePeriod(),
            CreateResources(),
            CreateShifts(),
            TotalEffectiveTargetHours: 16,
            MaximumAssignedHoursDeviationFromAverageHours: 8,
            Seed: Seed,
            ManagerConstraintRows: managerConstraintRows,
            ApplyPostRunLocalAddImprovement: true,
            IncludeTargetGapDiagnostics: true));

        Assert.True(result.Succeeded);

        Assert.NotNull(result.ReviewText);
        Assert.Contains("Schedule Optimization Review", result.ReviewText);

        Assert.NotNull(result.ScheduleTableCsv);
        Assert.Contains("Date,DayOfWeek,Morning,Afternoon,Night", result.ScheduleTableCsv);

        Assert.Equal(expectedXlsxBytes, result.ScheduleTableXlsxBytes);

        Assert.NotNull(result.TargetGapDiagnosticsText);
        Assert.Contains("Clean GA Target Gap Explainability Diagnostics", result.TargetGapDiagnosticsText);

        Assert.Empty(result.ImportFatalErrors);
        Assert.Empty(result.ManagerConstraintImportFatalErrors);

        Assert.Equal(
            1,
            result.ManagerConstraintImportSummary.ImportedForbiddenAssignmentCount);

        Assert.Equal(
            1,
            result.ManagerConstraintImportSummary.ImportedAvoidAssignmentCount);

        Assert.Equal(
            1,
            result.ManagerConstraintImportSummary.ImportedShiftCapacityOverrideCount);

        Assert.Same(inputStream, runner.LastRequest!.InputStream);
        Assert.Same(managerConstraintRows, runner.LastRequest.ManagerConstraintRows);
        Assert.True(runner.LastRequest.ApplyPostRunLocalAddImprovement);

        Assert.NotNull(xlsxExporter.LastProjection);
        Assert.NotEmpty(xlsxExporter.LastProjection.Days);

        Assert.NotNull(result.Summary);
        var summary = result.Summary!;

        Assert.Equal(2, summary.ImportedWorkerSubmissionCount);
        Assert.Equal(2, summary.SubmittedShiftSelectionCount);
        Assert.Equal(2, summary.ResourceCount);
        Assert.Equal(2, summary.ShiftCount);
        Assert.Equal(2, summary.ResourceWorkloadDemandCount);
        Assert.True(summary.AssignmentCount >= 0);
        Assert.True(summary.IsFeasible);
        Assert.Equal(0, summary.HardViolationCount);
        Assert.True(summary.SoftViolationCount >= 0);
        Assert.True(summary.TotalPenalty >= 0);
        Assert.False(summary.PostRunLocalAddImprovementApplied);
        Assert.True(summary.GenerationDiagnosticCount > 0);
    }

    [Fact]
    public void Run_ShouldReturnFailedResult_WhenManagerConstraintImportHasFatalErrors()
    {
        using var inputStream = new MemoryStream([1, 2, 3]);

        IReadOnlyList<IReadOnlyList<string>> managerConstraintRows =
        [
            [
                "Type",
                "WorkerName",
                "Date",
                "ShiftKind",
                "MinResourceCount",
                "MaxResourceCount"
            ],
            [
                "ForbidAssignment",
                "Unknown",
                "2026-06-15",
                "Morning",
                string.Empty,
                string.Empty
            ]
        ];

        var managerConstraintFatalError = new ManagerConstraintRowsImportFatalError(
            ManagerConstraintRowsImportFatalErrorType.UnresolvedWorkerName,
            RowIndex: 1,
            ColumnIndex: 1,
            Header: "WorkerName",
            RawValue: "Unknown");

        var runner = new FakeAvailabilityMatrixStreamOptimizationRunner(
            new AvailabilityMatrixImportedRowsOptimizationResult(
                ImportedWorkerSubmissions: [],
                ImportWarnings: [],
                ImportFatalErrors: [],
                OptimizationResult: null,
                ManagerConstraintImportWarnings: [],
                ManagerConstraintImportFatalErrors: [managerConstraintFatalError],
                managerConstraintImportSummary: ManagerConstraintImportSummary.Empty));

        var xlsxExporter = new FakeScheduleTableXlsxExporter(
            [0x50, 0x4B, 0x03, 0x04]);

        var useCase = new CleanXlsxScheduleGenerationUseCase(
            runner,
            xlsxExporter);

        var result = useCase.Run(new CleanXlsxScheduleGenerationRequest(
            inputStream,
            CreateSchedulePeriod(),
            CreateResources(),
            CreateShifts(),
            TotalEffectiveTargetHours: 16,
            MaximumAssignedHoursDeviationFromAverageHours: 8,
            Seed: Seed,
            ManagerConstraintRows: managerConstraintRows,
            ApplyPostRunLocalAddImprovement: true,
            IncludeTargetGapDiagnostics: true));

        Assert.False(result.Succeeded);
        Assert.Equal(
            ScheduleGenerationFailureType.ManagerConstraintImportFailed,
            result.FailureType);

        Assert.Empty(result.ImportFatalErrors);

        var fatalError = Assert.Single(result.ManagerConstraintImportFatalErrors);

        Assert.Equal(
            ManagerConstraintRowsImportFatalErrorType.UnresolvedWorkerName,
            fatalError.Type);

        Assert.Equal("WorkerName", fatalError.Header);
        Assert.Equal("Unknown", fatalError.RawValue);

        Assert.Null(result.ReviewText);
        Assert.Null(result.ScheduleTableCsv);
        Assert.Null(result.ScheduleTableXlsxBytes);
        Assert.Null(result.TargetGapDiagnosticsText);
        Assert.Null(result.Summary);

        Assert.Null(xlsxExporter.LastProjection);

        Assert.Same(inputStream, runner.LastRequest!.InputStream);
        Assert.Same(managerConstraintRows, runner.LastRequest.ManagerConstraintRows);
    }


    [Fact]
    public void Run_ShouldReturnFailedResult_WhenAvailabilityMatrixImportHasFatalErrors()
    {
        using var inputStream = new MemoryStream([1, 2, 3]);

        var importFatalError = new AvailabilityMatrixImportFatalError(
            AvailabilityMatrixImportFatalErrorType.EmptyTable);

        var runner = new FakeAvailabilityMatrixStreamOptimizationRunner(
            new AvailabilityMatrixImportedRowsOptimizationResult(
                ImportedWorkerSubmissions: [],
                ImportWarnings: [],
                ImportFatalErrors: [importFatalError],
                OptimizationResult: null));

        var xlsxExporter = new FakeScheduleTableXlsxExporter(
            [0x50, 0x4B, 0x03, 0x04]);

        var useCase = new CleanXlsxScheduleGenerationUseCase(
            runner,
            xlsxExporter);

        var result = useCase.Run(new CleanXlsxScheduleGenerationRequest(
            inputStream,
            CreateSchedulePeriod(),
            CreateResources(),
            CreateShifts(),
            TotalEffectiveTargetHours: 16,
            MaximumAssignedHoursDeviationFromAverageHours: 8,
            Seed: Seed,
            ApplyPostRunLocalAddImprovement: true,
            IncludeTargetGapDiagnostics: true));

        Assert.False(result.Succeeded);

        Assert.Equal(
            ScheduleGenerationFailureType.AvailabilityMatrixImportFailed,
            result.FailureType);

        var fatalError = Assert.Single(result.ImportFatalErrors);

        Assert.Equal(
            AvailabilityMatrixImportFatalErrorType.EmptyTable,
            fatalError.Type);

        Assert.Empty(result.ManagerConstraintImportFatalErrors);
        Assert.Equal(ManagerConstraintImportSummary.Empty, result.ManagerConstraintImportSummary);

        Assert.Null(result.ReviewText);
        Assert.Null(result.ScheduleTableCsv);
        Assert.Null(result.ScheduleTableXlsxBytes);
        Assert.Null(result.TargetGapDiagnosticsText);
        Assert.Null(result.Summary);

        Assert.Null(xlsxExporter.LastProjection);

        Assert.Same(inputStream, runner.LastRequest!.InputStream);
    }


    [Fact]
    public void Run_ShouldReturnFailedResult_WhenOptimizationResultIsMissing()
    {
        using var inputStream = new MemoryStream([1, 2, 3]);

        var runner = new FakeAvailabilityMatrixStreamOptimizationRunner(
            new AvailabilityMatrixImportedRowsOptimizationResult(
                ImportedWorkerSubmissions: [],
                ImportWarnings: [],
                ImportFatalErrors: [],
                OptimizationResult: null,
                ManagerConstraintImportWarnings: [],
                ManagerConstraintImportFatalErrors: [],
                managerConstraintImportSummary: ManagerConstraintImportSummary.Empty));

        var xlsxExporter = new FakeScheduleTableXlsxExporter(
            [0x50, 0x4B, 0x03, 0x04]);

        var useCase = new CleanXlsxScheduleGenerationUseCase(
            runner,
            xlsxExporter);

        var result = useCase.Run(new CleanXlsxScheduleGenerationRequest(
            inputStream,
            CreateSchedulePeriod(),
            CreateResources(),
            CreateShifts(),
            TotalEffectiveTargetHours: 16,
            MaximumAssignedHoursDeviationFromAverageHours: 8,
            Seed: Seed,
            ApplyPostRunLocalAddImprovement: true,
            IncludeTargetGapDiagnostics: true));

        Assert.False(result.Succeeded);

        Assert.Equal(
            ScheduleGenerationFailureType.OptimizationResultMissing,
            result.FailureType);

        Assert.Empty(result.ImportFatalErrors);
        Assert.Empty(result.ManagerConstraintImportFatalErrors);
        Assert.Equal(ManagerConstraintImportSummary.Empty, result.ManagerConstraintImportSummary);

        Assert.Null(result.ReviewText);
        Assert.Null(result.ScheduleTableCsv);
        Assert.Null(result.ScheduleTableXlsxBytes);
        Assert.Null(result.TargetGapDiagnosticsText);
        Assert.Null(result.Summary);

        Assert.Null(xlsxExporter.LastProjection);

        Assert.Same(inputStream, runner.LastRequest!.InputStream);
        Assert.True(runner.LastRequest.ApplyPostRunLocalAddImprovement);
    }


    private static AvailabilityMatrixImportedRowsOptimizationResult CreateSuccessfulRunnerResult()
    {
        var resources = CreateResources();
        var shifts = CreateShifts();
        var date = new DateOnly(2026, 6, 15);

        IReadOnlyList<WorkerSubmission> workerSubmissions =
        [
            new WorkerSubmission(
                resources[0].Id,
                [
                    new WorkerShiftSubmission(
                        date,
                        ShiftKind.Morning,
                        ShiftSubmissionChoice.StrongAvailable)
                ]),
            new WorkerSubmission(
                resources[1].Id,
                [
                    new WorkerShiftSubmission(
                        date,
                        ShiftKind.Afternoon,
                        ShiftSubmissionChoice.StrongAvailable)
                ])
        ];

        var optimizationResult = new ClosedFormSubmissionOptimizationRunner()
            .Run(new ClosedFormSubmissionOptimizationRequest(
                CreateSchedulePeriod(),
                resources,
                shifts,
                workerSubmissions,
                TotalEffectiveTargetHours: 16,
                MaximumAssignedHoursDeviationFromAverageHours: 8,
                Seed: Seed));

        return new AvailabilityMatrixImportedRowsOptimizationResult(
            workerSubmissions,
            ImportWarnings: [],
            ImportFatalErrors: [],
            optimizationResult,
            ManagerConstraintImportWarnings: [],
            ManagerConstraintImportFatalErrors: [],
            managerConstraintImportSummary: new ManagerConstraintImportSummary(
                ImportedForbiddenAssignmentCount: 1,
                ImportedAvoidAssignmentCount: 1,
                ImportedShiftCapacityOverrideCount: 1));
    }

    private static SchedulePeriod CreateSchedulePeriod()
    {
        return new SchedulePeriod(
            new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 16, 0, 0, 0, DateTimeKind.Utc));
    }

    private static IReadOnlyList<Resource> CreateResources()
    {
        return
        [
            new Resource(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                "Dana",
                hourlyCost: 100m),
            new Resource(
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                "Yossi",
                hourlyCost: 100m)
        ];
    }

    private static IReadOnlyList<Shift> CreateShifts()
    {
        var date = new DateOnly(2026, 6, 15);

        return
        [
            CreateShift(
                "cccccccc-cccc-cccc-cccc-cccccccccccc",
                date,
                ShiftKind.Morning),
            CreateShift(
                "dddddddd-dddd-dddd-dddd-dddddddddddd",
                date,
                ShiftKind.Afternoon)
        ];
    }

    private static Shift CreateShift(
        string id,
        DateOnly date,
        ShiftKind kind)
    {
        return new Shift(
            Guid.Parse(id),
            GetStartUtc(date, kind),
            GetEndUtc(date, kind),
            kind,
            minResourceCount: 0,
            maxResourceCount: 2,
            requiresPreferenceToAssign: true);
    }

    private static DateTime GetStartUtc(
        DateOnly date,
        ShiftKind kind)
    {
        return kind switch
        {
            ShiftKind.Morning => date.ToDateTime(new TimeOnly(6, 30), DateTimeKind.Utc),
            ShiftKind.Afternoon => date.ToDateTime(new TimeOnly(14, 20), DateTimeKind.Utc),
            ShiftKind.Night => date.ToDateTime(new TimeOnly(22, 40), DateTimeKind.Utc),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private static DateTime GetEndUtc(
        DateOnly date,
        ShiftKind kind)
    {
        return kind switch
        {
            ShiftKind.Morning => date.ToDateTime(new TimeOnly(14, 20), DateTimeKind.Utc),
            ShiftKind.Afternoon => date.ToDateTime(new TimeOnly(22, 40), DateTimeKind.Utc),
            ShiftKind.Night => date.AddDays(1).ToDateTime(new TimeOnly(6, 30), DateTimeKind.Utc),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private sealed class FakeAvailabilityMatrixStreamOptimizationRunner
        : IAvailabilityMatrixStreamOptimizationRunner
    {
        private readonly AvailabilityMatrixImportedRowsOptimizationResult result;

        public AvailabilityMatrixStreamOptimizationRequest? LastRequest { get; private set; }

        public FakeAvailabilityMatrixStreamOptimizationRunner(
            AvailabilityMatrixImportedRowsOptimizationResult result)
        {
            this.result = result;
        }

        public AvailabilityMatrixImportedRowsOptimizationResult Run(
            AvailabilityMatrixStreamOptimizationRequest request)
        {
            LastRequest = request;

            return result;
        }
    }

    private sealed class FakeScheduleTableXlsxExporter : IScheduleTableXlsxExporter
    {
        private readonly byte[] bytes;

        public ScheduleTableProjection? LastProjection { get; private set; }

        public FakeScheduleTableXlsxExporter(byte[] bytes)
        {
            this.bytes = bytes;
        }

        public byte[] ExportToXlsx(ScheduleTableProjection projection)
        {
            LastProjection = projection;

            return bytes;
        }
    }
}
