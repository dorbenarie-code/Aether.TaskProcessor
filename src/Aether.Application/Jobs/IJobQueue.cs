namespace Aether.Application.Jobs;

public interface IJobQueue
{
    Task EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<Guid> DequeueAsync(CancellationToken cancellationToken = default);
}