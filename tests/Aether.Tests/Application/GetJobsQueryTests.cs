using Aether.Application.Jobs;
using Aether.Domain.Jobs;

namespace Aether.Tests.Application;

public class GetJobsQueryTests
{
    [Fact]
    public void Constructor_ShouldUseDefaultValues()
    {
        var query = new GetJobsQuery();

        Assert.Null(query.Status);
        Assert.Equal(1, query.Page);
        Assert.Equal(20, query.PageSize);
    }

    [Fact]
    public void Constructor_ShouldSetProvidedValues()
    {
        var query = new GetJobsQuery(
            status: JobStatus.Failed,
            page: 2,
            pageSize: 50);

        Assert.Equal(JobStatus.Failed, query.Status);
        Assert.Equal(2, query.Page);
        Assert.Equal(50, query.PageSize);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenPageIsZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GetJobsQuery(page: 0));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenPageSizeIsZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GetJobsQuery(pageSize: 0));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenPageSizeIsTooLarge()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GetJobsQuery(pageSize: 101));
    }
}