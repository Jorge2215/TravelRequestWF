# Gandalf — Task Brief: Stage 2 AuditLogEntry Fix & Migration

**Assigned by:** Aragorn  
**Date:** 2026-08-11T22:35:00-03:00  
**Branch:** `dev` (never `main`)

---

## Context

Stage 1 built all four EF Core entities and applied `InitialCreate` to Azure SQL. One gap remained unresolved:

The ERD's `LogAuditoria` table shows that audit entries can reference actions on either a `SolicitudesViaje` (TravelRequest) OR a `DocumentosAdjuntos` (RequestDocument). Our current `AuditLogEntry` entity only has a non-nullable `TravelRequestId` FK — `RequestDocument` support is missing.

---

## Your Deliverables (in order)

### A — Update `AuditLogEntry` Entity

File: `TravelRequestWF.Infrastructure/Entities/AuditLogEntry.cs`

1. Make `TravelRequestId` nullable (`int?`)
2. Add a new nullable `RequestDocumentId` (`int?`) property
3. Add the corresponding navigation properties:
   - `public TravelRequest? TravelRequest { get; set; }`
   - `public RequestDocument? RequestDocument { get; set; }`
4. Add an XML doc comment on the class or a `// NOTE` clarifying the invariant:
   > Exactly one of TravelRequestId or RequestDocumentId must be set. Both null and both non-null are invalid. Enforced at the service layer (IAuditLogger), not at the DB level.

### B — Update `AppDbContext` FK Configuration

File: `TravelRequestWF.Infrastructure/Data/AppDbContext.cs`

In `OnModelCreating`, for the `AuditLogEntry` entity:

1. Update the existing `TravelRequestId` relationship to be optional (`.IsRequired(false)`)
2. Keep `DeleteBehavior.Restrict` on it
3. Add a new relationship for `RequestDocumentId`:
   ```csharp
   entity.HasOne(a => a.RequestDocument)
         .WithMany()
         .HasForeignKey(a => a.RequestDocumentId)
         .IsRequired(false)
         .OnDelete(DeleteBehavior.Restrict);
   ```

### C — Add EF Core Migration

Do NOT touch or recreate `InitialCreate`.

Run:
```
dotnet ef migrations add AuditLogDocumentLink --project TravelRequestWF.Infrastructure --startup-project TravelRequestWF.Web
```

Verify the generated migration:
- `TravelRequestId` column altered to nullable `int?`
- New nullable `RequestDocumentId` column added with FK constraint → `RequestDocuments` table (Restrict delete)

### D — Apply Migration to Database

Check in this order for the Azure SQL connection string:
1. `TravelRequestWF.Web/appsettings.Development.json`
2. .NET user-secrets: `dotnet user-secrets list --project TravelRequestWF.Web`
3. Environment variable `ConnectionStrings__DefaultConnection`

**If a real Azure SQL connection string is available:**
```
dotnet ef database update --project TravelRequestWF.Infrastructure --startup-project TravelRequestWF.Web
```

**If NOT available (only LocalDB is configured locally):**
- Apply against LocalDB (the default in appsettings.json)
- Document the exact command Jorgito must run against Azure SQL:
  ```
  dotnet ef database update --project TravelRequestWF.Infrastructure --startup-project TravelRequestWF.Web --connection "<AzureSQLConnectionString>"
  ```
- Add a note in `.squad/agents/gandalf/history.md` with the migration name and the command

### E — Verify Build

```
dotnet build TravelRequestWF.sln
```

Build must succeed with 0 errors. Warnings are acceptable but note any new ones.

### F — Commit & Push to `dev`

Commit message:
```
feat: extend AuditLogEntry with nullable RequestDocumentId FK

- TravelRequestId made nullable (was non-nullable)
- Added nullable RequestDocumentId FK → RequestDocument (Restrict)
- Added AuditLogDocumentLink EF Core migration
- Enforces "exactly one FK set" at service layer, not DB level

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
```

Push to `dev` only. Do not touch `main`.

---

## Definition of Done

- [ ] `AuditLogEntry.cs` has both FKs nullable with nav properties and invariant note
- [ ] `AppDbContext.cs` configures both relationships with Restrict delete
- [ ] Migration `AuditLogDocumentLink` exists and looks correct
- [ ] Migration applied to LocalDB (and to Azure SQL if credentials available, otherwise documented)
- [ ] `dotnet build` succeeds with 0 errors
- [ ] Changes committed and pushed to `dev`
- [ ] If Azure apply was skipped, exact command documented in `.squad/agents/gandalf/history.md`
