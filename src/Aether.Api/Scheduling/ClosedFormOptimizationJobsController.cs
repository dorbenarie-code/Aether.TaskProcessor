using System.Text.Json;
using Aether.Application.Jobs;
using Aether.Application.Scheduling.SubmissionForms;
using Aether.Domain.Jobs;
using Microsoft.AspNetCore.Mvc;

namespace Aether.Api.Scheduling;

[ApiController]
[Route("api/scheduling/closed-form-optimization-jobs")]
public sealed class ClosedFormOptimizationJobsController : ControllerBase
{
    private const int DefaultMaxRetries = 0;
    private const string GetJobByIdRouteName = "GetJobById";

    private static readonly JsonSerializerOptions PayloadSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IJobSubmissionService _jobSubmissionService;

    public ClosedFormOptimizationJobsController(
        IJobSubmissionService jobSubmissionService)
    {
        _jobSubmissionService = jobSubmissionService ?? throw new ArgumentNullException(nameof(jobSubmissionService));
    }

    [HttpPost]
    public async Task<ActionResult<ClosedFormOptimizationJobResponse>> SubmitAsync(
        ClosedFormOptimizationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payload = JsonSerializer.Serialize(
            request,
            PayloadSerializerOptions);

        var job = await _jobSubmissionService.SubmitAsync(
            ClosedFormOptimizationJobTypes.ClosedFormOptimization,
            payload,
            DefaultMaxRetries,
            cancellationToken);

        var response = ToResponse(job);

        return AcceptedAtRoute(
            GetJobByIdRouteName,
            new { id = job.Id },
            response);
    }

    private static ClosedFormOptimizationJobResponse ToResponse(Job job)
    {
        return new ClosedFormOptimizationJobResponse(
            job.Id,
            job.JobType,
            job.Status.ToString(),
            job.RetryCount,
            job.MaxRetries,
            job.CreatedAtUtc);
    }
}
