using TravelRequestWF.Infrastructure.Entities;

namespace TravelRequestWF.Infrastructure.Services;

public interface ITravelRequestService
{
    Task<TravelRequest> SubmitRequestAsync(int employeeId, string actorUserId, SubmitRequestDto dto, CancellationToken ct = default);
    Task ApproveRequestAsync(int requestId, int managerEmployeeId, string actorUserId, string? comments, CancellationToken ct = default);
    Task RejectRequestAsync(int requestId, int managerEmployeeId, string actorUserId, string? comments, CancellationToken ct = default);
    Task ReturnRequestAsync(int requestId, int managerEmployeeId, string actorUserId, string? comments, CancellationToken ct = default);
    Task ResubmitRequestAsync(int requestId, int employeeId, string actorUserId, CancellationToken ct = default);
    Task<IReadOnlyList<TravelRequest>> GetRequestsForEmployeeAsync(int employeeId, CancellationToken ct = default);
    Task<IReadOnlyList<TravelRequest>> GetRequestsForManagerAsync(int managerEmployeeId, CancellationToken ct = default);
    Task<TravelRequest?> GetRequestByIdAsync(int requestId, CancellationToken ct = default);
}
