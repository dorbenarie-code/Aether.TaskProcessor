namespace Aether.Application.Scheduling.Optimization;

public sealed class CollectingGeneticOptimizerDiagnosticsSink : IGeneticOptimizerDiagnosticsSink
{
    private readonly List<GeneticGenerationDiagnostic> _diagnostics = [];

    public IReadOnlyList<GeneticGenerationDiagnostic> Diagnostics => _diagnostics;

    public void ReportGeneration(GeneticGenerationDiagnostic diagnostic)
    {
        _diagnostics.Add(diagnostic);
    }
}
