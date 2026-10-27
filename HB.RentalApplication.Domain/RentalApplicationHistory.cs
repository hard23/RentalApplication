using System;
using HB.RentalApplication.Domain;

namespace HB.RentalApplication.Domain;

public class RentalApplicationHistory
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public RentalApplicationStatus FromStatus { get; set; }
    public RentalApplicationStatus ToStatus { get; set; }
    public DateTime TimestampUtc { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string? Reason { get; set; }
}
