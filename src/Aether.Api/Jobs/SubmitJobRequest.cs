using System.ComponentModel.DataAnnotations;

namespace Aether.Api.Jobs;

public sealed record SubmitJobRequest(
    [Required]
    [MinLength(1)]
    string JobType,

    [Required]
    [MinLength(1)]
    string Payload,

    [Range(0, int.MaxValue)]
    int MaxRetries = 3);