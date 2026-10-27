using System.Net;
using System.Net.Http.Json;
using HB.RentalApplication.Domain;
using HB.RentalApplication.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace RentalApplication.Tests.Endpoints;

public class ApplicationsEndpointTests : IClassFixture<ApplicationsApiFactory>
{
    private readonly ApplicationsApiFactory _factory;

    public ApplicationsEndpointTests(ApplicationsApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<Guid> SeedAsync(RentalApplicationStatus status)
    {
        var id = Guid.NewGuid();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RentalApplicationDbContext>();
        db.RentalApplications.Add(new HB.RentalApplication.Domain.RentalApplication
        {
            Id = id,
            PropertyId = Guid.NewGuid(),
            ApplicantFullName = "Jane Doe",
            ApplicantEmail = "jane@example.com",
            PropertyAddress = "1 Main St",
            AnnualIncome = 85000m,
            Status = status,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            IsActive = true,
            IsDeleted = false,
            RowVersion = Array.Empty<byte>()
        });
        await db.SaveChangesAsync();
        return id;
    }

    private static object Body(string to, string actor = "owner", string? reason = null)
        => new { to, actor, reason };

    [Fact]
    public async Task Transition_ValidTransition_Returns200WithNewStatus()
    {
        var id = await SeedAsync(RentalApplicationStatus.Submitted);
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/applications/{id}/transitions",
            Body("UnderReview", reason: "Documents received"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<Dictionary<string, System.Text.Json.JsonElement>>();
        Assert.NotNull(json);
        Assert.Equal("UnderReview", json!["status"].GetString());
    }

    [Fact]
    public async Task Transition_InvalidTransition_Returns409()
    {
        var id = await SeedAsync(RentalApplicationStatus.Submitted);
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/applications/{id}/transitions",
            Body("Approved"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Transition_FromFinalState_Returns409()
    {
        var id = await SeedAsync(RentalApplicationStatus.Denied);
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/applications/{id}/transitions",
            Body("UnderReview"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Transition_UnknownApplication_Returns404()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/applications/{Guid.NewGuid()}/transitions",
            Body("UnderReview"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Transition_MissingActor_Returns400()
    {
        var id = await SeedAsync(RentalApplicationStatus.Submitted);
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/applications/{id}/transitions",
            Body("UnderReview", actor: " "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Transition_UnsupportedActor_Returns400()
    {
        var id = await SeedAsync(RentalApplicationStatus.Submitted);
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/applications/{id}/transitions",
            Body("UnderReview", actor: "admin"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
