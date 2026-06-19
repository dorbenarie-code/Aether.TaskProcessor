namespace Aether.Application.Scheduling.Contracts;

public sealed record SchedulingRunComparison(
    bool GeneticRankedBetter,
    int DeterministicHardViolationCount,
    int GeneticHardViolationCount,
    int DeterministicTotalPenalty,
    int GeneticTotalPenalty,
    int DeterministicIgnoredAvoidPreferenceViolations,
    int GeneticIgnoredAvoidPreferenceViolations,
    int DeterministicShiftSequenceQuotaViolations,
    int GeneticShiftSequenceQuotaViolations);
