using HB.RentalApplication.Application.Commands.CreateApplication;
using HB.RentalApplication.Domain;
using HB.RentalApplication.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace RentalApplication.Tests.Commands;

public class CreateApplicationCommandHandlerTests
{
    private static RentalApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<RentalApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new RentalApplicationDbContext(options);
    }

    private static CreateApplicationCommand CreateCommand() => new()
    {
        PropertyId = Guid.NewGuid(),
        RenterEmail = "jane@example.com",
        RenterFullName = "Jane Doe",
        PropertyAddress = "1 Main St",
        AnnualIncome = 85000m
    };

    private static CreateApplicationCommandHandler CreateHandler(RentalApplicationDbContext context)
        => new(new RentalApplicationRepository(context), new RentalApplicationHistoryRepository(context));

    [Fact]
    public async Task Handle_ValidCommand_PersistsApplicationAsSubmitted()
    {
        await using var context = CreateContext();
        var handler = CreateHandler(context);
        var command = CreateCommand();

        var response = await handler.Handle(command, CancellationToken.None);

        var persisted = await context.RentalApplications.AsNoTracking().SingleAsync(x => x.Id == response.ApplicationId);
        Assert.Equal(RentalApplicationStatus.Submitted, persisted.Status);
        Assert.Equal(command.PropertyId, persisted.PropertyId);
        Assert.Equal(command.RenterFullName, persisted.ApplicantFullName);
        Assert.Equal(command.RenterEmail, persisted.ApplicantEmail);
        Assert.Equal(command.PropertyAddress, persisted.PropertyAddress);
        Assert.Equal(command.AnnualIncome, persisted.AnnualIncome);
        Assert.True(persisted.IsActive);
        Assert.False(persisted.IsDeleted);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsResponseMatchingPersistedApplication()
    {
        await using var context = CreateContext();
        var handler = CreateHandler(context);

        var response = await handler.Handle(CreateCommand(), CancellationToken.None);

        var persisted = await context.RentalApplications.AsNoTracking().SingleAsync(x => x.Id == response.ApplicationId);
        Assert.NotEqual(Guid.Empty, response.ApplicationId);
        Assert.Equal(RentalApplicationStatus.Submitted.ToString(), response.Status);
        Assert.Equal(persisted.CreatedAtUtc, response.CreatedAtUtc);
    }

    [Fact]
    public async Task Handle_CalledTwice_CreatesDistinctApplications()
    {
        await using var context = CreateContext();
        var handler = CreateHandler(context);

        var first = await handler.Handle(CreateCommand(), CancellationToken.None);
        var second = await handler.Handle(CreateCommand(), CancellationToken.None);

        Assert.NotEqual(first.ApplicationId, second.ApplicationId);
        Assert.Equal(2, await context.RentalApplications.CountAsync());
    }

    [Fact]
    public async Task Handle_ValidCommand_RecordsInitialHistoryEntry()
    {
        await using var context = CreateContext();
        var handler = CreateHandler(context);

        var response = await handler.Handle(CreateCommand(), CancellationToken.None);

        var entry = await context.RentalApplicationHistories.AsNoTracking().SingleAsync(x => x.ApplicationId == response.ApplicationId);
        Assert.Equal(RentalApplicationStatus.Submitted, entry.FromStatus);
        Assert.Equal(RentalApplicationStatus.Submitted, entry.ToStatus);
        Assert.Equal(response.CreatedAtUtc, entry.TimestampUtc);
        Assert.Equal("renter", entry.Actor);
        Assert.Equal("Application created", entry.Reason);
    }
}
