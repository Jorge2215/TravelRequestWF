namespace TravelRequestWF.Infrastructure.Entities;

public class AuditLogEntry
{
    public int Id { get; set; }
    public int TravelRequestId { get; set; }
    public TravelRequest TravelRequest { get; set; } = null!;
    public string Action { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string ActorId { get; set; } = string.Empty;
}
