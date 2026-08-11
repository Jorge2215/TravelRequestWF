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
