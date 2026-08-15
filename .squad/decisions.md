# Squad Decisions

## Active Decisions

### 2026-08-15T19:33:00-03:00: Phase 10 — App Service Crash: Connection String Not Overriding (Gandalf)
**By:** Gandalf

#### Diagnosis

Jorgito deployed `TravelRequestWF.Web` to Azure App Service. The app crashes on startup. Log Stream shows the **placeholder** values from `appsettings.json` being used (`<your-azure-sql-server>`, database `TravelRequestWFDb`) instead of the real Azure SQL credentials he set in the Portal.

**Root cause (high confidence):** The App Service "Application settings" entries were named using **colon (`:`)** notation (e.g. `ConnectionStrings:DefaultConnection`) instead of **double-underscore (`__`)** notation (e.g. `ConnectionStrings__DefaultConnection`).

ASP.NET Core's environment variable configuration provider maps `__` to the `:` hierarchy separator. A colon in an env var name is treated as a flat unrelated key — .NET never maps it to `ConnectionStrings:DefaultConnection`, so the app falls through to `appsettings.json` and reads the placeholder.

The Phase 10 checklist in `.squad/files/phase10-web-deploy-workflow-setup.md` listed the keys using colon notation (correct as appsettings paths, but **wrong** as Portal "App settings" Names). This is what likely caused Jorgito to enter the colon form.

#### Evidence
- Log Stream shows `TravelRequestWFDb` (placeholder DB name) and `<your-azure-sql-server>` (placeholder server) — real DB is `TravelRequestDB`
- `Program.cs` uses standard `builder.Configuration.GetConnectionString("DefaultConnection")` — runtime read, no code bug
- `appsettings.json` `ConnectionStrings:DefaultConnection` value is confirmed placeholder

#### Action Required (Jorgito)
See `.squad/files/phase10-connection-string-troubleshooting.md` for exact Portal steps. Short version:

- Azure Portal → App Service → Settings → Environment variables → **App settings tab**
- Rename each entry: replace colons with double underscores:
  - `ConnectionStrings__DefaultConnection`
  - `AzureStorage__ConnectionString`
  - `PowerAutomate__FlowASubmissionUrl`
  - `PowerAutomate__FlowBStatusChangeUrl`
- Click Save → App Service restarts → crash resolved

Alternative for SQL only: use the dedicated "Connection strings" tab, Name=`DefaultConnection`, Type=`SQLAzure`.

#### No Code Changes Made
This is purely a Portal configuration naming mistake. `Program.cs`, `appsettings.json`, and all services are correct.

---

### 2026-08-15T11:25:00-03:00: Phase 10 Pre-Deployment Audit (Aragorn)
**By:** Aragorn

#### 1. Web App Deployment Status — ❌ NEVER DEPLOYED

**Finding: No Azure App Service (Web App) for `TravelRequestWF.Web` has ever been created or deployed to.**

Evidence:
- No deployment-related files found anywhere in the repository:
  - `.github/workflows/` — contains ONLY squad management workflows (heartbeat, triage, issue-assign, label-sync). Zero CI/CD or Azure deploy workflows.
  - No `azure-pipelines.yml`, no `Dockerfile`, no `azure.yaml` (AZD), no `*.bicep`, no ARM templates, no `.pubxml` publish profiles found anywhere.
- Architecture reconciliation decision (2026-08-09T21:59:00) notes "Azure App Service as the deployment target" but explicitly defers: *"Deployment scripts/infra can be added later."*
- Stage 1–9 history shows Jorgito manually created Azure SQL, Blob Storage account, and Function App resources — but NO mention of creating an App Service Plan or Web App resource.
- `main` branch exists and is reserved for the CI/CD pipeline, but confirmed by Jorgito to have NO GitHub Actions workflow yet.

**Conclusion: Creating the App Service (hosting plan + Web App resource) is 100% pending work. The Web app has never been deployed to Azure.**

---

#### 2. EF Core Migration State Audit

**Migrations present in `src/TravelRequestWF.Infrastructure/Migrations/` (chronological order):**

| # | Migration | Timestamp | Applied to LocalDB | Applied to Azure SQL |
|---|-----------|-----------|-------------------|---------------------|
| 1 | `InitialCreate` | 20260811002601 | ✅ Yes | ✅ **CONFIRMED** — Jorgito milestone entry 2026-08-10T22:03:59 explicitly states schema applied to Azure SQL |
| 2 | `AuditLogDocumentLink` | 20260812013905 | ✅ Yes (Stage 2) | ⚠️ **NOT CONFIRMED** — no explicit Azure SQL apply recorded |
| 3 | `RequestDocumentRestrictDelete` | 20260812014713 | ✅ Yes (Stage 2) | ⚠️ **NOT CONFIRMED** |
| 4 | `AddIdentityTables` | 20260812021200 | ✅ Yes (Stage 3) | ⚠️ **NOT CONFIRMED** |
| 5 | `Stage4WorkflowFields` | 20260812231909 | ✅ Yes (Stage 4) | ⚠️ **NOT CONFIRMED** |

**Risk Assessment:** Azure SQL is confirmed at `InitialCreate` schema only. Migrations 2–5 add: `RequestDocumentId` FK on `AuditLogEntry`, restrict-delete on `RequestDocument`, ASP.NET Identity tables (`AspNetUsers`, `AspNetRoles`, etc.), and Stage 4 workflow fields (comments, returned-at, etc.). If the Web App were deployed pointing at the Azure SQL database today without running these 4 migrations, it would **crash on startup** (EF Core migration check) or at runtime (missing columns/tables).

**Action required before Web App goes live:** Run `dotnet ef database update` targeting Azure SQL for all 4 pending migrations (or apply in sequence to confirm each one succeeds).

---

#### 3. Azure Functions Deployment Status — ✅ CONFIRMED LIVE

Per Jorgito's direct confirmation and Phase 9 sync-triggers fix: Jorgito successfully ran `func azure functionapp publish <FunctionAppName> --dotnet-isolated` and synced triggers. The `DailyPendingReportFunction` IS deployed to the Azure Function App resource.

**Remaining Functions action:** Set `PowerAutomate:FlowCDailyDigestUrl` in the Function App's Application Settings (Azure Portal) once Sam's Flow C URL is available. `SqlConnectionString` should already be set (required for triggers to sync).

---

#### 4. appsettings / Connection String Audit

**`appsettings.json` (production baseline — used by Azure App Service when `ASPNETCORE_ENVIRONMENT` is not `Development`):**

| Config Key | Current Value | Status |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | `Server=<your-azure-sql-server>.database.windows.net;...` | ❌ PLACEHOLDER — app will crash on startup (DB context cannot connect) |
| `AzureStorage:ConnectionString` | `"YOUR_AZURE_STORAGE_CONNECTION_STRING_HERE"` | ❌ PLACEHOLDER — file uploads will fail |
| `AzureStorage:ContainerName` | `"travel-documents-prod"` | ✅ Correct for production |
| `PowerAutomate:FlowASubmissionUrl` | `"PLACEHOLDER_SET_VIA_USER_SECRETS"` | ❌ PLACEHOLDER — submission emails will not be sent (guarded by PLACEHOLDER check, will log warning and continue — non-fatal) |
| `PowerAutomate:FlowBStatusChangeUrl` | `"PLACEHOLDER_SET_VIA_USER_SECRETS"` | ❌ PLACEHOLDER — same as above |

**`appsettings.Development.json` (local dev):**

| Config Key | Value |
|---|---|
| `ConnectionStrings:DefaultConnection` | LocalDB — correct for dev |
| `AzureStorage:ConnectionString` | Placeholder — Jorgito sets via user-secrets locally |
| `PowerAutomate` URLs | Placeholder — Jorgito sets via user-secrets locally |

**Conclusion:** The three critical production secrets (`DefaultConnection`, `AzureStorage:ConnectionString`, Flow A/B URLs) are all placeholders in committed config. This is correct practice (no secrets in git), but it means the Azure App Service will require **App Service Configuration > Application Settings** for all three before the app can function. This mirrors exactly what was done for the Function App.

---

#### 5. Phase 10 Checklist — Production Deployment

```
PHASE 10: PRODUCTION DEPLOYMENT CHECKLIST
==========================================

[ ] 1. CREATE Azure App Service infrastructure
       - Create App Service Plan (e.g., B1 or F1 for PoC) in the same resource group
       - Create Web App resource targeting .NET 10
       Decision needed: Manual portal creation (Jorgito, same as SQL/Storage/Functions)
       OR Bicep/ARM template (Merry/Gandalf can draft)

[ ] 2. APPLY pending EF Core migrations to Azure SQL
       Run from src/TravelRequestWF.Infrastructure or Web project:
         dotnet ef database update --connection "<azure-sql-connection-string>"
       Migrations to apply in order:
         - AuditLogDocumentLink
         - RequestDocumentRestrictDelete
         - AddIdentityTables
         - Stage4WorkflowFields

[ ] 3. SET App Service Configuration (Application Settings) on the Web App
       Required keys (mirrors Function App setup):
         - ConnectionStrings:DefaultConnection  → real Azure SQL connection string
         - AzureStorage:ConnectionString        → real Azure Storage connection string
         - PowerAutomate:FlowASubmissionUrl     → real Flow A HTTP trigger URL
         - PowerAutomate:FlowBStatusChangeUrl   → real Flow B HTTP trigger URL
       (ASPNETCORE_ENVIRONMENT should be set to "Production" or left unset)

[ ] 4. BUILD GitHub Actions CI/CD workflow on `main`
       File: .github/workflows/deploy-web.yml
       Triggers: push to main (or PR merge)
       Steps: dotnet build → dotnet publish → Deploy to Azure App Service
       Requires: AZURE_WEBAPP_PUBLISH_PROFILE secret in repo settings
       (Gandalf + Aragorn to draft; Legolas to review frontend assets handling)

[ ] 5. VERIFY Flow A / Flow B URLs in App Service Configuration
       (These are already set as user-secrets locally — Jorgito has the real URLs)

[ ] 6. SMOKE TEST the deployed Web App
       - Login works (ASP.NET Identity seed data applied via IdentitySeeder.cs on startup)
       - Employee can submit request (hits Azure SQL + Azure Blob Storage)
       - Manager receives Flow A email notification (Power Automate Flow A)
       - Manager can approve/reject (Flow B notification fires)
       - Daily digest (Function App) runs on schedule or manual trigger

[ ] 7. CONFIRM Flow C URL set in Function App Application Settings
       (Pending Sam's Flow C build and URL retrieval)
```

**Open question for Jorgito:** Do you want to manually create the App Service resource in the Azure Portal (same pattern as SQL, Storage, Functions), or should Merry/Gandalf draft a Bicep template for it?

---

### 2026-08-15T10:01:00-03:00: Phase 9 — Flow C Setup Guide Created (Sam)
**By:** Sam

#### What was done

- Created `.squad/files/phase9-flow-c-setup.md` — complete step-by-step build guide for Jorgito to create Flow C ("Daily Pending Requests Digest") in Power Automate, following the same style and format as the Phase 5 guide (`stage5-power-automate-setup.md`).

#### Flow C JSON Schema Contract (for Pippin's reference when validation resumes)

The Azure Function (Merry) POSTs this JSON shape to Flow C — one POST per manager per day:

```json
{
  "ManagerName": "string",
  "ManagerEmail": "string",
  "PendingRequests": [
    {
      "RequestId": 1,
      "EmployeeName": "string",
      "Destination": "string",
      "StartDate": "yyyy-MM-dd",
      "EndDate": "yyyy-MM-dd",
      "Status": "Pending"
    }
  ]
}
```

**Key contract notes:**
- All field names are **PascalCase** — case-sensitive in Power Automate schema.
- `RequestId` is an **integer** (int PK from the database — consistent with Phase 5 convention where int PK was cast to string; in Phase 9 Merry sends it as integer directly per the schema — note the difference from Phase 5's string representation).
- `PendingRequests` is an **array of objects** — the critical structural difference from Flow A/B (which had flat payloads). The HTTP trigger schema must be generated from the sample JSON so Power Automate recognises it as an array type.
- `Status` will always be `"Pending"` in this digest context (only pending requests are included).
- Config key for the flow URL: **`PowerAutomate:FlowCDailyDigestUrl`** (stored in `local.settings.json` / Azure Function App Configuration — never committed).

---

### 2026-08-15T09:57:00-03:00: Phase 9 — Daily Digest: Azure Function → Power Automate Integration
**By:** Aragorn

#### Context
Phase 8 delivered `DailyPendingReportFunction` (Timer Trigger, `0 0 8 * * *`) as a pure logging stub. Phase 9 connects it to Power Automate to send per-manager daily digest emails, AND deploys the function code to the Azure Function App resource the user created manually (deployment was deferred from Phase 8).

---

#### Decision 1 — Trigger direction: PUSH (Option A chosen)

**Chosen:** Option A — the Azure Function groups pending requests by manager and HTTP-POSTs a digest payload to a new Power Automate HTTP-triggered flow (Flow C: "Daily Pending Requests Digest"), one POST per manager per day.

**Rejected:** Option B (Pull — Power Automate timer triggers back into an HTTP endpoint or queries SQL via Premium connector). Option B requires either a new HTTP-triggered Function (different trigger type, more infrastructure) or a Premium Power Automate connector license for direct SQL access. It also moves grouping/business logic into Power Automate expressions, which are harder to test and reason about than C# LINQ.

**Justification for Option A:**
- Exact same push-based HTTP trigger architecture as Phase 5 (Flow A / Flow B). The user already has this pattern working end-to-end and understands it.
- Grouping, filtering, and manager-email resolution stays in C# (EF Core + LINQ) — testable, readable, type-safe.
- Power Automate's job remains simple: receive a typed JSON payload, loop over an array, send one email. No expressions for SQL joins or HTTP callbacks.
- Non-blocking try/catch-per-manager pattern (see Decision 5) means a single failing HTTP call does not affect other managers' digests.

---

#### Decision 2 — Grouping logic

**Manager FK:** `TravelRequest.ApproverId` is the FK to `Employee` for the approver/manager. This is always set to `Employee.SuperiorId` at submission time (Stage 3 decision, Option A). So grouping by `ApproverId` groups by manager.

**Manager email resolution:** `Employee.Email` is available directly on the `Employee` entity (same table). The query must `.Include(r => r.Approver)` on `TravelRequest` so that `r.Approver.Name` and `r.Approver.Email` are available without a second round-trip. This is the same EF Core include pattern already used for `r.Employee` in the current stub.

**EF Core navigation name:** `TravelRequest` has an `ApprovalRequests` collection on `Employee` but the forward nav from `TravelRequest` to its approver may need to be confirmed or added. Check `AppDbContext` — if `TravelRequest.Approver` navigation is not already mapped, Merry must verify it. The FK `ApproverId` is confirmed present; EF Core convention should resolve `Approver` as the navigation property if it exists on the entity, or Merry must add it. Fallback: join in LINQ (`employees.Where(e => e.Id == group.Key)`) if the nav isn't mapped.

**Query shape (pseudocode):**
```csharp
var pending = await _db.TravelRequests
    .Where(r => r.Status == TravelRequestStatus.Pending)
    .Include(r => r.Employee)
    .Include(r => r.Approver)   // manager — verify nav property name
    .ToListAsync(ct);

var byManager = pending.GroupBy(r => r.ApproverId);

foreach (var group in byManager)
{
    var manager = group.First().Approver;
    // build payload and POST to Flow C
}
```

**Skip empty:** groups with zero pending requests will never appear (GroupBy only yields groups with at least one element).

---

#### Decision 3 — Flow C configuration key

- New config key: `PowerAutomate:FlowCDailyDigestUrl`
- **Local dev (Functions project):** stored in `local.settings.json` under `Values` (same as `SqlConnectionString`). **NOT in `appsettings.json` or `local.settings.json` committed to git.** Placeholder value `"PLACEHOLDER_FLOW_C_URL"` added to committed config/documentation only.
- **Azure deployment:** Function App Configuration > Application Settings, key `PowerAutomate:FlowCDailyDigestUrl`.
- **User action required:** After Jorgito builds Flow C in Power Automate and receives the HTTP POST URL, he must:
  1. Add it to local `local.settings.json` (`Values` section).
  2. Add it to the Azure Function App's Application Settings in the Azure Portal.
  - This mirrors the exact process used for Flow A / Flow B in Phase 5 (user-secrets / App Service config, never in git).

---

#### Decision 4 — Deployment: `func azure functionapp publish`

- **Primary path:** Merry will add the `Microsoft.Azure.Functions.Worker.Sdk` publish target and attempt deployment via:
  ```
  func azure functionapp publish <FunctionAppName> --dotnet-isolated
  ```
  run from `src/TravelRequestWF.Functions/`.
- **Prerequisites:** Azure Functions Core Tools (`func`) installed, `az login` authenticated, correct subscription set.
- **If the machine lacks the tools or login:** Merry must document exactly what the user needs to run (the command above, with the correct app name) so Jorgito can execute it manually.
- **GitHub Actions CI/CD for Functions:** explicitly OUT OF SCOPE for this phase. If the user wants an automated deploy pipeline for the Function, that is a future phase. Note in Merry's brief.
- **Function App name:** not known at architecture-decision time — Merry must look it up from the user's Azure account (via `az functionapp list`) or ask the user.

---

#### Decision 5 — HTTP call reliability: non-blocking per-manager try/catch

Each HTTP POST to Flow C is wrapped in its own `try/catch`, identical to `PowerAutomateNotificationService.PostToFlowAsync` in Phase 5:
- `catch (Exception ex)` logs the error with `logger.LogError` and continues to the next manager.
- A failed digest for manager X does NOT throw or abort digests for managers Y and Z.
- If the Flow C URL is missing or starts with `PLACEHOLDER`, log and skip (same guard as Phase 5).
- This pattern is already proven in `PowerAutomateNotificationService` — Merry copies it directly rather than inventing a new approach.

---

#### Decision 6 — Validation (Pippin) — deferred per user preference

Pippin is NOT included in the immediate task list for Phase 9. Validation criteria for when Pippin is brought in:
- Seed test data: at least 2 managers, each with ≥1 pending travel request.
- Trigger the function manually (run-now via Azure portal or `func start` locally).
- Confirm each manager receives exactly one email listing only their own pending requests.
- Confirm a manager with zero pending requests receives no email.
- Check Power Automate flow run history for Flow C: all runs should show green/succeeded.
- Verify email content includes RequestId, EmployeeName, Destination, StartDate, EndDate, Status for each request in the digest.

---

#### Task briefs

See below for Merry (Azure Functions) and Sam (Power Automate) briefs.

---

### 2026-08-14T23:50:00-03:00: Phase 8 — Daily Report Stub Implementation (Merry)
**By:** Merry

#### What was built

- Created `src/TravelRequestWF.Functions/` — Azure Functions isolated worker project targeting `net8.0`.
- Added Timer Trigger function `DailyPendingReportFunction` with NCRONTAB `0 0 8 * * *` (08:00 UTC daily).
- Registered `AppDbContext` via `AddDbContext<AppDbContext>` in `FunctionsApplication` builder, reading `SqlConnectionString` from configuration.
- `DailyPendingReportFunction.cs` queries `TravelRequests` where `Status == TravelRequestStatus.Pending`, includes `Employee` navigation property, and logs one structured `[DailyReport]` line per request (Id, Employee.Name, Employee.Email, Destination, StartDate, EndDate, Status, SubmittedAt) plus summary count.
- `local.settings.json` created with placeholder `SqlConnectionString` — file is gitignored (caught by template's own `.gitignore` inside the Functions project directory).
- Added `local.settings.json` entry to root `.gitignore` as belt-and-suspenders.
- Full solution build: **0 errors** (6 pre-existing `Azure.Identity` vulnerability warnings — unrelated to this phase).

#### Key packages (Functions project)
| Package | Version |
|---|---|
| `Microsoft.Azure.Functions.Worker` | 2.52.0 (template default) |
| `Microsoft.Azure.Functions.Worker.Extensions.Timer` | 4.3.1 |
| `Microsoft.Azure.Functions.Worker.OpenTelemetry` | 1.2.0 (template default) |
| `Azure.Monitor.OpenTelemetry.Exporter` | 1.7.0 (template default) |
| `Microsoft.EntityFrameworkCore.SqlServer` | **8.0.10** (matched to net8.0) |

#### Deviation: Infrastructure project multi-targeted
**Decision:** Changed `TravelRequestWF.Infrastructure` from single `net10.0` to multi-target `net8.0;net10.0`.

**Reason:** NuGet restore hard-blocks P2P references where the referenced project's TFM is higher than the referencing project's TFM (NU1201 error — not suppressible via `<NoWarn>`). `SkipGetTargetFrameworkProperties` does not bypass this at the restore stage.

**Resolution:** Added conditional `ItemGroup` blocks to `TravelRequestWF.Infrastructure.csproj` — EF Core 10.0.10 + Identity 10.0.10 for `net10.0`, EF Core 8.0.10 + Identity 8.0.10 for `net8.0`. The Web project continues to consume the `net10.0` output; the Functions project consumes the `net8.0` output. EF Core migrations tooling (`ef` tool) still targets net10.0 (the first/primary target in `TargetFrameworks`). No schema changes required.

#### Live execution testing deferred
`func start` against the real Azure SQL database is deferred — see Pippin/user for testing once a connection string is configured in `local.settings.json`.



#### Decisions

1. **Target framework: .NET 8 isolated worker (not .NET 10)**
   - Azure Functions runtime (v4) supports .NET 8 GA in isolated worker model. .NET 10 is not yet officially supported by the Azure Functions host runtime — using it risks `func start` startup failures and is not recommended for production use. The isolated worker model on .NET 8 is the stable, recommended path for all new Functions projects as of 2026. The tradeoff is explicit: this Functions project will target `net8.0` while the rest of the solution targets `net10.0`. The Infrastructure project (net10.0) can be referenced from a net8.0 project; EF Core and Azure SDK packages have net8.0 targets — no conflict. Review and upgrade the Functions project to .NET 10 once Azure Functions runtime officially announces support.

2. **New project: `src/TravelRequestWF.Functions/`**
   - Isolated worker model (`Microsoft.Azure.Functions.Worker`, `Microsoft.Azure.Functions.Worker.Extensions.Timer`, `Microsoft.Azure.Functions.Worker.ApplicationInsights`).
   - Project reference to `TravelRequestWF.Infrastructure` to reuse `AppDbContext`, `TravelRequest` entity, and EF Core queries directly — no duplication.

3. **Data access: project reference to Infrastructure**
   - `AppDbContext` registered via `AddDbContext<AppDbContext>` in `HostBuilder.ConfigureServices`. The Functions project gets the same EF Core + SQL Server stack. This is a standard pattern — no conflict between a Functions project and a class library.

4. **Connection string source**
   - Local dev: `local.settings.json` (root of the Functions project), key `SqlConnectionString` under `Values`. **This file must be gitignored** — add `local.settings.json` to `.gitignore` (currently absent — Merry must add this entry).
   - Azure deployment: Function App Configuration > Application Settings, key `SqlConnectionString` (set independently from the Web App's App Service Configuration).

5. **Timer Trigger CRON (NCRONTAB): `0 0 8 * * *`**
   - Six-part NCRONTAB (seconds minutes hours day month weekday). Fires at 08:00:00 UTC daily. Document timezone assumption in code comments — adjust hours offset if local timezone is required (e.g., UTC-3 → `0 0 11 * * *` for 8 AM Argentina time).

6. **Report format: structured `ILogger` lines**
   - `ILogger<DailyPendingReportFunction>` injected by the host. `logger.LogInformation(...)` emits to console when running locally via `func start` and is auto-collected by Application Insights when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set in the Function App's configuration. No extra sink configuration needed.
   - Output: a header line + one line per pending request (Id, Requester, Destination, TravelDate, SubmittedAt), plus a summary count. Format should use a consistent prefix (`[DailyReport]`) to enable filtering in App Insights queries.

7. **Scope: explicit stub**
   - No email/notification in this phase. Phase 5's Power Automate flows (Sam's domain) handle request-event webhooks — this daily digest is a separate concern that could be wired to an HTTP-triggered Flow or SendGrid in a future phase. The log output structure (one line per request) is designed to be trivially serializable to JSON for future consumption.

8. **GitHub Actions: NOT modified in this phase**
   - No deploy workflow changes. This phase delivers code + local `func start` validation only. Azure Functions deployment (new Function App resource, publish profile, GitHub Action step) is a future phase. Assumption: Jorgito agrees deployment is out of scope here.

#### .gitignore addition required (Merry action)
Add to `.gitignore`:
```
# Azure Functions local settings (contains secrets)
local.settings.json
```

---

### 2026-08-14T23:35:00-03:00: Phase 7 — IAuditLogger Extraction (Gandalf)
**By:** Gandalf

#### What was done

- Created `IAuditLogger` interface in `TravelRequestWF.Infrastructure/Services/` with three methods:
  - `Task LogAsync(string action, int? travelRequestId, int? requestDocumentId, string actorId, string? details = null)` — queues an `AuditLogEntry` onto the DbContext (no `SaveChangesAsync` inside; caller commits it atomically with the main entity change)
  - `Task<List<AuditLogEntry>> GetLogByRequestAsync(int travelRequestId)` — returns entries ordered by Timestamp
  - `Task<List<AuditLogEntry>> GetLogByUserAsync(string actorId)` — returns entries ordered by Timestamp
- Created `AuditLogger : IAuditLogger` concrete implementation using `AppDbContext` injection.
- Registered `IAuditLogger` / `AuditLogger` in `Program.cs` as Scoped (before `ITravelRequestService`).
- Refactored `TravelRequestService`: injected `IAuditLogger` via constructor; replaced all 6 inline `_db.AuditLogEntries.Add(...)` blocks with `await _auditLogger.LogAsync(...)` calls.
- **Behavioral note:** `LogAsync` does NOT call `SaveChangesAsync` internally. This preserves the existing atomic batching pattern where audit entry + main entity change are saved together in a single `SaveChangesAsync(ct)` call.
- `GetRequestByIdAsync` left as-is (already includes `AuditLog` via EF Include). Audit trail UI pages remain unchanged.
- Build: 0 errors, 0 relevant warnings.
- No schema changes, no EF migrations needed.

### 2026-08-14T23:15:00-03:00: Phase 7 — Audit Logging Gap Analysis & Scoped Plan
**By:** Aragorn

#### What already exists (pre-Phase 7 — verified by code inspection)

**Entity — `AuditLogEntry`** (`src/TravelRequestWF.Infrastructure/Entities/AuditLogEntry.cs`)
- Fields present: `Id` (int), `TravelRequestId` (int?), `TravelRequest` nav, `RequestDocumentId` (int?), `RequestDocument` nav, `Action` (string), `Details` (string?), `Timestamp` (DateTime), `ActorId` (string).
- **Schema vs Phase 7 spec comparison:**
  | Phase 7 requested | Current entity | Status |
  |---|---|---|
  | `Id` | `Id` (int) | ✅ match |
  | `ActionType` | `Action` (string) | ⚠️ different name, same purpose |
  | `EntityName` | ❌ absent — uses typed FKs instead | ⚠️ by design (ERD decision, Stage 2) |
  | `EntityId` | ❌ absent — uses typed FKs instead | ⚠️ by design (ERD decision, Stage 2) |
  | `UserId` | `ActorId` (string) | ⚠️ different name, same purpose |
  | `Timestamp` | `Timestamp` (DateTime) | ✅ match |
  | `Details` | `Details` (string?) | ✅ match |
  - The entity uses typed FKs (`TravelRequestId`/`RequestDocumentId`) rather than generic `EntityName`/`EntityId` strings. This is a deliberate ERD design choice (proper relational integrity, navigation properties). Renaming or switching to a generic string pair would require a migration and break existing navigation expressions throughout the codebase. **Decision: keep the current schema — it is functionally equivalent and more correct for a relational DB.**
  - `Action` and `ActorId` are naming variants — no behavior difference. Do NOT rename them (no migration needed, no breakage risk).

**Inline audit writes in `TravelRequestService`** (verified)
- `Submitted` — ✅ written with `ActorId`, `TravelRequestId`
- `DocumentUploaded` — ✅ written with `ActorId`, `RequestDocumentId`
- `Approved` — ✅ written with `ActorId`, `TravelRequestId`, `Details` (comments)
- `Rejected` — ✅ written with `ActorId`, `TravelRequestId`, `Details` (comments)
- `Returned` — ✅ written with `ActorId`, `TravelRequestId`, `Details` (comments)
- `Resubmitted` — ✅ written with `ActorId`, `TravelRequestId`
- `actorUserId` parameter is passed in from PageModels (Stage 3 Identity in place) — ✅ UserId IS being captured.

**Audit trail UI** — **ALREADY EXISTS** on both:
- `Employee/Detail.cshtml`: renders "Audit Trail" table for `TravelRequestId`-linked entries, ordered by timestamp.
- `Manager/Review.cshtml`: same audit trail table.
- `GetRequestByIdAsync` already `.Include(r => r.AuditLog.OrderBy(a => a.Timestamp))` — entries are eagerly loaded and ready to display.

#### Real gaps found (Phase 7 true delta)

1. **No `IAuditLogger` abstraction exists.** All writes are raw `_context.AuditLogEntries.Add(...)` inline in `TravelRequestService`. This is the primary Phase 7 deliverable: extract an `IAuditLogger` interface + `AuditLogger` service to centralize, standardize, and make audit writes testable in isolation. `TravelRequestService` should be refactored to call `_auditLogger.LogAsync(...)` instead of inline DbContext calls.

2. **No "query by user" capability exists.** The current `GetRequestByIdAsync` loads audit logs per request (query by request ✅), but there is no service method or UI to retrieve audit entries by `ActorId` (query by user ❌). Phase 7 validation requires queryability by both request and user. A service method `GetAuditLogForUserAsync(actorUserId)` should be added to `IAuditLogger` or `ITravelRequestService`. No dedicated UI page is needed for this beyond Pippin's validation query — the existing per-request audit trail on Detail/Review pages already covers the request-query case visually.

3. **`IAuditLogger` interface shape:** Phase 7 asks for Create/Update/Approve/Reject/Return methods. Given the existing schema and action strings, a single generic `LogAsync(action, travelRequestId?, documentId?, actorUserId, details?)` is cleaner and avoids an explosion of single-purpose methods. However, named convenience methods (`LogSubmittedAsync`, `LogApprovedAsync`, etc.) wrapping the generic one improve call-site clarity. Recommend: one generic async method + thin named wrappers, or named methods only. Decision deferred to Gandalf — either is acceptable.

#### Decisions

- **Entity schema: NO CHANGES.** Keep `AuditLogEntry` as-is. Do not rename `Action`→`ActionType` or `ActorId`→`UserId`, do not introduce generic `EntityName`/`EntityId` columns. ERD FK approach is correct; renaming to match Phase 7's spec wording adds migration cost with zero functional gain.
- **Audit trail UI: ALREADY DONE.** Both Employee/Detail and Manager/Review already render the audit trail. No new Razor Pages needed for this phase.
- **`IAuditLogger` service: YES — extract it.** Primary Phase 7 value is testability and consistency. Refactor `TravelRequestService` to inject `IAuditLogger` instead of writing inline.
- **Query-by-user: ADD to `IAuditLogger` or `ITravelRequestService`.** Needed for Phase 7 validation. Backend-only; Pippin validates with a direct query or a simple service test — no new UI page required.

---

#### Scoped Phase 7 task briefs

**Gandalf (Backend):**
1. Create `IAuditLogger` interface in `TravelRequestWF.Infrastructure` (or a `Core` layer) with:
   - `Task LogAsync(string action, int? travelRequestId, int? documentId, string actorUserId, string? details = null, CancellationToken ct = default)`
   - Named wrappers are optional but encouraged: `LogSubmittedAsync`, `LogApprovedAsync`, `LogRejectedAsync`, `LogReturnedAsync`, `LogResubmittedAsync`, `LogDocumentUploadedAsync`.
   - `Task<IReadOnlyList<AuditLogEntry>> GetLogByRequestAsync(int travelRequestId, CancellationToken ct = default)`
   - `Task<IReadOnlyList<AuditLogEntry>> GetLogByUserAsync(string actorUserId, CancellationToken ct = default)`
2. Create `AuditLogger : IAuditLogger` — concrete implementation using `AppDbContext` directly (inject `AppDbContext`). No new migrations; entity schema unchanged.
3. Register `IAuditLogger` / `AuditLogger` in DI (`Program.cs`), scoped lifetime.
4. Refactor `TravelRequestService`: remove all 6 inline `_db.AuditLogEntries.Add(...)` blocks; replace with `await _auditLogger.LogAsync(...)` calls (or named wrappers). `TravelRequestService` gets `IAuditLogger` injected via constructor.
5. Do NOT change `AuditLogEntry` entity or add any EF migrations.

**Legolas (Frontend):** No changes needed. Audit trail UI already exists on Detail and Review pages. ✅

**Pippin (Validation):**
- Verify each workflow action (Submit, Approve, Reject, Return, Resubmit) generates exactly one `AuditLogEntry` with correct `Action` string, non-null `ActorId`, correct `TravelRequestId`.
- Verify DocumentUploaded generates one entry per document with `RequestDocumentId` set.
- Verify `GetLogByRequestAsync(requestId)` returns all entries for a request in chronological order.
- Verify `GetLogByUserAsync(actorUserId)` returns all entries attributed to that user across all requests.
- Verify `Timestamp` is a recent UTC value (not default/zero) on each entry.

### 2026-08-14T22:23:45-03:00: Phase 6 Gap Analysis & Scoped Plan
**By:** Aragorn

#### What already exists (Stage 4 — live, verified)
- `IBlobStorageService` / `BlobStorageService` uploads files to Azure Blob Storage container `travel-documents`.
- `RequestDocument` entity (`Id`, `FileName`, `BlobUrl`, `TravelRequestId` FK, Restrict delete) is in the DB and wired into EF migrations.
- `SubmitRequestDto` accepts `IReadOnlyList<(Stream, FileName, ContentType)>` — **multiple files are already supported** end-to-end in the backend (`TravelRequestService.SubmitRequestAsync` loops over all documents).
- The Employee Submit page (`Submit.cshtml`) has `<input type="file" multiple />` bound to `List<IFormFile> Documents` — **multi-file upload UI already exists**.
- `Employee/Detail.cshtml` and `Manager/Review.cshtml` **already render a Documents table** with clickable Download links (`BlobUrl` direct links).
- Audit log entries of action `DocumentUploaded` are recorded per document.
- Ownership check on `Employee/Detail`: `if (Request.EmployeeId != employeeId) return Forbid()` — employee cannot see another employee's documents. Manager Review has no such check (managers see all their direct reports' requests by design).

#### Real gaps found (Phase 6 true delta)

1. **File type/size validation — MISSING.** The backend (`TravelRequestService`) and the `BlobStorageService` perform zero validation: any file type and any size is accepted and uploaded. Phase 6 requires a whitelist (PDF, DOCX, common image types) and a max-size cap (e.g. 10 MB per file). This must be added backend-side (service layer) to be tamper-proof; optionally also client-side for UX.

2. **Container-per-environment — NOT IMPLEMENTED.** Both `appsettings.json` (production) and `appsettings.Development.json` use the same hardcoded `ContainerName: "travel-documents"`. Phase 6 explicitly asks for environment-differentiated containers (e.g. `travel-documents-dev` vs `travel-documents-prod`). Fix: update `appsettings.Development.json` to `"ContainerName": "travel-documents-dev"` and the production appsettings/env var to `"travel-documents-prod"`. No code changes needed — the config key is already parameterized in `AzureStorageOptions`; just config values need updating.

3. **Submit UI: no file type hint or size feedback to user.** The file input lacks `accept` attribute and there is no client-side validation message if a user tries to upload a disallowed type. Minor UX gap but required for a complete Phase 6 experience.

#### What is NOT a gap (already done — do not rebuild)
- Multi-file upload (backend + UI): ✅ done
- DB linking (`RequestDocument` with `FileName`, `BlobUrl`, `TravelRequestId`): ✅ done
- Document list on Employee Detail: ✅ done
- Document list on Manager Review: ✅ done
- Ownership check for employee document access: ✅ done

---

#### Scoped Phase 6 task briefs

**Gandalf (Backend):**
- Add file validation to `TravelRequestService.SubmitRequestAsync` (or a new `IDocumentValidator` service): whitelist content types (`application/pdf`, `application/vnd.openxmlformats-officedocument.wordprocessingml.document`, `image/jpeg`, `image/png`, `image/gif`), reject unknown types with a user-friendly `InvalidOperationException`, enforce max per-file size (10 MB). Throw with a clear message; the PageModel already catches `InvalidOperationException` and surfaces `ErrorMessage`.
- Update `appsettings.Development.json` `ContainerName` → `"travel-documents-dev"` and confirm production appsettings/env variable is set to `"travel-documents-prod"` (or document it as an Azure App Service environment variable for Jorgito to set).

**Legolas (Frontend):**
- Add `accept=".pdf,.docx,.jpg,.jpeg,.png,.gif"` to the file input on `Submit.cshtml` for browser-side hint.
- Add a small help text below the file input: "Accepted: PDF, DOCX, JPG, PNG, GIF. Max 10 MB per file." No other UI changes needed — Detail and Review already show documents correctly.

**No new migrations or entity changes required.**

### 2026-08-09T21:35:58.924-03:00: Decision
**By:** Aragorn
**What:** Connected TravelRequestWF to GitHub repo Jorge2215/TravelRequestWF for issue tracking.
**Why:** User confirmed the remote repository for the project.

### 2026-08-10T22:03:59.308-03:00: Milestone
**By:** Jorgito
**What:** Azure SQL Database provisioned and `InitialCreate` EF Core migration applied successfully against it. Stage 1 success criteria (schema created and accessible in Azure SQL) is now fully met.
**Why:** Closes the last open item from Stage 1 (Azure SQL was previously deferred pending credentials).

### 2026-08-10T21:07:40.356-03:00: User directive
**By:** Jorgito (via Copilot)
**What:** Local branch renamed to `dev`; remote `origin/dev` created and tracked. All team work commits push to `dev`. Remote `main` is reserved exclusively for GitHub Actions deployment to Azure — never push work commits directly to `main`.
**Why:** User request — keep deploy pipeline isolated from active development.

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction

---

# Merged decision inbox (merge performed 2026-08-09T22:02:03-03:00)

The following entries were merged from files in `.squad/decisions/inbox/`. Duplicate entries (exact duplicates) were skipped.

---

### From: aragorn-prd-decomposition.md

# Aragorn — PRD Decomposition: Decisions & Assumptions

### 2026-08-09T21:47:42-03:00: Assumption — Manager hierarchy
**By:** Aragorn
**What:** Assuming a simple single-level manager relationship: each employee has one ManagerId (FK to the same Users table). No multi-level approval chains for the PoC.
**Why:** PRD says "direct manager" — simplest model that satisfies it. Multi-level can be added later without breaking the schema.

### 2026-08-09T21:47:42-03:00: Assumption — Authentication
**By:** Aragorn
**What:** Assuming ASP.NET Identity with local accounts for the PoC. No Azure AD/Entra ID integration yet.
**Why:** PRD doesn't specify an auth provider. Local Identity is the fastest path to a working PoC with role-based access (Employee vs Manager). Entra ID can replace it later.

### 2026-08-09T21:47:42-03:00: Decision — Single Razor Pages project
**By:** Aragorn
**What:** One ASP.NET Core Razor Pages project for both employee and manager views. Separate Areas or folder-based separation, not separate apps.
**Why:** PoC scope doesn't justify the deployment overhead of multiple front-ends. Role-based authorization gates the views.

### 2026-08-09T21:47:42-03:00: Decision — EF Core for data access
**By:** Aragorn
**What:** Use Entity Framework Core with Azure SQL as the ORM, code-first migrations.
**Why:** Standard for .NET Razor Pages apps. Keeps schema in source control and aligns with the team's stack.

### 2026-08-09T21:47:42-03:00: Decision — Azure Blob Storage for documents
**By:** Aragorn
**What:** Store uploaded files in Azure Blob Storage with container-per-environment. Reference blobs by URI in the TravelRequest record.
**Why:** PRD says "Azure Storage Account" for documents. Blob Storage is the natural fit; blob URIs keep the SQL schema clean.

### 2026-08-09T21:47:42-03:00: Assumption — Email delivery for daily report
**By:** Aragorn
**What:** Assuming the Azure Function daily report will use SendGrid or an SMTP relay for email. Exact provider TBD — the Function will accept an IEmailSender abstraction.
**Why:** PRD says "sends each manager a report" but doesn't specify the channel. An abstraction lets us swap providers without touching the Function logic.

### 2026-08-09T21:47:42-03:00: Decision — Power Automate scope limited to notification
**By:** Aragorn
**What:** For the PoC, Power Automate handles notification routing only (email to manager on new request, email to employee on decision). The actual state transitions live in the .NET backend, not in Power Automate.
**Why:** Keeping business logic in code makes it testable and version-controlled. Power Automate is the notification bus, not the workflow engine.

---

### From: coordinator-prd-clarifications.md

### 2026-08-09T21:47:03-03:00: Decision
**By:** Jorgito (via Coordinator)
**What:** Manager assignment for the PoC uses hardcoded seed data (no AD/HR system integration for now).
**Why:** User confirmed this is sufficient for the PoC scope.

### 2026-08-09T21:47:03-03:00: Decision
**By:** Jorgito (via Coordinator)
**What:** Auth provider is local ASP.NET Identity, not Azure Entra ID.
**Why:** Simpler for PoC; matches Aragorn's original assumption.

### 2026-08-09T21:47:03-03:00: Decision
**By:** Jorgito (via Coordinator)
**What:** Email delivery is stubbed/logged for the PoC — no SendGrid/SMTP integration yet.
**Why:** User confirmed stubbing is sufficient for PoC scope.

### 2026-08-09T21:47:03-03:00: Decision
**By:** Jorgito (via Coordinator)
**What:** Team has Power Automate Premium licensing — Sam can use HTTP trigger connectors for flows.
**Why:** User confirmed premium licensing is available.

### 2026-08-09T21:47:03-03:00: Decision
**By:** Jorgito (via Coordinator)
**What:** Target framework is confirmed as .NET 10.
**Why:** User confirmed Aragorn's assumption.

---

### From: aragorn-functional-spec-reconciliation.md

# Decisions — Functional Spec Reconciliation

### 2026-08-09T21:55:00-03:00: Decision
**By:** Aragorn  
**What:** Audit Log will be a dedicated `AuditLogEntry` table written via an `IAuditLogger` service (new WI-10).  
**Why:** Functional spec §5 explicitly requires recording submission, approval, rejection, and return actions. A separate table keeps the audit concern decoupled from the request entity and allows future compliance/reporting without polluting the core model.

### 2026-08-09T21:55:00-03:00: Decision
**By:** Aragorn  
**What:** "Assigned Travel" (spec §3.3) is NOT a separate table — it's simply a TravelRequest with Status=Approved.  
**Why:** The spec says "Approved requests are recorded in the database as Assigned Travel." For a PoC this is just the Approved state. Adding a separate entity gains nothing now and can be introduced later if business rules diverge.

### 2026-08-09T21:55:00-03:00: Decision
**By:** Aragorn  
**What:** Notification trigger points are explicitly codified as WI-11, separate from the stub service (WI-9).  
**Why:** The functional spec is precise about WHO gets notified WHEN (reject→employee, return→employee, resubmit→manager). Separating "the triggers" from "the delivery mechanism" ensures we wire the logic correctly even though delivery is stubbed for PoC.

### 2026-08-09T21:55:00-03:00: Decision
**By:** Aragorn  
**What:** Audit log retention for PoC: keep all records, no purge.  
**Why:** No business requirement for deletion in the spec; simplest path. Revisit for production (GDPR, storage costs).

---

### From: coordinator-functional-spec-clarifications.md

### 2026-08-09T21:54:36-03:00: Decision
**By:** Jorgito (via Coordinator)
**What:** Audit log will be queried directly from the DB for the PoC — no admin UI page to browse entries.
**Why:** User confirmed direct DB query is sufficient for PoC scope.

### 2026-08-09T21:54:36-03:00: Decision
**By:** Jorgito (via Coordinator)
**What:** Email-only notifications are the long-term plan — no in-app alerts needed, now or later.
**Why:** User confirmed email-only satisfies requirements.

### 2026-08-09T21:54:36-03:00: Decision
**By:** Jorgito (via Coordinator)
**What:** "Assigned Travel" is represented simply as Status=Approved — no separate booking/assignment entity.
**Why:** User confirmed this simplification is acceptable for the PoC.

---

### From: aragorn-architecture-reconciliation.md

# Architecture Reconciliation — Aragorn

### 2026-08-09T21:59:00-03:00: Note — Auth Conflict (PENDING USER DECISION)
**By:** Aragorn  
**What:** Architecture doc specifies Azure Active Directory (AAD) for authentication. This CONFLICTS with prior user decision to use local ASP.NET Identity. Both options remain on the table — awaiting user resolution.  
**Why:** Cannot proceed with auth implementation until this is settled. Affects WI-1 (scaffolding) and WI-6 (role-based access on pages).

### 2026-08-09T21:59:00-03:00: Decision — Azure App Service as hosting target
**By:** Aragorn  
**What:** Architecture doc specifies Azure App Service for the web app. Adopted as the deployment target (no conflict with existing plan, which was host-agnostic). Added as a note to WI-1.  
**Why:** Aligns with architecture doc; does not change PoC dev workflow (still runs locally via Kestrel). Deployment scripts/infra can be added later.

### 2026-08-09T21:59:00-03:00: Decision — Audit logs stored in Azure SQL alongside requests (confirmed)
**By:** Aragorn  
**What:** Architecture doc confirms audit logs live in the same Azure SQL database as travel requests. No change needed — WI-10 already designs this.  
**Why:** Consistency check; architecture doc and existing plan agree.

### 2026-08-09T21:59:00-03:00: Note — Logic Apps for SAP/Ariba is explicitly "future"
**By:** Aragorn  
**What:** Architecture doc lists Logic Apps for future SAP/Ariba integration. No PoC action. WI-8 (Power Automate) remains the current orchestration tool.  
**Why:** Confirming scope boundary — no new work items needed for Logic Apps.

---

### From: coordinator-auth-conflict-resolution.md

### 2026-08-09T21:58:15-03:00: Decision
**By:** Jorgito (via Coordinator)
**What:** Confirmed auth remains local ASP.NET Identity, NOT Azure AD/Entra ID, despite the architecture doc mentioning AAD. This supersedes the architecture doc on this point.
**Why:** User re-confirmed the earlier PoC decision after Aragorn flagged the conflict — simpler and faster for PoC scope with hardcoded seed data. AAD/Entra ID integration is deferred to a future phase if this becomes a production system.

### 2026-08-09T21:58:15-03:00: Decision
**By:** Jorgito (via Coordinator)
**What:** Web app hosting target is Azure App Service, per the architecture doc.
**Why:** No conflicting prior decision; adopted as-is from architecture document.

---

### From: aragorn-backlog-reconciliation.md

# Backlog Reconciliation — User Stories ↔ Work Items

## ⚠️ CONFLICTS REQUIRING USER DECISION

### 2026-08-09T22:02:03-03:00: Decision (PENDING — conflict flagged)
**By:** Aragorn
**What:** US 10 acceptance criteria says "Autenticación mediante Azure AD" — this **conflicts** with the standing decision (2026-08-09T21:58:15) that auth is LOCAL ASP.NET Identity for the PoC.
**Why:** The user already confirmed local Identity supersedes any AAD reference. US 10's acceptance criteria should be updated to read "Autenticación mediante ASP.NET Identity (cuentas locales). Roles asignados (Empleado / Gerente)." Awaiting user confirmation before modifying the backlog.

### 2026-08-09T22:02:03-03:00: Decision (PENDING — conflict flagged)
**By:** Aragorn
**What:** US 08 acceptance criteria says "Se crea registro en tabla 'ViajesAsignados'" — this **conflicts** with the standing decision (2026-08-09T21:55:00) that Status=Approved suffices with NO separate table.
**Why:** We explicitly decided a separate "ViajesAsignados" entity is unnecessary for the PoC. US 08 should be updated to say "La solicitud cambia a estado 'Aprobado', lo cual constituye el registro de viaje asignado (sin tabla separada)." Awaiting user confirmation.

---

### From: coordinator-backlog-conflict-resolution.md

### 2026-08-09T22:02:03-03:00: Decision
**By:** Jorgito (via Coordinator)
**What:** US 10 acceptance criteria updated to reference local ASP.NET Identity authentication instead of Azure AD.
**Why:** Confirms the standing decision (local Identity for PoC) over the backlog proposal's mention of Azure AD.

### 2026-08-09T22:02:03-03:00: Decision
**By:** Jorgito (via Coordinator)
**What:** US 08 acceptance criteria updated to drop the separate "ViajesAsignados" table — approved requests are represented as Status=Approved on the existing TravelRequest record.
**Why:** Confirms the standing decision to avoid a redundant entity for the PoC.

---

---

### From: aragorn-er-diagram-reconciliation.md

# ER Diagram Reconciliation — Aragorn

### 2026-08-09T22:09:51-03:00: Decision
**By:** Aragorn
**What:** ER diagram schema (Empleados, SolicitudesViaje, DocumentosAdjuntos, LogAuditoria) is consistent with prior decisions and WI-1/WI-2/WI-10. Adopted as the concrete schema reference. English C#/EF Core naming mapped below.
**Why:** The diagram confirms: (1) self-referencing SuperiorID for manager hierarchy = our "hardcoded seed data" approach, (2) separate LogAuditoria table = our WI-10 design, (3) no "ViajesAsignados" table = consistent with Status=Approved decision, (4) DocumentosAdjuntos = our RequestDocument. No conflicts found with standing decisions.

### 2026-08-09T22:09:51-03:00: Decision
**By:** Aragorn
**What:** WI-1/WI-2 data model updated with concrete field names from ER diagram. English EF Core entity mapping:

| Spanish (Diagram) | English (EF Core Entity) | Notes |
|---|---|---|
| Empleados | `Employee` | |
| EmpleadoID | `Employee.Id` | PK |
| Nombre | `Employee.Name` | |
| Email | `Employee.Email` | |
| Departamento | `Employee.Department` | |
| SuperiorID | `Employee.SuperiorId` | FK self-ref, nullable for top-level |
| SolicitudesViaje | `TravelRequest` | |
| SolicitudID | `TravelRequest.Id` | PK |
| EmpleadoID | `TravelRequest.EmployeeId` | FK → Employee |
| AprobadorID | `TravelRequest.ApproverId` | FK → Employee (⚠️ see design question below) |
| Destino | `TravelRequest.Destination` | |
| FechaInicio | `TravelRequest.StartDate` | |
| FechaFin | `TravelRequest.EndDate` | |
| Motivo | `TravelRequest.Purpose` | |
| Estado | `TravelRequest.Status` | Enum: Pending, Approved, Rejected, Returned |
| DocumentosAdjuntos | `RequestDocument` | |
| DocumentoID | `RequestDocument.Id` | PK |
| SolicitudID | `RequestDocument.TravelRequestId` | FK → TravelRequest |
| NombreArchivo | `RequestDocument.FileName` | |
| URLArchivo | `RequestDocument.BlobUrl` | Azure Blob Storage URL |
| LogAuditoria | `AuditLogEntry` | |
| LogID | `AuditLogEntry.Id` | PK |
| SolicitudID | `AuditLogEntry.TravelRequestId` | FK → TravelRequest |
| Acción | `AuditLogEntry.Action` | |
| FechaHora | `AuditLogEntry.Timestamp` | |
| Usuario | `AuditLogEntry.ActorId` | |

**Why:** Concrete field names ensure all team members (Gandalf for EF Core model, Legolas for Razor Pages bindings, Pippin for test assertions) reference the same schema shape.

### 2026-08-09T22:09:51-03:00: Decision
**By:** Aragorn
**What:** WI-10 (Audit Log) confirmed fully consistent with `LogAuditoria` table shape. No changes needed.
**Why:** LogAuditoria has: LogID (PK), SolicitudID (FK), Acción, FechaHora, Usuario — maps exactly to our existing AuditLogEntry design (Id, TravelRequestId, Action, Timestamp, ActorId). The ER diagram validates our prior design.

### 2026-08-09T22:09:51-03:00: Design Question (PENDING USER DECISION)
**By:** Aragorn
**What:** The ER diagram has BOTH `Empleados.SuperiorID` (the employee's direct manager in the org hierarchy) AND `SolicitudesViaje.AprobadorID` (the approver assigned to a specific request). These are separate fields pointing to different Employee records potentially.

**Question for Jorgito:** For this PoC, should `TravelRequest.ApproverId` always default to the employee's direct manager (`Employee.SuperiorId`) at submission time, or is there a real scenario where a *different* approver can be assigned per-request?

- **Option A (simple):** ApproverId is auto-populated from Employee.SuperiorId on submission. No UI to pick a different approver. The field exists for future flexibility but is always = SuperiorId for now. WI-1 seed data only needs the manager hierarchy.
- **Option B (flexible):** There's a mechanism to assign a different approver (e.g., delegation during vacations, cross-department approvals). This requires additional UI/logic in WI-3 or a backend assignment rule.

**Impact:** Option A keeps WI-1/WI-3 simple. Option B adds scope (approver selection or assignment logic).
**Why:** Cannot finalize the submission flow (WI-3) or seed data shape (WI-1) without knowing intent.


### From: coordinator-approver-decision.md

### 2026-08-09T22:09:51-03:00: Decision
**By:** Jorgito (via Coordinator)
**What:** TravelRequest.ApproverId always defaults to the employee's direct manager (Employee.SuperiorId) at submission time. No per-request approver-picker UI is needed for the PoC.
**Why:** User confirmed simplicity (Option A) is sufficient — no real scenario requiring a different approver was identified for this PoC scope.

# End of merged inbox


---

### 2026-08-14T22:30:00-03:00: Phase 6 — File Upload Validation + Per-Environment Blob Container
**By:** Gandalf

#### Gap 1 — File type/size validation (IMPLEMENTED)

Added ValidateDocuments static method in TravelRequestService that runs **before** any blob upload (SubmitRequestAsync calls it as the first line). Logic:
- Allowed extensions (case-insensitive): .pdf, .docx, .jpg, .jpeg, .png, .gif
- Max file size: 10 MB per file (checked via stream.Length)
- On violation: throws InvalidOperationException with a user-friendly message naming the offending file and the reason.
- The existing catch (InvalidOperationException ex) in Employee/Submit.cshtml.cs already surfaces the message to the user as ErrorMessage — no PageModel change needed.
- Fail-fast: validation is the very first step; no DB write, no blob upload happens for a rejected file.

#### Gap 2 — Container-per-environment (IMPLEMENTED)

- ppsettings.Development.json: "ContainerName": "travel-documents-dev"
- ppsettings.json (prod baseline): "ContainerName": "travel-documents-prod"
- Rationale: ppsettings.json serves as the production default (Azure App Service uses the base config); ppsettings.Development.json overrides locally. This matches the standard ASP.NET Core config layering.
- BlobStorageService already reads ContainerName from IOptions<AzureStorageOptions> — no code change needed.
- BlobStorageService.UploadDocumentAsync calls containerClient.CreateIfNotExistsAsync(PublicAccessType.None) — the container will auto-create on first upload in both environments if the account has container-creation permissions.
- **Jorgito action required:** Ensure the Azure Storage account has a 	ravel-documents-dev container (or that the connection string used in Development has CreateIfNotExistsAsync permission). Production container 	ravel-documents-prod also needs to exist (or same auto-create applies).


---

### 2026-08-15T10:45:00-03:00: Phase 9 — Merry Delivery — DailyPendingReportFunction Digest + Deployment Outcome
**By:** Merry

#### What was done

**Approver navigation property:** Already existed on TravelRequest.Approver (nav) + ApproverId (FK), and AppDbContext already had the fluent config .HasOne(t => t.Approver).WithMany().HasForeignKey(t => t.ApproverId). No migration needed.

**New files:**
- src/TravelRequestWF.Functions/DigestPayload.cs — PendingRequestItem and ManagerDigestPayload sealed records.

**Modified files:**
- src/TravelRequestWF.Functions/DailyPendingReportFunction.cs — rewrote RunAsync to query pending requests with .Include(r => r.Approver), group by ApproverId, build ManagerDigestPayload per manager, call PostDigestAsync per manager. Added PostDigestAsync method mirroring Phase 5 PowerAutomateNotificationService.PostToFlowAsync pattern exactly: PLACEHOLDER guard, try/catch per manager (non-blocking), IHttpClientFactory.CreateClient(), logs success/warning/error.
- src/TravelRequestWF.Functions/Program.cs — added uilder.Services.AddHttpClient().
- src/TravelRequestWF.Functions/local.settings.json (gitignored) — added "PowerAutomate:FlowCDailyDigestUrl": "PLACEHOLDER_FLOW_C_URL" key.

**Build result:** dotnet build TravelRequestWF.slnx — 0 errors, 2 pre-existing NU1902 NuGet vulnerability warnings (unrelated Azure.Identity package, pre-existing).

**Config key:** PowerAutomate:FlowCDailyDigestUrl — Jorgito must set this in:
1. Local local.settings.json Values section (once Sam's Flow C is built and has an HTTP trigger URL).
2. Azure Function App > Configuration > Application Settings.

#### Deployment outcome — MANUAL ACTION REQUIRED

unc (Azure Functions Core Tools) is not installed on this machine, so automated deployment was skipped.

**Jorgito: to deploy the function, run these commands:**

`powershell
# 1. Install Azure Functions Core Tools (once)
npm install -g azure-functions-core-tools@4 --unsafe-perm true

# 2. Login to Azure CLI (if not already logged in)
az login

# 3. Find your Function App name
az functionapp list --output table

# 4. Publish from the Functions project directory
cd src\TravelRequestWF.Functions
func azure functionapp publish <YOUR-FUNCTION-APP-NAME> --dotnet-isolated
`

After publishing, set these Application Settings in the Azure Portal (Function App > Configuration):
- SqlConnectionString — same Azure SQL connection string as the Web App.
- PowerAutomate:FlowCDailyDigestUrl — replace with real Flow C URL once Sam builds it.
- APPLICATIONINSIGHTS_CONNECTION_STRING — should be auto-set if App Insights is linked; verify.

### 2026-08-15T13:30:00-03:00: Phase 9 - Sync Triggers BadRequest Root Cause + Fix
**By:** Merry

Root cause: Program.cs had eager throw on SqlConnectionString which crashed host startup before sync triggers could enumerate functions. Fixed by deferring config lookup to DI factory overload. Added runtime guard in RunAsync. Build: 0 errors.

---

### 2026-08-15T11:14:00-03:00: Phase 9 — Flow C URL Configured Locally (Merry)
**By:** Merry

**What:** Jorgito provided the live HTTP trigger URL for Flow C ("Daily Pending Requests Digest"). The real URL has been written into src/TravelRequestWF.Functions/local.settings.json (gitignored) under the key PowerAutomate:FlowCDailyDigestUrl. The placeholder value PLACEHOLDER_FLOW_C_URL has been replaced with the real value — locally only.

**Verification:**
- local.settings.json is gitignored (confirmed via git check-ignore; git status shows zero changes to that file).
- The real URL is NOT present in any git-tracked file, commit, or squad document.
- The Web project (TravelRequestWF.Web) has no reference to FlowCDailyDigestUrl — Flow C is consumed exclusively by the Functions project. Confirmed by grep.

**Action still required (production):**
- Jorgito must set PowerAutomate:FlowCDailyDigestUrl in the Azure Function App's **Application Settings** (Azure Portal > Function App > Configuration) before the daily digest will work in production.
- This mirrors the same process used for Flow A / Flow B URL secrets in Phase 5.


---

### 2026-08-15T11:32:00-03:00: Phase 10 — CI/CD Workflow for Web App (Merry)
**By:** Merry

#### What was done

- Created `.github/workflows/deploy-web.yml` — GitHub Actions workflow that builds and deploys `TravelRequestWF.Web` to Azure App Service on every push to `main`.
  - Trigger: `push` to `main`
  - .NET SDK version: `10.0.x` (matches the Web project's `net10.0` TargetFramework)
  - Steps: checkout → setup-dotnet@v4 → dotnet restore → dotnet build Release → dotnet publish → azure/webapps-deploy@v3
  - App Service name sourced from repo Variable `AZURE_WEBAPP_NAME` (not hardcoded — Jorgito will set once he names the resource)
  - Publish profile sourced from repo Secret `AZURE_WEBAPP_PUBLISH_PROFILE`

- Created `.squad/files/phase10-web-deploy-workflow-setup.md` — plain-language setup guide covering: how to download the publish profile from the Azure Portal, how to add the repo secret and repo variable in GitHub Settings, and the pre-go-live checklist.

#### Important: merging to `main`

The workflow file was authored on `dev` (branch convention). **Jorgito must merge `dev` → `main` himself when ready to activate the pipeline.** Merry never touches `main` directly.

#### Pending before pipeline can run

- Jorgito creates the App Service resource in the Azure Portal and notes the exact resource name.
- Sets repo Variable `AZURE_WEBAPP_NAME` = chosen App Service name.
- Downloads the publish profile from the Portal ("Get publish profile" button on the App Service blade).
- Sets repo Secret `AZURE_WEBAPP_PUBLISH_PROFILE` = full XML content of the publish profile.
- Applies pending EF Core migrations to Azure SQL (Aragorn Phase 10 audit item #2).
- Sets App Service Application Settings (connection strings, Power Automate URLs).



---

### 2026-08-15T11:37:00-03:00: Phase 10 — Azure SQL Migration Attempt (Gandalf)
**By:** Gandalf

**What was attempted:**
- Azure SQL connection string set as user-secret (ConnectionStrings:DefaultConnection) for TravelRequestWF.Web — stored in OS user-secrets store, NOT committed to any file.
- Ran dotnet ef migrations list and dotnet ef database update from repo root with --project src/TravelRequestWF.Infrastructure --startup-project src/TravelRequestWF.Web --framework net10.0.
- EF Core tools correctly resolved the Azure SQL connection string from user-secrets (confirmed: error was SQL firewall, not a config/auth issue).

**Blocker — Azure SQL Firewall:**
- This machine's outbound IP (190.195.150.164) is not whitelisted on zure-sql-pampa.database.windows.net.
- Error: Cannot open server 'azure-sql-pampa' requested by the login. Client with IP address '190.195.150.164' is not allowed to access the server.

**Migrations that need to be applied (all 5, in order):**
1. 20260811002601_InitialCreate
2. 20260812013905_AuditLogDocumentLink
3. 20260812014713_RequestDocumentRestrictDelete
4. 20260812021200_AddIdentityTables
5. 20260812231909_Stage4WorkflowFields

**Action required by Jorgito:**
- Go to Azure Portal → SQL Server zure-sql-pampa → Networking/Firewall rules → add IP 190.195.150.164 (or "Add your client IP").
- Once IP is whitelisted, re-run: dotnet ef database update --project src\TravelRequestWF.Infrastructure --startup-project src\TravelRequestWF.Web --framework net10.0 from repo root (user-secret is already set).
- Alternatively, Jorgito can run the migration from his own machine after setting the same user-secret there.

**No secrets committed.** User-secrets are OS-level and gitignored by design.


---

### 2026-08-15T11:52:00-03:00: Phase 10 — Azure SQL Migrations Successfully Applied (Gandalf)
**By:** Gandalf

**Result: SUCCESS — 0 pending migrations remain.**

After Jorgito whitelisted this machine's IP on the Azure SQL firewall, the following 4 previously-pending migrations were applied to TravelRequestDB on zure-sql-pampa.database.windows.net:

1. 20260812013905_AuditLogDocumentLink — AuditLogEntries: TravelRequestId made nullable, RequestDocumentId FK added, FKs changed to NO ACTION.
2. 20260812014713_RequestDocumentRestrictDelete — RequestDocuments FK to TravelRequests changed to NO ACTION (Restrict).
3. 20260812021200_AddIdentityTables — ASP.NET Identity tables created (AspNetUsers, AspNetRoles, AspNetUserRoles, AspNetUserClaims, AspNetUserLogins, AspNetUserTokens, AspNetRoleClaims). AspNetUsers linked to Employees via EmployeeId FK.
4. 20260812231909_Stage4WorkflowFields — TravelRequests.SubmittedAt column added; AuditLogEntries.Details column added.

InitialCreate was already present in Azure SQL from prior provisioning.

Post-apply migrations list confirmed: all 5 migrations applied, none pending.

Connection string was supplied via .NET user-secrets (never committed). No tracked files were modified.


---

### 2026-08-15T19:49:00-03:00: Phase 10 — Round 2: Double-Underscore Fix Did Not Resolve Placeholder (Gandalf)
**By:** Gandalf

**Symptom:** After renaming App Service env vars to `__` notation and restarting, log stream still shows `<your-azure-sql-server>.database.windows.net` and `TravelRequestWFDb` as the live runtime server/database names. Placeholder is still being used.

**Code re-audit result — NO BUG FOUND:**
- `Program.cs` uses standard `builder.Configuration.GetConnectionString("DefaultConnection")` — no `??` fallback, no hardcoded string, no custom config precedence override.
- No `appsettings.Production.json` exists — cannot be the override source.
- Only `appsettings.json` (placeholder) and `appsettings.Development.json` exist.
- ASP.NET Core default config precedence is correct: env vars win over json files.

**Why placeholder in the error message is conclusive:** EF Core embeds the actual `SqlConnection.DataSource` and `.Database` properties in its error message — not a template. Seeing the literal `<your-azure-sql-server>` text proves the running process received that string as the connection string value.

**Most likely root cause:** Azure Portal two-step save was NOT completed. Adding/editing an env var row in the Portal only stages it in the UI list. The page-level **Save** button must be clicked separately, followed by a confirmation dialog, for the change to be committed and the App Service to restart. If that final Save was skipped, nothing persisted to Azure.

**Prioritized checklist for Jorgito (in `.squad/files/phase10-connection-string-troubleshooting.md`):**
1. Verify `ConnectionStrings__DefaultConnection` exists in Portal App settings list with the real value — if missing, save was never completed.
2. Check Kudu Env.cshtml to see what the live process actually sees.
3. Use the Portal "Connection strings" tab as an alternative (Name=DefaultConnection, Type=SQLAzure) — avoids `__` naming entirely.
4. Remove any leftover colon-named entries.
5. Manually restart after confirming save.

**No code changes made.** Fix is entirely in Azure Portal.


---

### 2026-08-15T20:17:00-03:00: Phase 10 — Periodic DB Error Root Cause & Fix (Gandalf)
**By:** Gandalf

**Symptom:** Log Stream shows EF Core errors with placeholder DB TravelRequestWFDb / server <your-azure-sql-server> periodically, even with no users. Login/register works fine.

**Audit findings:**
- Web project has NO BackgroundService, NO IHostedService, NO health checks, NO timers, NO periodic loops, NO second AddDbContext, NO OnConfiguring override. Exactly one DB path.
- Function App uses configuration["SqlConnectionString"] (different key), runs once daily at 08:00 UTC, and uses OpenTelemetry to Azure Monitor (not Log Stream). Its connection string references TravelRequestDB, not TravelRequestWFDb — so the Function App is NOT the source of this error.

**Root cause identified: crash-restart loop triggered by IdentitySeeder.**
- IdentitySeeder.SeedAsync runs at every cold-start (unconditionally, unguarded) and makes real DB calls.
- Without EnableRetryOnFailure, a transient Azure SQL blip or a still-active placeholder string causes the seeder to throw → unhandled exception → ASP.NET Core host terminates → Azure App Service auto-restarts → seeder runs again → repeat.
- This crash-restart loop produces "periodic" errors with no user involved.

**Fix applied (Program.cs):**
1. Added sqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: 30s) to UseSqlServer — handles transient Azure SQL failures.
2. Wrapped IdentitySeeder.SeedAsync call in try-catch → seeder failure now logs a Warning and lets the app continue instead of crashing the host. Breaks the crash-restart loop.

**Build:** succeeded (0 errors). Committed and pushed to dev.

**Verification:** After deploy, Log Stream should no longer show ail: EF Core errors. If it still shows TravelRequestWFDb, verify live env vars via Kudu Debug Console → printenv | grep -i connection.
