using System.Text.Json;
using Aether.Application.Jobs;
using Aether.Domain.Jobs;

namespace Aether.Console.Jobs;

public sealed class PrintMessageJobHandler : IJobHandler
{
    public string JobType => "PrintMessage";

    public Task HandleAsync(Job job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        using var document = JsonDocument.Parse(job.Payload);

        var root = document.RootElement;

        var message = root.GetProperty("message").GetString();

        System.Console.WriteLine($"[Handler] Processing job '{job.Id}'");
        System.Console.WriteLine($"[Handler] Message: {message}");

        return Task.CompletedTask;
    }
}