using Aether.Domain.Jobs;

namespace Aether.Application.Jobs;

public sealed class JobSubmissionService : IJobSubmissionService
{
    private readonly IJobRepository _repository;
    private readonly IJobQueue _queue;
    private readonly TimeProvider _timeProvider;

    public JobSubmissionService(
        IJobRepository repository,
        IJobQueue queue,
        TimeProvider timeProvider)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<Job> SubmitAsync(
        string jobType,
        string payload,
        int maxRetries = 3,
        CancellationToken cancellationToken = default)
    {
        var job = Job.Create(
            jobType,
            payload,
            _timeProvider.GetUtcNow().UtcDateTime,
            maxRetries);

        await _repository.AddAsync(job, cancellationToken);
        await _queue.EnqueueAsync(job.Id, cancellationToken);

        return job;
    }
}