namespace Aether.Application.Jobs;

public sealed class JobWorkerPool
{
    private readonly Func<JobWorker> _workerFactory;

    public JobWorkerPool(Func<JobWorker> workerFactory)
    {
        _workerFactory = workerFactory ?? throw new ArgumentNullException(nameof(workerFactory));
    }

    public async Task RunAsync(int workerCount, CancellationToken cancellationToken)
    {
        if (workerCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(workerCount), "Worker count must be greater than zero.");
        }

        var workerTasks = Enumerable.Range(1, workerCount)
            .Select(_ => Task.Run(() =>
            {
                var worker = _workerFactory();

                return worker.RunAsync(cancellationToken);
            }, cancellationToken))
            .ToArray();

        await Task.WhenAll(workerTasks);
    }
}