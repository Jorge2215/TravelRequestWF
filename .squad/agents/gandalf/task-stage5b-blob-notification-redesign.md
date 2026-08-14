# Gandalf — Task: Stage 5b Blob Notification Redesign

**Assigned to:** Gandalf
**Requested by:** Aragorn
**Date:** 2026-08-13T22:15:00-03:00
**Branch:** `dev`
**Supersedes:** `.squad/agents/gandalf/task-stage5-notification-integration.md`

---

## Context

Jorgito's Power Automate plan is non-Premium. The HTTP trigger approach from Stage 5 is no longer viable — "When an HTTP request is received" requires a Premium license.

The redesign replaces HTTP POST with Azure Blob Storage writes. Power Automate uses the Standard "When a blob is added or modified (properties only)" trigger instead. Full architectural rationale is in `.squad/decisions/inbox/aragorn-stage5-blob-trigger-redesign.md`.

The `INotificationService` interface is **unchanged**. `TravelRequestService` requires **no modifications**. Only the infrastructure implementation changes.

---

## What Already Exists (do NOT remove or break)

- `TravelRequestWF.Infrastructure/Services/BlobStorageService.cs` — handles document uploads; uses `Azure.Storage.Blobs` SDK with `AzureStorage:ConnectionString`. **Do not touch this class.**
- `TravelRequestWF.Infrastructure/Services/PowerAutomateNotificationService.cs` — the Stage 5 HTTP implementation. **This is the class you are replacing.**
- `TravelRequestWF.Core/Interfaces/INotificationService.cs` — interface with `NotifySubmittedAsync` and `NotifyStatusChangedAsync`. **Do not change this interface.**
- `TravelRequestWF.Web/Program.cs` — DI registration for `INotificationService`. **Update the registration only** (swap old class for new class).
- `appsettings.json` — already has `AzureStorage:ConnectionString` (from Stage 4). Already has `PowerAutomate:FlowASubmissionUrl` and `PowerAutomate:FlowBStatusChangeUrl` from Stage 5. **Remove the old URL keys; add new container name keys.**

---

## Your Task

### 1. Create `BlobNotificationService`

Create `TravelRequestWF.Infrastructure/Services/BlobNotificationService.cs`.

**Class responsibilities:**
- Implements `INotificationService`.
- Injected dependencies: `IConfiguration`, `ILogger<BlobNotificationService>`.
- On instantiation (or lazily on first use), reads:
  - `AzureStorage:ConnectionString` from config (reuse existing key, same value as BlobStorageService uses).
  - `PowerAutomate:SubmissionContainerName` (default: `"notification-submitted"` if key is absent).
  - `PowerAutomate:StatusChangeContainerName` (default: `"notification-status-changed"` if key is absent).
- Creates `BlobServiceClient` from the connection string.

**`NotifySubmittedAsync(NotificationPayload payload)` implementation:**
1. Serialize `payload` to JSON (`System.Text.Json`, camelCase or PascalCase — match the existing `NotificationPayload` DTO serialization convention already used in the project).
2. Compute blob name: `{payload.RequestId}-{payload.EventType}-{DateTime.UtcNow.Ticks}.json`.
3. Get `BlobContainerClient` for `SubmissionContainerName`.
4. Call `CreateIfNotExistsAsync(PublicAccessType.None)`.
5. Upload the JSON as a blob with the computed name (`UploadAsync` with `overwrite: false`; unique name guarantees no collision).
6. Log information: `"Notification blob written: {BlobName} to container {ContainerName}"`.
7. Wrap in `try/catch`: on any exception, log error (include `RequestId`, `EventType`, exception message) and **do not rethrow**.

**`NotifyStatusChangedAsync(NotificationPayload payload)` implementation:**
- Same logic, but use `StatusChangeContainerName` instead of `SubmissionContainerName`.

**Non-blocking contract:** Both methods must be `async Task` (not `async void`). Exceptions are always caught and logged — never propagate out of these methods.

### 2. Delete (or mark obsolete) `PowerAutomateNotificationService`

Delete `TravelRequestWF.Infrastructure/Services/PowerAutomateNotificationService.cs`.

If any other file (besides `Program.cs`) still references `PowerAutomateNotificationService` by name, update those references too. There should be none other than the DI registration.

### 3. Update DI Registration in `Program.cs`

Replace:
```csharp
builder.Services.AddScoped<INotificationService, PowerAutomateNotificationService>();
// (or however it is currently registered — AddSingleton, AddTransient, etc.)
```
With:
```csharp
builder.Services.AddScoped<INotificationService, BlobNotificationService>();
```

`BlobNotificationService` is safe as Scoped (it creates BlobContainerClient per scope, which is lightweight). If the project consistently uses Singleton for infrastructure services that hold no request state, Singleton is also fine — match the existing convention used by `BlobStorageService`.

### 4. Update `appsettings.json`

**Remove:**
```json
"PowerAutomate": {
  "FlowASubmissionUrl": "PLACEHOLDER_FLOW_A_URL",
  "FlowBStatusChangeUrl": "PLACEHOLDER_FLOW_B_URL"
}
```

**Replace with:**
```json
"PowerAutomate": {
  "SubmissionContainerName": "notification-submitted",
  "StatusChangeContainerName": "notification-status-changed"
}
```

If `appsettings.Development.json` or `appsettings.Production.json` also have PowerAutomate keys, update them consistently.

### 5. Remove `HttpClient` Registration for Notifications (if any)

If `Program.cs` registers a named or typed `HttpClient` specifically for Power Automate notifications (e.g., `AddHttpClient<PowerAutomateNotificationService>(...)`), remove that registration. The blob client does not use HttpClient.

### 6. Verify Build

Run `dotnet build` from the solution root. Ensure:
- Zero errors.
- No remaining references to `PowerAutomateNotificationService` (deleted class).
- No remaining references to `FlowASubmissionUrl` or `FlowBStatusChangeUrl` in code.

### 7. Commit and Push to `dev`

Commit message:
```
feat: replace PowerAutomateNotificationService with BlobNotificationService (Stage 5b)

- Removes HTTP trigger approach (Premium connector, not available on current plan)
- Implements blob-write transport: JSON notification events written to Azure Blob Storage
- Two containers: notification-submitted (Flow A) and notification-status-changed (Flow B)
- INotificationService interface unchanged; TravelRequestService unchanged
- Reuses existing AzureStorage:ConnectionString; removes FlowA/B URL config keys
- Containers auto-created via CreateIfNotExistsAsync on first use
- Non-blocking: exceptions caught/logged, never thrown

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
```

Push to `dev`. Do not touch `main`.

---

## NotificationPayload DTO (for reference)

If the existing `NotificationPayload` class (or equivalent DTO in `TravelRequestWF.Core` or `TravelRequestWF.Infrastructure`) needs to be verified, it should have these properties:

```csharp
public string RequestId { get; set; }
public string EventType { get; set; }
public string EmployeeName { get; set; }
public string EmployeeEmail { get; set; }
public string ManagerName { get; set; }
public string ManagerEmail { get; set; }
public string Destination { get; set; }
public string StartDate { get; set; }   // "yyyy-MM-dd"
public string EndDate { get; set; }     // "yyyy-MM-dd"
public string Purpose { get; set; }
public string Status { get; set; }
public string? Comments { get; set; }
```

Do not add new properties. Do not rename existing ones. The JSON keys in the blob must match what Sam's Power Automate flows expect (same shape as the prior HTTP POST payload).

---

## Out of Scope

- Do NOT modify `INotificationService`.
- Do NOT modify `TravelRequestService`.
- Do NOT modify `BlobStorageService`.
- Do NOT create Power Automate flows (that is Sam's task).
- Do NOT create the Azure Storage containers manually — the code auto-creates them.
- Do NOT add unit tests unless they already exist for `PowerAutomateNotificationService` and you are updating them to cover `BlobNotificationService`.
