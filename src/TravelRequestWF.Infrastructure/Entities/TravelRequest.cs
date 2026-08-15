namespace TravelRequestWF.Infrastructure.Entities;

public class TravelRequest
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public int ApproverId { get; set; }
    public Employee Approver { get; set; } = null!;

    public string Destination { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public TravelRequestStatus Status { get; set; } = TravelRequestStatus.Pending;
    public DateTime SubmittedAt { get; set; }

    public ICollection<RequestDocument> Documents { get; set; } = new List<RequestDocument>();
    public ICollection<AuditLogEntry> AuditLog { get; set; } = new List<AuditLogEntry>();
}
