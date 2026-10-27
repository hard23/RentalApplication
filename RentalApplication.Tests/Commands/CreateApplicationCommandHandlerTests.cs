using HB.RentalApplication.Application.Commands.CreateApplication;
using HB.RentalApplication.Domain;
using HB.RentalApplication.Domain.Repositories;
using NSubstitute;

namespace RentalApplication.Tests.Commands;

public class CreateApplicationCommandHandlerTests
{
    private static CreateApplicationCommand CreateCommand() => new()
    {
        PropertyId = Guid.NewGuid(),
        RenterEmail = "jane@example.com",
        RenterFullName = "Jane Doe",
        PropertyAddress = "1 Main St",
        AnnualIncome = 85000m
    };

    private static (CreateApplicationCommandHandler Handler, IRentalApplicationRepository Repository, IRentalApplicationHistoryRepository HistoryRepository)
        CreateMockedHandler()
    {
        var repository = Substitute.For<IRentalApplicationRepository>();
        var historyRepository = Substitute.For<IRentalApplicationHistoryRepository>();
        return (new CreateApplicationCommandHandler(repository, historyRepository), repository, historyRepository);
    }

    [Fact]
    public async Task Handle_ValidCommand_PersistsApplicationAsSubmitted()
    {
        var command = CreateCommand();
        var (handler, repository, _) = CreateMockedHandler();

        await handler.Handle(command, CancellationToken.None);

        await repository.Received(1).AddAsync(
            Arg.Is<HB.RentalApplication.Domain.RentalApplication>(a =>
                a.Status == RentalApplicationStatus.Submitted
                && a.PropertyId == command.PropertyId
                && a.ApplicantFullName == command.RenterFullName
                && a.ApplicantEmail == command.RenterEmail
                && a.PropertyAddress == command.PropertyAddress
                && a.AnnualIncome == command.AnnualIncome
                && a.HouseholdSize == command.HouseholdSize
                && a.IsActive
                && !a.IsDeleted),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsResponseMatchingPersistedApplication()
    {
        var (handler, repository, _) = CreateMockedHandler();

        var response = await handler.Handle(CreateCommand(), CancellationToken.None);

        var persisted = await CapturePersistedAsync(repository);
        Assert.NotEqual(Guid.Empty, response.ApplicationId);
        Assert.Equal(persisted.Id, response.ApplicationId);
        Assert.Equal(RentalApplicationStatus.Submitted.ToString(), response.Status);
        Assert.Equal(persisted.CreatedAtUtc, response.CreatedAtUtc);
    }

    [Fact]
    public async Task Handle_CalledTwice_CreatesDistinctApplications()
    {
        var (handler, repository, _) = CreateMockedHandler();

        var first = await handler.Handle(CreateCommand(), CancellationToken.None);
        var second = await handler.Handle(CreateCommand(), CancellationToken.None);

        Assert.NotEqual(first.ApplicationId, second.ApplicationId);
        await repository.Received(2).AddAsync(Arg.Any<HB.RentalApplication.Domain.RentalApplication>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidCommand_StagesInitialHistoryEntry()
    {
        var (handler, repository, historyRepository) = CreateMockedHandler();

        var response = await handler.Handle(CreateCommand(), CancellationToken.None);

        var persisted = await CapturePersistedAsync(repository);
        await historyRepository.Received(1).AddAsync(
            Arg.Is<RentalApplicationHistory>(h =>
                h.ApplicationId == response.ApplicationId
                && h.FromStatus == RentalApplicationStatus.Submitted
                && h.ToStatus == RentalApplicationStatus.Submitted
                && h.TimestampUtc == response.CreatedAtUtc
                && h.Actor == "renter"
                && h.Reason == "Application created"),
            Arg.Any<CancellationToken>());
        Assert.Equal(persisted.Id, response.ApplicationId);
    }

    // Returns the application passed to the repository's AddAsync call.
    private static async Task<HB.RentalApplication.Domain.RentalApplication> CapturePersistedAsync(IRentalApplicationRepository repository)
    {
        var calls = repository.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IRentalApplicationRepository.AddAsync))
            .ToList();
        var last = calls.Last();
        var application = (HB.RentalApplication.Domain.RentalApplication)last.GetArguments()[0]!;
        await Task.CompletedTask;
        return application;
    }
}
