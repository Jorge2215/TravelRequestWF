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

### 2026-08-10T22:07:00-03:00 — Gitignore / Build Artifact Cleanup

- **bin/ and obj/ were accidentally committed early in the project.** `git rm -r --cached` is the correct fix: removes them from the index without touching disk. 352 files were untracked this way.
- **Root .gitignore** now has a comprehensive `.NET / Visual Studio` section covering `bin/`, `obj/`, `.vs/`, `*.dll`, `*.pdb`, `*.exe`, `*.cache`, `*.user`, NuGet, test results, and Rider/JetBrains dirs. Adding it after the fact only stops future tracking — `git rm --cached` must be run separately to untrack already-indexed files.
- **Commit `95d16d4`** on `dev`: 356 files changed — 352 build artifacts removed from index + 4 legitimate file updates. Working tree clean after push.

