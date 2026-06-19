using Aether.Application.Scheduling.Contracts;
using Aether.Application.Scheduling.Interfaces;
using Aether.Application.Scheduling.Optimization;
using Aether.Application.Scheduling.Services;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class SchedulingRunServiceTests
{
    [Fact]
    public void Run_ShouldBuildProblemAndRunDeterministicAndGeneticOptimizers()
    {
        var request = CreateRequest();
        var problem = CreateProblem();

        var deterministicCandidate = new ScheduleCandidate([]);
        var deterministicEvaluation = CreateEvaluation(scoreValue: 900);

        var geneticCandidate = new ScheduleCandidate([]);
        var geneticEvaluation = CreateEvaluation(scoreValue: 1000);

        var builder = new FakeSchedulingProblemBuilder(
            new SchedulingProblemBuildResult(problem, []));

        var deterministicOptimizer = new FakeScheduleOptimizer(
            new ScheduleOptimizationResult(
                deterministicCandidate,
                deterministicEvaluation));

        var geneticOptimizer = new FakeScheduleOptimizer(
            new ScheduleOptimizationResult(
                geneticCandidate,
                geneticEvaluation));

        var service = new SchedulingRunService(
            builder,
            deterministicOptimizer,
            _ => geneticOptimizer);

        var result = service.Run(request);

        Assert.Same(request, builder.ReceivedRequest);
        Assert.Same(problem, deterministicOptimizer.ReceivedProblem);
        Assert.Same(problem, geneticOptimizer.ReceivedProblem);

        Assert.Same(problem, result.Problem);

        Assert.Same(deterministicCandidate, result.DeterministicResult.Candidate);
        Assert.Same(deterministicEvaluation, result.DeterministicResult.Evaluation);

        Assert.Same(geneticCandidate, result.GeneticResult.Candidate);
        Assert.Same(geneticEvaluation, result.GeneticResult.Evaluation);
    }

    [Fact]
    public void Run_ShouldReturnBuilderWarnings()
    {
        var warning = new SchedulingProblemBuildWarning(
            SchedulingProblemBuildWarningType.RawSpecialRequestNote,
            "Raw note was not parsed.",
            ResourceName: "Dana");

        var problem = CreateProblem();

        var builder = new FakeSchedulingProblemBuilder(
            new SchedulingProblemBuildResult(problem, [warning]));

        var deterministicOptimizer = new FakeScheduleOptimizer(
            CreateOptimizationResult());

        var geneticOptimizer = new FakeScheduleOptimizer(
            CreateOptimizationResult());

        var service = new SchedulingRunService(
            builder,
            deterministicOptimizer,
            _ => geneticOptimizer);

        var result = service.Run(CreateRequest());

        var actualWarning = Assert.Single(result.Warnings);
        Assert.Same(warning, actualWarning);
    }

    [Fact]
    public void Run_ShouldReturnComparison_WhenGeneticRanksBetter()
    {
        var problem = CreateProblem();

        var hardViolation = new ConstraintViolation(
            ConstraintViolationType.ResourceMinimumAssignedHoursNotMet,
            ConstraintViolationSeverity.Hard,
            "Resource minimum hours not met.");

        var deterministicEvaluation = CreateEvaluation(
            scoreValue: 0,
            hardViolationCount: 1,
            totalPenalty: 50_000,
            violations: [hardViolation]);

        var geneticEvaluation = CreateEvaluation(
            scoreValue: 1000,
            hardViolationCount: 0,
            totalPenalty: 0);

        var builder = new FakeSchedulingProblemBuilder(
            new SchedulingProblemBuildResult(problem, []));

        var deterministicOptimizer = new FakeScheduleOptimizer(
            new ScheduleOptimizationResult(
                new ScheduleCandidate([]),
                deterministicEvaluation));

        var geneticOptimizer = new FakeScheduleOptimizer(
            new ScheduleOptimizationResult(
                new ScheduleCandidate([]),
                geneticEvaluation));

        var service = new SchedulingRunService(
            builder,
            deterministicOptimizer,
            _ => geneticOptimizer);

        var result = service.Run(CreateRequest());

        Assert.True(result.Comparison.GeneticRankedBetter);
        Assert.Equal(1, result.Comparison.DeterministicHardViolationCount);
        Assert.Equal(0, result.Comparison.GeneticHardViolationCount);
        Assert.Equal(50_000, result.Comparison.DeterministicTotalPenalty);
        Assert.Equal(0, result.Comparison.GeneticTotalPenalty);
    }

    [Fact]
    public void Run_ShouldReturnLoadByResourceAndViolationsByType()
    {
        var problem = CreateProblem();
        var resource = problem.Resources.First();
        var firstShift = problem.Shifts.First();
        var secondShift = problem.Shifts.Last();

        var candidate = new ScheduleCandidate(
        [
            new Assignment(resource.Id, firstShift.Id),
            new Assignment(resource.Id, secondShift.Id)
        ]);

        var firstAvoidViolation = new ConstraintViolation(
            ConstraintViolationType.IgnoredAvoidPreference,
            ConstraintViolationSeverity.Soft,
            "Avoid preference ignored.",
            resource.Id,
            firstShift.Id);

        var secondAvoidViolation = new ConstraintViolation(
            ConstraintViolationType.IgnoredAvoidPreference,
            ConstraintViolationSeverity.Soft,
            "Avoid preference ignored.",
            resource.Id,
            secondShift.Id);

        var evaluation = CreateEvaluation(
            scoreValue: 400,
            softViolationCount: 2,
            totalPenalty: 600,
            violations:
            [
                firstAvoidViolation,
                secondAvoidViolation
            ]);

        var builder = new FakeSchedulingProblemBuilder(
            new SchedulingProblemBuildResult(problem, []));

        var deterministicOptimizer = new FakeScheduleOptimizer(
            new ScheduleOptimizationResult(candidate, evaluation));

        var geneticOptimizer = new FakeScheduleOptimizer(
            CreateOptimizationResult());

        var service = new SchedulingRunService(
            builder,
            deterministicOptimizer,
            _ => geneticOptimizer);

        var result = service.Run(CreateRequest());

        var load = Assert.Single(
            result.DeterministicResult.LoadByResource,
            item => item.ResourceId == resource.Id);

        Assert.Equal(resource.Name, load.ResourceName);
        Assert.Equal(16.0, load.AssignedHours);
        Assert.Equal(2, load.AssignmentCount);

        Assert.Equal(
            2,
            result.DeterministicResult.ViolationsByType[ConstraintViolationType.IgnoredAvoidPreference]);
    }

    [Fact]
    public void Run_ShouldReturnGeneticGenerationDiagnostics()
    {
        var problem = CreateProblem();

        var diagnostic = new GeneticGenerationDiagnostic(
            GenerationIndex: 0,
            PopulationSize: 10,
            FeasibleCandidateCount: 1,
            BestScoreValue: 900,
            BestTotalPenalty: 100,
            BestHardViolationCount: 0,
            BestSoftViolationCount: 1);

        var builder = new FakeSchedulingProblemBuilder(
            new SchedulingProblemBuildResult(problem, []));

        var deterministicOptimizer = new FakeScheduleOptimizer(
            CreateOptimizationResult());

        var geneticOptimizer = new FakeScheduleOptimizer(
            CreateOptimizationResult(),
            onOptimize: sink => sink.ReportGeneration(diagnostic));

        var service = new SchedulingRunService(
            builder,
            deterministicOptimizer,
            sink => geneticOptimizer.WithDiagnosticsSink(sink));

        var result = service.Run(CreateRequest());

        Assert.Empty(result.DeterministicResult.GenerationDiagnostics);

        var actualDiagnostic = Assert.Single(
            result.GeneticResult.GenerationDiagnostics);

        Assert.Equal(diagnostic, actualDiagnostic);
    }

    [Fact]
    public void Run_ShouldThrow_WhenRequestIsNull()
    {
        var problem = CreateProblem();

        var builder = new FakeSchedulingProblemBuilder(
            new SchedulingProblemBuildResult(problem, []));

        var deterministicOptimizer = new FakeScheduleOptimizer(
            CreateOptimizationResult());

        var geneticOptimizer = new FakeScheduleOptimizer(
            CreateOptimizationResult());

        var service = new SchedulingRunService(
            builder,
            deterministicOptimizer,
            _ => geneticOptimizer);

        var exception = Assert.Throws<ArgumentNullException>(() =>
            service.Run(null!));

        Assert.Equal("request", exception.ParamName);
    }

    private static SchedulingProblemBuildRequest CreateRequest()
    {
        var resource = CreateResource();
        var shift = CreateShift(
            new DateTime(2026, 6, 7, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 7, 14, 30, 0, DateTimeKind.Utc));

        return new SchedulingProblemBuildRequest(
            Period: CreatePeriod(),
            Resources: [resource],
            Shifts: [shift],
            ResourceSubmissions: []);
    }

    private static SchedulingProblem CreateProblem()
    {
        var resource = CreateResource();

        var firstShift = CreateShift(
            new DateTime(2026, 6, 7, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 7, 14, 30, 0, DateTimeKind.Utc));

        var secondShift = CreateShift(
            new DateTime(2026, 6, 8, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 8, 14, 30, 0, DateTimeKind.Utc));

        return new SchedulingProblem(
            period: CreatePeriod(),
            resources: [resource],
            shifts: [firstShift, secondShift],
            availabilityWindows:
            [
                new AvailabilityWindow(resource.Id, firstShift.StartUtc, firstShift.EndUtc),
                new AvailabilityWindow(resource.Id, secondShift.StartUtc, secondShift.EndUtc)
            ],
            resourcePreferences: []);
    }

    private static ScheduleOptimizationResult CreateOptimizationResult()
    {
        return new ScheduleOptimizationResult(
            new ScheduleCandidate([]),
            CreateEvaluation());
    }

    private static ScheduleEvaluationResult CreateEvaluation(
        int scoreValue = 1000,
        int hardViolationCount = 0,
        int softViolationCount = 0,
        int totalPenalty = 0,
        IReadOnlyCollection<ConstraintViolation>? violations = null)
    {
        var score = new ScheduleScore(
            value: scoreValue,
            hardViolationCount: hardViolationCount,
            softViolationCount: softViolationCount,
            totalPenalty: totalPenalty);

        return new ScheduleEvaluationResult(
            score,
            violations ?? []);
    }

    private static SchedulePeriod CreatePeriod()
    {
        return new SchedulePeriod(
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));
    }

    private static Resource CreateResource()
    {
        return new Resource(
            Guid.NewGuid(),
            "Dana",
            hourlyCost: 100m);
    }

    private static Shift CreateShift(
        DateTime startUtc,
        DateTime endUtc)
    {
        return new Shift(
            Guid.NewGuid(),
            startUtc,
            endUtc,
            kind: ShiftKind.Morning,
            minResourceCount: 0,
            maxResourceCount: 2);
    }

    private sealed class FakeSchedulingProblemBuilder : ISchedulingProblemBuilder
    {
        private readonly SchedulingProblemBuildResult _result;

        public FakeSchedulingProblemBuilder(SchedulingProblemBuildResult result)
        {
            _result = result;
        }

        public SchedulingProblemBuildRequest? ReceivedRequest { get; private set; }

        public SchedulingProblemBuildResult Build(SchedulingProblemBuildRequest request)
        {
            ReceivedRequest = request;

            return _result;
        }
    }

    private sealed class FakeScheduleOptimizer : IScheduleOptimizer
    {
        private readonly ScheduleOptimizationResult _result;
        private readonly Action<IGeneticOptimizerDiagnosticsSink>? _onOptimize;
        private IGeneticOptimizerDiagnosticsSink? _diagnosticsSink;

        public FakeScheduleOptimizer(
            ScheduleOptimizationResult result,
            Action<IGeneticOptimizerDiagnosticsSink>? onOptimize = null)
        {
            _result = result;
            _onOptimize = onOptimize;
        }

        public SchedulingProblem? ReceivedProblem { get; private set; }

        public FakeScheduleOptimizer WithDiagnosticsSink(
            IGeneticOptimizerDiagnosticsSink diagnosticsSink)
        {
            _diagnosticsSink = diagnosticsSink;

            return this;
        }

        public ScheduleOptimizationResult Optimize(SchedulingProblem problem)
        {
            ReceivedProblem = problem;

            if (_diagnosticsSink is not null)
            {
                _onOptimize?.Invoke(_diagnosticsSink);
            }

            return _result;
        }
    }
}
