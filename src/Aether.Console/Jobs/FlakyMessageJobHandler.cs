using System.Collections.Concurrent;
using System.Text.Json;
using Aether.Application.Jobs;
using Aether.Domain.Jobs;

namespace Aether.Console.Jobs;

public sealed class FlakyMessageJobHandler : IJobHandler
{
    private readonly ConcurrentDictionary<Guid, int> _attemptsByJobId = new();

    public string JobType => "FlakyMessage";

    public Task HandleAsync(Job job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        var attempt = _attemptsByJobId.AddOrUpdate(
            job.Id,
            addValue: 1,
            updateValueFactory: (_, currentAttempt) => currentAttempt + 1);

        using var document = JsonDocument.Parse(job.Payload);
        var message = document.RootElement.GetProperty("message").GetString();

        System.Console.WriteLine($"[Flaky Handler] Processing job '{job.Id}'");
        System.Console.WriteLine($"[Flaky Handler] Attempt: {attempt}");
        System.Console.WriteLine($"[Flaky Handler] Message: {message}");

        if (attempt == 1)
        {
            throw new InvalidOperationException("Simulated temporary failure.");
        }

        System.Console.WriteLine("[Flaky Handler] Job recovered successfully after retry.");

        return Task.CompletedTask;
    }
}