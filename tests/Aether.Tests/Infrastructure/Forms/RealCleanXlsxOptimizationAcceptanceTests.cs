using Aether.Application.Scheduling.Reports;
using Aether.Application.Scheduling.SubmissionForms;
using Aether.Domain.Optimization;
using Aether.Infrastructure.Forms;

namespace Aether.Tests.Infrastructure.Forms;

public sealed class RealCleanXlsxOptimizationAcceptanceTests
{
    private const string FixtureFileName = "last-dor-clean-availability-matrix.xlsx";
    private const int ExpectedResourceCount = 19;
    private const int DaysInSchedule = 14;
    private const int ExpectedShiftCount = DaysInSchedule * 3;
    private const int ExpectedSubmittedShiftCount = 205;
    private const double ExpectedTotalEffectiveTargetHours = 736.0;
    private const double BalanceToleranceHours = 5.0;
    private const int Seed = 20260603;

    [Fact]
    [Trait("Category", "SchedulingExperiment")]
    public void Run_ShouldOptimizeRealCleanLastDorWorkbook_FromXlsxStream()
    {
        using var stream = File.OpenRead(FindFixturePath());

        var resources = CreateResources();
        var shifts = CreateBiWeeklySequencePressureShifts();

        var runner = new AvailabilityMatrixStreamOptimizationRunner(
            new XlsxFormTableReader());

        var result = runner.Run(new AvailabilityMatrixStreamOptimizationRequest(
            stream,
            CreateSchedulePeriod(),
            resources,
            shifts,
            TotalEffectiveTargetHours: ExpectedTotalEffectiveTargetHours,
            MaximumAssignedHoursDeviationFromAverageHours: BalanceToleranceHours,
            Seed: Seed));

        Assert.Empty(result.ImportFatalErrors);

        var warning = Assert.Single(result.ImportWarnings);

        Assert.Equal(
            AvailabilityMatrixImportWarningType.DateColumnOutsideSchedulePeriod,
            warning.Type);

        Assert.Contains("06/10", warning.Header ?? string.Empty);

        Assert.Equal(ExpectedResourceCount, result.ImportedWorkerSubmissions.Count);

        Assert.Equal(
            ExpectedSubmittedShiftCount,
            result.ImportedWorkerSubmissions.Sum(submission => submission.ShiftSubmissions.Count));

        Assert.NotNull(result.OptimizationResult);

        var optimizationResult = result.OptimizationResult!;

        Assert.Empty(optimizationResult.Warnings);

        Assert.Equal(ExpectedResourceCount, optimizationResult.Problem.Resources.Count);
        Assert.Equal(ExpectedShiftCount, optimizationResult.Problem.Shifts.Count);
        Assert.Equal(ExpectedResourceCount, optimizationResult.Problem.ResourceWorkloadDemands.Count);

        Assert.Equal(
            ExpectedTotalEffectiveTargetHours,
            optimizationResult.Problem.ResourceWorkloadDemands.Sum(demand => demand.EffectiveTargetHours),
            0.000001);

        Assert.Equal(
            BalanceToleranceHours,
            optimizationResult.Problem.MaximumAssignedHoursDeviationFromAverageHours);

        Assert.NotNull(optimizationResult.GeneticResult.Candidate);
        Assert.NotNull(optimizationResult.GeneticResult.Evaluation);
        Assert.NotEmpty(optimizationResult.GeneticResult.Candidate.Assignments);

        var generationDiagnostics = optimizationResult.GeneticResult.GenerationDiagnostics.ToArray();

        Assert.NotEmpty(generationDiagnostics);

        Assert.True(
            generationDiagnostics[^1].BestSoFarTotalPenalty <=
            generationDiagnostics[0].BestSoFarTotalPenalty,
            "Accepted real XLSX Clean GA run should not return a best-so-far penalty worse than generation 0.");

        AssertCandidateReferencesKnownProblemEntities(
            optimizationResult.Problem,
            optimizationResult.GeneticResult.Candidate);

        AssertNoBasicStructuralViolations(
            optimizationResult.GeneticResult.Evaluation);

        Assert.True(
            optimizationResult.GeneticResult.Evaluation.IsFeasible,
            $"Expected real clean XLSX optimization run to be feasible, but got {optimizationResult.GeneticResult.Evaluation.Score.HardViolationCount} hard violations.");

        var reviewReport = new SchedulingRunReportFormatter()
            .FormatOptimizationReview(
                optimizationResult.Problem,
                optimizationResult.GeneticResult);

        Assert.Contains("Schedule Optimization Review", reviewReport);
        Assert.Contains("Problem Summary", reviewReport);
        Assert.Contains("Resources: 19", reviewReport);
        Assert.Contains("Shifts: 42", reviewReport);
        Assert.Contains("ScheduleByShift", reviewReport);
        Assert.Contains("LoadByResource", reviewReport);
        Assert.Contains("TargetGapByResource", reviewReport);
        Assert.Contains("PreferenceFulfillment", reviewReport);
        Assert.Contains("ViolationsByType", reviewReport);
        Assert.Contains("GenerationDiagnostics", reviewReport);
        Assert.Contains("FulfillmentRate:", reviewReport);
    }

    private static string FindFixturePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidatePath = Path.Combine(
                directory.FullName,
                "TestData",
                "Forms",
                FixtureFileName);

            if (File.Exists(candidatePath))
            {
                return candidatePath;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not find test fixture '{FixtureFileName}'.");
    }

    private static SchedulePeriod CreateSchedulePeriod()
    {
        return new SchedulePeriod(
            new DateTime(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 28, 0, 0, 0, DateTimeKind.Utc));
    }

    private static IReadOnlyList<Resource> CreateResources()
    {
        var names = new[]
        {
            "Worker01",
            "Worker02",
            "Worker03",
            "Worker04",
            "Worker05",
            "Worker06",
            "Worker07",
            "Worker08",
            "Worker09",
            "Worker10",
            "Worker11",
            "Worker12",
            "Worker13",
            "Worker14",
            "Worker15",
            "Worker16",
            "Worker17",
            "Worker18",
            "Worker19"
        };

        return names
            .Select((name, index) => new Resource(
                CreateResourceGuid(index + 1),
                name,
                hourlyCost: 100m))
            .ToArray();
    }

    private static IReadOnlyList<Shift> CreateBiWeeklySequencePressureShifts()
    {
        var shifts = new List<Shift>();
        var shiftIndex = 1;

        for (var dayOffset = 0; dayOffset < DaysInSchedule; dayOffset++)
        {
            var date = new DateOnly(2026, 6, 14).AddDays(dayOffset);

            shifts.Add(CreateShift(shiftIndex++, date, ShiftKind.Morning));
            shifts.Add(CreateShift(shiftIndex++, date, ShiftKind.Afternoon));
            shifts.Add(CreateShift(shiftIndex++, date, ShiftKind.Night));
        }

        return shifts
            .OrderBy(shift => shift.StartUtc)
            .ToArray();
    }

    private static Shift CreateShift(
        int shiftIndex,
        DateOnly date,
        ShiftKind kind)
    {
        var capacity = GetCapacityRule(date, kind);

        return new Shift(
            CreateShiftGuid(shiftIndex),
            GetStartUtc(date, kind),
            GetEndUtc(date, kind),
            kind,
            capacity.Min,
            capacity.Max,
            requiresPreferenceToAssign: capacity.RequiresPreference,
            requiresMinimumWhenPreferenceExists: false,
            nightShiftCategory: GetNightShiftCategory(date, kind));
    }

    private static CapacityRule GetCapacityRule(
        DateOnly date,
        ShiftKind kind)
    {
        if (date.DayOfWeek == DayOfWeek.Friday)
        {
            return kind switch
            {
                ShiftKind.Morning => new CapacityRule(0, 2, true),
                ShiftKind.Afternoon => new CapacityRule(0, 1, true),
                ShiftKind.Night => new CapacityRule(0, 1, true),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
        }

        if (date.DayOfWeek == DayOfWeek.Saturday)
        {
            return kind switch
            {
                ShiftKind.Morning => new CapacityRule(0, 1, true),
                ShiftKind.Afternoon => new CapacityRule(0, 1, true),
                ShiftKind.Night => new CapacityRule(3, 3, false),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
        }

        return kind switch
        {
            ShiftKind.Morning => new CapacityRule(3, 6, false),
            ShiftKind.Afternoon => new CapacityRule(2, 4, true),
            ShiftKind.Night => new CapacityRule(0, 1, true),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private static DateTime GetStartUtc(
        DateOnly date,
        ShiftKind kind)
    {
        var time = kind switch
        {
            ShiftKind.Morning => new TimeOnly(6, 30),
            ShiftKind.Afternoon => new TimeOnly(14, 20),
            ShiftKind.Night => new TimeOnly(22, 40),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        return date.ToDateTime(time, DateTimeKind.Utc);
    }

    private static DateTime GetEndUtc(
        DateOnly date,
        ShiftKind kind)
    {
        var endDate = kind == ShiftKind.Night
            ? date.AddDays(1)
            : date;

        var time = kind switch
        {
            ShiftKind.Morning => new TimeOnly(14, 20),
            ShiftKind.Afternoon => new TimeOnly(22, 40),
            ShiftKind.Night => new TimeOnly(6, 30),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        return endDate.ToDateTime(time, DateTimeKind.Utc);
    }

    private static NightShiftCategory? GetNightShiftCategory(
        DateOnly date,
        ShiftKind kind)
    {
        if (kind != ShiftKind.Night)
        {
            return null;
        }

        return date.DayOfWeek switch
        {
            DayOfWeek.Friday => NightShiftCategory.FridayNight,
            DayOfWeek.Saturday => NightShiftCategory.MotzeiShabbatNight,
            _ => NightShiftCategory.Regular
        };
    }

    private static void AssertCandidateReferencesKnownProblemEntities(
        SchedulingProblem problem,
        ScheduleCandidate candidate)
    {
        var resourceIds = problem.Resources
            .Select(resource => resource.Id)
            .ToHashSet();

        var shiftIds = problem.Shifts
            .Select(shift => shift.Id)
            .ToHashSet();

        Assert.All(candidate.Assignments, assignment =>
        {
            Assert.Contains(assignment.ResourceId, resourceIds);
            Assert.Contains(assignment.ShiftId, shiftIds);
        });

        Assert.Equal(
            candidate.Assignments.Count,
            candidate.Assignments
                .Select(assignment => $"{assignment.ResourceId}:{assignment.ShiftId}")
                .Distinct()
                .Count());
    }

    private static void AssertNoBasicStructuralViolations(
        ScheduleEvaluationResult evaluation)
    {
        Assert.Equal(
            0,
            CountViolations(
                evaluation,
                ConstraintViolationType.ResourceUnavailable));

        Assert.Equal(
            0,
            CountViolations(
                evaluation,
                ConstraintViolationType.ResourceAssignedToOverlappingShifts));

        Assert.Equal(
            0,
            CountViolations(
                evaluation,
                ConstraintViolationType.AssignedWithoutRequiredPreference));

        Assert.Equal(
            0,
            CountViolations(
                evaluation,
                ConstraintViolationType.ShiftUnderstaffed));

        Assert.Equal(
            0,
            CountViolations(
                evaluation,
                ConstraintViolationType.ShiftOverstaffed));
    }

    private static int CountViolations(
        ScheduleEvaluationResult evaluation,
        ConstraintViolationType type)
    {
        return evaluation.Violations.Count(violation => violation.Type == type);
    }

    private static Guid CreateResourceGuid(int index)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{index:000000000000}");
    }

    private static Guid CreateShiftGuid(int index)
    {
        return Guid.Parse($"00000000-0000-0000-0001-{index:000000000000}");
    }

    private sealed record CapacityRule(
        int Min,
        int Max,
        bool RequiresPreference);
}
