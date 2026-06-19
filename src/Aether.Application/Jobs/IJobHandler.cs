using Aether.Domain.Jobs;

namespace Aether.Application.Jobs;

public interface IJobHandler
{
    string JobType { get; }

    Task HandleAsync(Job job, CancellationToken cancellationToken = default);
}