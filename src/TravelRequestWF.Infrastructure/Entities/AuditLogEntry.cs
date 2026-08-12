namespace TravelRequestWF.Infrastructure.Entities;

// NOTE: Exactly one of TravelRequestId or RequestDocumentId must be set.
// Both null and both non-null are invalid. Enforced at the service layer (IAuditLogger), not at the DB level.
public class AuditLogEntry
{
    public int Id { get; set; }
    public int? TravelRequestId { get; set; }
    public TravelRequest? TravelRequest { get; set; }
    public int? RequestDocumentId { get; set; }
    public RequestDocument? RequestDocument { get; set; }
    public string Action { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string ActorId { get; set; } = string.Empty;
}
