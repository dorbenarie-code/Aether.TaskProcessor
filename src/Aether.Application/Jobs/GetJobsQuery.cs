using Aether.Domain.Jobs;

namespace Aether.Application.Jobs;

public sealed class GetJobsQuery
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public JobStatus? Status { get; }
    public int Page { get; }
    public int PageSize { get; }

    public GetJobsQuery(
        JobStatus? status = null,
        int page = DefaultPage,
        int pageSize = DefaultPageSize)
    {
        if (page <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(page), "Page must be greater than zero.");
        }

        if (pageSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be greater than zero.");
        }

        if (pageSize > MaxPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), $"Page size cannot be greater than {MaxPageSize}.");
        }

        Status = status;
        Page = page;
        PageSize = pageSize;
    }
}