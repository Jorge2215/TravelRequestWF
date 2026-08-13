# Stage 4 Workflow — Test Results
**Tester:** Pippin  
**Branch:** dev

---

## Attempt History

| Attempt | Date | Blocking Issue | TC1 | TCs 2–14 |
|---|---|---|---|---|
| #1 | 2026-08-12T20:03:51-03:00 | `BlobStorageService` constructor throws on every DI resolution (all pages 500) | ✅ PASS | ❌ ALL BLOCKED |
| **#2** | **2026-08-12T23:47:34-03:00** | **`Stage4WorkflowFields` migration NOT applied to Azure SQL DB** (`Invalid column name 'SubmittedAt'`) | **✅ PASS** | **❌ ALL BLOCKED** |

---

## Attempt #2 — RE-RUN (2026-08-12T23:47:34-03:00)

**Commit under test:** `311c24f` (Gandalf fix — BlobStorageService constructor guard moved to `UploadDocumentAsync`)
**Build:** `dotnet build TravelRequestWF.slnx` — ✅ 0 errors, 0 warnings

### ⚠️ NEW BLOCKING BUG DISCOVERED

**Bug:** `Stage4WorkflowFields` EF Core migration is PENDING — not applied to the Azure SQL database.

**Migration:** `20260812231909_Stage4WorkflowFields`

**Columns not yet in DB:**
- `TravelRequests.SubmittedAt` (datetime2, not nullable)
- `AuditLogEntries.Details` (nvarchar(max), nullable)

**Symptom:** Any authenticated page that runs a query on `TravelRequests` or `AuditLogEntries` crashes with:
```
Microsoft.Data.SqlClient.SqlException: Invalid column name 'SubmittedAt'.
```

**Evidence:** `GET /Employee/Index` as employee1 → HTTP 500 with SqlException stack trace hitting `TravelRequestService.GetRequestsForEmployeeAsync` at line 174.

**Confirmed via EF tools:** `dotnet ef migrations list` shows `Stage4WorkflowFields (Pending)`.

**Fix needed (by Gandalf):** Run `dotnet ef database update --project src/TravelRequestWF.Infrastructure --startup-project src/TravelRequestWF.Web` against the Azure SQL connection, OR set up a startup migration auto-apply.

---

### Test Case Table (Attempt #2)

| # | Description | Expected | Actual | Result |
|---|---|---|---|---|
| TC1 | Build: 0 errors | 0 errors | 0 errors, 0 warnings | ✅ PASS |
| TC2 | Employee1 login → Employee/Index loads | HTTP 200 | HTTP 500 — `Invalid column name 'SubmittedAt'` (pending migration) | ❌ BLOCKED |
| TC3 | Submit travel request (no file) → Status=Pending | Redirect, Status=Pending | Not reached — blocked at TC2 | ❌ BLOCKED |
| TC4 | Employee/Index shows Pending badge | Badge visible | Not reached | ❌ BLOCKED |
| TC5 | Employee/Detail — details display, no Resubmit | Details page renders | Not reached | ❌ BLOCKED |
| TC6 | Employee2 accesses employee1's Detail → Forbid | HTTP 403 | Not reached | ❌ BLOCKED |
| TC7 | Manager1/Index shows pending request | Request listed | Not reached | ❌ BLOCKED |
| TC8 | Manager2 accesses Manager/Review → Forbid | HTTP 403 | Not reached | ❌ BLOCKED |
| TC9 | Manager1 approves with comment | Status=Approved, audit entry | Not reached | ❌ BLOCKED |
| TC10 | Manager1 rejects with comment | Status=Rejected, audit entry | Not reached | ❌ BLOCKED |
| TC11 | Manager1 returns with comment | Status=Returned | Not reached | ❌ BLOCKED |
| TC12 | Employee1 resubmits Returned → Status=Pending | Status=Pending, "Resubmitted" audit | Not reached | ❌ BLOCKED |
| TC13 | File upload → graceful failure (storage not configured) | No crash; error message | Not reached | ❌ BLOCKED |
| TC14 | Audit trail order (submit→return→resubmit) | Chronological entries | Not reached | ❌ BLOCKED |

---

### Attempt #2 Summary

| Category | Count |
|---|---|
| ✅ PASS | 1 (TC1 Build) |
| ❌ BLOCKED by pending DB migration | 13 (TC2–TC14) |

**Overall Stage 4 verdict (Attempt #2): ❌ NOT TESTABLE — pending `Stage4WorkflowFields` migration blocks all runtime tests.**

**Good news:** The BlobStorageService fix from Gandalf (commit 311c24f) appears correct in code — the constructor no longer performs the connection string validation. That previous blocker should now be resolved once the DB migration is applied.

---

## Attempt #1 — Original Run (2026-08-12T20:03:51-03:00)

### ⚠️ BLOCKING BUG (NOW FIXED by 311c24f)

**Bug:** `BlobStorageService` constructor threw `InvalidOperationException` on every DI resolution, causing HTTP 500 on all workflow pages even when no file upload was attempted.

**Root cause:** Connection string validation was in the constructor; service registered as Scoped → thrown on every request.

**Fix applied:** Gandalf moved validation to `UploadDocumentAsync` (commit 311c24f). ✅

### Code Review (from Attempt #1, still valid)

- **Authorization logic**: Ownership checks (`Forbid()` on EmployeeId/ApproverId mismatch) correctly placed in OnGet handlers.
- **ApproverId assignment**: Auto-set from `Employee.SuperiorId` on submission. Null guard present.
- **State transitions**: All validated before transitioning. Correct state machine.
- **Audit log writes**: Every transition writes `AuditLogEntry` with `TravelRequestId`, `Action`, `ActorId`, `Details`, `Timestamp`.
- **Resubmit**: Validates `Status == Returned`, resets to `Pending`, writes "Resubmitted" audit.
- **Audit ordering**: `GetRequestByIdAsync` includes `.OrderBy(a => a.Timestamp)`.
- **Index queries**: Correctly filtered by `EmployeeId` / `ApproverId`.
