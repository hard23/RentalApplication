using HB.RentalApplication.Application.Commands.TransitionApplication;
using HB.RentalApplication.Domain;
using HB.RentalApplication.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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

    private static TransitionApplicationCommandHandler CreateHandler(RentalApplicationDbContext context)
        => new(new RentalApplicationRepository(context), new RentalApplicationHistoryRepository(context));

    private static TransitionApplicationCommand CreateCommand(Guid applicationId, RentalApplicationStatus to, string actor, string? reason = null)
        => new()
        {
            ApplicationId = applicationId,
            To = to,
            Actor = actor,
            Reason = reason
        };

    [Theory]
    [InlineData(RentalApplicationStatus.Submitted, RentalApplicationStatus.UnderReview, "owner")]
    [InlineData(RentalApplicationStatus.UnderReview, RentalApplicationStatus.Approved, "owner")]
    [InlineData(RentalApplicationStatus.UnderReview, RentalApplicationStatus.Denied, "owner")]
    [InlineData(RentalApplicationStatus.Submitted, RentalApplicationStatus.Withdrawn, "renter")]
    [InlineData(RentalApplicationStatus.UnderReview, RentalApplicationStatus.Withdrawn, "Renter")]
    public async Task Handle_ValidTransition_UpdatesStatusAndReturnsResponse(
        RentalApplicationStatus from, RentalApplicationStatus to, string actor)
    {
        await using var context = CreateContext();
        var application = CreateApplication(from);
        context.RentalApplications.Add(application);
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        var response = await handler.Handle(CreateCommand(application.Id, to, actor), CancellationToken.None);

        Assert.Equal(application.Id, response.ApplicationId);
        Assert.Equal(to.ToString(), response.Status);
        Assert.True(response.TransitionedAtUtc > application.CreatedAtUtc);

        var persisted = await context.RentalApplications.AsNoTracking().SingleAsync(x => x.Id == application.Id);
        Assert.Equal(to, persisted.Status);
        Assert.Equal(persisted.UpdatedAtUtc, response.TransitionedAtUtc);
    }

    [Fact]
    public async Task Handle_ValidTransition_PersistsHistoryEntry()
    {
        await using var context = CreateContext();
        var application = CreateApplication(RentalApplicationStatus.Submitted);
        context.RentalApplications.Add(application);
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        await handler.Handle(
            CreateCommand(application.Id, RentalApplicationStatus.Withdrawn, "renter", "Found another place"),
            CancellationToken.None);

        var history = await context.RentalApplicationHistories.AsNoTracking().SingleAsync();
        Assert.Equal(application.Id, history.ApplicationId);
        Assert.Equal(RentalApplicationStatus.Submitted, history.FromStatus);
        Assert.Equal(RentalApplicationStatus.Withdrawn, history.ToStatus);
        Assert.Equal("renter", history.Actor);
        Assert.Equal("Found another place", history.Reason);
        Assert.NotEqual(Guid.Empty, history.Id);
    }

    [Fact]
    public async Task Handle_NonexistentId_ThrowsKeyNotFoundException()
    {
        await using var context = CreateContext();
        var handler = CreateHandler(context);
        var missingId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => handler.Handle(CreateCommand(missingId, RentalApplicationStatus.UnderReview, "owner"), CancellationToken.None));

        Assert.Contains(missingId.ToString(), ex.Message);
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
        await using var context = CreateContext();
        var application = CreateApplication(from);
        context.RentalApplications.Add(application);
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(CreateCommand(application.Id, to, actor), CancellationToken.None));

        var persisted = await context.RentalApplications.AsNoTracking().SingleAsync(x => x.Id == application.Id);
        Assert.Equal(from, persisted.Status);
        Assert.Empty(await context.RentalApplicationHistories.ToListAsync());
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
        await using var context = CreateContext();
        var application = CreateApplication(from);
        context.RentalApplications.Add(application);
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(CreateCommand(application.Id, to, actor), CancellationToken.None));

        var persisted = await context.RentalApplications.AsNoTracking().SingleAsync(x => x.Id == application.Id);
        Assert.Equal(from, persisted.Status);
        Assert.Empty(await context.RentalApplicationHistories.ToListAsync());
    }

    [Theory]
    [InlineData(RentalApplicationStatus.Approved)]
    [InlineData(RentalApplicationStatus.Denied)]
    [InlineData(RentalApplicationStatus.Withdrawn)]
    public async Task Handle_FromFinalState_ThrowsInvalidOperationException(RentalApplicationStatus finalStatus)
    {
        await using var context = CreateContext();
        var application = CreateApplication(finalStatus);
        context.RentalApplications.Add(application);
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(CreateCommand(application.Id, RentalApplicationStatus.UnderReview, "owner"), CancellationToken.None));

        Assert.Contains("final state", ex.Message);
        var persisted = await context.RentalApplications.AsNoTracking().SingleAsync(x => x.Id == application.Id);
        Assert.Equal(finalStatus, persisted.Status);
    }

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
            TryHandleAsync(CreateHandler(contextA), command),
            TryHandleAsync(CreateHandler(contextB), command));

        Assert.Equal(1, outcomes.Count(succeeded => succeeded));

        await using var verify = CreateContext(databaseName);
        var persisted = await verify.RentalApplications.AsNoTracking().SingleAsync(x => x.Id == application.Id);
        Assert.Equal(RentalApplicationStatus.UnderReview, persisted.Status);

        // Only the winning request's history entry should be persisted.
        var history = await verify.RentalApplicationHistories.AsNoTracking().ToListAsync();
        Assert.Single(history);
        Assert.Equal(RentalApplicationStatus.UnderReview, history[0].ToStatus);
    }

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
