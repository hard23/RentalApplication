using MediatR;

namespace HB.RentalApplication.Application.Commands.CreateApplication;

public class CreateApplicationCommand : IRequest<CreateApplicationResponse>
{
    public Guid PropertyId { get; set; }
    public string RenterEmail { get; set; } = string.Empty;
    public string RenterFullName { get; set; } = string.Empty;
    public string PropertyAddress { get; set; } = string.Empty;
    public decimal AnnualIncome { get; set; }
    public int HouseholdSize { get; set; }
}
