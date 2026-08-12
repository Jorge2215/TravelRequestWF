# Pippin — Task Brief: Stage 2 Validation & ERD-vs-Schema Comparison

**Assigned by:** Aragorn  
**Date:** 2026-08-11T22:35:00-03:00  
**Depends on:** Gandalf's `AuditLogDocumentLink` migration merged to `dev`  
**Branch:** `dev` (read-only validation work; any output files committed to `dev`)

---

## Context

Stage 2 success criteria require:
> "Verify that the generated schema matches the ERD (tables, columns, relationships). Ensure foreign keys and constraints are correctly applied. Document any discrepancies or adjustments needed."

Gandalf has (or will have) applied the `AuditLogDocumentLink` migration that closes the known `AuditLogEntry` gap. Your job is to validate the entire schema end-to-end and produce the comparison artifact.

The authoritative ERD is at `.squad/er-diagram.md`.  
The EF Core entities are in `TravelRequestWF.Infrastructure/Entities/`.  
The DbContext is at `TravelRequestWF.Infrastructure/Data/AppDbContext.cs`.

---

## Your Deliverables

### A — Verify Migration Applied (LocalDB)

Run:
```
dotnet ef migrations list --project TravelRequestWF.Infrastructure --startup-project TravelRequestWF.Web
```

Confirm both `InitialCreate` and `AuditLogDocumentLink` are listed and applied (no `(Pending)` marker on them for the active connection).

### B — Verify FK Constraints in EF Core Configuration

Read `AppDbContext.cs` and verify:

1. `Employee.SuperiorId` — self-referencing FK, nullable, Restrict delete ✓
2. `TravelRequest.EmployeeId` — FK → Employee, required, Restrict delete ✓
3. `TravelRequest.ApproverId` — FK → Employee, nullable or required (document what's configured), Restrict delete ✓
4. `RequestDocument.TravelRequestId` — FK → TravelRequest, required, Restrict/Cascade (document) ✓
5. `AuditLogEntry.TravelRequestId` — FK → TravelRequest, **nullable**, Restrict delete ✓ (NEW)
6. `AuditLogEntry.RequestDocumentId` — FK → RequestDocument, **nullable**, Restrict delete ✓ (NEW)

Flag any FK that doesn't match expectations.

### C — Produce ERD-vs-Schema Comparison Table

Create `.squad/files/stage2-erd-vs-schema.md` with the following sections:

#### Section 1: Tables

| ERD Table (Spanish) | EF Core Entity | Present in Migration? | Notes |
|---|---|---|---|
| Empleados | Employee | ✓/✗ | |
| SolicitudesViaje | TravelRequest | ✓/✗ | |
| DocumentosAdjuntos | RequestDocument | ✓/✗ | |
| LogAuditoria | AuditLogEntry | ✓/✗ | |

#### Section 2: Columns (entity by entity)

For each entity, list ERD column → EF Core property → Type match → Present.

**Employee**
| ERD Column | EF Core Property | Type | Present | Notes |
|---|---|---|---|---|
| EmpleadoID | Id | int PK | ✓/✗ | |
| Nombre | Name | string | ✓/✗ | |
| Email | Email | string | ✓/✗ | |
| Departamento | Department | string | ✓/✗ | |
| SuperiorID | SuperiorId | int? FK | ✓/✗ | |

**TravelRequest**
| ERD Column | EF Core Property | Type | Present | Notes |
|---|---|---|---|---|
| SolicitudID | Id | int PK | ✓/✗ | |
| EmpleadoID | EmployeeId | int FK | ✓/✗ | |
| AprobadorID | ApproverId | int? FK | ✓/✗ | |
| Destino | Destination | string | ✓/✗ | |
| FechaInicio | StartDate | DateTime | ✓/✗ | |
| FechaFin | EndDate | DateTime | ✓/✗ | |
| Motivo | Purpose | string | ✓/✗ | |
| Estado | Status | enum/string | ✓/✗ | |

**RequestDocument**
| ERD Column | EF Core Property | Type | Present | Notes |
|---|---|---|---|---|
| DocumentoID | Id | int PK | ✓/✗ | |
| SolicitudID | TravelRequestId | int FK | ✓/✗ | |
| NombreArchivo | FileName | string | ✓/✗ | |
| URLArchivo | BlobUrl | string | ✓/✗ | |

**AuditLogEntry**
| ERD Column | EF Core Property | Type | Present | Notes |
|---|---|---|---|---|
| LogID | Id | int PK | ✓/✗ | |
| SolicitudID | TravelRequestId | int? FK | ✓/✗ | Made nullable in Stage 2 |
| (ERD gap closed) | RequestDocumentId | int? FK | ✓/✗ | Added in Stage 2 |
| Acción | Action | string | ✓/✗ | |
| FechaHora | Timestamp | DateTime | ✓/✗ | |
| Usuario | ActorId | string/int | ✓/✗ | |

#### Section 3: Relationships

| ERD Relationship | EF Core Config | DeleteBehavior | Status | Notes |
|---|---|---|---|---|
| Empleados 1→N self (SuperiorID) | Employee.SuperiorId self-ref | Restrict | ✓/✗ | |
| Empleados 1→N SolicitudesViaje (EmpleadoID) | TravelRequest.EmployeeId | Restrict | ✓/✗ | |
| Empleados 1→N SolicitudesViaje (AprobadorID) | TravelRequest.ApproverId | Restrict | ✓/✗ | |
| SolicitudesViaje 1→N DocumentosAdjuntos | RequestDocument.TravelRequestId | Restrict/Cascade | ✓/✗ | |
| SolicitudesViaje 1→N LogAuditoria | AuditLogEntry.TravelRequestId | Restrict | ✓/✗ | |
| DocumentosAdjuntos →N LogAuditoria | AuditLogEntry.RequestDocumentId | Restrict | ✓/✗ | Stage 2 addition |

#### Section 4: Summary

State clearly:
- Total discrepancies found: N
- List any remaining gaps or deviations from the ERD
- Confirm Stage 2 success criteria met (or not, with specifics)

### D — Commit Output

Commit `.squad/files/stage2-erd-vs-schema.md` to `dev` with message:
```
docs: Stage 2 ERD-vs-schema validation report

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
```

---

## Definition of Done

- [ ] Both migrations confirmed applied (no pending migrations)
- [ ] All 6 FK relationships verified in AppDbContext
- [ ] `stage2-erd-vs-schema.md` produced with all 4 sections filled in
- [ ] Summary section explicitly states whether Stage 2 success criteria are met
- [ ] Report committed and pushed to `dev`
