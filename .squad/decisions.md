# Squad Decisions

## Active Decisions

### 2026-08-09T21:35:58.924-03:00: Decision
**By:** Aragorn
**What:** Connected TravelRequestWF to GitHub repo Jorge2215/TravelRequestWF for issue tracking.
**Why:** User confirmed the remote repository for the project.

### 2026-08-10T22:03:59.308-03:00: Milestone
**By:** Jorgito
**What:** Azure SQL Database provisioned and `InitialCreate` EF Core migration applied successfully against it. Stage 1 success criteria (schema created and accessible in Azure SQL) is now fully met.
**Why:** Closes the last open item from Stage 1 (Azure SQL was previously deferred pending credentials).

### 2026-08-10T21:07:40.356-03:00: User directive
**By:** Jorgito (via Copilot)
**What:** Local branch renamed to `dev`; remote `origin/dev` created and tracked. All team work commits push to `dev`. Remote `main` is reserved exclusively for GitHub Actions deployment to Azure — never push work commits directly to `main`.
**Why:** User request — keep deploy pipeline isolated from active development.

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction

---

# Merged decision inbox (merge performed 2026-08-09T22:02:03-03:00)

The following entries were merged from files in `.squad/decisions/inbox/`. Duplicate entries (exact duplicates) were skipped.

---

### From: aragorn-prd-decomposition.md

# Aragorn — PRD Decomposition: Decisions & Assumptions

### 2026-08-09T21:47:42-03:00: Assumption — Manager hierarchy
**By:** Aragorn
**What:** Assuming a simple single-level manager relationship: each employee has one ManagerId (FK to the same Users table). No multi-level approval chains for the PoC.
**Why:** PRD says "direct manager" — simplest model that satisfies it. Multi-level can be added later without breaking the schema.

### 2026-08-09T21:47:42-03:00: Assumption — Authentication
**By:** Aragorn
**What:** Assuming ASP.NET Identity with local accounts for the PoC. No Azure AD/Entra ID integration yet.
**Why:** PRD doesn't specify an auth provider. Local Identity is the fastest path to a working PoC with role-based access (Employee vs Manager). Entra ID can replace it later.

### 2026-08-09T21:47:42-03:00: Decision — Single Razor Pages project
**By:** Aragorn
**What:** One ASP.NET Core Razor Pages project for both employee and manager views. Separate Areas or folder-based separation, not separate apps.
**Why:** PoC scope doesn't justify the deployment overhead of multiple front-ends. Role-based authorization gates the views.

### 2026-08-09T21:47:42-03:00: Decision — EF Core for data access
**By:** Aragorn
**What:** Use Entity Framework Core with Azure SQL as the ORM, code-first migrations.
**Why:** Standard for .NET Razor Pages apps. Keeps schema in source control and aligns with the team's stack.

### 2026-08-09T21:47:42-03:00: Decision — Azure Blob Storage for documents
**By:** Aragorn
**What:** Store uploaded files in Azure Blob Storage with container-per-environment. Reference blobs by URI in the TravelRequest record.
**Why:** PRD says "Azure Storage Account" for documents. Blob Storage is the natural fit; blob URIs keep the SQL schema clean.

### 2026-08-09T21:47:42-03:00: Assumption — Email delivery for daily report
**By:** Aragorn
**What:** Assuming the Azure Function daily report will use SendGrid or an SMTP relay for email. Exact provider TBD — the Function will accept an IEmailSender abstraction.
**Why:** PRD says "sends each manager a report" but doesn't specify the channel. An abstraction lets us swap providers without touching the Function logic.

### 2026-08-09T21:47:42-03:00: Decision — Power Automate scope limited to notification
**By:** Aragorn
**What:** For the PoC, Power Automate handles notification routing only (email to manager on new request, email to employee on decision). The actual state transitions live in the .NET backend, not in Power Automate.
**Why:** Keeping business logic in code makes it testable and version-controlled. Power Automate is the notification bus, not the workflow engine.

---

### From: coordinator-prd-clarifications.md

### 2026-08-09T21:47:03-03:00: Decision
**By:** Jorgito (via Coordinator)
**What:** Manager assignment for the PoC uses hardcoded seed data (no AD/HR system integration for now).
**Why:** User confirmed this is sufficient for the PoC scope.

### 2026-08-09T21:47:03-03:00: Decision
**By:** Jorgito (via Coordinator)
**What:** Auth provider is local ASP.NET Identity, not Azure Entra ID.
**Why:** Simpler for PoC; matches Aragorn's original assumption.

### 2026-08-09T21:47:03-03:00: Decision
**By:** Jorgito (via Coordinator)
**What:** Email delivery is stubbed/logged for the PoC — no SendGrid/SMTP integration yet.
**Why:** User confirmed stubbing is sufficient for PoC scope.

### 2026-08-09T21:47:03-03:00: Decision
**By:** Jorgito (via Coordinator)
**What:** Team has Power Automate Premium licensing — Sam can use HTTP trigger connectors for flows.
**Why:** User confirmed premium licensing is available.

### 2026-08-09T21:47:03-03:00: Decision
**By:** Jorgito (via Coordinator)
**What:** Target framework is confirmed as .NET 10.
**Why:** User confirmed Aragorn's assumption.

---

### From: aragorn-functional-spec-reconciliation.md

# Decisions — Functional Spec Reconciliation

### 2026-08-09T21:55:00-03:00: Decision
**By:** Aragorn  
**What:** Audit Log will be a dedicated `AuditLogEntry` table written via an `IAuditLogger` service (new WI-10).  
**Why:** Functional spec §5 explicitly requires recording submission, approval, rejection, and return actions. A separate table keeps the audit concern decoupled from the request entity and allows future compliance/reporting without polluting the core model.

### 2026-08-09T21:55:00-03:00: Decision
**By:** Aragorn  
**What:** "Assigned Travel" (spec §3.3) is NOT a separate table — it's simply a TravelRequest with Status=Approved.  
**Why:** The spec says "Approved requests are recorded in the database as Assigned Travel." For a PoC this is just the Approved state. Adding a separate entity gains nothing now and can be introduced later if business rules diverge.

### 2026-08-09T21:55:00-03:00: Decision
**By:** Aragorn  
**What:** Notification trigger points are explicitly codified as WI-11, separate from the stub service (WI-9).  
**Why:** The functional spec is precise about WHO gets notified WHEN (reject→employee, return→employee, resubmit→manager). Separating "the triggers" from "the delivery mechanism" ensures we wire the logic correctly even though delivery is stubbed for PoC.

### 2026-08-09T21:55:00-03:00: Decision
**By:** Aragorn  
**What:** Audit log retention for PoC: keep all records, no purge.  
**Why:** No business requirement for deletion in the spec; simplest path. Revisit for production (GDPR, storage costs).

---

### From: coordinator-functional-spec-clarifications.md

### 2026-08-09T21:54:36-03:00: Decision
**By:** Jorgito (via Coordinator)
**What:** Audit log will be queried directly from the DB for the PoC — no admin UI page to browse entries.
**Why:** User confirmed direct DB query is sufficient for PoC scope.

### 2026-08-09T21:54:36-03:00: Decision
**By:** Jorgito (via Coordinator)
**What:** Email-only notifications are the long-term plan — no in-app alerts needed, now or later.
**Why:** User confirmed email-only satisfies requirements.

### 2026-08-09T21:54:36-03:00: Decision
**By:** Jorgito (via Coordinator)
**What:** "Assigned Travel" is represented simply as Status=Approved — no separate booking/assignment entity.
**Why:** User confirmed this simplification is acceptable for the PoC.

---

### From: aragorn-architecture-reconciliation.md

# Architecture Reconciliation — Aragorn

### 2026-08-09T21:59:00-03:00: Note — Auth Conflict (PENDING USER DECISION)
**By:** Aragorn  
**What:** Architecture doc specifies Azure Active Directory (AAD) for authentication. This CONFLICTS with prior user decision to use local ASP.NET Identity. Both options remain on the table — awaiting user resolution.  
**Why:** Cannot proceed with auth implementation until this is settled. Affects WI-1 (scaffolding) and WI-6 (role-based access on pages).

### 2026-08-09T21:59:00-03:00: Decision — Azure App Service as hosting target
**By:** Aragorn  
**What:** Architecture doc specifies Azure App Service for the web app. Adopted as the deployment target (no conflict with existing plan, which was host-agnostic). Added as a note to WI-1.  
**Why:** Aligns with architecture doc; does not change PoC dev workflow (still runs locally via Kestrel). Deployment scripts/infra can be added later.

### 2026-08-09T21:59:00-03:00: Decision — Audit logs stored in Azure SQL alongside requests (confirmed)
**By:** Aragorn  
**What:** Architecture doc confirms audit logs live in the same Azure SQL database as travel requests. No change needed — WI-10 already designs this.  
**Why:** Consistency check; architecture doc and existing plan agree.

### 2026-08-09T21:59:00-03:00: Note — Logic Apps for SAP/Ariba is explicitly "future"
**By:** Aragorn  
**What:** Architecture doc lists Logic Apps for future SAP/Ariba integration. No PoC action. WI-8 (Power Automate) remains the current orchestration tool.  
**Why:** Confirming scope boundary — no new work items needed for Logic Apps.

---

### From: coordinator-auth-conflict-resolution.md

### 2026-08-09T21:58:15-03:00: Decision
**By:** Jorgito (via Coordinator)
**What:** Confirmed auth remains local ASP.NET Identity, NOT Azure AD/Entra ID, despite the architecture doc mentioning AAD. This supersedes the architecture doc on this point.
**Why:** User re-confirmed the earlier PoC decision after Aragorn flagged the conflict — simpler and faster for PoC scope with hardcoded seed data. AAD/Entra ID integration is deferred to a future phase if this becomes a production system.

### 2026-08-09T21:58:15-03:00: Decision
**By:** Jorgito (via Coordinator)
**What:** Web app hosting target is Azure App Service, per the architecture doc.
**Why:** No conflicting prior decision; adopted as-is from architecture document.

---

### From: aragorn-backlog-reconciliation.md

# Backlog Reconciliation — User Stories ↔ Work Items

## ⚠️ CONFLICTS REQUIRING USER DECISION

### 2026-08-09T22:02:03-03:00: Decision (PENDING — conflict flagged)
**By:** Aragorn
**What:** US 10 acceptance criteria says "Autenticación mediante Azure AD" — this **conflicts** with the standing decision (2026-08-09T21:58:15) that auth is LOCAL ASP.NET Identity for the PoC.
**Why:** The user already confirmed local Identity supersedes any AAD reference. US 10's acceptance criteria should be updated to read "Autenticación mediante ASP.NET Identity (cuentas locales). Roles asignados (Empleado / Gerente)." Awaiting user confirmation before modifying the backlog.

### 2026-08-09T22:02:03-03:00: Decision (PENDING — conflict flagged)
**By:** Aragorn
**What:** US 08 acceptance criteria says "Se crea registro en tabla 'ViajesAsignados'" — this **conflicts** with the standing decision (2026-08-09T21:55:00) that Status=Approved suffices with NO separate table.
**Why:** We explicitly decided a separate "ViajesAsignados" entity is unnecessary for the PoC. US 08 should be updated to say "La solicitud cambia a estado 'Aprobado', lo cual constituye el registro de viaje asignado (sin tabla separada)." Awaiting user confirmation.

---

### From: coordinator-backlog-conflict-resolution.md

### 2026-08-09T22:02:03-03:00: Decision
**By:** Jorgito (via Coordinator)
**What:** US 10 acceptance criteria updated to reference local ASP.NET Identity authentication instead of Azure AD.
**Why:** Confirms the standing decision (local Identity for PoC) over the backlog proposal's mention of Azure AD.

### 2026-08-09T22:02:03-03:00: Decision
**By:** Jorgito (via Coordinator)
**What:** US 08 acceptance criteria updated to drop the separate "ViajesAsignados" table — approved requests are represented as Status=Approved on the existing TravelRequest record.
**Why:** Confirms the standing decision to avoid a redundant entity for the PoC.

---

---

### From: aragorn-er-diagram-reconciliation.md

# ER Diagram Reconciliation — Aragorn

### 2026-08-09T22:09:51-03:00: Decision
**By:** Aragorn
**What:** ER diagram schema (Empleados, SolicitudesViaje, DocumentosAdjuntos, LogAuditoria) is consistent with prior decisions and WI-1/WI-2/WI-10. Adopted as the concrete schema reference. English C#/EF Core naming mapped below.
**Why:** The diagram confirms: (1) self-referencing SuperiorID for manager hierarchy = our "hardcoded seed data" approach, (2) separate LogAuditoria table = our WI-10 design, (3) no "ViajesAsignados" table = consistent with Status=Approved decision, (4) DocumentosAdjuntos = our RequestDocument. No conflicts found with standing decisions.

### 2026-08-09T22:09:51-03:00: Decision
**By:** Aragorn
**What:** WI-1/WI-2 data model updated with concrete field names from ER diagram. English EF Core entity mapping:

| Spanish (Diagram) | English (EF Core Entity) | Notes |
|---|---|---|
| Empleados | `Employee` | |
| EmpleadoID | `Employee.Id` | PK |
| Nombre | `Employee.Name` | |
| Email | `Employee.Email` | |
| Departamento | `Employee.Department` | |
| SuperiorID | `Employee.SuperiorId` | FK self-ref, nullable for top-level |
| SolicitudesViaje | `TravelRequest` | |
| SolicitudID | `TravelRequest.Id` | PK |
| EmpleadoID | `TravelRequest.EmployeeId` | FK → Employee |
| AprobadorID | `TravelRequest.ApproverId` | FK → Employee (⚠️ see design question below) |
| Destino | `TravelRequest.Destination` | |
| FechaInicio | `TravelRequest.StartDate` | |
| FechaFin | `TravelRequest.EndDate` | |
| Motivo | `TravelRequest.Purpose` | |
| Estado | `TravelRequest.Status` | Enum: Pending, Approved, Rejected, Returned |
| DocumentosAdjuntos | `RequestDocument` | |
| DocumentoID | `RequestDocument.Id` | PK |
| SolicitudID | `RequestDocument.TravelRequestId` | FK → TravelRequest |
| NombreArchivo | `RequestDocument.FileName` | |
| URLArchivo | `RequestDocument.BlobUrl` | Azure Blob Storage URL |
| LogAuditoria | `AuditLogEntry` | |
| LogID | `AuditLogEntry.Id` | PK |
| SolicitudID | `AuditLogEntry.TravelRequestId` | FK → TravelRequest |
| Acción | `AuditLogEntry.Action` | |
| FechaHora | `AuditLogEntry.Timestamp` | |
| Usuario | `AuditLogEntry.ActorId` | |

**Why:** Concrete field names ensure all team members (Gandalf for EF Core model, Legolas for Razor Pages bindings, Pippin for test assertions) reference the same schema shape.

### 2026-08-09T22:09:51-03:00: Decision
**By:** Aragorn
**What:** WI-10 (Audit Log) confirmed fully consistent with `LogAuditoria` table shape. No changes needed.
**Why:** LogAuditoria has: LogID (PK), SolicitudID (FK), Acción, FechaHora, Usuario — maps exactly to our existing AuditLogEntry design (Id, TravelRequestId, Action, Timestamp, ActorId). The ER diagram validates our prior design.

### 2026-08-09T22:09:51-03:00: Design Question (PENDING USER DECISION)
**By:** Aragorn
**What:** The ER diagram has BOTH `Empleados.SuperiorID` (the employee's direct manager in the org hierarchy) AND `SolicitudesViaje.AprobadorID` (the approver assigned to a specific request). These are separate fields pointing to different Employee records potentially.

**Question for Jorgito:** For this PoC, should `TravelRequest.ApproverId` always default to the employee's direct manager (`Employee.SuperiorId`) at submission time, or is there a real scenario where a *different* approver can be assigned per-request?

- **Option A (simple):** ApproverId is auto-populated from Employee.SuperiorId on submission. No UI to pick a different approver. The field exists for future flexibility but is always = SuperiorId for now. WI-1 seed data only needs the manager hierarchy.
- **Option B (flexible):** There's a mechanism to assign a different approver (e.g., delegation during vacations, cross-department approvals). This requires additional UI/logic in WI-3 or a backend assignment rule.

**Impact:** Option A keeps WI-1/WI-3 simple. Option B adds scope (approver selection or assignment logic).
**Why:** Cannot finalize the submission flow (WI-3) or seed data shape (WI-1) without knowing intent.


### From: coordinator-approver-decision.md

### 2026-08-09T22:09:51-03:00: Decision
**By:** Jorgito (via Coordinator)
**What:** TravelRequest.ApproverId always defaults to the employee's direct manager (Employee.SuperiorId) at submission time. No per-request approver-picker UI is needed for the PoC.
**Why:** User confirmed simplicity (Option A) is sufficient — no real scenario requiring a different approver was identified for this PoC scope.

# End of merged inbox
