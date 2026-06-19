using System.Threading.Channels;
using Aether.Application.Jobs;

namespace Aether.Infrastructure.Queues;

public sealed class InMemoryJobQueue : IJobQueue
{
    private readonly Channel<Guid> _channel;

    public InMemoryJobQueue()
    {
        _channel = Channel.CreateUnbounded<Guid>();
    }

    public async Task EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (jobId == Guid.Empty)
        {
            throw new ArgumentException("Job id cannot be empty.", nameof(jobId));
        }

        await _channel.Writer.WriteAsync(jobId, cancellationToken);
    }

    public async Task<Guid> DequeueAsync(CancellationToken cancellationToken = default)
    {
        var jobId = await _channel.Reader.ReadAsync(cancellationToken);

        return jobId;
    }
}