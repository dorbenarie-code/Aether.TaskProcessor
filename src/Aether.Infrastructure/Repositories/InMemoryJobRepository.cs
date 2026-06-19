using System.Collections.Concurrent;
using Aether.Application.Jobs;
using Aether.Domain.Jobs;

namespace Aether.Infrastructure.Repositories;

public sealed class InMemoryJobRepository : IJobRepository
{
    private readonly ConcurrentDictionary<Guid, Job> _jobs = new();

    public Task AddAsync(Job job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        var wasAdded = _jobs.TryAdd(job.Id, job);

        if (!wasAdded)
        {
            throw new InvalidOperationException($"A job with id '{job.Id}' already exists.");
        }

        return Task.CompletedTask;
    }

    public Task<Job?> FindByIdAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        _jobs.TryGetValue(jobId, out var job);

        return Task.FromResult(job);
    }

    public Task UpdateAsync(Job job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (!_jobs.ContainsKey(job.Id))
        {
            throw new InvalidOperationException($"Cannot update job '{job.Id}' because it does not exist.");
        }

        _jobs[job.Id] = job;

        return Task.CompletedTask;
    }
    public Task<IReadOnlyCollection<Job>> GetAllAsync(CancellationToken cancellationToken = default)
{
    IReadOnlyCollection<Job> jobs = _jobs.Values
        .OrderBy(job => job.CreatedAtUtc)
        .ToArray();

    return Task.FromResult(jobs);
}

    public Task<IReadOnlyCollection<Job>> GetFailedJobsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<Job> failedJobs = _jobs.Values
            .Where(job => job.Status == JobStatus.Failed)
            .ToArray();

        return Task.FromResult(failedJobs);
    }
}