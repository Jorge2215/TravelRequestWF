# Gandalf — Task Brief: Stage 4 Workflow Backend
**Assigned by:** Aragorn  
**Date:** 2026-08-12T20:04:41-03:00  
**Branch:** `dev`

---

## Your Scope

You own ALL C# code: services, DI wiring, and PageModel code-behind (`.cshtml.cs`). Legolas owns only `.cshtml` markup. Keep your interface clean — Legolas will bind Razor forms to exactly the properties you expose.

---

## 1. Add NuGet Package

```
Azure.Storage.Blobs
```

Add to `TravelRequestWF.Infrastructure.csproj`.

---

## 2. Configuration (appsettings.json in Web project)

Add to `appsettings.json`:
```json
"AzureStorage": {
  "ConnectionString": "YOUR_AZURE_STORAGE_CONNECTION_STRING_HERE",
  "ContainerName": "travel-documents"
}
```

Add to `appsettings.Development.json` the same keys (same placeholder). Document in a comment that Jorgito must replace the placeholder.

---

## 3. IBlobStorageService / BlobStorageService

**Location:** `TravelRequestWF.Infrastructure/Services/`

```csharp
// IBlobStorageService.cs
public interface IBlobStorageService
{
    Task<string> UploadDocumentAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct = default);
}
```

`BlobStorageService` implementation:
- Constructor takes `IOptions<AzureStorageOptions>` (a new `AzureStorageOptions` POCO with `ConnectionString` and `ContainerName`).
- Use `BlobServiceClient` → `GetBlobContainerClient(containerName)` → `CreateIfNotExistsAsync` → `GetBlobClient(uniqueName)` → `UploadAsync`.
- Generate a unique blob name: `$"{Guid.NewGuid():N}_{Path.GetFileName(fileName)}"`.
- Return the blob URI as a string (`blobClient.Uri.ToString()`).
- If `ConnectionString` is the placeholder string `"YOUR_AZURE_STORAGE_CONNECTION_STRING_HERE"`, throw `InvalidOperationException("Azure Storage connection string is not configured. Set AzureStorage:ConnectionString in appsettings.json.")` on construction.

**AzureStorageOptions:**
```csharp
public class AzureStorageOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string ContainerName { get; set; } = "travel-documents";
}
```

---

## 4. DTOs

**Location:** `TravelRequestWF.Infrastructure/Services/`

```csharp
public record SubmitRequestDto(
    string Destination,
    DateOnly StartDate,
    DateOnly EndDate,
    string Purpose,
    IReadOnlyList<(Stream Stream, string FileName, string ContentType)> Documents
);
```

---

## 5. ITravelRequestService / TravelRequestService

**Location:** `TravelRequestWF.Infrastructure/Services/`

### Interface

```csharp
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
```

(`actorUserId` = `ApplicationUser.Id` string, used for audit log `ActorId`.)

### TravelRequestService implementation rules

**SubmitRequestAsync:**
1. Load `Employee` by `employeeId`. Throw `InvalidOperationException` if not found.
2. If `employee.SuperiorId == null`, throw `InvalidOperationException("No approver assigned to your account. Contact HR.")`.
3. Create `TravelRequest` with `Status = TravelRequestStatus.Pending`, `ApproverId = employee.SuperiorId.Value`, `SubmittedAt = DateTime.UtcNow` (add `SubmittedAt` field — see schema note below).
4. For each document in `dto.Documents`: call `IBlobStorageService.UploadDocumentAsync`, create `RequestDocument` with `FileName` and `BlobUrl`, add to context. Write `AuditLogEntry` with `Action = "DocumentUploaded"`, `RequestDocumentId = doc.Id`, `TravelRequestId = null`, `ActorId = actorUserId`.
5. Write `AuditLogEntry` with `Action = "Submitted"`, `TravelRequestId = request.Id`, `RequestDocumentId = null`, `ActorId = actorUserId`.
6. `SaveChangesAsync`. Return the new `TravelRequest`.

**ApproveRequestAsync:**
1. Load request. Throw `KeyNotFoundException` if not found.
2. Verify `request.ApproverId == managerEmployeeId`. Throw `UnauthorizedAccessException` if mismatch.
3. Verify `request.Status == TravelRequestStatus.Pending`. Throw `InvalidOperationException("Only Pending requests can be approved.")` otherwise.
4. Set `Status = Approved`. Write audit log `Action = "Approved"`, include `comments` in `AuditLogEntry.Details` (add nullable `Details` field — see schema note). `SaveChangesAsync`.

**RejectRequestAsync:** Same pattern as Approve. Valid from Pending only. Action = "Rejected".

**ReturnRequestAsync:** Same pattern. Valid from Pending only. Action = "Returned".

**ResubmitRequestAsync:**
1. Load request. Verify `request.EmployeeId == employeeId`. Throw `UnauthorizedAccessException` if mismatch.
2. Verify `request.Status == TravelRequestStatus.Returned`. Throw `InvalidOperationException("Only Returned requests can be resubmitted.")` otherwise.
3. Set `Status = Pending`. Write audit log `Action = "Resubmitted"`. `SaveChangesAsync`.

**GetRequestsForEmployeeAsync:** EF query filtered by `TravelRequestId == employeeId`, include `RequestDocuments` and `Employee` navigation. Order by `SubmittedAt` descending.

**GetRequestsForManagerAsync:** EF query filtered by `ApproverId == managerEmployeeId`, include `Employee` navigation. Order by `SubmittedAt` descending.

**GetRequestByIdAsync:** Load by id, include `RequestDocuments`, `AuditLogEntries` (order by Timestamp), `Employee`, `Approver` navigations.

---

## 6. Schema Notes — Possible Migration Needed

Check whether these fields already exist on the entities. If NOT, create a new EF Core migration:

- `TravelRequest.SubmittedAt` (`DateTime`, UTC) — set on submission
- `AuditLogEntry.Details` (`string?`, nullable) — for manager comments on Approve/Reject/Return
- `AuditLogEntry.RequestDocumentId` (`int?`, nullable FK → RequestDocument) — check if this exists from Stage 2

If `RequestDocumentId` already exists on `AuditLogEntry` and the existing migration already has it, skip that part. Do NOT re-add it.

Run `dotnet ef migrations add Stage4WorkflowFields` only if any of the above fields are missing. Apply with `dotnet ef database update` (or note Jorgito must run it).

---

## 7. DI Registration (Program.cs in Web project)

```csharp
// AzureStorage options
builder.Services.Configure<AzureStorageOptions>(
    builder.Configuration.GetSection("AzureStorage"));

// Services
builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();
builder.Services.AddScoped<ITravelRequestService, TravelRequestService>();
```

---

## 8. PageModel Code-Behind (.cshtml.cs files)

Implement full `OnGet`/`OnPost` logic. Inject `ITravelRequestService`, `UserManager<ApplicationUser>`. Helper: get current user's `EmployeeId`:
```csharp
var user = await _userManager.GetUserAsync(User);
var employeeId = user?.EmployeeId ?? throw new InvalidOperationException("User not linked to Employee.");
```

### Pages/Employee/Submit.cshtml.cs

**Properties exposed (Legolas binds to these):**
```csharp
[BindProperty] public string Destination { get; set; } = "";
[BindProperty] public DateOnly StartDate { get; set; }
[BindProperty] public DateOnly EndDate { get; set; }
[BindProperty] public string Purpose { get; set; } = "";
[BindProperty] public List<IFormFile> Documents { get; set; } = new();
public string? ErrorMessage { get; set; }
public string? SuccessMessage { get; set; }
```

**OnGetAsync:** Returns Page().

**OnPostAsync:**
1. Validate ModelState. If invalid, return Page().
2. Validate `StartDate <= EndDate`. Add ModelState error if not.
3. Get current user EmployeeId.
4. Build `SubmitRequestDto` (convert `IFormFile` list to document tuples via `file.OpenReadStream()`).
5. Call `_travelRequestService.SubmitRequestAsync(employeeId, userId, dto)`.
6. Catch `InvalidOperationException` → set `ErrorMessage`, return Page().
7. On success → `RedirectToPage("/Employee/Index")`.

### Pages/Employee/Index.cshtml.cs

**Properties:**
```csharp
public IReadOnlyList<TravelRequest> Requests { get; set; } = Array.Empty<TravelRequest>();
```

**OnGetAsync:** Load via `GetRequestsForEmployeeAsync(employeeId)`. Assign to `Requests`.

### Pages/Employee/Detail.cshtml.cs

**Route:** `@page "{id:int}"`

**Properties:**
```csharp
public TravelRequest? Request { get; set; }
public bool CanResubmit => Request?.Status == TravelRequestStatus.Returned;
public string? ErrorMessage { get; set; }
```

**OnGetAsync(int id):** Load via `GetRequestByIdAsync(id)`. Check `Request.EmployeeId == employeeId`, else `return Forbid()`.

**OnPostResubmitAsync(int id):** Call `ResubmitRequestAsync(id, employeeId, userId)`. Catch exceptions → `ErrorMessage`. On success → redirect to same page.

### Pages/Manager/Index.cshtml.cs

**Properties:**
```csharp
public IReadOnlyList<TravelRequest> Requests { get; set; } = Array.Empty<TravelRequest>();
```

**OnGetAsync:** Load via `GetRequestsForManagerAsync(employeeId)`.

### Pages/Manager/Review.cshtml.cs

**Route:** `@page "{id:int}"`

**Properties:**
```csharp
public TravelRequest? Request { get; set; }
[BindProperty] public string? Comments { get; set; }
public string? ErrorMessage { get; set; }
```

**OnGetAsync(int id):** Load via `GetRequestByIdAsync(id)`. Check `Request.ApproverId == employeeId`, else `return Forbid()`.

**OnPostApproveAsync(int id):** Call `ApproveRequestAsync`. On success → redirect to Manager/Index.

**OnPostRejectAsync(int id):** Call `RejectRequestAsync`. On success → redirect to Manager/Index.

**OnPostReturnAsync(int id):** Call `ReturnRequestAsync`. On success → redirect to Manager/Index.

All three post handlers catch `InvalidOperationException` / `UnauthorizedAccessException` → set `ErrorMessage`, reload Request, return Page().

---

## 9. Authorize Attributes

All Employee pages: `[Authorize(Roles = "Employee")]`  
All Manager pages: `[Authorize(Roles = "Manager")]`  
(These may already be on the stubs — verify and keep.)

---

## 10. Coordination with Legolas

Legolas will bind `.cshtml` markup to exactly the property names above. Do NOT rename properties after publication of this brief without notifying Legolas. The named handlers (`OnPostApproveAsync`, `OnPostRejectAsync`, `OnPostReturnAsync`, `OnPostResubmitAsync`) correspond to `asp-page-handler` values `"Approve"`, `"Reject"`, `"Return"`, `"Resubmit"` in the Razor forms.

---

## 11. Validation

After implementation:
1. `dotnet build` — must succeed with 0 errors.
2. If a migration was added, document the migration name.
3. Smoke test by running the app and submitting a request as employee1, then approving as manager1.
