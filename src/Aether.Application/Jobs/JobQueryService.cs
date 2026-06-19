using Aether.Domain.Jobs;

namespace Aether.Application.Jobs;

public sealed class JobQueryService : IJobQueryService
{
    private readonly IJobRepository _repository;

    public JobQueryService(IJobRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public Task<Job?> GetByIdAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return _repository.FindByIdAsync(jobId, cancellationToken);
    }

    public Task<IReadOnlyCollection<Job>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetAllAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Job>> GetAllAsync(
        GetJobsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var jobs = await _repository.GetAllAsync(cancellationToken);

        var filteredJobs = jobs.AsEnumerable();

        if (query.Status is not null)
        {
            filteredJobs = filteredJobs.Where(job => job.Status == query.Status.Value);
        }

        var pagedJobs = filteredJobs
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArray();

        return pagedJobs;
    }

    public Task<IReadOnlyCollection<Job>> GetFailedJobsAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetFailedJobsAsync(cancellationToken);
    }
}