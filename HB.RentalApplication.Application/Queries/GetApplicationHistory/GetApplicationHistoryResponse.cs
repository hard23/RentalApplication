namespace HB.RentalApplication.Application.Queries.GetApplicationHistory;

public class GetApplicationHistoryResponse
{
    public Guid ApplicationId { get; set; }
    public List<GetApplicationHistoryEntryResponse> Entries { get; set; } = new();
}

public class GetApplicationHistoryEntryResponse
{
    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string? Reason { get; set; }
}
