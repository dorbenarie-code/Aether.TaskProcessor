using Aether.Domain.Jobs;

namespace Aether.Application.Jobs;

public sealed class JobWorker
{
    private readonly IJobQueue _queue;
    private readonly IJobRepository _repository;
    private readonly IReadOnlyCollection<IJobHandler> _handlers;
    private readonly TimeProvider _timeProvider;

    public JobWorker(
        IJobQueue queue,
        IJobRepository repository,
        IEnumerable<IJobHandler> handlers,
        TimeProvider timeProvider)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _handlers = handlers?.ToArray() ?? throw new ArgumentNullException(nameof(handlers));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var jobId = await _queue.DequeueAsync(cancellationToken);

            var job = await _repository.FindByIdAsync(jobId, cancellationToken);

            if (job is null)
            {
                continue;
            }

            var handler = _handlers.FirstOrDefault(h => h.JobType == job.JobType);

            if (handler is null)
            {
                var failedAtUtc = GetUtcNow();

                job.StartProcessing(failedAtUtc);
                job.MarkAsPermanentlyFailed($"No handler found for job type '{job.JobType}'", failedAtUtc);

                await _repository.UpdateAsync(job, cancellationToken);
                continue;
            }

            await ProcessJobAsync(job, handler, cancellationToken);
        }
    }

    private async Task ProcessJobAsync(Job job, IJobHandler handler, CancellationToken cancellationToken)
    {
        job.StartProcessing(GetUtcNow());

        try
        {
            await handler.HandleAsync(job, cancellationToken);

            job.Complete(GetUtcNow());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            job.ReturnToPendingAfterCancellation();

            await _repository.UpdateAsync(job, CancellationToken.None);

            throw;
        }
        catch (Exception ex)
        {
            job.Fail(ex.Message, GetUtcNow());
        }

        await _repository.UpdateAsync(job, cancellationToken);

        if (job.Status == JobStatus.Pending)
        {
            await ScheduleRetryAsync(job, cancellationToken);
        }
    }

    private async Task ScheduleRetryAsync(Job job, CancellationToken cancellationToken)
    {
        if (job.NextRetryAtUtc is DateTime nextRetryAtUtc)
        {
            var delay = nextRetryAtUtc - GetUtcNow();

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }
        }

        await _queue.EnqueueAsync(job.Id, cancellationToken);
    }

    private DateTime GetUtcNow()
    {
        return _timeProvider.GetUtcNow().UtcDateTime;
    }
}