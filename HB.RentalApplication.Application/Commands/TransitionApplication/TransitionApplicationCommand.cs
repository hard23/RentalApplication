using HB.RentalApplication.Domain;
using MediatR;

namespace HB.RentalApplication.Application.Commands.TransitionApplication;

public class TransitionApplicationCommand : IRequest<TransitionApplicationResponse>
{
    public Guid ApplicationId { get; set; }
    public RentalApplicationStatus To { get; set; }
    public string? Reason { get; set; }
    public string Actor { get; set; } = string.Empty; // "owner" or "renter"
}
