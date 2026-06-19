using Aether.Domain.Jobs;

namespace Aether.Application.Jobs;

public interface IJobQueryService
{
    Task<Job?> GetByIdAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Job>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Job>> GetAllAsync(GetJobsQuery query, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Job>> GetFailedJobsAsync(CancellationToken cancellationToken = default);
}