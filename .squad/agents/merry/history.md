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

