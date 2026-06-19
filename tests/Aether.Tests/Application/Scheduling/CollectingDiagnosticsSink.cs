using Aether.Application.Scheduling.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class CollectingDiagnosticsSink : IGeneticOptimizerDiagnosticsSink
{
    private readonly List<GeneticGenerationDiagnostic> _diagnostics = [];

    public IReadOnlyList<GeneticGenerationDiagnostic> Diagnostics => _diagnostics;

    public void ReportGeneration(GeneticGenerationDiagnostic diagnostic)
    {
        _diagnostics.Add(diagnostic);
    }
}
