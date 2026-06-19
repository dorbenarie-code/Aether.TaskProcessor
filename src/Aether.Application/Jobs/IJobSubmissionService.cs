using Aether.Domain.Jobs;

namespace Aether.Application.Jobs;

public interface IJobSubmissionService
{
    Task<Job> SubmitAsync(
        string jobType,
        string payload,
        int maxRetries = 3,
        CancellationToken cancellationToken = default);
}