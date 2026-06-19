using System.Net;
using System.Net.Http.Json;
using Aether.Api.Jobs;
using Aether.Application.Jobs;
using Aether.Domain.Jobs;
using Aether.Infrastructure.Queues;
using Aether.Infrastructure.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Aether.Tests.Api;

public class JobsApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public JobsApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostJobs_ShouldCreateJob()
    {
        var client = CreateClientWithoutHostedService();

        var request = new SubmitJobRequest(
            JobType: "PrintMessage",
            Payload: "{\"message\":\"hello from test\"}");

        var response = await client.PostAsJsonAsync("/api/jobs", request);
        var body = await response.Content.ReadFromJsonAsync<SubmitJobResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.Id);
        Assert.Equal("PrintMessage", body.JobType);
        Assert.Equal(JobStatus.Pending, body.Status);
        Assert.Equal(0, body.RetryCount);
        Assert.Equal(3, body.MaxRetries);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task PostJobs_ShouldReturnBadRequest_WhenJobTypeIsEmpty()
    {
        var client = CreateClientWithoutHostedService();

        var request = new SubmitJobRequest(
            JobType: "",
            Payload: "{\"message\":\"hello\"}");

        var response = await client.PostAsJsonAsync("/api/jobs", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostJobs_ShouldReturnBadRequest_WhenPayloadIsEmpty()
    {
        var client = CreateClientWithoutHostedService();

        var request = new SubmitJobRequest(
            JobType: "PrintMessage",
            Payload: "");

        var response = await client.PostAsJsonAsync("/api/jobs", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostJobs_ShouldReturnBadRequest_WhenMaxRetriesIsNegative()
    {
        var client = CreateClientWithoutHostedService();

        var request = new SubmitJobRequest(
            JobType: "PrintMessage",
            Payload: "{\"message\":\"hello\"}",
            MaxRetries: -1);

        var response = await client.PostAsJsonAsync("/api/jobs", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostJobs_ShouldReturnValidationProblem_WhenRequestIsInvalid()
    {
        var client = CreateClientWithoutHostedService();

        var request = new SubmitJobRequest(
            JobType: "",
            Payload: "",
            MaxRetries: -1);

        var response = await client.PostAsJsonAsync("/api/jobs", request);
        var body = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(body);
        Assert.Contains(nameof(SubmitJobRequest.JobType), body.Errors.Keys);
        Assert.Contains(nameof(SubmitJobRequest.Payload), body.Errors.Keys);
        Assert.Contains(nameof(SubmitJobRequest.MaxRetries), body.Errors.Keys);
    }

    [Fact]
    public async Task GetJobs_ShouldReturnCreatedJobs()
    {
        var client = CreateClientWithoutHostedService();

        var firstRequest = new SubmitJobRequest(
            JobType: "PrintMessage",
            Payload: "{\"message\":\"first job\"}");

        var secondRequest = new SubmitJobRequest(
            JobType: "PrintMessage",
            Payload: "{\"message\":\"second job\"}");

        var firstCreateResponse = await client.PostAsJsonAsync("/api/jobs", firstRequest);
        var secondCreateResponse = await client.PostAsJsonAsync("/api/jobs", secondRequest);

        var firstCreatedJob = await firstCreateResponse.Content.ReadFromJsonAsync<SubmitJobResponse>();
        var secondCreatedJob = await secondCreateResponse.Content.ReadFromJsonAsync<SubmitJobResponse>();

        Assert.NotNull(firstCreatedJob);
        Assert.NotNull(secondCreatedJob);

        var response = await client.GetAsync("/api/jobs");
        var body = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<JobResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Contains(body, job => job.Id == firstCreatedJob.Id);
        Assert.Contains(body, job => job.Id == secondCreatedJob.Id);
    }

    [Fact]
    public async Task GetJobs_ShouldFilterByStatus()
    {
        var client = CreateClientWithoutHostedService();

        var response = await client.GetAsync("/api/jobs?status=Failed");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetJobs_ShouldApplyPagination()
    {
        var client = CreateClientWithoutHostedService();

        var firstRequest = new SubmitJobRequest(
            JobType: "PrintMessage",
            Payload: "{\"message\":\"first paged job\"}");

        var secondRequest = new SubmitJobRequest(
            JobType: "PrintMessage",
            Payload: "{\"message\":\"second paged job\"}");

        var firstCreateResponse = await client.PostAsJsonAsync("/api/jobs", firstRequest);
        var secondCreateResponse = await client.PostAsJsonAsync("/api/jobs", secondRequest);

        var firstCreatedJob = await firstCreateResponse.Content.ReadFromJsonAsync<SubmitJobResponse>();
        var secondCreatedJob = await secondCreateResponse.Content.ReadFromJsonAsync<SubmitJobResponse>();

        Assert.NotNull(firstCreatedJob);
        Assert.NotNull(secondCreatedJob);

        var response = await client.GetAsync("/api/jobs?page=2&pageSize=1");
        var body = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<JobResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Single(body);
        Assert.Contains(body, job => job.Id == secondCreatedJob.Id);
        Assert.DoesNotContain(body, job => job.Id == firstCreatedJob.Id);
    }

    [Fact]
    public async Task GetJobs_ShouldReturnBadRequest_WhenPageIsZero()
    {
        var client = CreateClientWithoutHostedService();

        var response = await client.GetAsync("/api/jobs?page=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetJobs_ShouldReturnBadRequest_WhenPageSizeIsZero()
    {
        var client = CreateClientWithoutHostedService();

        var response = await client.GetAsync("/api/jobs?pageSize=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetJobs_ShouldReturnBadRequest_WhenPageSizeIsTooLarge()
    {
        var client = CreateClientWithoutHostedService();

        var response = await client.GetAsync("/api/jobs?pageSize=101");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetJobById_ShouldReturnJob_WhenJobExists()
    {
        var client = CreateClientWithoutHostedService();

        var request = new SubmitJobRequest(
            JobType: "PrintMessage",
            Payload: "{\"message\":\"hello from test\"}");

        var createResponse = await client.PostAsJsonAsync("/api/jobs", request);
        var createdJob = await createResponse.Content.ReadFromJsonAsync<SubmitJobResponse>();

        Assert.NotNull(createdJob);

        var getResponse = await client.GetAsync($"/api/jobs/{createdJob.Id}");
        var body = await getResponse.Content.ReadFromJsonAsync<JobResponse>();

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(createdJob.Id, body.Id);
        Assert.Equal("PrintMessage", body.JobType);
    }

    [Fact]
    public async Task GetJobById_ShouldReturnNotFound_WhenJobDoesNotExist()
    {
        var client = CreateClientWithoutHostedService();

        var response = await client.GetAsync($"/api/jobs/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetFailedJobs_ShouldReturnOk()
    {
        var client = CreateClientWithoutHostedService();

        var response = await client.GetAsync("/api/jobs/failed");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CancelJob_ShouldCancelPendingJob_WhenJobExists()
    {
        var client = CreateClientWithoutHostedService();

        var request = new SubmitJobRequest(
            JobType: "PrintMessage",
            Payload: "{\"message\":\"job to cancel\"}");

        var createResponse = await client.PostAsJsonAsync("/api/jobs", request);
        var createdJob = await createResponse.Content.ReadFromJsonAsync<SubmitJobResponse>();

        Assert.NotNull(createdJob);

        var cancelResponse = await client.PostAsync($"/api/jobs/{createdJob.Id}/cancel", content: null);
        var body = await cancelResponse.Content.ReadFromJsonAsync<JobResponse>();

        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(createdJob.Id, body.Id);
        Assert.Equal(JobStatus.Cancelled, body.Status);
        Assert.NotNull(body.CompletedAtUtc);
    }

    [Fact]
    public async Task CancelJob_ShouldReturnNotFound_WhenJobDoesNotExist()
    {
        var client = CreateClientWithoutHostedService();

        var response = await client.PostAsync($"/api/jobs/{Guid.NewGuid()}/cancel", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CancelJob_ShouldReturnConflict_WhenJobCannotBeCancelled()
    {
        var client = CreateClientWithHostedService();

        var request = new SubmitJobRequest(
            JobType: "PrintMessage",
            Payload: "{\"message\":\"job that will complete\"}");

        var createResponse = await client.PostAsJsonAsync("/api/jobs", request);
        var createdJob = await createResponse.Content.ReadFromJsonAsync<SubmitJobResponse>();

        Assert.NotNull(createdJob);

        await WaitUntilJobStatusAsync(
            client,
            createdJob.Id,
            JobStatus.Completed);

        var cancelResponse = await client.PostAsync($"/api/jobs/{createdJob.Id}/cancel", content: null);

        Assert.Equal(HttpStatusCode.Conflict, cancelResponse.StatusCode);
    }

    private HttpClient CreateClientWithoutHostedService()
    {
        return CreateClient(removeHostedService: true);
    }

    private HttpClient CreateClientWithHostedService()
    {
        return CreateClient(removeHostedService: false);
    }

    private HttpClient CreateClient(bool removeHostedService)
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IJobRepository>();
                services.RemoveAll<IJobQueue>();

                services.AddSingleton<IJobRepository, InMemoryJobRepository>();
                services.AddSingleton<IJobQueue, InMemoryJobQueue>();

                if (removeHostedService)
                {
                    services.RemoveAll<IHostedService>();
                }
            });
        });

        return factory.CreateClient();
    }

    private static async Task WaitUntilJobStatusAsync(
        HttpClient client,
        Guid jobId,
        JobStatus expectedStatus)
    {
        var timeoutAtUtc = DateTime.UtcNow.AddSeconds(3);

        while (DateTime.UtcNow < timeoutAtUtc)
        {
            var response = await client.GetAsync($"/api/jobs/{jobId}");

            if (response.StatusCode == HttpStatusCode.OK)
            {
                var job = await response.Content.ReadFromJsonAsync<JobResponse>();

                if (job?.Status == expectedStatus)
                {
                    return;
                }
            }

            await Task.Delay(20);
        }

        throw new TimeoutException($"Job '{jobId}' did not reach status '{expectedStatus}'.");
    }
}
