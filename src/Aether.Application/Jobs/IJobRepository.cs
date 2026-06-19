using Aether.Domain.Jobs;

namespace Aether.Application.Jobs;

public interface IJobRepository
{
    Task AddAsync(Job job, CancellationToken cancellationToken = default);

    Task<Job?> FindByIdAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task UpdateAsync(Job job, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Job>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Job>> GetFailedJobsAsync(CancellationToken cancellationToken = default);
}