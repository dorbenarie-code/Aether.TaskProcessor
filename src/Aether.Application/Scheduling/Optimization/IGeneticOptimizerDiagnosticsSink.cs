namespace Aether.Application.Scheduling.Optimization;

public interface IGeneticOptimizerDiagnosticsSink
{
    void ReportGeneration(GeneticGenerationDiagnostic diagnostic);
}
