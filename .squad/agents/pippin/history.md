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
