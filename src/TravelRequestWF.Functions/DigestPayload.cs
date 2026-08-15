namespace TravelRequestWF.Functions;

public sealed record PendingRequestItem(
    int RequestId,
    string EmployeeName,
    string Destination,
    string StartDate,    // formatted yyyy-MM-dd
    string EndDate,      // formatted yyyy-MM-dd
    string Status
);

public sealed record ManagerDigestPayload(
    string ManagerName,
    string ManagerEmail,
    IReadOnlyList<PendingRequestItem> PendingRequests
);
