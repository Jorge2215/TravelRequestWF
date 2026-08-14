# Gandalf — History

## Project Context

- **Project:** TravelRequestWF — a Web Application built on .NET 10 Razor Pages, with Azure SQL Database, and likely some Azure Functions and MS Power Automate flows.
- **Owner:** Jorgito
- **Team cast:** Lord of the Rings universe (Aragorn, Legolas, Gandalf, Merry, Sam, Pippin)

## Learnings

### 2026-08-10T21:16:33-03:00 — Stage 1 Foundation

- **SDK version:** .NET 10.0.302 is installed and used. EF Core tools are on 10.0.8 (slightly behind runtime 10.0.10) — just a warning, not an error.
- **Solution format:** `dotnet new sln` on .NET 10 creates `.slnx` (new XML solution format), NOT `.sln`. Build/ef commands use `TravelRequestWF.slnx`.
- **Entities location:** `src/TravelRequestWF.Infrastructure/Entities/` — Employee, TravelRequest, RequestDocument, AuditLogEntry, TravelRequestStatus enum.
- **DbContext:** `src/TravelRequestWF.Infrastructure/Data/AppDbContext.cs` — Employee self-ref configured with `DeleteBehavior.Restrict`; both TravelRequest FKs (EmployeeId + ApproverId) also Restrict to avoid cascade cycles.
- **Migration:** `InitialCreate` lives in `src/TravelRequestWF.Infrastructure/Migrations/`. Generated and applied successfully to LocalDB (TravelRequestWFDb_Dev).
- **ApproverId rule:** Per team decision, ApproverId is always auto-populated from Employee.SuperiorId at submission time. No per-request approver picker needed for PoC.
- **Pages stub locations:** `Pages/Employee/{Index,Submit,Detail}` and `Pages/Manager/{Index,Review}` — all stub OnGet, no logic yet (Legolas owns markup).
- **Connection strings:** Placeholder in `appsettings.json`; LocalDB override in `appsettings.Development.json`; Azure SQL deploy command documented in `docs/database-setup.md`.

### 2026-08-11T22:45:00-03:00 — Stage 2 AuditLogEntry Fix (AuditLogDocumentLink migration)

- **Entity change:** `AuditLogEntry.TravelRequestId` changed from `int` (non-nullable) to `int?` (nullable). Added `int? RequestDocumentId` FK and corresponding `RequestDocument? RequestDocument` navigation property. Added invariant comment: exactly one FK must be set; enforced at service layer, not DB level.
- **AppDbContext change:** Added explicit Fluent config for both AuditLogEntry relationships with `IsRequired(false)` + `DeleteBehavior.Restrict`. Used `WithMany(t => t.AuditLog)` to wire the existing nav collection on TravelRequest — omitting this caused EF to generate a spurious shadow `TravelRequestId1` column on the first migration attempt. Removed the bad migration and regenerated cleanly.
- **Migration:** `AuditLogDocumentLink` (file: `20260812013905_AuditLogDocumentLink.cs`). Changes: `TravelRequestId` altered to nullable `int`, new nullable `RequestDocumentId` column, FK → `RequestDocuments` (Restrict), FK → `TravelRequests` updated to Restrict (was Cascade).
- **Migration applied to:** LocalDB (`TravelRequestWFDb_Dev` via `Server=(localdb)\mssqllocaldb`) — this is the only available connection string locally (`appsettings.Development.json`). Azure SQL credentials are not present in the local environment.
- **Azure SQL apply command (Jorgito must run):**
  ```
  dotnet ef database update --project src/TravelRequestWF.Infrastructure --startup-project src/TravelRequestWF.Web --connection "<AzureSQLConnectionString>"
  ```
  Replace `<AzureSQLConnectionString>` with the real Azure SQL connection string (same one used for `InitialCreate` in Stage 1).
- **Build:** `dotnet build TravelRequestWF.slnx` — succeeded with 0 errors, 0 warnings.
- **Commit:** `3029740` on `dev`, pushed to `origin/dev`.



### 2026-08-11T22:46:34-03:00 — Stage 2 Gap Fix: RequestDocument Restrict Delete (RequestDocumentRestrictDelete migration)

- **Gap:** `RequestDocument.TravelRequestId` FK had no explicit `OnModelCreating` config → EF Core defaulted to `Cascade`. Inconsistent with every other FK in the schema (all Restrict). Deleting a TravelRequest would silently cascade-delete all its RequestDocuments.
- **Fix in AppDbContext:** Added `HasOne(d => d.TravelRequest).WithMany(t => t.Documents).HasForeignKey(d => d.TravelRequestId).OnDelete(DeleteBehavior.Restrict)` block. Placed it adjacent to the AuditLogEntry configs for consistency.
- **Migration:** `RequestDocumentRestrictDelete` (file: `20260812014713_RequestDocumentRestrictDelete.cs`). SQL: drops the old FK and recreates it `ON DELETE NO ACTION` (SQL Server equivalent of Restrict).
- **Migration applied to:** LocalDB (`TravelRequestWFDb_Dev`). No Azure SQL connection string found locally (no user-secrets, env var, or appsettings.Development.json Azure entry).
- **Azure SQL apply command (Jorgito must run):**
  ```
  dotnet ef database update --project src/TravelRequestWF.Infrastructure --startup-project src/TravelRequestWF.Web --connection "<AzureSQLConnectionString>"
  ```
- **Build:** `dotnet build TravelRequestWF.slnx` — succeeded 0 errors, 0 warnings.
- **Commit:** `6594067` on `dev`, pushed to `origin/dev`.

- **bin/ and obj/ were accidentally committed early in the project.** `git rm -r --cached` is the correct fix: removes them from the index without touching disk. 352 files were untracked this way.
- **Root .gitignore** now has a comprehensive `.NET / Visual Studio` section covering `bin/`, `obj/`, `.vs/`, `*.dll`, `*.pdb`, `*.exe`, `*.cache`, `*.user`, NuGet, test results, and Rider/JetBrains dirs. Adding it after the fact only stops future tracking — `git rm --cached` must be run separately to untrack already-indexed files.
- **Commit `95d16d4`** on `dev`: 356 files changed — 352 build artifacts removed from index + 4 legitimate file updates. Working tree clean after push.

### 2026-08-11T23:10:00-03:00 — Stage 3: ASP.NET Identity Integration

- **NuGet packages added:** `Microsoft.AspNetCore.Identity.EntityFrameworkCore 10.0.10` → Infrastructure project; `Microsoft.AspNetCore.Identity.UI 10.0.10` → Web project.
- **ApplicationUser:** Created at `src/TravelRequestWF.Infrastructure/Identity/ApplicationUser.cs`. Inherits `IdentityUser`. Adds `int? EmployeeId` + `Employee? Employee` navigation property. Namespace: `TravelRequestWF.Infrastructure.Identity`.
- **AppDbContext:** Changed base class from `DbContext` to `IdentityDbContext<ApplicationUser, IdentityRole, string>`. Added FK config for ApplicationUser → Employee (`DeleteBehavior.SetNull`, optional). `base.OnModelCreating(builder)` called first (required by Identity).
- **Program.cs:** Added `AddIdentity<ApplicationUser, IdentityRole>` with password policy (RequireDigit, RequiredLength=8, RequireUppercase, RequireNonAlphanumeric), `AddEntityFrameworkStores<AppDbContext>`, `AddDefaultTokenProviders`. Added `ConfigureApplicationCookie` (LoginPath=/Account/Login, 8h expiry). Added `UseAuthentication()` before `UseAuthorization()` in pipeline. Added startup seeder call.
- **IdentitySeeder:** Created at `src/TravelRequestWF.Web/IdentitySeeder.cs`. Namespace: `TravelRequestWF.Web`. Seeds roles `Employee` and `Manager`. Seeds 4 test users (see credentials below), creating Employee records linked via EmployeeId. Idempotent: checks for existing user before creating. Managers seeded before employees so employees can reference manager1's EmployeeId as SuperiorId.
- **Migration:** `AddIdentityTables` applied to LocalDB (`TravelRequestWFDb_Dev`). Creates: `AspNetUsers` (with `EmployeeId` FK), `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens`, `AspNetRoleClaims`.
- **Azure SQL deploy command documented** in `.squad/files/stage3-azure-sql-deploy-note.md`.
- **Build:** `dotnet build TravelRequestWF.slnx` — 0 errors, 0 warnings.
- **Test credentials:** employee1@test.com/Employee1!Pass (Employee), employee2@test.com/Employee2!Pass (Employee), manager1@test.com/Manager1!Pass (Manager), manager2@test.com/Manager2!Pass (Manager).
- **Important for Legolas:** ApplicationUser is in namespace `TravelRequestWF.Infrastructure.Identity`. The cookie LoginPath is `/Account/Login` — Legolas needs to create that page (or update it). `AddIdentity` also auto-registers `UseAuthentication` services but the middleware must be ordered: `UseAuthentication()` → `UseAuthorization()` → `MapRazorPages()` (already done).

### 2026-08-12T20:03:51-03:00 — Stage 4: Workflow Backend Services & PageModels

- **New NuGet:** `Azure.Storage.Blobs 12.29.1` added to `TravelRequestWF.Infrastructure.csproj`.
- **New services (all in `src/TravelRequestWF.Infrastructure/Services/`):**
  - `AzureStorageOptions` — POCO with `ConnectionString` and `ContainerName`; registered via `IOptions<>`.
  - `IBlobStorageService` / `BlobStorageService` — uploads to Azure Blob Storage, returns blob URI. Throws `InvalidOperationException` with clear message if connection string is still the placeholder.
  - `SubmitRequestDto` — record type for submit payload (Destination, StartDate, EndDate, Purpose, Documents).
  - `ITravelRequestService` / `TravelRequestService` — full workflow: Submit, Approve, Reject, Return, Resubmit, GetForEmployee, GetForManager, GetById.
- **Entity changes & migration (`Stage4WorkflowFields`):**
  - `TravelRequest.SubmittedAt` (`DateTime`, UTC) — added.
  - `AuditLogEntry.Details` (`string?`, nullable) — added.
  - Migration file: `20260812231909_Stage4WorkflowFields.cs`.
  - **Jorgito must apply to Azure SQL:** `dotnet ef database update --project src/TravelRequestWF.Infrastructure --startup-project src/TravelRequestWF.Web --connection "<AzureSQLConnectionString>"`
- **DI in Program.cs:** `Configure<AzureStorageOptions>`, `AddScoped<IBlobStorageService, BlobStorageService>`, `AddScoped<ITravelRequestService, TravelRequestService>`.
- **AzureStorage config:** Added to both `appsettings.json` and `appsettings.Development.json`. Jorgito must replace `"YOUR_AZURE_STORAGE_CONNECTION_STRING_HERE"` with the real Azure Storage account connection string. Container name defaults to `"travel-documents"`.
- **Razor fix:** Legolas's stubs in `Detail.cshtml` and `Review.cshtml` had `@{...}` code blocks nested inside `else { }` blocks (Razor parser bug RZ1010). Fixed by inlining the LINQ query directly into the `@if`/`@foreach` expressions — no intermediate variable needed.
- **CS0108 `new` keyword:** `DetailModel.Request` and `ReviewModel.Request` shadow `PageModel.Request` — added `new` keyword to suppress warning and make intent explicit.
- **Build:** 0 errors, 0 warnings.
- **Commit:** `b4d725d` on `dev`, pushed to `origin/dev`.

### 2026-08-12T20:45:50-03:00 — Stage 4 Hotfix: BlobStorageService Constructor Guard (Pippin's Bug Report)

- **Root cause:** `BlobStorageService` constructor threw `InvalidOperationException` when the Azure Storage connection string was still the placeholder value. Because it was registered as Scoped DI, it was instantiated on every HTTP request that resolved `ITravelRequestService`, which is injected into every workflow PageModel. Result: HTTP 500 on ALL workflow pages even for non-upload operations (Index, Detail, Approve, Reject, Return, Resubmit).
- **Fix:** Moved the placeholder/empty connection-string guard from the constructor into `UploadDocumentAsync`. The constructor now only stores the options; the validation fires only when an actual blob upload is attempted.
- **Lesson (DI/Constructor Design):** Never do environment/resource validation in a constructor of a Scoped or Transient DI service unless that resource is truly required for every operation the service performs. Constructors in DI containers are called eagerly on every resolution — guard checks belong in the specific methods that actually need the resource. This is the "fail at use, not at construction" principle. For services that are optionally configured (e.g., Azure Storage only needed for file upload), constructor guards are actively harmful because they break all consumers, not just the ones that trigger the unconfigured path.
- **Build:** 0 errors, 0 warnings.
- **Commit:** `311c24f` on `dev`, pushed to `origin/dev`.

### 2026-08-12T21:03:28-03:00 — Stage 4 DB Fix: Stage4WorkflowFields Migration Applied (Pippin's Bug Report)

- **Root cause:** `Stage4WorkflowFields` migration (adds `TravelRequests.SubmittedAt` and `AuditLogEntries.Details`) was created but never applied to the LocalDB instance used for testing. `dotnet ef migrations list` showed it as `(Pending)`. First page load triggered `SqlException: Invalid column name 'SubmittedAt'` → HTTP 500.
- **Active connection string:** LocalDB — `Server=(localdb)\mssqllocaldb;Database=TravelRequestWFDb_Dev` (from `appsettings.Development.json`). No Azure SQL credentials present locally.
- **Fix:** `dotnet ef database update --project src/TravelRequestWF.Infrastructure --startup-project src/TravelRequestWF.Web` — applied `Stage4WorkflowFields` cleanly (ALTER TABLE TravelRequests ADD SubmittedAt + ALTER TABLE AuditLogEntries ADD Details). All 5 migrations now applied, none pending.
- **Sanity check:** Started app on `http://localhost:5050`, logged in as `employee1@test.com`, hit `/Employee/Index` → **HTTP 200**, no SqlException, no error content.
- **No code changes were needed** — pure DB state fix. No commit required.
- **⚠️ LESSON — Always verify pending migrations before signaling done:** When a migration is created and committed, it is NOT automatically applied to the running database. The "migration created" step and the "migration applied" step are separate. Before declaring any schema-related work item complete, explicitly run `dotnet ef migrations list` and confirm no `(Pending)` entries remain. A pending migration will produce `SqlException: Invalid column name` on the very first query that touches the new column — a crash that is confusing to diagnose if you didn't check migration state first. Standard closing checklist: (1) create migration, (2) **apply migration**, (3) verify `migrations list` shows all applied, (4) sanity test the affected page.

#### Exact PageModel Properties/Handlers Legolas Must Bind To

**Employee/Submit.cshtml** (`SubmitModel`):
- `[BindProperty] string Destination`
- `[BindProperty] DateOnly StartDate`
- `[BindProperty] DateOnly EndDate`
- `[BindProperty] string Purpose`
- `[BindProperty] List<IFormFile> Documents`
- `string? ErrorMessage` (read-only, display only)
- `string? SuccessMessage` (read-only, display only)
- Handler: `OnPostAsync` → `asp-page-handler` not required (default POST)

**Employee/Index.cshtml** (`IndexModel`):
- `IReadOnlyList<TravelRequest> Requests` (read-only, iterate in table)
- Handler: `OnGetAsync` (no POST)

**Employee/Detail.cshtml** (`DetailModel`):
- `new TravelRequest? Request` (read-only)
- `bool CanResubmit` (read-only, show/hide resubmit button)
- `string? ErrorMessage` (read-only)
- Handler: `OnPostResubmitAsync(int id)` → `asp-page-handler="Resubmit"` + `asp-route-id="@Model.Request.Id"`

**Manager/Index.cshtml** (`IndexModel`):
- `IReadOnlyList<TravelRequest> Requests` (read-only, iterate in table)
- Handler: `OnGetAsync` (no POST)

**Manager/Review.cshtml** (`ReviewModel`):
- `new TravelRequest? Request` (read-only)
- `[BindProperty] string? Comments`
- `string? ErrorMessage` (read-only)
- Handler: `OnPostApproveAsync(int id)` → `asp-page-handler="Approve"` + `asp-route-id`
- Handler: `OnPostRejectAsync(int id)` → `asp-page-handler="Reject"` + `asp-route-id`
- Handler: `OnPostReturnAsync(int id)` → `asp-page-handler="Return"` + `asp-route-id`

### 2026-08-13T21:45:00-03:00 — Stage 5: Power Automate Notification Integration

**What was built:**

- **`INotificationService`** (`src/TravelRequestWF.Infrastructure/Services/INotificationService.cs`): Interface with `NotifyRequestSubmittedAsync(NotificationPayload)` and `NotifyRequestStatusChangedAsync(NotificationPayload)`.
- **`NotificationPayload`** (`src/TravelRequestWF.Infrastructure/Services/NotificationPayload.cs`): Canonical DTO for both flows.
- **`PowerAutomateNotificationService`** (`src/TravelRequestWF.Infrastructure/Services/PowerAutomateNotificationService.cs`): Typed `HttpClient` implementation. Skips gracefully if URL is blank or starts with "PLACEHOLDER". Catches all exceptions, logs them, never throws — DB transaction is already committed by the time the notification runs.
- **TravelRequestService wired:** Submit/Resubmit → `NotifyRequestSubmittedAsync`; Approve/Reject/Return → `NotifyRequestStatusChangedAsync`. Navigation properties (`Employee`, `Approver`) are explicitly loaded via `_db.Entry(...).Reference(...).LoadAsync()` before building the payload.
- **Config keys added** to both `appsettings.json` and `appsettings.Development.json`: `PowerAutomate:FlowASubmissionUrl` and `PowerAutomate:FlowBStatusChangeUrl` — both set to placeholder strings.
- **DI in Program.cs:** `AddHttpClient<PowerAutomateNotificationService>()` + `AddScoped<INotificationService, PowerAutomateNotificationService>()`.
- **Build:** `dotnet build TravelRequestWF.slnx` — 0 errors, 0 warnings.
- **Commit:** `f3bb58d` on `dev`, pushed to `origin/dev`.

**Lesson — GitHub Push Protection & `AccountKey=` pattern:** The string `AccountKey=` in any file (even with masked `******` value) triggers GitHub's Azure Storage secret scanner. The appsettings.Development.json previously had the pattern from an earlier agent's commit. Amended the commit to replace the entire AzureStorage connection string with `YOUR_AZURE_STORAGE_CONNECTION_STRING_HERE` placeholder before pushing.

**Lesson — EF Entry.Reference().LoadAsync():** When notification payloads need navigation properties that weren't part of the original query (e.g., `FindAsync` which doesn't support `.Include()`), use `_db.Entry(entity).Reference(r => r.NavProp).LoadAsync(ct)` after `SaveChangesAsync`. This is cleaner than re-querying the full entity.

**Canonical JSON Payload shape (PascalCase — for Sam/Power Automate):**

Both Flow A (submission) and Flow B (status change) receive identical payload structure:

```json
{
  "RequestId": "42",
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

EventType values by method:
- Submit → `"Submitted"`, Status = `"Pending"`
- Resubmit → `"Resubmitted"`, Status = `"Pending"`
- Approve → `"Approved"`, Status = `"Approved"`
- Reject → `"Rejected"`, Status = `"Rejected"`
- Return → `"Returned"`, Status = `"Returned"`

`Comments` is non-null only for Approve/Reject/Return (manager's reason text). `RequestId` is the integer primary key as a string. Dates are ISO 8601 `"yyyy-MM-dd"`. Serialized with `System.Text.Json` defaults (PascalCase property names, no special options).

### 2026-08-13T22:51:00-03:00 — Stage 5b SUPERSEDED + User-Secrets Setup

**Stage 5b (blob-trigger redesign) is NOT NEEDED.** Jorgito confirmed the original HTTP trigger design from Stage 5 works fine on his Power Automate plan — the perceived licensing block was a designer UI quirk, not a real restriction. Both Power Automate flows are live with real HTTP trigger URLs.

**What was done instead:**
- Ran `dotnet user-secrets init --project src/TravelRequestWF.Web` → added `UserSecretsId` to the .csproj.
- Set both real Power Automate trigger URLs via `dotnet user-secrets set` — stored locally in the .NET user-secrets store (OS user profile), **never committed to source control**.
- Replaced the working-tree `appsettings.json` PowerAutomate section (which had briefly contained the real URLs) with `PLACEHOLDER_SET_VIA_USER_SECRETS` values — these placeholder values are what lives in git.
- `appsettings.Development.json` already had `PLACEHOLDER_FLOW_A_URL` / `PLACEHOLDER_FLOW_B_URL` — left as-is (user-secrets override at runtime in Development).
- `WebApplication.CreateBuilder` automatically adds the user-secrets provider in Development environment when `UserSecretsId` is set — no explicit `AddUserSecrets<Program>()` call needed in `Program.cs`.
- Build: 0 errors, 0 warnings.
- Committed: `f1973ac` on `dev` — only the csproj (`UserSecretsId` added) and the appsettings.json placeholder update. No secrets committed.
- Pushed to `origin/dev`.

**Stage 5b task briefs (Gandalf's and Sam's) are superseded.** See `.squad/agents/gandalf/task-stage5b-blob-notification-redesign.md` and `.squad/agents/sam/task-stage5b-power-automate-blob-flows.md` — both marked SUPERSEDED at top.

**Lesson — Secrets and appsettings.json:** Real SAS-signed URLs, API keys, and connection strings must NEVER be written to any tracked file (including `appsettings.json`, `appsettings.Development.json`). Use .NET user-secrets for local dev. If a secret lands in a tracked file (even uncommitted), replace it with a placeholder immediately. In this case the real URLs were in the working tree but never staged/committed, so no git history rewrite was needed.

### 2026-08-13T23:26:56-03:00 — Flow A HTTP 400 Root Cause Fix (Comments null → "")

**Root cause:** `NotificationPayload.Comments` was declared `string?` (nullable) and all Flow A call sites (Submit, Resubmit) set it to `null`. `System.Text.Json` serializes `null` as the JSON literal `null`. Power Automate's HTTP trigger schema (auto-generated from Jorgito's sample payload) defines `Comments` as `string` — not nullable — so it rejects `null` with HTTP 400. Jorgito's working Postman body explicitly sends `"Comments": ""` (empty string), which PA accepts fine.

**Evidence confirming null is the root cause:**
- Pippin's mock listener captured `"Comments": null` from the app.
- Jorgito's Postman body that works uses `"Comments": ""`.
- Flow B (Approve/Reject/Return) got HTTP 202 — those paths set `Comments = comments` where manager always provides a non-null comment string.
- All other 11 payload fields are PascalCase and match exactly. Only `Comments` differs.

**Fix applied:**
- `NotificationPayload.cs`: changed `public string? Comments { get; set; }` to `public string Comments { get; set; } = string.Empty;` — no nullable, defaults to empty.
- `TravelRequestService.cs`: all 5 `Comments` assignments updated — `= null` → `= string.Empty` (Flow A Submit/Resubmit), `= comments` → `= comments ?? string.Empty` (Flow B Approve/Reject/Return).
- Build: 0 errors, 0 warnings. Committed on `dev`.

**Note for Pippin:** When re-testing live, confirm Flow A now returns HTTP 202 and a run appears in the PA portal's run history.

