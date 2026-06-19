using System.Threading.RateLimiting;
using Aether.Api.Errors;
using Aether.Api.Jobs;
using Aether.Api.RateLimiting;
using Aether.Application.Jobs;
using Aether.Application.Scheduling.SubmissionForms;
using Aether.Console.Jobs;
using Aether.Infrastructure.Data.SqlServer;
using Aether.Infrastructure.Queues;
using Aether.Infrastructure.Repositories;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter(
        policyName: RateLimitingPolicies.JobSubmission,
        configureOptions: limiterOptions =>
        {
            limiterOptions.PermitLimit = 20;
            limiterOptions.Window = TimeSpan.FromMinutes(1);
            limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            limiterOptions.QueueLimit = 0;
        });
});

builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);

builder.Services.AddSingleton<ClosedFormSubmissionOptimizationRunner>();

builder.Services.Configure<SqlServerOptions>(
    builder.Configuration.GetSection(SqlServerOptions.SectionName));

builder.Services.AddSingleton<SqlServerConnectionFactory>();

builder.Services.AddSingleton<IJobRepository, SqlServerJobRepository>();
builder.Services.AddSingleton<IJobQueue, InMemoryJobQueue>();

builder.Services.AddSingleton<IJobSubmissionService, JobSubmissionService>();
builder.Services.AddSingleton<IJobQueryService, JobQueryService>();
builder.Services.AddSingleton<IJobCancellationService, JobCancellationService>();

builder.Services.AddTransient<JobWorker>();

builder.Services.AddSingleton<JobWorkerPool>(sp =>
    new JobWorkerPool(() => sp.GetRequiredService<JobWorker>()));

builder.Services.AddHostedService<JobWorkerHostedService>();

builder.Services.AddSingleton<IJobHandler, PrintMessageJobHandler>();
builder.Services.AddSingleton<IJobHandler, FlakyMessageJobHandler>();
builder.Services.AddSingleton<IJobHandler, AlwaysFailingJobHandler>();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRateLimiter();

app.MapControllers();

app.Run();

public partial class Program
{
}