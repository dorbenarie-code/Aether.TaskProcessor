using Aether.Application.Jobs;

namespace Aether.Api.Jobs;

public sealed class JobWorkerHostedService : BackgroundService
{
    private readonly JobWorkerPool _workerPool;

    public JobWorkerHostedService(JobWorkerPool workerPool)
    {
        _workerPool = workerPool ?? throw new ArgumentNullException(nameof(workerPool));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _workerPool.RunAsync(
            workerCount: 2,
            stoppingToken);
    }
}