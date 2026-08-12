using Microsoft.EntityFrameworkCore;
using TravelRequestWF.Infrastructure.Data;
using TravelRequestWF.Infrastructure.Entities;

namespace TravelRequestWF.Infrastructure.Services;

public class TravelRequestService : ITravelRequestService
{
    private readonly AppDbContext _db;
    private readonly IBlobStorageService _blob;

    public TravelRequestService(AppDbContext db, IBlobStorageService blob)
    {
        _db = db;
        _blob = blob;
    }

    public async Task<TravelRequest> SubmitRequestAsync(int employeeId, string actorUserId, SubmitRequestDto dto, CancellationToken ct = default)
    {
        var employee = await _db.Employees.FindAsync(new object[] { employeeId }, ct)
            ?? throw new InvalidOperationException("Employee not found.");

        if (employee.SuperiorId == null)
            throw new InvalidOperationException("No approver assigned to your account. Contact HR.");

        var request = new TravelRequest
        {
            EmployeeId = employeeId,
            ApproverId = employee.SuperiorId.Value,
            Destination = dto.Destination,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Purpose = dto.Purpose,
            Status = TravelRequestStatus.Pending,
            SubmittedAt = DateTime.UtcNow
        };
        _db.TravelRequests.Add(request);
        await _db.SaveChangesAsync(ct); // get request.Id

        foreach (var (stream, fileName, contentType) in dto.Documents)
        {
            var blobUrl = await _blob.UploadDocumentAsync(stream, fileName, contentType, ct);
            var doc = new RequestDocument
            {
                TravelRequestId = request.Id,
                FileName = fileName,
                BlobUrl = blobUrl
            };
            _db.RequestDocuments.Add(doc);
            await _db.SaveChangesAsync(ct); // get doc.Id

            // Audit: exactly RequestDocumentId set, TravelRequestId null
            _db.AuditLogEntries.Add(new AuditLogEntry
            {
                TravelRequestId = null,
                RequestDocumentId = doc.Id,
                Action = "DocumentUploaded",
                ActorId = actorUserId,
                Timestamp = DateTime.UtcNow
            });
        }

        // Audit: exactly TravelRequestId set, RequestDocumentId null
        _db.AuditLogEntries.Add(new AuditLogEntry
        {
            TravelRequestId = request.Id,
            RequestDocumentId = null,
            Action = "Submitted",
            ActorId = actorUserId,
            Timestamp = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        return request;
    }

    public async Task ApproveRequestAsync(int requestId, int managerEmployeeId, string actorUserId, string? comments, CancellationToken ct = default)
    {
        var request = await _db.TravelRequests.FindAsync(new object[] { requestId }, ct)
            ?? throw new KeyNotFoundException($"Travel request {requestId} not found.");

        if (request.ApproverId != managerEmployeeId)
            throw new UnauthorizedAccessException("You are not the approver for this request.");

        if (request.Status != TravelRequestStatus.Pending)
            throw new InvalidOperationException("Only Pending requests can be approved.");

        request.Status = TravelRequestStatus.Approved;
        _db.AuditLogEntries.Add(new AuditLogEntry
        {
            TravelRequestId = request.Id,
            RequestDocumentId = null,
            Action = "Approved",
            Details = comments,
            ActorId = actorUserId,
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task RejectRequestAsync(int requestId, int managerEmployeeId, string actorUserId, string? comments, CancellationToken ct = default)
    {
        var request = await _db.TravelRequests.FindAsync(new object[] { requestId }, ct)
            ?? throw new KeyNotFoundException($"Travel request {requestId} not found.");

        if (request.ApproverId != managerEmployeeId)
            throw new UnauthorizedAccessException("You are not the approver for this request.");

        if (request.Status != TravelRequestStatus.Pending)
            throw new InvalidOperationException("Only Pending requests can be rejected.");

        request.Status = TravelRequestStatus.Rejected;
        _db.AuditLogEntries.Add(new AuditLogEntry
        {
            TravelRequestId = request.Id,
            RequestDocumentId = null,
            Action = "Rejected",
            Details = comments,
            ActorId = actorUserId,
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task ReturnRequestAsync(int requestId, int managerEmployeeId, string actorUserId, string? comments, CancellationToken ct = default)
    {
        var request = await _db.TravelRequests.FindAsync(new object[] { requestId }, ct)
            ?? throw new KeyNotFoundException($"Travel request {requestId} not found.");

        if (request.ApproverId != managerEmployeeId)
            throw new UnauthorizedAccessException("You are not the approver for this request.");

        if (request.Status != TravelRequestStatus.Pending)
            throw new InvalidOperationException("Only Pending requests can be returned.");

        request.Status = TravelRequestStatus.Returned;
        _db.AuditLogEntries.Add(new AuditLogEntry
        {
            TravelRequestId = request.Id,
            RequestDocumentId = null,
            Action = "Returned",
            Details = comments,
            ActorId = actorUserId,
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task ResubmitRequestAsync(int requestId, int employeeId, string actorUserId, CancellationToken ct = default)
    {
        var request = await _db.TravelRequests.FindAsync(new object[] { requestId }, ct)
            ?? throw new KeyNotFoundException($"Travel request {requestId} not found.");

        if (request.EmployeeId != employeeId)
            throw new UnauthorizedAccessException("You do not own this request.");

        if (request.Status != TravelRequestStatus.Returned)
            throw new InvalidOperationException("Only Returned requests can be resubmitted.");

        request.Status = TravelRequestStatus.Pending;
        _db.AuditLogEntries.Add(new AuditLogEntry
        {
            TravelRequestId = request.Id,
            RequestDocumentId = null,
            Action = "Resubmitted",
            ActorId = actorUserId,
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<TravelRequest>> GetRequestsForEmployeeAsync(int employeeId, CancellationToken ct = default)
    {
        return await _db.TravelRequests
            .Where(r => r.EmployeeId == employeeId)
            .Include(r => r.Documents)
            .Include(r => r.Employee)
            .OrderByDescending(r => r.SubmittedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TravelRequest>> GetRequestsForManagerAsync(int managerEmployeeId, CancellationToken ct = default)
    {
        return await _db.TravelRequests
            .Where(r => r.ApproverId == managerEmployeeId)
            .Include(r => r.Employee)
            .OrderByDescending(r => r.SubmittedAt)
            .ToListAsync(ct);
    }

    public async Task<TravelRequest?> GetRequestByIdAsync(int requestId, CancellationToken ct = default)
    {
        return await _db.TravelRequests
            .Include(r => r.Documents)
            .Include(r => r.AuditLog.OrderBy(a => a.Timestamp))
            .Include(r => r.Employee)
            .Include(r => r.Approver)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct);
    }
}
