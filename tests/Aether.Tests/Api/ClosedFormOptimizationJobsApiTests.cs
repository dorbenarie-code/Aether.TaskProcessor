using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aether.Application.Jobs;
using Aether.Domain.Jobs;
using Aether.Infrastructure.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Aether.Tests.Api;

public sealed class ClosedFormOptimizationJobsApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ClosedFormOptimizationJobsApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostClosedFormOptimizationJob_ShouldSubmitPendingOptimizationJob()
    {
        var client = CreateClientWithoutHostedService();

        var request = CreateMinimalClosedFormOptimizationRequest();

        var response = await client.PostAsJsonAsync(
            "/api/scheduling/closed-form-optimization-jobs",
            request);

        var responseText = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        using var document = JsonDocument.Parse(responseText);
        var root = document.RootElement;

        var jobId = root.GetProperty("id").GetGuid();

        Assert.NotEqual(Guid.Empty, jobId);
        Assert.Equal("ClosedFormOptimization", root.GetProperty("jobType").GetString());
        Assert.Equal(nameof(JobStatus.Pending), root.GetProperty("status").GetString());
        Assert.Equal(0, root.GetProperty("retryCount").GetInt32());
        Assert.Equal(0, root.GetProperty("maxRetries").GetInt32());

        var getResponse = await client.GetAsync($"/api/jobs/{jobId}");
        var getResponseText = await getResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        using var getDocument = JsonDocument.Parse(getResponseText);
        var jobRoot = getDocument.RootElement;

        Assert.Equal(jobId, jobRoot.GetProperty("id").GetGuid());
        Assert.Equal("ClosedFormOptimization", jobRoot.GetProperty("jobType").GetString());
        Assert.Equal((int)JobStatus.Pending, jobRoot.GetProperty("status").GetInt32());
    }

    private static object CreateMinimalClosedFormOptimizationRequest()
    {
        var resourceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var shiftId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        return new
        {
            PeriodStartUtc = new DateTime(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc),
            PeriodEndUtc = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc),
            Resources = new[]
            {
                new
                {
                    Id = resourceId,
                    Name = "Guard01",
                    HourlyCost = 100m,
                    WorkloadCategory = "Full"
                }
            },
            Shifts = new[]
            {
                new
                {
                    Id = shiftId,
                    StartUtc = new DateTime(2026, 6, 14, 6, 30, 0, DateTimeKind.Utc),
                    EndUtc = new DateTime(2026, 6, 14, 14, 30, 0, DateTimeKind.Utc),
                    Kind = "Morning",
                    MinResourceCount = 1,
                    MaxResourceCount = 1,
                    RequiresPreferenceToAssign = false,
                    RequiresMinimumWhenPreferenceExists = false,
                    NightShiftCategory = (string?)null
                }
            },
            WorkerSubmissions = new[]
            {
                new
                {
                    ResourceId = resourceId,
                    ShiftSubmissions = new[]
                    {
                        new
                        {
                            Date = new DateOnly(2026, 6, 14),
                            ShiftKind = "Morning",
                            Choice = "StrongAvailable"
                        }
                    }
                }
            },
            TotalEffectiveTargetHours = 8.0,
            MaximumAssignedHoursDeviationFromAverageHours = (double?)null,
            Seed = 20260603,
            ResourceMonthlyNightShiftHistories = Array.Empty<object>()
        };
    }

    private HttpClient CreateClientWithoutHostedService()
    {
        return _factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IHostedService>();
                    services.RemoveAll<IJobRepository>();
                    services.AddSingleton<IJobRepository, InMemoryJobRepository>();
                });
            })
            .CreateClient();
    }
}
