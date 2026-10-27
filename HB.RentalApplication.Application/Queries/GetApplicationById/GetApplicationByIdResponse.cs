namespace HB.RentalApplication.Application.Queries.GetApplicationById;

public class GetApplicationByIdResponse
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public string RenterEmail { get; set; } = string.Empty;
    public int HouseholdSize { get; set; }
    public decimal AnnualIncome { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime SubmittedAtUtc { get; set; }
}
