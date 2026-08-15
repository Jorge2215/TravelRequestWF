# Stage 2 — ERD vs Schema Validation Report

**Author:** Pippin (Tester)  
**Date:** 2026-08-11T22:43:57-03:00  
**Branch:** dev  
**Migrations verified against:** Azure SQL (live connection)

---

## Section 1: Tables

| ERD Table (Spanish) | EF Core Entity | Present in Migration? | Notes |
|---|---|---|---|
| Empleados | Employee | ✓ | Created in `InitialCreate` |
| SolicitudesViaje | TravelRequest | ✓ | Created in `InitialCreate` |
| DocumentosAdjuntos | RequestDocument | ✓ | Created in `InitialCreate` |
| LogAuditoria | AuditLogEntry | ✓ | Created in `InitialCreate`; extended in `AuditLogDocumentLink` |

---

## Section 2: Columns

### Employee

| ERD Column | EF Core Property | Type | Present | Notes |
|---|---|---|---|---|
| EmpleadoID | Id | int PK | ✓ | |
| Nombre | Name | string | ✓ | |
| Email | Email | string | ✓ | |
| Departamento | Department | string | ✓ | |
| SuperiorID | SuperiorId | int? FK | ✓ | Nullable, self-referencing |

### TravelRequest

| ERD Column | EF Core Property | Type | Present | Notes |
|---|---|---|---|---|
| SolicitudID | Id | int PK | ✓ | |
| EmpleadoID | EmployeeId | int FK | ✓ | Required (non-nullable) |
| AprobadorID | ApproverId | int FK | ✓ | **Non-nullable in entity** (see notes in Section 3) |
| Destino | Destination | string | ✓ | |
| FechaInicio | StartDate | DateOnly | ✓ | ERD says DATE — DateOnly is correct .NET 10 mapping |
| FechaFin | EndDate | DateOnly | ✓ | ERD says DATE — DateOnly is correct .NET 10 mapping |
| Motivo | Purpose | string | ✓ | |
| Estado | Status | TravelRequestStatus enum | ✓ | Stored as int; maps to Pending/Approved/Rejected/Returned |

### RequestDocument

| ERD Column | EF Core Property | Type | Present | Notes |
|---|---|---|---|---|
| DocumentoID | Id | int PK | ✓ | |
| SolicitudID | TravelRequestId | int FK | ✓ | Required (non-nullable) |
| NombreArchivo | FileName | string | ✓ | |
| URLArchivo | BlobUrl | string | ✓ | |

### AuditLogEntry

| ERD Column | EF Core Property | Type | Present | Notes |
|---|---|---|---|---|
| LogID | Id | int PK | ✓ | |
| SolicitudID | TravelRequestId | int? FK | ✓ | Made nullable in `AuditLogDocumentLink` |
| (Stage 2 addition) | RequestDocumentId | int? FK | ✓ | Added in `AuditLogDocumentLink` |
| Acción | Action | string | ✓ | |
| FechaHora | Timestamp | DateTime | ✓ | |
| Usuario | ActorId | string | ✓ | ERD shows Usuario as VARCHAR; implemented as string ActorId |

---

## Section 3: Relationships

| ERD Relationship | EF Core Config | DeleteBehavior | Status | Notes |
|---|---|---|---|---|
| Empleados 1→N self (SuperiorID) | `Employee.Superior` / `Employee.Subordinates` | Restrict | ✓ | Configured in `OnModelCreating` |
| Empleados 1→N SolicitudesViaje (EmpleadoID) | `TravelRequest.Employee` / `Employee.TravelRequests` | Restrict | ✓ | Configured in `OnModelCreating` |
| Empleados 1→N SolicitudesViaje (AprobadorID) | `TravelRequest.Approver` / `Employee.ApprovalRequests` | Restrict | ✓ | Configured in `OnModelCreating`; `ApproverId` is **required (non-nullable)** — consistent with "auto-populated from SuperiorId at submission" decision |
| SolicitudesViaje 1→N DocumentosAdjuntos | `RequestDocument.TravelRequest` / `TravelRequest.Documents` | **Not explicitly configured** | ⚠️ | No explicit `OnModelCreating` config found for this FK; EF Core will apply default (Cascade for required FK). Actual behavior: Cascade. ERD does not specify behavior; flagging for Gandalf to confirm. |
| SolicitudesViaje 1→N LogAuditoria | `AuditLogEntry.TravelRequest` / `TravelRequest.AuditLog` | Restrict | ✓ | Configured with `.IsRequired(false)` in `OnModelCreating` |
| DocumentosAdjuntos →N LogAuditoria | `AuditLogEntry.RequestDocument` / `WithMany()` | Restrict | ✓ | Configured with `.IsRequired(false)` in `OnModelCreating`; no inverse nav on `RequestDocument` |

---

## Section 4: Summary

### Migrations Status

| Migration | Status |
|---|---|
| `20260811002601_InitialCreate` | ✅ Applied |
| `20260812013905_AuditLogDocumentLink` | ✅ Applied |

No pending migrations. Both migrations applied to the live Azure SQL database.

### FK Verification Results (6 FKs)

| # | FK | Expected | Actual | Result |
|---|---|---|---|---|
| 1 | Employee.SuperiorId (self-ref) | nullable, Restrict | nullable, Restrict | ✅ |
| 2 | TravelRequest.EmployeeId | required, Restrict | required, Restrict | ✅ |
| 3 | TravelRequest.ApproverId | nullable or required, Restrict | **required** (non-nullable), Restrict | ✅ (consistent with decision to always populate from SuperiorId) |
| 4 | RequestDocument.TravelRequestId | required, Restrict or Cascade | required, **Cascade (EF Core default — not explicitly set)** | ⚠️ |
| 5 | AuditLogEntry.TravelRequestId | nullable, Restrict | nullable, Restrict | ✅ |
| 6 | AuditLogEntry.RequestDocumentId | nullable, Restrict | nullable, Restrict | ✅ |

### Discrepancies Found: 1

**⚠️ GAP — RequestDocument.TravelRequestId delete behavior not explicitly configured**

`AppDbContext.OnModelCreating` has no explicit configuration block for the `RequestDocument → TravelRequest` FK. EF Core will therefore default to **Cascade** for a required FK. This means deleting a `TravelRequest` will automatically delete its `RequestDocument` rows.

- This may or may not be the intended behavior — the task brief listed "Restrict/Cascade" as acceptable documentation, but the absence of explicit configuration is a code clarity issue.
- **Action required:** Gandalf should add an explicit `.OnDelete(DeleteBehavior.Restrict)` (or confirm Cascade is intentional) in `AppDbContext.OnModelCreating` for this relationship.

### Stage 2 Success Criteria

| Criterion | Result |
|---|---|
| Both migrations confirmed applied (no pending) | ✅ |
| All 6 FK relationships verified in AppDbContext | ✅ (with 1 gap flagged) |
| ERD tables all present in EF Core model | ✅ |
| ERD columns all mapped correctly | ✅ |
| `AuditLogEntry.TravelRequestId` made nullable | ✅ |
| `AuditLogEntry.RequestDocumentId` added with Restrict FK | ✅ |

**Overall Verdict: Stage 2 PASS with one advisory gap** — the `RequestDocument.TravelRequestId` delete behavior relies on EF Core's default (Cascade) rather than being explicitly declared. All ERD entities, columns, and relationships are present. The `AuditLogDocumentLink` migration is correctly applied.
