# Pippin — History

## Project Context

- **Project:** TravelRequestWF — a Web Application built on .NET 10 Razor Pages, with Azure SQL Database, and likely some Azure Functions and MS Power Automate flows.
- **Owner:** Jorgito
- **Team cast:** Lord of the Rings universe (Aragorn, Legolas, Gandalf, Merry, Sam, Pippin)

## Learnings

### 2026-08-10T21:28:28-03:00 — Stage 1 Foundation Validation

**What I validated:**
- Pulled origin/dev — already up to date (Gandalf's commit `c3cccfc` was present).
- `dotnet restore` + `dotnet build TravelRequestWF.slnx --no-restore` → **0 errors, 0 warnings**.
- Web app started via `dotnet run --project src/TravelRequestWF.Web` (HTTP port 5199 per launchSettings.json). PID 4732. Confirmed Kestrel responds.
- Root (`/`), `/Employee`, and `/Manager` routes all returned **HTTP 200**.
- `dotnet ef database update` confirmed **no new migrations needed — already up to date** on LocalDB (TravelRequestWFDb_Dev). Migration: `20260811002601_InitialCreate`. Note: EF Tools version 10.0.8 is older than runtime 10.0.10 — minor advisory, not a failure.
- Migration snapshot and migration files confirmed in `src/TravelRequestWF.Infrastructure/Migrations/`.
- `AppDbContext.cs` reviewed: `DeleteBehavior.Restrict` confirmed on all three FKs — Employee self-ref (SuperiorId), TravelRequest→Employee (EmployeeId), TravelRequest→Employee (ApproverId). Cascade-cycle risk properly mitigated.
- `docs/database-setup.md` exists and documents the Azure SQL `dotnet ef database update --connection` command.

**Files of note:**
- `src/TravelRequestWF.Infrastructure/Data/AppDbContext.cs` — FK/DeleteBehavior config
- `src/TravelRequestWF.Infrastructure/Migrations/20260811002601_InitialCreate.cs` — migration
- `src/TravelRequestWF.Web/Properties/launchSettings.json` — HTTP port 5199
- `docs/database-setup.md` — Azure SQL setup instructions

**Gaps found:**
- EF Tools version mismatch (10.0.8 tools vs 10.0.10 runtime). Functional but advisory. Worth a Gandalf fix to keep tools in sync.
- No automated tests exist yet (no test project in solution). Stage 1 scope didn't include tests, but this is the baseline I need to build from.

### 2026-08-11T22:43:57-03:00 — Stage 2 ERD-vs-Schema Validation

**What I validated:**
- `git pull origin dev` — already up to date (Gandalf's `AuditLogDocumentLink` migration was already present).
- `dotnet ef migrations list` (with live Azure SQL connection): both `20260811002601_InitialCreate` and `20260812013905_AuditLogDocumentLink` listed and applied. No pending migrations.
- Read all 4 entity files and `AppDbContext.cs` against the ERD in `.squad/er-diagram.md`.
- All 4 tables present, all ERD columns correctly mapped.
- `AuditLogEntry.TravelRequestId` confirmed nullable; `AuditLogEntry.RequestDocumentId` confirmed added with Restrict FK — Gandalf's migration is correct.
- 5 of 6 FK relationships explicitly configured in `OnModelCreating` with correct `DeleteBehavior`.

**Gaps found:**
- **RequestDocument.TravelRequestId FK delete behavior is NOT explicitly configured.** EF Core defaults to Cascade for required FKs, so deleting a `TravelRequest` will cascade-delete its `RequestDocument` rows. This may be intentional but is undeclared. Routed to Gandalf via decisions inbox.
- `TravelRequest.ApproverId` is non-nullable in the entity (int, not int?), consistent with the decision that ApproverId is always populated from SuperiorId at submission. Not a discrepancy — just documented.
- `DateOnly` used for StartDate/EndDate (not `DateTime`) — correct for DATE columns in Azure SQL.
- `AuditLogEntry.ActorId` is `string` (not `int`) — ERD shows `Usuario` as VARCHAR, so this is a correct and intentional design choice.

**Files produced:**
- `.squad/files/stage2-erd-vs-schema.md` — full comparison table with all 4 sections
- `.squad/decisions/inbox/pippin-stage2-validation.md` — gap report for Gandalf

### 2026-08-11T23:23:00-03:00 — Stage 3 Auth & Role-Based Access Validation

**What I validated:**
- Build: `dotnet build TravelRequestWF.slnx` → **0 errors, 0 warnings** on commit `f9f5541`.
- App started: `dotnet run --project src/TravelRequestWF.Web` on HTTP port 5199, PID 16068.
- Executed all 14 test cases via PowerShell `Invoke-WebRequest` with cookie-session containers.
- **All 14 TCs passed.** No bugs found.

**Key observations:**
- Unauthenticated access to both `/Employee/Index` and `/Manager/Index` correctly redirects to `/Account/Login?ReturnUrl=...` (TC-01, TC-02).
- Login flow works for all 4 seeded users (employee1, employee2, manager1, manager2) — (TC-03, TC-07, TC-13, TC-14).
- Employee role is correctly blocked from Manager pages → AccessDenied (TC-06).
- Manager role is correctly blocked from Employee pages → AccessDenied (TC-09).
- Logout properly clears session; CSRF protection on POST is correct security behavior (TC-10).
- New registration defaults to Employee role (TC-11).
- ReturnUrl preserved after login (TC-12).
- Nav shows `"Hello, <email>"` + role badge (`<span class="badge bg-secondary ms-1">Employee</span>`) — Legolas's UI work confirmed.

**Routing note:**
- `/Manager/Review` has a route constraint `{id:int}` — must be accessed as `/Manager/Review/1` etc. `/Manager/Review` alone returns 404. This is correct and expected; not a bug.

**Logout note:**
- Logout POST without CSRF token → 400 (correct antiforgery protection). Logout form in nav correctly includes CSRF token, so normal UI logout works fine.

**Files produced:**
- `.squad/files/stage3-auth-test-results.md` — full 14-TC results table

### 2026-08-12T20:03:51-03:00 — Stage 4 Workflow Validation Attempt

**What I validated:**
- `git pull origin dev` — already up to date (Gandalf + Legolas both merged).
- `dotnet build TravelRequestWF.slnx` → **0 errors, 0 warnings**. ✅
- App started at `http://localhost:5200` (port 5200). PID 10820 (child of dotnet run PID 8164).
- Login worked correctly for employee1 after discovering field names are `Input.Email` / `Input.Password` (same as Stage 3 pattern — remember this!).
- **Critical bug found IMMEDIATELY on first authenticated page access:** `BlobStorageService` constructor throws `InvalidOperationException` when connection string is placeholder. Registered as Scoped → throws on every request to every workflow page. All 13 workflow TCs blocked.

**Key learning — BlobStorageService registration pattern:**  
If a service validates configuration in its constructor AND is registered as Scoped, it will fail every request scope that resolves it — not just the requests that actually USE the configured resource. Configuration-validation guards must be in the method that actually uses the resource, not in the constructor, unless the service is registered as Singleton AND you want to fail-fast at startup.

**Files produced:**
- `.squad/files/stage4-workflow-test-results.md` — test case table (1 pass, 13 blocked, 1 deferred)
- `.squad/decisions/inbox/pippin-blobstorage-constructor-blocks-workflow.md` — bug report for Gandalf

**Gaps/bugs found:**
- **CRITICAL:** `BlobStorageService` constructor validation blocks ALL workflow pages. Fix: move validation to `UploadDocumentAsync`. Routed to decisions inbox.
- TC14 (file upload with Azure Blob) remains DEFERRED pending real connection string from Jorgito — this was expected.

### 2026-08-12T23:47:34-03:00 — Stage 4 Workflow Validation Re-Run (Attempt #2)

**What I validated:**
- `git pull origin dev` — commit `311c24f` (Gandalf's BlobStorageService fix) was already present.
- `dotnet ef migrations list` → `20260812231909_Stage4WorkflowFields (Pending)` — migration NOT applied to Azure SQL.
- `dotnet build TravelRequestWF.slnx` → **0 errors, 0 warnings**. ✅
- App started at `http://localhost:5199`, PID 18388.
- Login worked for employee1. GET /Employee/Index → **HTTP 500: `Invalid column name 'SubmittedAt'`**.
- Confirmed: `Stage4WorkflowFields` migration adds `TravelRequests.SubmittedAt` and `AuditLogEntries.Details` — both absent from DB. All 13 workflow TCs blocked again.

**Key learnings:**
1. **Two blockers in a row:** First the BlobStorageService DI bug, now the unapplied migration. When multiple bugs block the same test pass, each must be fixed AND the migration applied before tests can run.
2. **Always check `dotnet ef migrations list` before starting workflow tests.** If any migration is `(Pending)`, tests will fail at the SQL layer regardless of code correctness.
3. **Handoff gap:** Gandalf fixed the code bug but did not apply the pending migration to the Azure SQL database before handing off to Pippin for re-testing. Need to make this a standard checklist item in the handoff protocol.
4. **BlobStorageService fix looks correct in code** (constructor no longer validates the connection string per commit 311c24f) — but I couldn't confirm it runtime-tested since the migration blocker hit first.

**Files produced:**
- `.squad/files/stage4-workflow-test-results.md` — updated with Attempt #2 results
- `.squad/decisions/inbox/pippin-pending-migration-stage4.md` — new bug report for Gandalf

**Gaps/bugs found:**
- **CRITICAL (Attempt #2):** `Stage4WorkflowFields` migration pending on Azure SQL — `SubmittedAt` column missing. Fix: `dotnet ef database update`. Routed to decisions inbox.

### 2026-08-13T00:45:00-03:00 — Stage 4 Workflow Validation Re-Run (Attempt #3) — FULL PASS

**What I validated:**
- `git pull origin dev` — already up to date. Gandalf confirmed migration applied to LocalDB.
- `dotnet build TravelRequestWF.slnx` → **0 errors, 0 warnings**. ✅
- App started via `dotnet run --project src/TravelRequestWF.Web` at `http://localhost:5199`, PID 14076.
- All 15 test cases executed and passed. No blockers.

**TC summary:**
- TC1 (build) ✅ — TC2 (Employee/Index) ✅ — TC3 (submit no file, Pending, ApproverId=manager1) ✅
- TC4 (Index shows badge) ✅ — TC5 (Detail, no Resubmit) ✅
- TC6 (Employee2 forbidden from employee1's Detail) ✅ — redirected to /Account/AccessDenied
- TC7 (Manager1/Index shows request) ✅ — TC8 (Manager2 forbidden from Review) ✅ — redirected to /Account/AccessDenied
- TC9 (Approve w/ comment, audit) ✅ — TC10 (Reject w/ comment, audit) ✅
- TC11 (Return w/ comment, Resubmit button appears) ✅ — TC12 (Resubmit → Pending, audit) ✅
- TC13 (file upload → graceful error, no 500) ✅ — TC14 (audit order Submitted→Returned→Resubmitted) ✅
- TC15 (kill process by PID 14076) ✅

**Key learnings — Attempt #3:**
1. **Login URL is /Account/Login**, NOT /Identity/Account/Login (important for future automation).
2. **Submit form field names are bare** (`Destination`, `StartDate`, etc.) — NOT prefixed with model name (e.g., not `TravelRequest.Destination`).
3. **Authorization redirects to /Account/AccessDenied with HTTP 200** — ASP.NET Identity returns 200 on the denial page, so HTTP status alone is insufficient to assert Forbid. Must check final URL OR page content.
4. **Manager/Review form uses `formaction` handlers** (`?handler=Approve`, `?handler=Reject`, `?handler=Return`) — POSTs go to `/Manager/Review/{id}?handler=X`.
5. **Resubmit form uses `action` with handler** (`/Employee/Detail/{id}?handler=Resubmit`).
6. **Audit actor shown as GUID** (Identity UserId), not display name — cosmetic, not a bug.
7. **TC13 upload behavior:** returns to Submit page with error message when blob storage not configured — the request is NOT created. This is the correct isolated-failure behavior.

**Files produced:**
- `.squad/files/stage4-workflow-test-results.md` — updated with Attempt #3 (full pass)

**Gaps/bugs found:**
- **None.** Stage 4 is clean. All authorization, state transitions, audit trail, and error handling working as designed.

### 2026-08-12T22:03:45-03:00 — TC13 Azure Storage E2E Upload Verification (FULL PASS)

**Context:** Jorgito created the real Azure Storage account and updated `appsettings.Development.json` with the real `AzureStorage:ConnectionString`. TC13 was previously deferred (graceful error was acceptable). Now verifying the real end-to-end upload works.

**What I validated:**
- `git pull origin dev` — already up to date.
- `dotnet build TravelRequestWF.slnx` → **0 errors, 0 warnings**. ✅
- App started at `http://localhost:5199`, PID 7216.
- Logged in as employee1@test.com / Employee1!Pass — ✅
- Submitted a new travel request (Paris, France, 2026-09-01 to 2026-09-07) WITH a real file (`test_upload.txt`) attached via multipart form POST using curl.
- Submission redirected to `/Employee` (index) — **no error page**. ✅
- **DB confirmed (TravelRequests):** RequestId=6, Destination="Paris, France", Status=0 (Pending). ✅
- **DB confirmed (RequestDocuments):** Id=1, TravelRequestId=6, FileName="test_upload.txt", BlobUrl=`https://travelrequeststorage.blob.core.windows.net/travel-documents/ce547ee36afb429981ea76309a9a7f8f_test_upload.txt`. Real, well-formed Azure Blob URL. ✅
- **Azure Storage confirmed via `az storage blob list`:** Blob `ce547ee36afb429981ea76309a9a7f8f_test_upload.txt` exists in container `travel-documents`, 79 bytes, content-type `text/plain`, Last Modified 2026-08-13T01:06:28+00:00. ✅
- **Employee/Detail/6 page:** `test_upload.txt` present in document list; Azure Storage URL rendered as an `href` link with the exact blob URL. ✅
- **DB confirmed (AuditLogEntries):** Two entries created for the submission:
  - Id=8, Action=`DocumentUploaded`, TravelRequestId=NULL, RequestDocumentId=1 — Stage 2 "exactly one FK" invariant satisfied. ✅
  - Id=9, Action=`Submitted`, TravelRequestId=6, RequestDocumentId=NULL — ✅
- Stopped PID 7216.

**Key learnings — TC13 real upload:**
1. **BlobStorageService uploads with `PublicAccessType.None`** — the container is private; URLs are correct format but require a SAS token or connection string to access directly. This is correct for a PoC (documents are private).
2. **Blob name format:** `{Guid:N}_{OriginalFileName}` — ensures uniqueness and preserves original name for display.
3. **`az storage blob list` with `--connection-string` flag works** even when `--auth-mode login` fails (which requires Azure CLI auth). The connection string in appsettings.Development.json (masked in source) is sufficient for az CLI verification.
4. **AuditLog MERGE INSERT:** EF Core emits a single MERGE statement for the two AuditLogEntry rows (DocumentUploaded + Submitted) — efficient and correct.

**Verdict:** TC13 FULL PASS. Azure Blob Storage integration is working end-to-end with real storage. No bugs found.

### 2026-08-13T21:39:42-03:00 — Stage 5 Notification Integration Validation (PASS with one doc note)

**What I validated:**
- `git pull origin dev` — already up to date.
- `dotnet build TravelRequestWF.slnx` → **0 errors, 0 warnings**. ✅
- Reviewed `PowerAutomateNotificationService`: explicit PLACEHOLDER detection + try/catch on HTTP call → non-blocking confirmed by code review.
- App started at `http://localhost:5199`, PID 6984.
- Logged in as employee1@test.com, submitted a request with mock listener on port 9999 as FlowASubmissionUrl → submission succeeded, redirect to `/Employee`. Log: `Flow A (Submission) notified successfully for RequestId=1006`.
- Logged in as manager1@test.com, approved request → success, redirect to `/Manager`. Log: `Flow B (Status Change) URL not configured — skipping notification for RequestId=1006`.
- Captured exact JSON payload from mock listener — all 12 fields correct.
- Reverted appsettings.Development.json to PLACEHOLDER. Stopped PID 6984.
- Cross-checked Sam's setup guide vs Gandalf's NotificationPayload: all 12 fields match. One minor doc mismatch: Sam's sample shows RequestId as UUID, actual value is integer string (int PK.ToString()). Not a runtime bug.

**Key learnings — Stage 5:**
1. **`System.Net.HttpListener` in a PowerShell Runspace** is a clean way to capture HTTP payloads locally without installing any extra tooling.
2. **Non-blocking pattern:** PLACEHOLDER-skip is in the `return` path (no exception). HTTP failures are caught and logged at Error level — workflow proceeds regardless.
3. **RequestId is integer string, not UUID.** Any future test or sample payload generator must use integers, not GUIDs.
4. **Login URL is /Account/Login** (confirmed again). Submit fields are bare, not prefixed.
5. **Manager Review route is `/Manager/Review/{id}` (path segment)**, not query string `?id=`. Handler is `?handler=Approve`.
6. **Log entries are visible at Information level** — not silently swallowed. Skips and successes both log.

**Files produced:**
- `.squad/files/stage5-notification-test-results.md`
- `.squad/decisions/inbox/pippin-requestid-uuid-mismatch.md`

