namespace Aether.Application.Scheduling.Optimization;

public sealed record GeneticGenerationDiagnostic(
    int GenerationIndex,
    int PopulationSize,
    int FeasibleCandidateCount,
    int BestScoreValue,
    int BestTotalPenalty,
    int BestHardViolationCount,
    int BestSoftViolationCount,
    int BestSoFarScoreValue,
    int BestSoFarTotalPenalty,
    int BestSoFarHardViolationCount,
    int BestSoFarSoftViolationCount)
{
    public GeneticGenerationDiagnostic(
        int GenerationIndex,
        int PopulationSize,
        int FeasibleCandidateCount,
        int BestScoreValue,
        int BestTotalPenalty,
        int BestHardViolationCount,
        int BestSoftViolationCount)
        : this(
            GenerationIndex,
            PopulationSize,
            FeasibleCandidateCount,
            BestScoreValue,
            BestTotalPenalty,
            BestHardViolationCount,
            BestSoftViolationCount,
            BestScoreValue,
            BestTotalPenalty,
            BestHardViolationCount,
            BestSoftViolationCount)
    {
    }
}
