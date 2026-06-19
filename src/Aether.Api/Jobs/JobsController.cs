using Aether.Application.Jobs;
using Aether.Domain.Jobs;
using Microsoft.AspNetCore.Mvc;
using Aether.Api.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
namespace Aether.Api.Jobs;

[ApiController]
[Route("api/jobs")]
public sealed class JobsController : ControllerBase
{
    private const string GetJobByIdRouteName = "GetJobById";

    private readonly IJobSubmissionService _jobSubmissionService;
    private readonly IJobQueryService _jobQueryService;
    private readonly IJobCancellationService _jobCancellationService;

    public JobsController(
        IJobSubmissionService jobSubmissionService,
        IJobQueryService jobQueryService,
        IJobCancellationService jobCancellationService)
    {
        _jobSubmissionService = jobSubmissionService ?? throw new ArgumentNullException(nameof(jobSubmissionService));
        _jobQueryService = jobQueryService ?? throw new ArgumentNullException(nameof(jobQueryService));
        _jobCancellationService = jobCancellationService ?? throw new ArgumentNullException(nameof(jobCancellationService));
    }

    [HttpPost]
[EnableRateLimiting(RateLimitingPolicies.JobSubmission)]
public async Task<ActionResult<SubmitJobResponse>> SubmitAsync(
    SubmitJobRequest request,
    CancellationToken cancellationToken)
    {
        var job = await _jobSubmissionService.SubmitAsync(
            request.JobType,
            request.Payload,
            request.MaxRetries,
            cancellationToken);

        var response = new SubmitJobResponse(
            job.Id,
            job.JobType,
            job.Status,
            job.RetryCount,
            job.MaxRetries,
            job.CreatedAtUtc);

        return CreatedAtRoute(
            GetJobByIdRouteName,
            new { id = job.Id },
            response);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<JobResponse>>> GetAllAsync(
        [FromQuery] JobStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        GetJobsQuery query;

        try
        {
            query = new GetJobsQuery(
                status,
                page,
                pageSize);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid jobs query.",
                Detail = ex.Message
            });
        }

        var jobs = await _jobQueryService.GetAllAsync(query, cancellationToken);

        var response = jobs
            .Select(ToResponse)
            .ToArray();

        return Ok(response);
    }

    [HttpGet("{id:guid}", Name = GetJobByIdRouteName)]
    public async Task<ActionResult<JobResponse>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var job = await _jobQueryService.GetByIdAsync(id, cancellationToken);

        if (job is null)
        {
            return NotFound();
        }

        return Ok(ToResponse(job));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<JobResponse>> CancelAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var job = await _jobCancellationService.CancelAsync(id, cancellationToken);

            if (job is null)
            {
                return NotFound();
            }

            return Ok(ToResponse(job));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Job cannot be cancelled.",
                Detail = ex.Message
            });
        }
    }

    [HttpGet("failed")]
    public async Task<ActionResult<IReadOnlyCollection<JobResponse>>> GetFailedAsync(
        CancellationToken cancellationToken)
    {
        var jobs = await _jobQueryService.GetFailedJobsAsync(cancellationToken);

        var response = jobs
            .Select(ToResponse)
            .ToArray();

        return Ok(response);
    }

    private static JobResponse ToResponse(Job job)
    {
        return new JobResponse(
            job.Id,
            job.JobType,
            job.Status,
            job.RetryCount,
            job.MaxRetries,
            job.ErrorMessage,
            job.CreatedAtUtc,
            job.StartedAtUtc,
            job.CompletedAtUtc,
            job.NextRetryAtUtc);
    }
}
