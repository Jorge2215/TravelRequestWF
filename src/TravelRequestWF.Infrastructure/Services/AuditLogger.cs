using Microsoft.EntityFrameworkCore;
using TravelRequestWF.Infrastructure.Data;
using TravelRequestWF.Infrastructure.Entities;

namespace TravelRequestWF.Infrastructure.Services;

public class AuditLogger : IAuditLogger
{
    private readonly AppDbContext _db;

    public AuditLogger(AppDbContext db)
    {
        _db = db;
    }

    public Task LogAsync(string action, int? travelRequestId, int? requestDocumentId, string actorId, string? details = null)
    {
        _db.AuditLogEntries.Add(new AuditLogEntry
        {
            Action = action,
            TravelRequestId = travelRequestId,
            RequestDocumentId = requestDocumentId,
            ActorId = actorId,
            Details = details,
            Timestamp = DateTime.UtcNow
        });
        return Task.CompletedTask;
    }

    public async Task<List<AuditLogEntry>> GetLogByRequestAsync(int travelRequestId)
    {
        return await _db.AuditLogEntries
            .Where(a => a.TravelRequestId == travelRequestId)
            .OrderBy(a => a.Timestamp)
            .ToListAsync();
    }

    public async Task<List<AuditLogEntry>> GetLogByUserAsync(string actorId)
    {
        return await _db.AuditLogEntries
            .Where(a => a.ActorId == actorId)
            .OrderBy(a => a.Timestamp)
            .ToListAsync();
    }
}
