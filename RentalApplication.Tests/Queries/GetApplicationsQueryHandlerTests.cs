using HB.RentalApplication.Application.Queries.GetApplications;
using HB.RentalApplication.Domain;
using HB.RentalApplication.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace RentalApplication.Tests.Queries;

public class GetApplicationsQueryHandlerTests
{
    private static readonly Guid PropertyA = Guid.NewGuid();
    private static readonly Guid PropertyB = Guid.NewGuid();

    private static RentalApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<RentalApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new RentalApplicationDbContext(options);
    }

    private static HB.RentalApplication.Domain.RentalApplication CreateApplication(
        Guid propertyId, RentalApplicationStatus status, int dayOffset) => new()
    {
        Id = Guid.NewGuid(),
        PropertyId = propertyId,
        ApplicantFullName = "Jane Doe",
        ApplicantEmail = $"renter{dayOffset}@example.com",
        PropertyAddress = "1 Main St",
        AnnualIncome = 85000m,
        Status = status,
        CreatedAtUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(dayOffset),
        UpdatedAtUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(dayOffset),
        IsActive = true,
        IsDeleted = false,
        RowVersion = Array.Empty<byte>()
    };

    private static GetApplicationsQueryHandler CreateHandler(RentalApplicationDbContext context)
        => new(new RentalApplicationRepository(context));

    [Fact]
    public async Task Handle_NoFilters_ReturnsAllApplicationsWithTotalCount()
    {
        await using var context = CreateContext();
        context.RentalApplications.AddRange(
            CreateApplication(PropertyA, RentalApplicationStatus.Submitted, 0),
            CreateApplication(PropertyB, RentalApplicationStatus.Approved, 1));
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        var response = await handler.Handle(new GetApplicationsQuery(), CancellationToken.None);

        Assert.Equal(2, response.TotalCount);
        Assert.Equal(2, response.Applications.Count);
        Assert.Equal(1, response.Page);
        Assert.Equal(10, response.PageSize);
    }

    [Fact]
    public async Task Handle_ReturnsMappedDtoFields()
    {
        await using var context = CreateContext();
        var application = CreateApplication(PropertyA, RentalApplicationStatus.UnderReview, 3);
        context.RentalApplications.Add(application);
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        var response = await handler.Handle(new GetApplicationsQuery(), CancellationToken.None);

        var dto = Assert.Single(response.Applications);
        Assert.Equal(application.Id, dto.Id);
        Assert.Equal(application.PropertyId, dto.PropertyId);
        Assert.Equal(application.ApplicantEmail, dto.RenterEmail);
        Assert.Equal(RentalApplicationStatus.UnderReview.ToString(), dto.Status);
        Assert.Equal(application.CreatedAtUtc, dto.SubmittedAtUtc);
    }

    [Fact]
    public async Task Handle_FilterByPropertyId_ReturnsOnlyMatchingApplications()
    {
        await using var context = CreateContext();
        context.RentalApplications.AddRange(
            CreateApplication(PropertyA, RentalApplicationStatus.Submitted, 0),
            CreateApplication(PropertyA, RentalApplicationStatus.Approved, 1),
            CreateApplication(PropertyB, RentalApplicationStatus.Submitted, 2));
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        var response = await handler.Handle(new GetApplicationsQuery { PropertyId = PropertyA }, CancellationToken.None);

        Assert.Equal(2, response.TotalCount);
        Assert.All(response.Applications, dto => Assert.Equal(PropertyA, dto.PropertyId));
    }

    [Fact]
    public async Task Handle_FilterByStatus_ReturnsOnlyMatchingApplications()
    {
        await using var context = CreateContext();
        context.RentalApplications.AddRange(
            CreateApplication(PropertyA, RentalApplicationStatus.Submitted, 0),
            CreateApplication(PropertyA, RentalApplicationStatus.Denied, 1),
            CreateApplication(PropertyB, RentalApplicationStatus.Denied, 2));
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        var response = await handler.Handle(
            new GetApplicationsQuery { Status = RentalApplicationStatus.Denied }, CancellationToken.None);

        Assert.Equal(2, response.TotalCount);
        Assert.All(response.Applications, dto => Assert.Equal(RentalApplicationStatus.Denied.ToString(), dto.Status));
    }

    [Fact]
    public async Task Handle_OrdersByCreatedAtDescending()
    {
        await using var context = CreateContext();
        context.RentalApplications.AddRange(
            CreateApplication(PropertyA, RentalApplicationStatus.Submitted, 0),
            CreateApplication(PropertyA, RentalApplicationStatus.Submitted, 5),
            CreateApplication(PropertyA, RentalApplicationStatus.Submitted, 2));
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        var response = await handler.Handle(new GetApplicationsQuery(), CancellationToken.None);

        var dates = response.Applications.Select(a => a.SubmittedAtUtc).ToList();
        Assert.Equal(dates.OrderByDescending(d => d).ToList(), dates);
    }

    [Fact]
    public async Task Handle_Paging_ReturnsRequestedPageAndKeepsTotalCount()
    {
        await using var context = CreateContext();
        for (var i = 0; i < 5; i++)
        {
            context.RentalApplications.Add(CreateApplication(PropertyA, RentalApplicationStatus.Submitted, i));
        }
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        var response = await handler.Handle(
            new GetApplicationsQuery { Page = 2, PageSize = 2 }, CancellationToken.None);

        Assert.Equal(2, response.Applications.Count);
        Assert.Equal(5, response.TotalCount);
        Assert.Equal(2, response.Page);
        Assert.Equal(2, response.PageSize);
    }

    [Fact]
    public async Task Handle_InvalidPageAndPageSize_AreClampedToValidRange()
    {
        await using var context = CreateContext();
        context.RentalApplications.Add(CreateApplication(PropertyA, RentalApplicationStatus.Submitted, 0));
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        var response = await handler.Handle(
            new GetApplicationsQuery { Page = 0, PageSize = 0 }, CancellationToken.None);

        Assert.Equal(1, response.Page);
        Assert.Equal(1, response.PageSize);
        Assert.Single(response.Applications);
    }

    [Fact]
    public async Task Handle_PageSizeAboveMaximum_IsClampedTo100()
    {
        await using var context = CreateContext();
        var handler = CreateHandler(context);

        var response = await handler.Handle(
            new GetApplicationsQuery { PageSize = 500 }, CancellationToken.None);

        Assert.Equal(100, response.PageSize);
    }

    [Fact]
    public async Task Handle_NoMatches_ReturnsEmptyResult()
    {
        await using var context = CreateContext();
        context.RentalApplications.Add(CreateApplication(PropertyA, RentalApplicationStatus.Submitted, 0));
        await context.SaveChangesAsync();
        var handler = CreateHandler(context);

        var response = await handler.Handle(
            new GetApplicationsQuery { PropertyId = PropertyB }, CancellationToken.None);

        Assert.Empty(response.Applications);
        Assert.Equal(0, response.TotalCount);
    }
}
