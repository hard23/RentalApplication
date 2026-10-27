namespace HB.RentalApplication.Application.Queries.GetApplications;

public class ApplicationDto
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public string RenterEmail { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime SubmittedAtUtc { get; set; }
}

public class GetApplicationsResponse
{
    public IReadOnlyList<ApplicationDto> Applications { get; set; } = new List<ApplicationDto>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}
