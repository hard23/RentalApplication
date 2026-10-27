using HB.RentalApplication.Application.Commands.TransitionApplication;
using HB.RentalApplication.Domain;
using HB.RentalApplication.Domain.Repositories;
using HB.RentalApplication.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace RentalApplication.Tests.Commands;

public class TransitionApplicationCommandHandlerTests
{
    // Simulates SQL Server rowversion behavior: every update to an application gets a new RowVersion,
    // so a second writer holding the old value fails with a concurrency conflict.
    private sealed class RowVersioningDbContext : RentalApplicationDbContext
    {
        public RowVersioningDbContext(DbContextOptions<RentalApplicationDbContext> options) : base(options)
        {
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries<HB.RentalApplication.Domain.RentalApplication>())
            {
                if (entry.State == EntityState.Modified)
                {
                    entry.Entity.RowVersion = Guid.NewGuid().ToByteArray();
                }
            }

            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
    }

    private static RentalApplicationDbContext CreateContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<RentalApplicationDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;
        return new RowVersioningDbContext(options);
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
        UpdatedAtUtc = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
        IsActive = true,
        IsDeleted = false,
        RowVersion = Array.Empty<byte>()
    };

    private static TransitionApplicationCommand CreateCommand(Guid applicationId, RentalApplicationStatus to, string actor, string? reason = null)
        => new()
        {
            ApplicationId = applicationId,
            To = to,
            Actor = actor,
            Reason = reason
        };

    private static (TransitionApplicationCommandHandler Handler, IRentalApplicationRepository Repository, IRentalApplicationHistoryRepository HistoryRepository)
        CreateMockedHandler()
    {
        var repository = Substitute.For<IRentalApplicationRepository>();
        var historyRepository = Substitute.For<IRentalApplicationHistoryRepository>();
        var handler = new TransitionApplicationCommandHandler(repository, historyRepository);
        return (handler, repository, historyRepository);
    }

    [Theory]
    [InlineData(RentalApplicationStatus.Submitted, RentalApplicationStatus.UnderReview, "owner")]
    [InlineData(RentalApplicationStatus.UnderReview, RentalApplicationStatus.Approved, "owner")]
    [InlineData(RentalApplicationStatus.UnderReview, RentalApplicationStatus.Denied, "owner")]
    [InlineData(RentalApplicationStatus.Submitted, RentalApplicationStatus.Withdrawn, "renter")]
    [InlineData(RentalApplicationStatus.UnderReview, RentalApplicationStatus.Withdrawn, "Renter")]
    public async Task Handle_ValidTransition_UpdatesStatusAndReturnsResponse(
        RentalApplicationStatus from, RentalApplicationStatus to, string actor)
    {
        var application = CreateApplication(from);
        var (handler, repository, _) = CreateMockedHandler();
        repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        var response = await handler.Handle(CreateCommand(application.Id, to, actor), CancellationToken.None);

        Assert.Equal(application.Id, response.ApplicationId);
        Assert.Equal(to.ToString(), response.Status);
        Assert.Equal(to, application.Status);
        Assert.True(response.TransitionedAtUtc > application.CreatedAtUtc);
        Assert.Equal(application.UpdatedAtUtc, response.TransitionedAtUtc);
        await repository.Received(1).UpdateAsync(application, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidTransition_StagesHistoryEntry()
    {
        var application = CreateApplication(RentalApplicationStatus.Submitted);
        var (handler, repository, historyRepository) = CreateMockedHandler();
        repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        await handler.Handle(
            CreateCommand(application.Id, RentalApplicationStatus.Withdrawn, "renter", "Found another place"),
            CancellationToken.None);

        await historyRepository.Received(1).AddAsync(
            Arg.Is<RentalApplicationHistory>(h =>
                h.ApplicationId == application.Id
                && h.FromStatus == RentalApplicationStatus.Submitted
                && h.ToStatus == RentalApplicationStatus.Withdrawn
                && h.Actor == "renter"
                && h.Reason == "Found another place"
                && h.Id != Guid.Empty),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NonexistentId_ThrowsKeyNotFoundException()
    {
        var missingId = Guid.NewGuid();
        var (handler, repository, historyRepository) = CreateMockedHandler();
        repository.GetByIdAsync(missingId, Arg.Any<CancellationToken>()).Returns((HB.RentalApplication.Domain.RentalApplication?)null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => handler.Handle(CreateCommand(missingId, RentalApplicationStatus.UnderReview, "owner"), CancellationToken.None));

        Assert.Contains(missingId.ToString(), ex.Message);
        await historyRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await repository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Theory]
    [InlineData(RentalApplicationStatus.Submitted, RentalApplicationStatus.Approved, "owner")]
    [InlineData(RentalApplicationStatus.Submitted, RentalApplicationStatus.Denied, "owner")]
    [InlineData(RentalApplicationStatus.UnderReview, RentalApplicationStatus.Submitted, "owner")]
    [InlineData(RentalApplicationStatus.Submitted, RentalApplicationStatus.Withdrawn, "owner")]
    [InlineData(RentalApplicationStatus.UnderReview, RentalApplicationStatus.Withdrawn, "owner")]
    public async Task Handle_InvalidTransition_ThrowsInvalidOperationException_AndDoesNotChangeStatus(
        RentalApplicationStatus from, RentalApplicationStatus to, string actor)
    {
        var application = CreateApplication(from);
        var (handler, repository, historyRepository) = CreateMockedHandler();
        repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(CreateCommand(application.Id, to, actor), CancellationToken.None));

        Assert.Equal(from, application.Status);
        await historyRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await repository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Theory]
    [InlineData(RentalApplicationStatus.Submitted, RentalApplicationStatus.UnderReview, "renter")]
    [InlineData(RentalApplicationStatus.UnderReview, RentalApplicationStatus.Approved, "renter")]
    [InlineData(RentalApplicationStatus.UnderReview, RentalApplicationStatus.Denied, "renter")]
    [InlineData(RentalApplicationStatus.Submitted, RentalApplicationStatus.Withdrawn, "owner")]
    [InlineData(RentalApplicationStatus.UnderReview, RentalApplicationStatus.Withdrawn, "owner")]
    public async Task Handle_TransitionByWrongActor_ThrowsInvalidOperationException_AndDoesNotChangeStatus(
        RentalApplicationStatus from, RentalApplicationStatus to, string actor)
    {
        var application = CreateApplication(from);
        var (handler, repository, historyRepository) = CreateMockedHandler();
        repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(CreateCommand(application.Id, to, actor), CancellationToken.None));

        Assert.Equal(from, application.Status);
        await historyRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await repository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Theory]
    [InlineData(RentalApplicationStatus.Approved)]
    [InlineData(RentalApplicationStatus.Denied)]
    [InlineData(RentalApplicationStatus.Withdrawn)]
    public async Task Handle_FromFinalState_ThrowsInvalidOperationException(RentalApplicationStatus finalStatus)
    {
        var application = CreateApplication(finalStatus);
        var (handler, repository, _) = CreateMockedHandler();
        repository.GetByIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(CreateCommand(application.Id, RentalApplicationStatus.UnderReview, "owner"), CancellationToken.None));

        Assert.Contains("final state", ex.Message);
        Assert.Equal(finalStatus, application.Status);
    }

    // Concurrency needs real EF RowVersion behavior, so this test keeps the in-memory DbContext
    // and the concrete repositories (which implement the interfaces).
    [Fact]
    public async Task Handle_ConcurrentTransitionsOnSameApplication_OnlyOneSucceeds()
    {
        var databaseName = Guid.NewGuid().ToString();
        var application = CreateApplication(RentalApplicationStatus.Submitted);
        await using (var seed = CreateContext(databaseName))
        {
            seed.RentalApplications.Add(application);
            await seed.SaveChangesAsync();
        }

        await using var contextA = CreateContext(databaseName);
        await using var contextB = CreateContext(databaseName);
        var command = CreateCommand(application.Id, RentalApplicationStatus.UnderReview, "owner");

        var outcomes = await Task.WhenAll(
            TryHandleAsync(CreateEfHandler(contextA), command),
            TryHandleAsync(CreateEfHandler(contextB), command));

        Assert.Equal(1, outcomes.Count(succeeded => succeeded));

        await using var verify = CreateContext(databaseName);
        var persisted = await verify.RentalApplications.AsNoTracking().SingleAsync(x => x.Id == application.Id);
        Assert.Equal(RentalApplicationStatus.UnderReview, persisted.Status);

        // Only the winning request's history entry should be persisted.
        var history = await verify.RentalApplicationHistories.AsNoTracking().ToListAsync();
        Assert.Single(history);
        Assert.Equal(RentalApplicationStatus.UnderReview, history[0].ToStatus);
    }

    private static TransitionApplicationCommandHandler CreateEfHandler(RentalApplicationDbContext context)
        => new(new RentalApplicationRepository(context), new RentalApplicationHistoryRepository(context));

    // Returns true if the handler succeeded, false if it was rejected with InvalidOperationException
    // (concurrency conflict or business-rule rejection). Any other exception fails the test.
    private static async Task<bool> TryHandleAsync(TransitionApplicationCommandHandler handler, TransitionApplicationCommand command)
    {
        try
        {
            await handler.Handle(command, CancellationToken.None);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
