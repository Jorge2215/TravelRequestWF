# Entity-Relationship Diagram — Business Travel Request Workflow (PoC)

> Transcribed from `DiagramaEntRel.png` (user-provided ER diagram, Spanish labels).
> Diagram image copied to `.squad/files/er-diagram.png`.

## Entities

### Empleados
| Column | Type | Notes |
|--------|------|-------|
| EmpleadoID | Integer | PK |
| Nombre | VARCHAR | |
| Email | VARCHAR | |
| Departamento | VARCHAR | |
| SuperiorID | Integer | FK → Empleados.EmpleadoID (self-referencing, N:1) — represents the manager hierarchy |

### SolicitudesViaje
| Column | Type | Notes |
|--------|------|-------|
| SolicitudID | Integer | PK |
| EmpleadoID | Integer | FK → Empleados.EmpleadoID (N:1) — the requesting employee |
| AprobadorID | Integer | FK → Empleados.EmpleadoID — the assigned approver (separate from EmpleadoID; not necessarily the same as SuperiorID) |
| Destino | VARCHAR | |
| FechaInicio | DATE | |
| FechaFin | DATE | |
| Motivo | VARCHAR | |
| Estado | VARCHAR | Pending / Approved / Rejected / Returned |

### DocumentosAdjuntos
| Column | Type | Notes |
|--------|------|-------|
| DocumentoID | Integer | PK |
| SolicitudID | Integer | FK → SolicitudesViaje.SolicitudID (N:1) |
| NombreArchivo | VARCHAR | |
| URLArchivo | VARCHAR | Azure Storage blob URL |

### LogAuditoria
| Column | Type | Notes |
|--------|------|-------|
| LogID | Integer | PK |
| SolicitudID | Integer | FK → SolicitudesViaje.SolicitudID (N:1) |
| Acción | VARCHAR | |
| FechaHora | DATETIME | |
| Usuario | VARCHAR | |

## Relationships

- Empleados (1) → SolicitudesViaje (N) via EmpleadoID
- Empleados (1) → Empleados (N) via SuperiorID (self-referencing, manager hierarchy)
- SolicitudesViaje (1) → DocumentosAdjuntos (N) via SolicitudID
- SolicitudesViaje (1) → LogAuditoria (N) via SolicitudID
- DocumentosAdjuntos also links into LogAuditoria (diagram shows both SolicitudesViaje and DocumentosAdjuntos feeding LogAuditoria — audit log entries can reference actions on either the request or its documents)

## Notable Design Points vs Prior Decisions

- **Approver decoupled from manager:** `SolicitudesViaje.AprobadorID` is a separate FK from `Empleados.SuperiorID`. This means the approver for a given request doesn't have to be the employee's direct superior — worth confirming intent with the user.
- **Manager hierarchy mechanism:** `Empleados.SuperiorID` is the concrete field for the previously-decided "hardcoded seed data" approach to manager assignment.
- **Audit log shape:** `LogAuditoria` matches the already-planned WI-10 Audit Log component (DB-only, no admin UI).
