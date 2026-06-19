using System.Text.Json;
using Aether.Application.Jobs;
using Aether.Domain.Jobs;

namespace Aether.Console.Jobs;

public sealed class AlwaysFailingJobHandler : IJobHandler
{
    public string JobType => "AlwaysFailingMessage";

    public Task HandleAsync(Job job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        using var document = JsonDocument.Parse(job.Payload);
        var message = document.RootElement.GetProperty("message").GetString();

        System.Console.WriteLine($"[Always Failing Handler] Processing job '{job.Id}'");
        System.Console.WriteLine($"[Always Failing Handler] Message: {message}");

        throw new InvalidOperationException("Simulated permanent failure.");
    }
}