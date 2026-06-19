using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.SubmissionForms;

public sealed record GoogleFormsWorkerRowResolutionRequest(
    IReadOnlyList<IReadOnlyList<string>> Rows,
    GoogleFormsImportScopeDiscoveryResult Scope,
    IReadOnlyList<Resource> Resources,
    IReadOnlyDictionary<string, Guid>? AliasesByWorkerName = null);
