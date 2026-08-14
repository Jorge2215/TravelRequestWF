# Gandalf — Task Brief: Stage 5 Notification Integration

**Assigned to:** Gandalf (Backend Developer)  
**Stage:** 5 — Power Automate Notifications  
**Branch:** `dev`  
**Date issued:** 2026-08-13T21:27:04-03:00  
**Issued by:** Aragorn

---

## Context

Stage 1–4 is complete: TravelRequestWF.Infrastructure has `TravelRequestService` with Submit/Approve/Reject/Return/Resubmit, all writing `AuditLogEntry` rows. `BlobStorageService` is live and working. We now need `TravelRequestService` to push notification events to Power Automate HTTP-triggered flows after each state transition.

Aragorn has made all architecture decisions — see `.squad/decisions/inbox/aragorn-stage5-notification-scope.md`. This brief is the full implementation spec.

---

## What You Must Build

### 1. `INotificationService` interface

**Location:** `TravelRequestWF.Infrastructure/Services/INotificationService.cs`

```csharp
public interface INotificationService
{
    Task NotifyRequestSubmittedAsync(NotificationPayload payload);
    Task NotifyRequestStatusChangedAsync(NotificationPayload payload);
}
```

---

### 2. `NotificationPayload` DTO

**Location:** `TravelRequestWF.Infrastructure/Services/NotificationPayload.cs`

This is the canonical JSON payload shape. Both flows receive this exact structure (Aragorn's canonical contract):

```csharp
public class NotificationPayload
{
    public string RequestId { get; set; } = default!;
    public string EventType { get; set; } = default!;  // "Submitted", "Resubmitted", "Approved", "Rejected", "Returned"
    public string EmployeeName { get; set; } = default!;
    public string EmployeeEmail { get; set; } = default!;
    public string ManagerName { get; set; } = default!;
    public string ManagerEmail { get; set; } = default!;
    public string Destination { get; set; } = default!;
    public string StartDate { get; set; } = default!;   // ISO 8601: "yyyy-MM-dd"
    public string EndDate { get; set; } = default!;     // ISO 8601: "yyyy-MM-dd"
    public string Purpose { get; set; } = default!;
    public string Status { get; set; } = default!;      // TravelRequestStatus enum .ToString()
    public string? Comments { get; set; }
}
```

Serialize with `System.Text.Json` (camelCase or PascalCase — use PascalCase to match field names above so Power Automate schema parsing is consistent; do NOT use camelCase unless you explicitly set it in JsonSerializerOptions).

---

### 3. `PowerAutomateNotificationService` implementation

**Location:** `TravelRequestWF.Infrastructure/Services/PowerAutomateNotificationService.cs`

```csharp
public class PowerAutomateNotificationService : INotificationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PowerAutomateNotificationService> _logger;
    private readonly string _flowAUrl;
    private readonly string _flowBUrl;

    public PowerAutomateNotificationService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<PowerAutomateNotificationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _flowAUrl = configuration["PowerAutomate:FlowASubmissionUrl"] ?? string.Empty;
        _flowBUrl = configuration["PowerAutomate:FlowBStatusChangeUrl"] ?? string.Empty;
    }

    public async Task NotifyRequestSubmittedAsync(NotificationPayload payload)
        => await PostToFlowAsync(_flowAUrl, "Flow A (Submission)", payload);

    public async Task NotifyRequestStatusChangedAsync(NotificationPayload payload)
        => await PostToFlowAsync(_flowBUrl, "Flow B (Status Change)", payload);

    private async Task PostToFlowAsync(string url, string flowName, NotificationPayload payload)
    {
        if (string.IsNullOrWhiteSpace(url) || url.StartsWith("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Power Automate {FlowName} URL not configured — skipping notification for RequestId={RequestId}.", flowName, payload.RequestId);
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);
            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("Power Automate {FlowName} returned non-success status {StatusCode} for RequestId={RequestId}.", flowName, (int)response.StatusCode, payload.RequestId);
            else
                _logger.LogInformation("Power Automate {FlowName} notified successfully for RequestId={RequestId}.", flowName, payload.RequestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Power Automate {FlowName} notification failed for RequestId={RequestId}. Notification is non-blocking — workflow continues.", flowName, payload.RequestId);
        }
    }
}
```

**Key invariants:**
- NEVER throw from this service. All exceptions are caught and logged.
- Placeholder URL check must happen before any HTTP call attempt.
- Non-success HTTP status codes are warnings, not exceptions.

---

### 4. Wire into `TravelRequestService`

Inject `INotificationService` via constructor. After each successful `await _context.SaveChangesAsync()` call in each transition method, call the appropriate notification method. The notification call must be OUTSIDE any transaction scope and AFTER the DB commit — notification failure must not roll back the transition.

**Mapping:**

| TravelRequestService method | Notification call | EventType in payload |
|---|---|---|
| `Submit` | `NotifyRequestSubmittedAsync` | `"Submitted"` |
| `Resubmit` | `NotifyRequestSubmittedAsync` | `"Resubmitted"` |
| `Approve` | `NotifyRequestStatusChangedAsync` | `"Approved"` |
| `Reject` | `NotifyRequestStatusChangedAsync` | `"Rejected"` |
| `Return` | `NotifyRequestStatusChangedAsync` | `"Returned"` |

**Payload construction:**  
You need `Employee.Name`, `Employee.Email`, `Approver.Name`, `Approver.Email` for each transition. At the point of each method, you already have the `TravelRequest` loaded. Ensure eager loading includes `.Include(r => r.Employee).Include(r => r.Approver)` (or equivalent) before the notification call. Use `TravelRequest.ApproverId`/`Approver` navigation for ManagerName/ManagerEmail.

Construct payload like:
```csharp
var payload = new NotificationPayload
{
    RequestId = request.Id.ToString(),
    EventType = "Submitted",
    EmployeeName = request.Employee.Name,
    EmployeeEmail = request.Employee.Email,
    ManagerName = request.Approver!.Name,
    ManagerEmail = request.Approver!.Email,
    Destination = request.Destination,
    StartDate = request.StartDate.ToString("yyyy-MM-dd"),
    EndDate = request.EndDate.ToString("yyyy-MM-dd"),
    Purpose = request.Purpose,
    Status = request.Status.ToString(),
    Comments = /* the comments string passed into the method, if any */ null
};
await _notificationService.NotifyRequestSubmittedAsync(payload);
```

For `Approve`/`Reject`/`Return`, the `comments` parameter (manager's reason text) maps to `payload.Comments`.

---

### 5. `appsettings.json` configuration keys

Add to `TravelRequestWF.Web/appsettings.json` (alongside existing connection strings):

```json
"PowerAutomate": {
  "FlowASubmissionUrl": "PLACEHOLDER_FLOW_A_URL",
  "FlowBStatusChangeUrl": "PLACEHOLDER_FLOW_B_URL"
}
```

Add same keys to `appsettings.Development.json` with the same placeholder values (so dev environment doesn't blow up). Do NOT put real URLs here; Jorgito will paste them after creating flows in the portal.

---

### 6. DI Registration in `Program.cs`

```csharp
builder.Services.AddHttpClient<PowerAutomateNotificationService>();
builder.Services.AddScoped<INotificationService, PowerAutomateNotificationService>();
```

Use `AddHttpClient<T>()` to get proper `IHttpClientFactory`-managed lifetime for the typed client.

---

### 7. Build Verification

Run `dotnet build` from the solution root. Fix any errors. Do NOT run migrations (no schema changes in Stage 5).

---

### 8. Commit and Push

```
git add -A
git commit -m "Stage 5: Add INotificationService and PowerAutomateNotificationService for Power Automate HTTP notifications

- INotificationService interface with NotifyRequestSubmittedAsync / NotifyRequestStatusChangedAsync
- NotificationPayload DTO (canonical JSON contract for both flows)
- PowerAutomateNotificationService: best-effort HTTP POST to Flow A/B URLs, logs failures, never throws
- Placeholder config keys: PowerAutomate:FlowASubmissionUrl / PowerAutomate:FlowBStatusChangeUrl
- TravelRequestService: Submit/Resubmit → Flow A; Approve/Reject/Return → Flow B (post-commit, non-blocking)
- DI registration via AddHttpClient<PowerAutomateNotificationService>

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
git push origin dev
```

---

## Out of Scope (do NOT do these)

- Do NOT create Azure Functions for this stage — the notification path is direct HTTP to Power Automate.
- Do NOT modify Razor Pages or add any UI.
- Do NOT add EF Core migrations — no schema changes.
- Do NOT write Power Automate flow definitions or documentation — that is Sam's deliverable.

---

## Canonical JSON Payload (for Sam's reference)

Sam's flow trigger JSON schema must match this exactly (field names, PascalCase):

```json
{
  "RequestId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "EventType": "Submitted",
  "EmployeeName": "Ana López",
  "EmployeeEmail": "ana.lopez@company.com",
  "ManagerName": "Carlos Ruiz",
  "ManagerEmail": "carlos.ruiz@company.com",
  "Destination": "Buenos Aires",
  "StartDate": "2026-08-20",
  "EndDate": "2026-08-25",
  "Purpose": "Client meeting",
  "Status": "Pending",
  "Comments": null
}
```
