# Stage 4 Workflow — Test Results
**Tester:** Pippin  
**Date:** 2026-08-12T20:03:51-03:00  
**Branch:** dev  
**Build:** `dotnet build TravelRequestWF.slnx` — ✅ 0 errors, 0 warnings

---

## ⚠️ BLOCKING BUG DISCOVERED

**Before any workflow tests could run, a critical bug was found that blocks ALL Stage 4 workflow pages:**

### Bug: `BlobStorageService` Constructor Throws on ALL Workflow Requests

**Root cause:** `BlobStorageService` validates the Azure Storage connection string inside its **constructor** (line 17 of `BlobStorageService.cs`). When the placeholder value `"YOUR_AZURE_STORAGE_CONNECTION_STRING_HERE"` is present (as it is in both `appsettings.json` and `appsettings.Development.json`), the constructor throws:

```
System.InvalidOperationException: Azure Storage connection string is not configured.
```

**Why this is a bug (not an expected Azure Storage deferral):** The service is registered as **Scoped** (`builder.Services.AddScoped<IBlobStorageService, BlobStorageService>()`). This means it is instantiated **on every request** that resolves `ITravelRequestService` (which depends on `IBlobStorageService`). The DI container propagates the constructor exception, causing a 500 on **every authenticated page that uses `ITravelRequestService`** — including pages that never upload files: `Employee/Index`, `Employee/Submit`, `Employee/Detail`, `Manager/Index`, `Manager/Review`.

**Decision contract (from `aragorn-stage4-workflow-scope.md` Decision 1):**
> "The app will throw a clear `InvalidOperationException` at startup or on first upload attempt if the connection string is the placeholder value — NOT a silent stub."

The intent was to throw **at startup** (if registered as singleton) OR **on first upload attempt** (deferred). The actual implementation throws on every request, blocking all workflow pages regardless of whether the user tries to upload a file.

**Impact:** 100% of Stage 4 workflow tests blocked. The app is functionally untestable with the placeholder Azure Storage config.

**Evidence:** Authenticated request to `GET /Employee/Index` returns HTTP 500 with full stack trace:
```
System.InvalidOperationException: Azure Storage connection string is not configured...
  at BlobStorageService..ctor(IOptions`1 options)
  ...
  at Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure.DefaultPageModelFactoryProvider...
```

**Fix needed (by Gandalf):** Move the connection string validation from the constructor to the `UploadDocumentAsync` method. The constructor should only store `_options`. This way, the service resolves without throwing, non-upload pages work, and upload attempts fail with a clear error only when a file is actually being uploaded.

---

## Azure Blob Storage Note

The Azure Storage connection string is a placeholder. File upload tests would be **DEFERRED** (not failures) per the test brief. However, because of the bug above, even non-upload tests cannot run.

---

## Test Case Table

| # | Description | Steps | Expected | Actual | Result |
|---|---|---|---|---|---|
| TC1 | Build passes with 0 errors | Run `dotnet build TravelRequestWF.slnx` | 0 errors, 0 warnings | 0 errors, 0 warnings | ✅ PASS |
| TC2 | Employee1 submits travel request (no file) | Login as employee1; POST to /Employee/Submit with Destination, dates, Purpose | Success, redirect to Employee/Index, Status=Pending, ApproverId=manager1's EmployeeId | ❌ 500 — `BlobStorageService` constructor throws on DI resolution (blocking bug) | ❌ BLOCKED |
| TC3 | Employee/Index shows new request with Pending badge | After submit, GET /Employee/Index | Request appears with Pending status badge | ❌ 500 — same blocking bug | ❌ BLOCKED |
| TC4 | Employee/Detail shows details, no Resubmit button | GET /Employee/Detail/{id} | Request details visible, no Resubmit button (Status=Pending) | ❌ 500 — same blocking bug | ❌ BLOCKED |
| TC5 | Employee2 accessing employee1's request → Forbid | Login as employee2; GET /Employee/Detail/{employee1's request id} | HTTP 403 Forbidden (ownership check) | ❌ 500 — same blocking bug (DI fails before authorization check runs) | ❌ BLOCKED |
| TC6 | Manager1/Index shows employee1's pending request | Login as manager1; GET /Manager/Index | Employee1's request appears (ApproverId match) | ❌ 500 — same blocking bug | ❌ BLOCKED |
| TC7 | Manager2 accessing request assigned to manager1 → Forbid | Login as manager2; GET /Manager/Review/{id} | HTTP 403 Forbidden (ownership check) | ❌ 500 — same blocking bug | ❌ BLOCKED |
| TC8 | Manager1 approves request with comment | POST /Manager/Review/{id}?handler=Approve with Comments | Status=Approved, AuditLogEntry created with comment | ❌ 500 — same blocking bug | ❌ BLOCKED |
| TC9 | Manager1 rejects second request with comment | Submit 2nd request; Manager1 POST reject with comment | Status=Rejected, AuditLogEntry with comment | ❌ 500 — same blocking bug | ❌ BLOCKED |
| TC10 | Manager1 returns third request with comment | Submit 3rd request; Manager1 POST return with comment | Status=Returned, AuditLogEntry with comment | ❌ 500 — same blocking bug | ❌ BLOCKED |
| TC11 | Employee1 resubmits returned request | Get returned request Detail; click Resubmit | Status=Pending, new AuditLogEntry "Resubmitted" | ❌ 500 — same blocking bug | ❌ BLOCKED |
| TC12 | Submit as manager1 (no SuperiorId) | Login as manager1; attempt submit | Graceful error "No approver assigned" | ❌ 500 — same blocking bug | ❌ BLOCKED |
| TC13 | Audit trail ordering (submit→return→resubmit) | View Detail for multi-state request | Entries in chronological order | ❌ 500 — same blocking bug | ❌ BLOCKED |
| TC14 | File upload with Azure Blob (deferred) | Submit with file attached | Upload attempt | DEFERRED — no Azure Storage connection string (expected, not a bug) | ⏸️ DEFERRED |

---

## Code Review: Untested Path Assessment

While the blocking bug prevents runtime testing, the following was verified by **code review** of the implementation:

### ✅ Looks Correct (Code Review)
- **Authorization logic** (`Detail.cshtml.cs`, `Review.cshtml.cs`): Ownership checks (`Request.EmployeeId != employeeId` → `Forbid()`, `Request.ApproverId != info.employeeId` → `Forbid()`) are correctly placed in `OnGet`.
- **ApproverId assignment**: `TravelRequestService.SubmitRequestAsync` reads `employee.SuperiorId` and assigns it to `request.ApproverId`. Null guard throws descriptive error.
- **State transitions**: All methods validate current status before transitioning. Correct transitions: Pending→Approved, Pending→Rejected, Pending→Returned, Returned→Pending.
- **Audit log writes**: Every state transition writes an `AuditLogEntry` with `TravelRequestId`, `Action`, `ActorId`, `Details` (for comments), `Timestamp`.
- **Resubmit**: `ResubmitRequestAsync` validates `Status == Returned`, resets to `Pending`, writes "Resubmitted" audit entry.
- **`GetRequestByIdAsync`**: Includes `AuditLog.OrderBy(a => a.Timestamp)` — audit trail correctly ordered.
- **Index queries**: `GetRequestsForEmployeeAsync` filters by `EmployeeId`; `GetRequestsForManagerAsync` filters by `ApproverId`. Correct.
- **`[Authorize(Roles = ...)]`** present on all page models.

### ⚠️ Requires Fix
- **`BlobStorageService` constructor validation**: Must move to `UploadDocumentAsync` (see bug above).

### ℹ️ Code Note
- `TravelRequestService.SubmitRequestAsync` saves the `TravelRequest` to DB first (getting its `Id`), then loops through `dto.Documents` calling `_blob.UploadDocumentAsync`. Since `BlobStorageService` is registered Scoped and its constructor throws, the save-before-upload logic is never exercised. Once the bug is fixed, the sequence is: (1) save request, (2) per document: upload to blob → save RequestDocument → write DocumentUploaded audit, (3) write Submitted audit. This is sound but means a partial failure during document upload (blob succeeds, DB save fails) could leave orphaned blobs — acceptable for PoC.

---

## Summary

| Category | Count |
|---|---|
| ✅ PASS | 1 (TC1 Build) |
| ❌ BLOCKED by Bug | 13 (TC2–TC13) |
| ⏸️ DEFERRED | 1 (TC14 file upload — expected) |

**Overall Stage 4 verdict: ❌ NOT TESTABLE — blocked by critical DI/BlobStorageService bug.**

A single-line fix (move validation from constructor to `UploadDocumentAsync`) would unblock all 13 blocked tests.
