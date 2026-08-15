namespace TravelRequestWF.Infrastructure.Entities;

public class RequestDocument
{
    public int Id { get; set; }
    public int TravelRequestId { get; set; }
    public TravelRequest TravelRequest { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public string BlobUrl { get; set; } = string.Empty;
}
