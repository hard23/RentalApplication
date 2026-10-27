namespace HB.RentalApplication.Application.Commands.TransitionApplication;

public class TransitionApplicationResponse
{
    public Guid ApplicationId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime TransitionedAtUtc { get; set; }
}
