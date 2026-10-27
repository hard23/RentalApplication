using HB.RentalApplication.Application.Queries.GetApplicationHistory;
using HB.RentalApplication.Domain;
using HB.RentalApplication.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace RentalApplication.Tests.Queries;

public class GetApplicationHistoryQueryHandlerTests
{
    private static RentalApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<RentalApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new RentalApplicationDbContext(options);
    }

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

    private static GetApplicationHistoryQueryHandler CreateHandler(RentalApplicationDbContext context)
        => new(new RentalApplicationRepository(context), new RentalApplicationHistoryRepository(context));

    [Fact]
    public async Task Handle_ExistingApplication_ReturnsEntriesOrderedByTimestamp()
    {
        await using var context = CreateContext();
        var application = CreateApplication();
        context.RentalApplications.Add(application);
        context.RentalApplicationHistories.AddRange(
            CreateHistory(application.Id, RentalApplicationStatus.UnderReview, RentalApplicationStatus.Approved,
                new DateTime(2024, 1, 3, 0, 0, 0, DateTimeKind.Utc)),
            CreateHistory(application.Id, RentalApplicationStatus.Submitted, RentalApplicationStatus.UnderReview,
                new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc)));
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

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
        await using var context = CreateContext();
        var application = CreateApplication();
        var timestamp = new DateTime(2024, 1, 2, 9, 0, 0, DateTimeKind.Utc);
        context.RentalApplications.Add(application);
        context.RentalApplicationHistories.Add(CreateHistory(application.Id,
            RentalApplicationStatus.Submitted, RentalApplicationStatus.Withdrawn, timestamp, "Moved away"));
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

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
        await using var context = CreateContext();
        var application = CreateApplication();
        context.RentalApplications.Add(application);
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        var response = await handler.Handle(new GetApplicationHistoryQuery(application.Id), CancellationToken.None);

        Assert.Equal(application.Id, response.ApplicationId);
        Assert.Empty(response.Entries);
    }

    [Fact]
    public async Task Handle_OnlyReturnsHistoryForRequestedApplication()
    {
        await using var context = CreateContext();
        var application = CreateApplication();
        var other = CreateApplication();
        context.RentalApplications.AddRange(application, other);
        context.RentalApplicationHistories.Add(CreateHistory(other.Id,
            RentalApplicationStatus.Submitted, RentalApplicationStatus.UnderReview,
            new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc)));
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        var response = await handler.Handle(new GetApplicationHistoryQuery(application.Id), CancellationToken.None);

        Assert.Empty(response.Entries);
    }

    [Fact]
    public async Task Handle_NonexistentApplication_ThrowsKeyNotFoundException()
    {
        await using var context = CreateContext();
        var handler = CreateHandler(context);
        var missingId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => handler.Handle(new GetApplicationHistoryQuery(missingId), CancellationToken.None));

        Assert.Contains(missingId.ToString(), ex.Message);
    }
}
