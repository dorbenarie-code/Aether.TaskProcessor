using Aether.Application.Scheduling.Optimization;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class GeneticScheduleOptimizerTests
{
    [Fact]
    public void Optimize_ShouldThrow_WhenProblemIsNull()
    {
        var optimizer = new GeneticScheduleOptimizer(
            populationSize: 1,
            seed: 42);

        var exception = Assert.Throws<ArgumentNullException>(() =>
            optimizer.Optimize(null!));

        Assert.Equal("problem", exception.ParamName);
    }

    [Fact]
    public void Optimize_ShouldReturnCandidateAndEvaluation()
    {
        var dana = CreateResource("Dana");
        var yossi = CreateResource("Yossi");

        var shift = CreateShift(
            new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning);

        var problem = CreateProblem(
            resources: [dana, yossi],
            shifts: [shift],
            availabilityWindows:
            [
                CreateAvailability(dana, shift),
                CreateAvailability(yossi, shift)
            ],
            resourcePreferences: []);

        var optimizer = new GeneticScheduleOptimizer(
            populationSize: 10,
            seed: 42);

        var result = optimizer.Optimize(problem);

        Assert.NotNull(result.Candidate);
        Assert.NotNull(result.Evaluation);

        var assignment = Assert.Single(result.Candidate.Assignments);
        Assert.Equal(shift.Id, assignment.ShiftId);

        Assert.True(result.Evaluation.IsFeasible);
        Assert.Empty(result.Evaluation.Violations);
        Assert.Equal(1000, result.Evaluation.Score.Value);
    }

    [Fact]
    public void Optimize_ShouldNotAssignResourceWithoutAvailability()
    {
        var dana = CreateResource("Dana");

        var shift = CreateShift(
            new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning);

        var problem = CreateProblem(
            resources: [dana],
            shifts: [shift],
            availabilityWindows: [],
            resourcePreferences: []);

        var optimizer = new GeneticScheduleOptimizer(
            populationSize: 10,
            seed: 42);

        var result = optimizer.Optimize(problem);

        Assert.Empty(result.Candidate.Assignments);
        Assert.False(result.Evaluation.IsFeasible);

        Assert.Contains(
            result.Evaluation.Violations,
            violation => violation.Type == ConstraintViolationType.ShiftUnderstaffed);
    }

    [Fact]
    public void Optimize_ShouldRespectRequiresPreferenceToAssign()
    {
        var dana = CreateResource("Dana");

        var shift = CreateShift(
            new DateTime(2026, 6, 1, 22, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 2, 6, 30, 0, DateTimeKind.Utc),
            ShiftKind.Night,
            requiresPreferenceToAssign: true);

        var problem = CreateProblem(
            resources: [dana],
            shifts: [shift],
            availabilityWindows:
            [
                CreateAvailability(dana, shift)
            ],
            resourcePreferences: []);

        var optimizer = new GeneticScheduleOptimizer(
            populationSize: 10,
            seed: 42);

        var result = optimizer.Optimize(problem);

        Assert.Empty(result.Candidate.Assignments);
        Assert.False(result.Evaluation.IsFeasible);

        Assert.Contains(
            result.Evaluation.Violations,
            violation => violation.Type == ConstraintViolationType.ShiftUnderstaffed);
    }

    [Fact]
    public void Optimize_ShouldBeDeterministic_WhenSeedIsFixed()
    {
        var resources = new[]
        {
            CreateResource("Dana"),
            CreateResource("Yossi"),
            CreateResource("Noa")
        };

        var shifts = new[]
        {
            CreateShift(
                new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Utc),
                ShiftKind.Morning),
            CreateShift(
                new DateTime(2026, 6, 2, 14, 30, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 2, 22, 30, 0, DateTimeKind.Utc),
                ShiftKind.Afternoon),
            CreateShift(
                new DateTime(2026, 6, 3, 6, 30, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 3, 14, 30, 0, DateTimeKind.Utc),
                ShiftKind.Morning)
        };

        var problem = CreateProblem(
            resources: resources,
            shifts: shifts,
            availabilityWindows: CreateAvailabilityForAll(resources, shifts),
            resourcePreferences: []);

        var firstOptimizer = new GeneticScheduleOptimizer(
            populationSize: 20,
            seed: 123);

        var secondOptimizer = new GeneticScheduleOptimizer(
            populationSize: 20,
            seed: 123);

        var firstResult = firstOptimizer.Optimize(problem);
        var secondResult = secondOptimizer.Optimize(problem);

        var firstAssignments = ToAssignmentKeys(firstResult.Candidate);
        var secondAssignments = ToAssignmentKeys(secondResult.Candidate);

        Assert.Equal(firstAssignments, secondAssignments);
        Assert.Equal(
            firstResult.Evaluation.Score.TotalPenalty,
            secondResult.Evaluation.Score.TotalPenalty);
    }

    [Fact]
    public void Optimize_ShouldUseVariableAssignmentCount_WhenShiftAllowsAdditionalCapacity()
    {
        var dana = CreateResource("Dana");
        var yossi = CreateResource("Yossi");

        var shift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning,
            minResourceCount: 1,
            maxResourceCount: 2);

        var problem = CreateProblem(
            resources: [dana, yossi],
            shifts: [shift],
            availabilityWindows:
            [
                CreateAvailability(dana, shift),
                CreateAvailability(yossi, shift)
            ],
            resourcePreferences: [],
            minimumAssignedHoursPerResource: 8);

        var optimizer = new GeneticScheduleOptimizer(
            populationSize: 100,
            seed: 42);

        var result = optimizer.Optimize(problem);

        Assert.True(result.Evaluation.IsFeasible);
        Assert.Equal(2, result.Candidate.Assignments.Count);

        Assert.DoesNotContain(
            result.Evaluation.Violations,
            violation => violation.Type == ConstraintViolationType.ResourceMinimumAssignedHoursNotMet);
    }

    [Fact]
    public void Optimize_ShouldNotAssignSameResourceTwice_WhenTargetAssignmentCountIsGreaterThanAvailableResources()
    {
        var dana = CreateResource("Dana");

        var shift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning,
            minResourceCount: 2,
            maxResourceCount: 2);

        var problem = CreateProblem(
            resources: [dana],
            shifts: [shift],
            availabilityWindows:
            [
                CreateAvailability(dana, shift)
            ],
            resourcePreferences: []);

        var optimizer = new GeneticScheduleOptimizer(
            populationSize: 10,
            seed: 42);

        var result = optimizer.Optimize(problem);

        var assignment = Assert.Single(result.Candidate.Assignments);
        Assert.Equal(dana.Id, assignment.ResourceId);
        Assert.Equal(shift.Id, assignment.ShiftId);

        Assert.False(result.Evaluation.IsFeasible);
        Assert.Contains(
            result.Evaluation.Violations,
            violation => violation.Type == ConstraintViolationType.ShiftUnderstaffed);
    }


    [Fact]
    public void Optimize_ShouldAssignDemandTriggeredOptionalShift_WhenPreferExists()
    {
        var dana = CreateResource("Dana");

        var shift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 6, 1, 22, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 2, 6, 30, 0, DateTimeKind.Utc),
            ShiftKind.Night,
            minResourceCount: 0,
            maxResourceCount: 1,
            requiresPreferenceToAssign: true,
            requiresMinimumWhenPreferenceExists: true);

        var problem = CreateProblem(
            resources: [dana],
            shifts: [shift],
            availabilityWindows:
            [
                CreateAvailability(dana, shift)
            ],
            resourcePreferences:
            [
                CreatePreferPreference(dana, shift)
            ]);

        var optimizer = new GeneticScheduleOptimizer(
            populationSize: 1,
            seed: 1);

        var result = optimizer.Optimize(problem);

        var assignment = Assert.Single(result.Candidate.Assignments);
        Assert.Equal(dana.Id, assignment.ResourceId);
        Assert.Equal(shift.Id, assignment.ShiftId);

        Assert.True(result.Evaluation.IsFeasible);
        Assert.DoesNotContain(
            result.Evaluation.Violations,
            violation => violation.Type == ConstraintViolationType.ShiftUnderstaffed);
    }

    [Fact]
    public void Optimize_ShouldUseOptionalPreferredShift_WhenRequestOnlyShiftHasPreferDemand()
    {
        var dana = CreateResource("Dana");

        var shift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 6, 7, 14, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 7, 22, 30, 0, DateTimeKind.Utc),
            ShiftKind.Afternoon,
            minResourceCount: 0,
            maxResourceCount: 1,
            requiresPreferenceToAssign: true,
            requiresMinimumWhenPreferenceExists: false);

        var problem = CreateProblem(
            resources: [dana],
            shifts: [shift],
            availabilityWindows:
            [
                CreateAvailability(dana, shift)
            ],
            resourcePreferences:
            [
                CreatePreferPreference(dana, shift)
            ],
            minimumAssignedHoursPerResource: 8);

        var optimizer = new GeneticScheduleOptimizer(
            populationSize: 1,
            seed: 1);

        var result = optimizer.Optimize(problem);

        var assignment = Assert.Single(result.Candidate.Assignments);
        Assert.Equal(dana.Id, assignment.ResourceId);
        Assert.Equal(shift.Id, assignment.ShiftId);

        Assert.True(result.Evaluation.IsFeasible);
        Assert.DoesNotContain(
            result.Evaluation.Violations,
            violation => violation.Type == ConstraintViolationType.ResourceMinimumAssignedHoursNotMet);
    }

    [Fact]
    public void Optimize_ShouldNotAssignNonPreferredResource_ToOptionalRequestOnlyShift()
    {
        var dana = CreateResource("Dana");

        var shift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 6, 7, 14, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 7, 22, 30, 0, DateTimeKind.Utc),
            ShiftKind.Afternoon,
            minResourceCount: 0,
            maxResourceCount: 1,
            requiresPreferenceToAssign: true,
            requiresMinimumWhenPreferenceExists: false);

        var problem = CreateProblem(
            resources: [dana],
            shifts: [shift],
            availabilityWindows:
            [
                CreateAvailability(dana, shift)
            ],
            resourcePreferences: []);

        var optimizer = new GeneticScheduleOptimizer(
            populationSize: 1,
            seed: 1);

        var result = optimizer.Optimize(problem);

        Assert.Empty(result.Candidate.Assignments);
        Assert.True(result.Evaluation.IsFeasible);

        Assert.DoesNotContain(
            result.Evaluation.Violations,
            violation => violation.Type == ConstraintViolationType.AssignedWithoutRequiredPreference);
    }

    [Fact]
    public void Optimize_ShouldLeaveOptionalShiftEmpty_WhenNoPreferDemandExists()
    {
        var dana = CreateResource("Dana");

        var shift = new Shift(
            Guid.NewGuid(),
            new DateTime(2026, 6, 7, 22, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 8, 6, 30, 0, DateTimeKind.Utc),
            ShiftKind.Night,
            minResourceCount: 0,
            maxResourceCount: 1,
            requiresPreferenceToAssign: true,
            requiresMinimumWhenPreferenceExists: false);

        var problem = CreateProblem(
            resources: [dana],
            shifts: [shift],
            availabilityWindows:
            [
                CreateAvailability(dana, shift)
            ],
            resourcePreferences: []);

        var optimizer = new GeneticScheduleOptimizer(
            populationSize: 1,
            seed: 1);

        var result = optimizer.Optimize(problem);

        Assert.Empty(result.Candidate.Assignments);
        Assert.True(result.Evaluation.IsFeasible);
        Assert.Empty(result.Evaluation.Violations);
    }


    [Fact]
    public void Constructor_ShouldThrow_WhenGenerationCountIsNegative()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = new GeneticScheduleOptimizer(
                populationSize: 10,
                seed: 1,
                generationCount: -1);
        });

        Assert.Equal("generationCount", exception.ParamName);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenEliteCountIsNegative()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = new GeneticScheduleOptimizer(
                populationSize: 10,
                seed: 1,
                generationCount: 1,
                eliteCount: -1);
        });

        Assert.Equal("eliteCount", exception.ParamName);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenTournamentSizeIsZero()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = new GeneticScheduleOptimizer(
                populationSize: 10,
                seed: 1,
                generationCount: 1,
                eliteCount: 1,
                tournamentSize: 0);
        });

        Assert.Equal("tournamentSize", exception.ParamName);
    }


    [Fact]
    public void Optimize_ShouldBeDeterministic_WhenSeedIsFixed_WithGenerations()
    {
        var resources = new[]
        {
            CreateResource("Dana"),
            CreateResource("Yossi"),
            CreateResource("Noa")
        };

        var shifts = new[]
        {
            CreateShift(
                new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Utc),
                ShiftKind.Morning),
            CreateShift(
                new DateTime(2026, 6, 2, 14, 30, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 2, 22, 30, 0, DateTimeKind.Utc),
                ShiftKind.Afternoon),
            CreateShift(
                new DateTime(2026, 6, 3, 6, 30, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 3, 14, 30, 0, DateTimeKind.Utc),
                ShiftKind.Morning)
        };

        var problem = CreateProblem(
            resources: resources,
            shifts: shifts,
            availabilityWindows: CreateAvailabilityForAll(resources, shifts),
            resourcePreferences: []);

        var firstOptimizer = new GeneticScheduleOptimizer(
            populationSize: 20,
            seed: 123,
            generationCount: 10,
            eliteCount: 1,
            tournamentSize: 3);

        var secondOptimizer = new GeneticScheduleOptimizer(
            populationSize: 20,
            seed: 123,
            generationCount: 10,
            eliteCount: 1,
            tournamentSize: 3);

        var firstResult = firstOptimizer.Optimize(problem);
        var secondResult = secondOptimizer.Optimize(problem);

        Assert.Equal(
            ToAssignmentKeys(firstResult.Candidate),
            ToAssignmentKeys(secondResult.Candidate));

        Assert.Equal(
            firstResult.Evaluation.Score.TotalPenalty,
            secondResult.Evaluation.Score.TotalPenalty);
    }

    [Fact]
    public void Optimize_ShouldReturnEvaluatedCandidate_WhenGenerationCountIsGreaterThanZero()
    {
        var dana = CreateResource("Dana");
        var yossi = CreateResource("Yossi");

        var shift = CreateShift(
            new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning);

        var problem = CreateProblem(
            resources: [dana, yossi],
            shifts: [shift],
            availabilityWindows:
            [
                CreateAvailability(dana, shift),
                CreateAvailability(yossi, shift)
            ],
            resourcePreferences: []);

        var optimizer = new GeneticScheduleOptimizer(
            populationSize: 5,
            seed: 42,
            generationCount: 2,
            eliteCount: 1,
            tournamentSize: 2);

        var result = optimizer.Optimize(problem);

        Assert.NotNull(result.Candidate);
        Assert.NotNull(result.Evaluation);
        Assert.True(result.Evaluation.IsFeasible);
    }

    [Fact]
    public void Optimize_ShouldNotReturnWorseThanInitialBest_WhenElitismIsEnabled()
    {
        var resources = new[]
        {
            CreateResource("Dana"),
            CreateResource("Yossi"),
            CreateResource("Noa")
        };

        var shifts = new[]
        {
            CreateShift(
                new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Utc),
                ShiftKind.Morning),
            CreateShift(
                new DateTime(2026, 6, 2, 14, 30, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 2, 22, 30, 0, DateTimeKind.Utc),
                ShiftKind.Afternoon),
            CreateShift(
                new DateTime(2026, 6, 3, 6, 30, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 3, 14, 30, 0, DateTimeKind.Utc),
                ShiftKind.Morning)
        };

        var problem = CreateProblem(
            resources: resources,
            shifts: shifts,
            availabilityWindows: CreateAvailabilityForAll(resources, shifts),
            resourcePreferences: []);

        var initialOnly = new GeneticScheduleOptimizer(
                populationSize: 20,
                seed: 123,
                generationCount: 0,
                eliteCount: 1,
                tournamentSize: 3)
            .Optimize(problem);

        var evolved = new GeneticScheduleOptimizer(
                populationSize: 20,
                seed: 123,
                generationCount: 20,
                eliteCount: 1,
                tournamentSize: 3)
            .Optimize(problem);

        var ranker = new ScheduleEvaluationResultRanker();

        Assert.False(ranker.IsBetterThan(
            initialOnly.Evaluation,
            evolved.Evaluation));
    }

    [Fact]
    public void Optimize_ShouldReportGenerationZero_WhenDiagnosticsSinkIsProvided()
    {
        var dana = CreateResource("Dana");
        var yossi = CreateResource("Yossi");

        var shift = CreateShift(
            new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning);

        var problem = CreateProblem(
            resources: [dana, yossi],
            shifts: [shift],
            availabilityWindows:
            [
                CreateAvailability(dana, shift),
                CreateAvailability(yossi, shift)
            ],
            resourcePreferences: []);

        var diagnosticsSink = new CollectingDiagnosticsSink();

        var optimizer = new GeneticScheduleOptimizer(
            populationSize: 5,
            seed: 42,
            generationCount: 0,
            eliteCount: 1,
            tournamentSize: 2,
            diagnosticsSink: diagnosticsSink);

        optimizer.Optimize(problem);

        var diagnostic = Assert.Single(diagnosticsSink.Diagnostics);

        Assert.Equal(0, diagnostic.GenerationIndex);
        Assert.Equal(5, diagnostic.PopulationSize);
        Assert.Equal(5, diagnostic.FeasibleCandidateCount);
        Assert.Equal(1000, diagnostic.BestScoreValue);
        Assert.Equal(0, diagnostic.BestTotalPenalty);
        Assert.Equal(0, diagnostic.BestHardViolationCount);
        Assert.Equal(0, diagnostic.BestSoftViolationCount);
        Assert.Equal(1000, diagnostic.BestSoFarScoreValue);
        Assert.Equal(0, diagnostic.BestSoFarTotalPenalty);
        Assert.Equal(0, diagnostic.BestSoFarHardViolationCount);
        Assert.Equal(0, diagnostic.BestSoFarSoftViolationCount);
    }

    [Fact]
    public void Optimize_ShouldReportInitialAndEvolvedGenerations_WhenDiagnosticsSinkIsProvided()
    {
        var dana = CreateResource("Dana");
        var yossi = CreateResource("Yossi");

        var firstShift = CreateShift(
            new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning);

        var secondShift = CreateShift(
            new DateTime(2026, 6, 2, 14, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 2, 22, 30, 0, DateTimeKind.Utc),
            ShiftKind.Afternoon);

        var problem = CreateProblem(
            resources: [dana, yossi],
            shifts: [firstShift, secondShift],
            availabilityWindows:
            [
                CreateAvailability(dana, firstShift),
                CreateAvailability(dana, secondShift),
                CreateAvailability(yossi, firstShift),
                CreateAvailability(yossi, secondShift)
            ],
            resourcePreferences: []);

        var diagnosticsSink = new CollectingDiagnosticsSink();

        var optimizer = new GeneticScheduleOptimizer(
            populationSize: 5,
            seed: 42,
            generationCount: 2,
            eliteCount: 1,
            tournamentSize: 2,
            diagnosticsSink: diagnosticsSink);

        var result = optimizer.Optimize(problem);

        Assert.Equal([0, 1, 2], diagnosticsSink.Diagnostics
            .Select(diagnostic => diagnostic.GenerationIndex)
            .ToArray());

        Assert.All(
            diagnosticsSink.Diagnostics,
            diagnostic =>
            {
                Assert.Equal(5, diagnostic.PopulationSize);
                Assert.InRange(diagnostic.FeasibleCandidateCount, 0, 5);
                Assert.InRange(diagnostic.BestScoreValue, 0, 1000);
                Assert.True(diagnostic.BestTotalPenalty >= 0);
                Assert.True(diagnostic.BestHardViolationCount >= 0);
                Assert.True(diagnostic.BestSoftViolationCount >= 0);
                Assert.InRange(diagnostic.BestSoFarScoreValue, 0, 1000);
                Assert.True(diagnostic.BestSoFarTotalPenalty >= 0);
                Assert.True(diagnostic.BestSoFarHardViolationCount >= 0);
                Assert.True(diagnostic.BestSoFarSoftViolationCount >= 0);
            });

        var lastDiagnostic = diagnosticsSink.Diagnostics[
            diagnosticsSink.Diagnostics.Count - 1];

        Assert.Equal(result.Evaluation.Score.Value, lastDiagnostic.BestSoFarScoreValue);
        Assert.Equal(result.Evaluation.Score.TotalPenalty, lastDiagnostic.BestSoFarTotalPenalty);
        Assert.Equal(result.Evaluation.Score.HardViolationCount, lastDiagnostic.BestSoFarHardViolationCount);
        Assert.Equal(result.Evaluation.Score.SoftViolationCount, lastDiagnostic.BestSoFarSoftViolationCount);
    }

    [Fact]
    public void Optimize_ShouldNotRequireDiagnosticsSink()
    {
        var dana = CreateResource("Dana");

        var shift = CreateShift(
            new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Utc),
            ShiftKind.Morning);

        var problem = CreateProblem(
            resources: [dana],
            shifts: [shift],
            availabilityWindows:
            [
                CreateAvailability(dana, shift)
            ],
            resourcePreferences: []);

        var optimizer = new GeneticScheduleOptimizer(
            populationSize: 5,
            seed: 42,
            generationCount: 2,
            eliteCount: 1,
            tournamentSize: 2);

        var result = optimizer.Optimize(problem);

        Assert.NotNull(result.Candidate);
        Assert.NotNull(result.Evaluation);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenEvolutionModeIsUnsupported()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = new GeneticScheduleOptimizer(
                populationSize: 10,
                seed: 1,
                generationCount: 1,
                eliteCount: 1,
                tournamentSize: 1,
                evolutionMode: (GeneticEvolutionMode)999);
        });

        Assert.Equal("evolutionMode", exception.ParamName);
    }

    [Fact]
    public void Optimize_ShouldRunWithCleanEvolutionMode_UsingCrossoverBasedGenerations()
    {
        var resources = new[]
        {
            CreateResource("Dana"),
            CreateResource("Yossi"),
            CreateResource("Noa")
        };

        var shifts = new[]
        {
            CreateShift(
                new DateTime(2026, 6, 1, 6, 30, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Utc),
                ShiftKind.Morning),
            CreateShift(
                new DateTime(2026, 6, 1, 14, 30, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 1, 22, 30, 0, DateTimeKind.Utc),
                ShiftKind.Afternoon),
            CreateShift(
                new DateTime(2026, 6, 2, 6, 30, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 2, 14, 30, 0, DateTimeKind.Utc),
                ShiftKind.Morning)
        };

        var problem = CreateProblem(
            resources: resources,
            shifts: shifts,
            availabilityWindows: CreateAvailabilityForAll(resources, shifts),
            resourcePreferences: []);

        var diagnosticsSink = new CollectingDiagnosticsSink();

        var optimizer = new GeneticScheduleOptimizer(
            populationSize: 20,
            seed: 123,
            generationCount: 3,
            eliteCount: 1,
            tournamentSize: 3,
            diagnosticsSink: diagnosticsSink,
            evolutionMode: GeneticEvolutionMode.Clean);

        var result = optimizer.Optimize(problem);

        Assert.NotNull(result.Candidate);
        Assert.NotNull(result.Evaluation);
        Assert.NotEmpty(result.Candidate.Assignments);
        Assert.True(result.Evaluation.IsFeasible);

        Assert.Equal([0, 1, 2, 3], diagnosticsSink.Diagnostics
            .Select(diagnostic => diagnostic.GenerationIndex)
            .ToArray());

        Assert.All(
            diagnosticsSink.Diagnostics,
            diagnostic =>
            {
                Assert.Equal(20, diagnostic.PopulationSize);
                Assert.InRange(diagnostic.FeasibleCandidateCount, 0, 20);
            });

        var knownResourceIds = resources
            .Select(resource => resource.Id)
            .ToHashSet();

        var knownShiftIds = shifts
            .Select(shift => shift.Id)
            .ToHashSet();

        Assert.All(
            result.Candidate.Assignments,
            assignment =>
            {
                Assert.Contains(assignment.ResourceId, knownResourceIds);
                Assert.Contains(assignment.ShiftId, knownShiftIds);
            });
    }

    private static SchedulingProblem CreateProblem(
        IReadOnlyCollection<Resource> resources,
        IReadOnlyCollection<Shift> shifts,
        IReadOnlyCollection<AvailabilityWindow> availabilityWindows,
        IReadOnlyCollection<ResourcePreference> resourcePreferences,
        int minimumAssignedHoursPerResource = 0)
    {
        return new SchedulingProblem(
            period: new SchedulePeriod(
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc)),
            resources: resources,
            shifts: shifts,
            availabilityWindows: availabilityWindows,
            resourcePreferences: resourcePreferences,
            minimumAssignedHoursPerResource: minimumAssignedHoursPerResource);
    }

    private static Resource CreateResource(string name)
    {
        return new Resource(
            Guid.NewGuid(),
            name,
            hourlyCost: 100m);
    }

    private static Shift CreateShift(
        DateTime startUtc,
        DateTime endUtc,
        ShiftKind kind,
        bool requiresPreferenceToAssign = false)
    {
        return new Shift(
            Guid.NewGuid(),
            startUtc,
            endUtc,
            kind,
            minResourceCount: 1,
            maxResourceCount: 1,
            requiresPreferenceToAssign: requiresPreferenceToAssign);
    }

    private static AvailabilityWindow CreateAvailability(
        Resource resource,
        Shift shift)
    {
        return new AvailabilityWindow(
            resource.Id,
            shift.StartUtc,
            shift.EndUtc);
    }


    private static ResourcePreference CreatePreferPreference(
        Resource resource,
        Shift shift)
    {
        return new ResourcePreference(
            resource.Id,
            shift.StartUtc,
            shift.EndUtc,
            ResourcePreferenceType.Prefer,
            ResourcePreferencePriority.High);
    }

    private static IReadOnlyCollection<AvailabilityWindow> CreateAvailabilityForAll(
        IReadOnlyCollection<Resource> resources,
        IReadOnlyCollection<Shift> shifts)
    {
        return resources
            .SelectMany(resource => shifts.Select(shift =>
                CreateAvailability(resource, shift)))
            .ToArray();
    }

    private static string[] ToAssignmentKeys(ScheduleCandidate candidate)
    {
        return candidate.Assignments
            .OrderBy(assignment => assignment.ShiftId)
            .ThenBy(assignment => assignment.ResourceId)
            .Select(assignment => $"{assignment.ResourceId}:{assignment.ShiftId}")
            .ToArray();
    }
}
