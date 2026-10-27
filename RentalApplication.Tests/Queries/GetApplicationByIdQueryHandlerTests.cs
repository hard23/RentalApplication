using HB.RentalApplication.Application.Queries.GetApplicationById;
using HB.RentalApplication.Domain;
using HB.RentalApplication.Domain.Repositories;
using NSubstitute;

namespace RentalApplication.Tests.Queries;

public class GetApplicationByIdQueryHandlerTests
{
    private static HB.RentalApplication.Domain.RentalApplication CreateApplication(
        RentalApplicationStatus status = RentalApplicationStatus.Submitted) => new()
    {
        Id = Guid.NewGuid(),
        PropertyId = Guid.NewGuid(),
        ApplicantFullName = "Jane Doe",
        ApplicantEmail = "jane@example.com",
        PropertyAddress = "1 Main St",
        AnnualIncome = 85000m,
        HouseholdSize = 3,
        Status = status,
        CreatedAtUtc = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
        UpdatedAtUtc = new DateTime(2024, 1, 16, 10, 30, 0, DateTimeKind.Utc),
        IsActive = true,
        IsDeleted = false,
        RowVersion = Array.Empty<byte>()
    };

    private static (GetApplicationByIdQueryHandler Handler, IRentalApplicationRepository Repository) CreateMockedHandler()
    {
        var repository = Substitute.For<IRentalApplicationRepository>();
        return (new GetApplicationByIdQueryHandler(repository), repository);
    }

    [Fact]
    public async Task Handle_ExistingId_ReturnsMatchingApplicationDetails()
    {
        var application = CreateApplication();
        var (handler, repository) = CreateMockedHandler();
        repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        var response = await handler.Handle(new GetApplicationByIdQuery(application.Id), CancellationToken.None);

        Assert.Equal(application.Id, response.Id);
        Assert.Equal(application.PropertyId, response.PropertyId);
        Assert.Equal(application.ApplicantEmail, response.RenterEmail);
        Assert.Equal(application.CreatedAtUtc, response.SubmittedAtUtc);
        Assert.Equal(RentalApplicationStatus.Submitted.ToString(), response.Status);
    }

    [Fact]
    public async Task Handle_ExistingId_MapsHouseholdSizeAndAnnualIncome()
    {
        var application = CreateApplication();
        var (handler, repository) = CreateMockedHandler();
        repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        var response = await handler.Handle(new GetApplicationByIdQuery(application.Id), CancellationToken.None);

        Assert.Equal(3, response.HouseholdSize);
        Assert.Equal(85000m, response.AnnualIncome);
    }

    [Theory]
    [InlineData(RentalApplicationStatus.Submitted)]
    [InlineData(RentalApplicationStatus.UnderReview)]
    [InlineData(RentalApplicationStatus.Approved)]
    [InlineData(RentalApplicationStatus.Denied)]
    [InlineData(RentalApplicationStatus.Withdrawn)]
    public async Task Handle_ReturnsPersistedStatus(RentalApplicationStatus status)
    {
        var application = CreateApplication(status);
        var (handler, repository) = CreateMockedHandler();
        repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        var response = await handler.Handle(new GetApplicationByIdQuery(application.Id), CancellationToken.None);

        Assert.Equal(status.ToString(), response.Status);
    }

    [Fact]
    public async Task Handle_NonexistentId_ThrowsKeyNotFoundException()
    {
        var missingId = Guid.NewGuid();
        var (handler, repository) = CreateMockedHandler();
        repository.GetByIdAsync(missingId, Arg.Any<CancellationToken>()).Returns((HB.RentalApplication.Domain.RentalApplication?)null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => handler.Handle(new GetApplicationByIdQuery(missingId), CancellationToken.None));

        Assert.Contains(missingId.ToString(), ex.Message);
    }

    [Fact]
    public async Task Handle_DoesNotPersistChanges()
    {
        var application = CreateApplication(RentalApplicationStatus.UnderReview);
        var (handler, repository) = CreateMockedHandler();
        repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        await handler.Handle(new GetApplicationByIdQuery(application.Id), CancellationToken.None);

        await repository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
        await repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        Assert.Equal(RentalApplicationStatus.UnderReview, application.Status);
    }
}
