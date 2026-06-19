using Aether.Infrastructure.Queues;

namespace Aether.Tests.Infrastructure;

public class InMemoryJobQueueTests
{
    [Fact]
    public async Task EnqueueAsync_ThenDequeueAsync_ShouldReturnSameJobId()
    {
        var queue = new InMemoryJobQueue();
        var jobId = Guid.NewGuid();

        await queue.EnqueueAsync(jobId);
        var dequeuedJobId = await queue.DequeueAsync();

        Assert.Equal(jobId, dequeuedJobId);
    }

    [Fact]
    public async Task EnqueueAsync_ShouldThrow_WhenJobIdIsEmpty()
    {
        var queue = new InMemoryJobQueue();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            queue.EnqueueAsync(Guid.Empty));
    }

    [Fact]
    public async Task DequeueAsync_ShouldPreserveOrder()
    {
        var queue = new InMemoryJobQueue();
        var firstJobId = Guid.NewGuid();
        var secondJobId = Guid.NewGuid();

        await queue.EnqueueAsync(firstJobId);
        await queue.EnqueueAsync(secondJobId);

        var firstDequeued = await queue.DequeueAsync();
        var secondDequeued = await queue.DequeueAsync();

        Assert.Equal(firstJobId, firstDequeued);
        Assert.Equal(secondJobId, secondDequeued);
    }
}