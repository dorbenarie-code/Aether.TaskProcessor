using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aether.Application.Jobs;
using Aether.Infrastructure.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Aether.Tests.Api;

public sealed class ClosedFormOptimizationsApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ClosedFormOptimizationsApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostClosedFormOptimization_ShouldReturnOptimizationResult()
    {
        var client = CreateClientWithoutHostedService();

        var resourceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var shiftId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        var periodStartUtc = new DateTime(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc);
        var periodEndUtc = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        var shiftStartUtc = new DateTime(2026, 6, 14, 6, 30, 0, DateTimeKind.Utc);
        var shiftEndUtc = new DateTime(2026, 6, 14, 14, 30, 0, DateTimeKind.Utc);

        var request = new
        {
            PeriodStartUtc = periodStartUtc,
            PeriodEndUtc = periodEndUtc,
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
                    StartUtc = shiftStartUtc,
                    EndUtc = shiftEndUtc,
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

        var response = await client.PostAsJsonAsync(
            "/api/scheduling/closed-form-optimizations",
            request);

        var responseText = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(responseText);
        var root = document.RootElement;

        Assert.True(root.GetProperty("isFeasible").GetBoolean());
        Assert.Equal(0, root.GetProperty("hardViolationCount").GetInt32());
        Assert.Equal(1, root.GetProperty("assignmentCount").GetInt32());
        Assert.True(root.GetProperty("generationDiagnosticCount").GetInt32() > 0);

        var assignments = root.GetProperty("assignments").EnumerateArray().ToArray();

        var assignment = Assert.Single(assignments);

        Assert.Equal(
            resourceId,
            assignment.GetProperty("resourceId").GetGuid());

        Assert.Equal(
            shiftId,
            assignment.GetProperty("shiftId").GetGuid());

        Assert.False(root.TryGetProperty("problem", out _));
        Assert.False(root.TryGetProperty("geneticResult", out _));
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
