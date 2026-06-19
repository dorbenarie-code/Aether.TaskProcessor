using System.Text.Json;
using Aether.Application.Jobs;
using Aether.Console.Jobs;
using Aether.Console.Scheduling;
using Aether.Infrastructure.Queues;
using Aether.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

if (args.Length > 0 &&
    string.Equals(args[0], "optimize-clean-xlsx", StringComparison.OrdinalIgnoreCase))
{
    var exitCode = CleanXlsxOptimizationDemoCommand.Run(args[1..]);

    Environment.Exit(exitCode);

    return;
}

if (args.Length > 0 &&
    string.Equals(args[0], "optimize-clean-xlsx-morning-capacity-sensitivity", StringComparison.OrdinalIgnoreCase))
{
    var exitCode = MorningCapacitySensitivityDemoCommand.Run(args[1..]);

    Environment.Exit(exitCode);

    return;
}

var services = new ServiceCollection();

services.AddSingleton<TimeProvider>(TimeProvider.System);

services.AddSingleton<IJobRepository, InMemoryJobRepository>();
services.AddSingleton<IJobQueue, InMemoryJobQueue>();
services.AddSingleton<IJobSubmissionService, JobSubmissionService>();

services.AddSingleton<IJobHandler, PrintMessageJobHandler>();
services.AddSingleton<IJobHandler, FlakyMessageJobHandler>();
services.AddSingleton<IJobHandler, AlwaysFailingJobHandler>();

services.AddTransient<JobWorker>();

services.AddSingleton<JobWorkerPool>(serviceProvider =>
    new JobWorkerPool(() => serviceProvider.GetRequiredService<JobWorker>()));

var serviceProvider = services.BuildServiceProvider();

var repository = serviceProvider.GetRequiredService<IJobRepository>();
var jobSubmissionService = serviceProvider.GetRequiredService<IJobSubmissionService>();
var workerPool = serviceProvider.GetRequiredService<JobWorkerPool>();

var jobs = new List<Aether.Domain.Jobs.Job>();

foreach (var index in Enumerable.Range(1, 4))
{
    var payload = JsonSerializer.Serialize(new
    {
        message = $"Hello from Aether Task Processor #{index}"
    });

    var job = await jobSubmissionService.SubmitAsync(
        jobType: "PrintMessage",
        payload: payload);

    jobs.Add(job);
}

var flakyPayload = JsonSerializer.Serialize(new
{
    message = "This job should fail once and then recover"
});

var flakyJob = await jobSubmissionService.SubmitAsync(
    jobType: "FlakyMessage",
    payload: flakyPayload);

jobs.Add(flakyJob);

var alwaysFailingPayload = JsonSerializer.Serialize(new
{
    message = "This job should fail permanently"
});

var alwaysFailingJob = await jobSubmissionService.SubmitAsync(
    jobType: "AlwaysFailingMessage",
    payload: alwaysFailingPayload,
    maxRetries: 1);

jobs.Add(alwaysFailingJob);

using var cancellationTokenSource = new CancellationTokenSource();

var workerPoolTask = Task.Run(() =>
    workerPool.RunAsync(workerCount: 2, cancellationTokenSource.Token));

await Task.Delay(12000);

cancellationTokenSource.Cancel();

try
{
    await workerPoolTask;
}
catch (OperationCanceledException)
{
    System.Console.WriteLine();
    System.Console.WriteLine("Worker pool stopped.");
}

System.Console.WriteLine();
System.Console.WriteLine("=== Final Job States ===");

foreach (var job in jobs)
{
    var processedJob = await repository.FindByIdAsync(job.Id);

    System.Console.WriteLine();
    System.Console.WriteLine($"Id: {processedJob?.Id}");
    System.Console.WriteLine($"Type: {processedJob?.JobType}");
    System.Console.WriteLine($"Status: {processedJob?.Status}");
    System.Console.WriteLine($"RetryCount: {processedJob?.RetryCount}");
    System.Console.WriteLine($"CreatedAtUtc: {processedJob?.CreatedAtUtc}");
    System.Console.WriteLine($"StartedAtUtc: {processedJob?.StartedAtUtc}");
    System.Console.WriteLine($"CompletedAtUtc: {processedJob?.CompletedAtUtc}");
    System.Console.WriteLine($"NextRetryAtUtc: {processedJob?.NextRetryAtUtc}");
    System.Console.WriteLine($"ErrorMessage: {processedJob?.ErrorMessage ?? "None"}");
}

var failedJobs = await repository.GetFailedJobsAsync();

System.Console.WriteLine();
System.Console.WriteLine("=== Failed Jobs ===");

if (failedJobs.Count == 0)
{
    System.Console.WriteLine("No failed jobs.");
}
else
{
    foreach (var failedJob in failedJobs)
    {
        System.Console.WriteLine();
        System.Console.WriteLine($"Id: {failedJob.Id}");
        System.Console.WriteLine($"Type: {failedJob.JobType}");
        System.Console.WriteLine($"RetryCount: {failedJob.RetryCount}");
        System.Console.WriteLine($"CompletedAtUtc: {failedJob.CompletedAtUtc}");
        System.Console.WriteLine($"ErrorMessage: {failedJob.ErrorMessage}");
    }
}