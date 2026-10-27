using MediatR;

namespace HB.RentalApplication.Application.Queries.GetApplicationById;

public class GetApplicationByIdQuery : IRequest<GetApplicationByIdResponse>
{
    public Guid Id { get; set; }

    public GetApplicationByIdQuery(Guid id)
    {
        Id = id;
    }
}
