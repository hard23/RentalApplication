namespace HB.RentalApplication.Application.Commands.CreateApplication;

public class CreateApplicationResponse
{
    public Guid ApplicationId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
