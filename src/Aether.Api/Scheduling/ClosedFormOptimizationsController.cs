using Aether.Application.Scheduling.SubmissionForms;
using Microsoft.AspNetCore.Mvc;

namespace Aether.Api.Scheduling;

[ApiController]
[Route("api/scheduling/closed-form-optimizations")]
public sealed class ClosedFormOptimizationsController : ControllerBase
{
    private readonly ClosedFormSubmissionOptimizationRunner _runner;

    public ClosedFormOptimizationsController(
        ClosedFormSubmissionOptimizationRunner runner)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    }

    [HttpPost]
    public ActionResult<ClosedFormOptimizationResponse> Optimize(
        ClosedFormOptimizationRequest request)
    {
        try
        {
            var applicationRequest = request.ToApplicationRequest();
            var result = _runner.Run(applicationRequest);

            return Ok(result.ToResponse());
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid closed form optimization request.",
                Detail = ex.Message
            });
        }
    }
}
