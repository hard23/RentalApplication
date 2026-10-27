using HB.RentalApplication.Application.Queries.GetApplicationHistory;
using HB.RentalApplication.Domain;
using HB.RentalApplication.Domain.Repositories;
using NSubstitute;

namespace RentalApplication.Tests.Queries;

public class GetApplicationHistoryQueryHandlerTests
{
    private static HB.RentalApplication.Domain.RentalApplication CreateApplication() => new()
    {
        Id = Guid.NewGuid(),
        PropertyId = Guid.NewGuid(),
        ApplicantFullName = "Jane Doe",
        ApplicantEmail = "jane@example.com",
        PropertyAddress = "1 Main St",
        AnnualIncome = 85000m,
        Status = RentalApplicationStatus.Submitted,
        CreatedAtUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        UpdatedAtUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        IsActive = true,
        IsDeleted = false,
        RowVersion = Array.Empty<byte>()
    };

    private static RentalApplicationHistory CreateHistory(Guid applicationId, RentalApplicationStatus from,
        RentalApplicationStatus to, DateTime timestampUtc, string? reason = null) => new()
    {
        Id = Guid.NewGuid(),
        ApplicationId = applicationId,
        FromStatus = from,
        ToStatus = to,
        TimestampUtc = timestampUtc,
        Actor = "owner",
        Reason = reason
    };

    private static (GetApplicationHistoryQueryHandler Handler, IRentalApplicationRepository Repository, IRentalApplicationHistoryRepository HistoryRepository)
        CreateMockedHandler()
    {
        var repository = Substitute.For<IRentalApplicationRepository>();
        var historyRepository = Substitute.For<IRentalApplicationHistoryRepository>();
        var handler = new GetApplicationHistoryQueryHandler(repository, historyRepository);
        return (handler, repository, historyRepository);
    }

    [Fact]
    public async Task Handle_ExistingApplication_ReturnsEntriesOrderedByTimestamp()
    {
        var application = CreateApplication();
        var (handler, repository, historyRepository) = CreateMockedHandler();
        repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);
        historyRepository.GetByApplicationIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(new List<RentalApplicationHistory>
        {
            CreateHistory(application.Id, RentalApplicationStatus.Submitted, RentalApplicationStatus.UnderReview,
                new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc)),
            CreateHistory(application.Id, RentalApplicationStatus.UnderReview, RentalApplicationStatus.Approved,
                new DateTime(2024, 1, 3, 0, 0, 0, DateTimeKind.Utc))
        });

        var response = await handler.Handle(new GetApplicationHistoryQuery(application.Id), CancellationToken.None);

        Assert.Equal(application.Id, response.ApplicationId);
        Assert.Equal(2, response.Entries.Count);
        Assert.Equal(RentalApplicationStatus.UnderReview.ToString(), response.Entries[0].ToStatus);
        Assert.Equal(RentalApplicationStatus.Approved.ToString(), response.Entries[1].ToStatus);
        Assert.True(response.Entries[0].TimestampUtc < response.Entries[1].TimestampUtc);
    }

    [Fact]
    public async Task Handle_ReturnsMappedEntryFields()
    {
        var application = CreateApplication();
        var timestamp = new DateTime(2024, 1, 2, 9, 0, 0, DateTimeKind.Utc);
        var (handler, repository, historyRepository) = CreateMockedHandler();
        repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);
        historyRepository.GetByApplicationIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(new List<RentalApplicationHistory>
        {
            CreateHistory(application.Id, RentalApplicationStatus.Submitted, RentalApplicationStatus.Withdrawn, timestamp, "Moved away")
        });

        var response = await handler.Handle(new GetApplicationHistoryQuery(application.Id), CancellationToken.None);

        var entry = Assert.Single(response.Entries);
        Assert.Equal(RentalApplicationStatus.Submitted.ToString(), entry.FromStatus);
        Assert.Equal(RentalApplicationStatus.Withdrawn.ToString(), entry.ToStatus);
        Assert.Equal(timestamp, entry.TimestampUtc);
        Assert.Equal("owner", entry.Actor);
        Assert.Equal("Moved away", entry.Reason);
    }

    [Fact]
    public async Task Handle_ExistingApplicationWithoutHistory_ReturnsEmptyEntries()
    {
        var application = CreateApplication();
        var (handler, repository, historyRepository) = CreateMockedHandler();
        repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);
        historyRepository.GetByApplicationIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(new List<RentalApplicationHistory>());

        var response = await handler.Handle(new GetApplicationHistoryQuery(application.Id), CancellationToken.None);

        Assert.Equal(application.Id, response.ApplicationId);
        Assert.Empty(response.Entries);
    }

    [Fact]
    public async Task Handle_OnlyRequestsHistoryForRequestedApplication()
    {
        var application = CreateApplication();
        var (handler, repository, historyRepository) = CreateMockedHandler();
        repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);
        historyRepository.GetByApplicationIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(new List<RentalApplicationHistory>());

        await handler.Handle(new GetApplicationHistoryQuery(application.Id), CancellationToken.None);

        await historyRepository.Received(1).GetByApplicationIdAsync(application.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NonexistentApplication_ThrowsKeyNotFoundException()
    {
        var missingId = Guid.NewGuid();
        var (handler, repository, historyRepository) = CreateMockedHandler();
        repository.GetByIdAsync(missingId, Arg.Any<CancellationToken>()).Returns((HB.RentalApplication.Domain.RentalApplication?)null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => handler.Handle(new GetApplicationHistoryQuery(missingId), CancellationToken.None));

        Assert.Contains(missingId.ToString(), ex.Message);
        await historyRepository.DidNotReceiveWithAnyArgs().GetByApplicationIdAsync(default, default);
    }
}
