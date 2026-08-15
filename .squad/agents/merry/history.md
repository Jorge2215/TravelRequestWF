# Merry — History

## Project Context

- **Project:** TravelRequestWF — a Web Application built on .NET 10 Razor Pages + Azure Functions, with Azure SQL Database and Power Automate flows.
- **Owner:** Jorgito
- **Team cast:** Lord of the Rings universe (Aragorn, Legolas, Gandalf, Merry, Sam, Pippin)
- **Merry's role:** Azure Functions / Integration Specialist
- **Repository root:** `C:\Users\Jorgito\source\repos\TravelRequestWF`
- **Active branch:** `dev` (never touch `main`)

### Solution structure

| Component | Path | TFM |
|---|---|---|
| Web (Razor Pages) | `src/TravelRequestWF.Web/` | net10.0 |
| Infrastructure (EF Core, entities, services) | `src/TravelRequestWF.Infrastructure/` | net8.0;net10.0 (multi-targeted as of Phase 8) |
| Functions (Azure Functions isolated worker) | `src/TravelRequestWF.Functions/` | net8.0 |

**Solution file:** `TravelRequestWF.slnx` at repo root.

### Key Infrastructure types
- `AppDbContext` — namespace `TravelRequestWF.Infrastructure.Data`
- `TravelRequest` — namespace `TravelRequestWF.Infrastructure.Entities`; fields: `Id`, `EmployeeId`, `Employee` (nav), `ApproverId`, `Approver` (nav), `Destination`, `StartDate` (DateOnly), `EndDate` (DateOnly), `Purpose`, `Status` (TravelRequestStatus enum: Pending/Approved/Rejected/Returned), `SubmittedAt`
- `Employee`: `Id`, `Name`, `Email`, `Department`, `SuperiorId`

---

## Task History

### Phase 9 — Daily Digest: GroupBy Manager + Flow C HTTP POST
**Date:** 2026-08-15 | **Requested by:** Jorgito (brief by Aragorn)

**Delivered:**
- `src/TravelRequestWF.Functions/DigestPayload.cs` — `PendingRequestItem` + `ManagerDigestPayload` sealed records.
- `src/TravelRequestWF.Functions/DailyPendingReportFunction.cs` — rewrote `RunAsync`: queries pending with `.Include(r => r.Approver)`, groups by `ApproverId`, calls `PostDigestAsync` per manager. Added `PostDigestAsync`: PLACEHOLDER guard + non-blocking try/catch per manager mirroring Phase 5 pattern exactly.
- `src/TravelRequestWF.Functions/Program.cs` — added `builder.Services.AddHttpClient()`.
- `local.settings.json` (gitignored) — added `"PowerAutomate:FlowCDailyDigestUrl": "PLACEHOLDER_FLOW_C_URL"`.
- Full solution build: **0 errors**.

**Deployment:** `func` CLI not installed on machine. Deployment skipped — instructions documented in `decisions.md` and summary below.

**Status:** Code complete. Awaiting: (1) Jorgito installs `func` tools and runs `func azure functionapp publish`; (2) Sam builds Flow C and provides the HTTP trigger URL; (3) Jorgito sets `PowerAutomate:FlowCDailyDigestUrl` in both local.settings.json and Azure Portal.

---

### Phase 8 — Daily Pending Report Timer Trigger Stub
**Date:** 2026-08-14 | **Requested by:** Jorgito (brief by Aragorn)

**Delivered:**
- `src/TravelRequestWF.Functions/TravelRequestWF.Functions.csproj` — net8.0 isolated worker
- `src/TravelRequestWF.Functions/Program.cs` — FunctionsApplication builder with AppDbContext DI
- `src/TravelRequestWF.Functions/DailyPendingReportFunction.cs` — Timer Trigger `0 0 8 * * *` (08:00 UTC daily), queries pending TravelRequests, logs structured `[DailyReport]` lines
- `src/TravelRequestWF.Functions/local.settings.json` — placeholder `SqlConnectionString` (gitignored, NOT committed)
- Updated root `.gitignore` with `local.settings.json` entry
- Multi-targeted `TravelRequestWF.Infrastructure` to `net8.0;net10.0` to resolve NU1201 cross-TFM build error
- Added project to `TravelRequestWF.slnx`
- Full solution build: **0 errors**

**Status:** Stub complete. `func start` against real Azure SQL deferred — user/Pippin to test with actual connection string.

---

## Learnings

### 1. Isolated worker vs in-process Azure Functions
- **Isolated worker** (current choice): separate process, `FunctionsApplication.CreateBuilder(args)` pattern (new API, similar to ASP.NET Core minimal host). Template uses OpenTelemetry + Azure Monitor Exporter by default.
- **In-process** (legacy): being deprecated — do NOT use for new projects.
- Configuration from `local.settings.json` `Values` section is available as flat `builder.Configuration["Key"]` entries.

### 2. EF Core cross-targeting notes
- EF Core major versions align with .NET major: EF Core 8 → net8.0, EF Core 9 → net9.0, EF Core 10 → net10.0.
- **NuGet NU1201** is a hard error (not suppressible via `<NoWarn>`) when a lower-TFM project references a higher-TFM library.
- **Solution:** Multi-target the shared library (`net8.0;net10.0`) with conditional `ItemGroup` per TFM using `Condition="'$(TargetFramework)' == 'net8.0'"`. EF Core migrations tooling uses the first-listed TFM — put the primary TFM first in `TargetFrameworks`.

### 3. NCRONTAB gotchas
- Azure Functions uses **six-part NCRONTAB** (`{seconds} {minutes} {hours} {day} {month} {weekday}`), NOT five-part cron.
- Timer Triggers fire in **UTC**. Argentina (ART, UTC-3): 8 AM ART = 11:00 UTC → use `0 0 11 * * *`.
- For quick local testing, temporarily change to `0 * * * * *` (every minute), then revert.

### 4. `dotnet new func` template behavior
- Template scaffolds files into the current directory when `--name` is given without `--output`. Use `--output <dirname>` or `cd` first.
- Template creates its own `.gitignore` (includes `local.settings.json`) and `Properties/launchSettings.json`.
- Newer template uses `FunctionsApplication.CreateBuilder` — adapt Program.cs accordingly.


### 5. IHttpClientFactory in Azure Functions isolated worker
- Register with uilder.Services.AddHttpClient() in Program.cs. The Functions host does NOT auto-register it.
- Inject IHttpClientFactory into the function constructor; call _httpClientFactory.CreateClient() per HTTP operation (not reusing the same instance).
- IConfiguration is automatically available via DI in the isolated worker model (the host registers it from local.settings.json Values section + environment). Just inject it in the constructor.

### 6. Non-blocking per-item HTTP loop pattern (Phase 5 mirror)
- Wrap each HTTP call in its own 	ry { ... } catch (Exception ex) { _logger.LogError(ex, ...); }.
- PLACEHOLDER guard: if (string.IsNullOrWhiteSpace(url) || url.StartsWith("PLACEHOLDER", ...)) { log info; return; }.
- Return a bool from the helper to track per-manager failure counts for the summary log line.
- This guarantees one manager's failure never aborts subsequent managers' digests.

### 7. local.settings.json management with special characters
- The dit tool may fail to match lines containing special characters (URLs with semicolons, asterisks, etc.) even with exact copy-paste.
- Fallback: use PowerShell Set-Content with a here-string @'...'@ to overwrite the entire file. Reliable regardless of special characters.

### 8. Deployment prerequisites check
- Always check unc --version and z functionapp list before claiming you can deploy.
- If unc is not installed, document the install command: 
pm install -g azure-functions-core-tools@4 --unsafe-perm true.
- Function App name must be found via z functionapp list --output table (or Azure Portal) — never guess it.


### 9. Sync Triggers BadRequest - Eager Config Throw at Host Startup
- **Root cause pattern:** Any ?? throw or GetRequiredSection called at the top level of Program.cs (outside a factory lambda) executes at host startup. If the referenced config key is absent in Azure Application Settings, the worker crashes before sync triggers can enumerate functions -> BadRequest.
- **Fix:** Use AddDbContext((serviceProvider, options) => { var cfg = serviceProvider.GetRequiredService<IConfiguration>(); ... }) factory overload so config resolution is deferred to first DI resolution (not host build time).
- **Runtime guard pattern:** Store config values as fields in the constructor (null-safe). At the top of the function method body, check for empty/placeholder values and LogWarning + eturn early. Never let missing config throw an unhandled exception from within a timer trigger body.
- **Ordering gotcha:** unc azure functionapp publish runs 'sync triggers' as part of the publish flow. This step requires the worker process to start cleanly. If Application Settings are set AFTER publish, but the code needs them at startup, the sequence is wrong. Always either (a) set Application Settings first, then publish; or (b) make the host startup null-safe (preferred) so order doesn't matter.