using Aether.Domain.Jobs;

namespace Aether.Application.Jobs;

public sealed class JobCancellationService : IJobCancellationService
{
    private readonly IJobRepository _repository;
    private readonly TimeProvider _timeProvider;

    public JobCancellationService(
        IJobRepository repository,
        TimeProvider timeProvider)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<Job?> CancelAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _repository.FindByIdAsync(jobId, cancellationToken);

        if (job is null)
        {
            return null;
        }

        job.Cancel(_timeProvider.GetUtcNow().UtcDateTime);

        await _repository.UpdateAsync(job, cancellationToken);

        return job;
    }
}