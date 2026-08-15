using TravelRequestWF.Infrastructure.Entities;

namespace TravelRequestWF.Infrastructure.Services;

public interface IAuditLogger
{
    /// <summary>
    /// Logs an audit event. Exactly one of travelRequestId or requestDocumentId must be non-null.
    /// </summary>
    Task LogAsync(string action, int? travelRequestId, int? requestDocumentId, string actorId, string? details = null);

    Task<List<AuditLogEntry>> GetLogByRequestAsync(int travelRequestId);
    Task<List<AuditLogEntry>> GetLogByUserAsync(string actorId);
}
