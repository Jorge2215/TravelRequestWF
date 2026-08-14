namespace TravelRequestWF.Infrastructure.Services;

public class NotificationPayload
{
    public string RequestId { get; set; } = default!;
    public string EventType { get; set; } = default!; // "Submitted", "Resubmitted", "Approved", "Rejected", "Returned"
    public string EmployeeName { get; set; } = default!;
    public string EmployeeEmail { get; set; } = default!;
    public string ManagerName { get; set; } = default!;
    public string ManagerEmail { get; set; } = default!;
    public string Destination { get; set; } = default!;
    public string StartDate { get; set; } = default!;   // ISO 8601: "yyyy-MM-dd"
    public string EndDate { get; set; } = default!;     // ISO 8601: "yyyy-MM-dd"
    public string Purpose { get; set; } = default!;
    public string Status { get; set; } = default!;      // TravelRequestStatus enum .ToString()
    public string Comments { get; set; } = string.Empty;
}
