using HB.RentalApplication.Application.Queries.GetApplicationById;
using HB.RentalApplication.Domain;
using HB.RentalApplication.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace RentalApplication.Tests.Queries;

public class GetApplicationByIdQueryHandlerTests
{
    private static RentalApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<RentalApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new RentalApplicationDbContext(options);
    }

    private static HB.RentalApplication.Domain.RentalApplication CreateApplication(
        RentalApplicationStatus status = RentalApplicationStatus.Submitted) => new()
    {
        Id = Guid.NewGuid(),
        PropertyId = Guid.NewGuid(),
        ApplicantFullName = "Jane Doe",
        ApplicantEmail = "jane@example.com",
        PropertyAddress = "1 Main St",
        AnnualIncome = 85000m,
        Status = status,
        CreatedAtUtc = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
        UpdatedAtUtc = new DateTime(2024, 1, 16, 10, 30, 0, DateTimeKind.Utc),
        IsActive = true,
        IsDeleted = false,
        RowVersion = Array.Empty<byte>()
    };

    private static GetApplicationByIdQueryHandler CreateHandler(RentalApplicationDbContext context)
        => new(new RentalApplicationRepository(context));

    [Fact]
    public async Task Handle_ExistingId_ReturnsMatchingApplicationDetails()
    {
        await using var context = CreateContext();
        var application = CreateApplication();
        context.RentalApplications.Add(application);
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        var response = await handler.Handle(new GetApplicationByIdQuery(application.Id), CancellationToken.None);

        Assert.Equal(application.Id, response.Id);
        Assert.Equal(application.PropertyId, response.PropertyId);
        Assert.Equal(application.ApplicantEmail, response.RenterEmail);
        Assert.Equal(application.CreatedAtUtc, response.SubmittedAtUtc);
        Assert.Equal(RentalApplicationStatus.Submitted.ToString(), response.Status);
    }

    [Fact]
    public async Task Handle_DefaultFieldsNotInDomain_AreZero()
    {
        await using var context = CreateContext();
        var application = CreateApplication();
        context.RentalApplications.Add(application);
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        var response = await handler.Handle(new GetApplicationByIdQuery(application.Id), CancellationToken.None);

        Assert.Equal(0, response.HouseholdSize);
        Assert.Equal(0m, response.AnnualIncome);
    }

    [Theory]
    [InlineData(RentalApplicationStatus.Submitted)]
    [InlineData(RentalApplicationStatus.UnderReview)]
    [InlineData(RentalApplicationStatus.Approved)]
    [InlineData(RentalApplicationStatus.Denied)]
    [InlineData(RentalApplicationStatus.Withdrawn)]
    public async Task Handle_ReturnsPersistedStatus(RentalApplicationStatus status)
    {
        await using var context = CreateContext();
        var application = CreateApplication(status);
        context.RentalApplications.Add(application);
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        var response = await handler.Handle(new GetApplicationByIdQuery(application.Id), CancellationToken.None);

        Assert.Equal(status.ToString(), response.Status);
    }

    [Fact]
    public async Task Handle_NonexistentId_ThrowsKeyNotFoundException()
    {
        await using var context = CreateContext();
        var handler = CreateHandler(context);
        var missingId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => handler.Handle(new GetApplicationByIdQuery(missingId), CancellationToken.None));

        Assert.Contains(missingId.ToString(), ex.Message);
    }

    [Fact]
    public async Task Handle_DoesNotModifyPersistedApplication()
    {
        await using var context = CreateContext();
        var application = CreateApplication(RentalApplicationStatus.UnderReview);
        context.RentalApplications.Add(application);
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        await handler.Handle(new GetApplicationByIdQuery(application.Id), CancellationToken.None);

        // Query must not leave tracked changes behind.
        Assert.False(context.ChangeTracker.HasChanges());
        var persisted = await context.RentalApplications.AsNoTracking().SingleAsync(x => x.Id == application.Id);
        Assert.Equal(RentalApplicationStatus.UnderReview, persisted.Status);
        Assert.Equal(application.UpdatedAtUtc, persisted.UpdatedAtUtc);
        Assert.Equal(application.AnnualIncome, persisted.AnnualIncome);
    }
}
