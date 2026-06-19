using Aether.Domain.Jobs;

namespace Aether.Application.Jobs;

public interface IJobCancellationService
{
    Task<Job?> CancelAsync(Guid jobId, CancellationToken cancellationToken = default);
}