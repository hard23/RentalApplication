namespace HB.RentalApplication.Domain;

public class RentalApplication
{
    public Guid Id { get;  set; }
    public Guid PropertyId { get; set; }
    public string ApplicantFullName { get;  set; }
    public string ApplicantEmail { get;  set; }
    public string PropertyAddress { get;  set; }
    public decimal AnnualIncome { get; set; }
    public int HouseholdSize { get; set; }
    public RentalApplicationStatus Status { get;  set; }
    public DateTime CreatedAtUtc { get;  set; }
    public DateTime UpdatedAtUtc { get;  set; }
    public bool IsActive { get;  set; }
    public bool IsDeleted { get;  set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

}
