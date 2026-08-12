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

