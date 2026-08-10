# Work Items — TravelRequestWF PoC

> Reconciled against PRD + Functional Spec (2026-08-09)

## Phase 1 — Foundation

### WI-1: Project Scaffolding & Data Model
**Owner:** Gandalf  
**Status:** Not Started  
**⚠️ BLOCKED:** Auth approach PENDING USER DECISION — AAD vs local ASP.NET Identity (see `.squad/decisions/inbox/aragorn-architecture-reconciliation.md`).  
**Hosting target:** Azure App Service (per architecture doc).  
**Description:**  
- Create .NET 10 Razor Pages solution with ASP.NET Identity (local accounts) **← subject to auth decision**.
- Define EF Core data model:
  - `TravelRequest` (ID, EmployeeId, Destination, StartDate, EndDate, Purpose, Status, ManagerId, CreatedAt, UpdatedAt)
  - `RequestDocument` (ID, RequestId, FileName, BlobUrl, UploadedAt)
  - `AuditLogEntry` (ID, RequestId, Action, ActorId, Timestamp, Notes) ← **NEW from functional spec §5**
- Seed data: hardcoded Employee→Manager assignments (per decisions.md).
- Status enum: Pending, Approved, Rejected, Returned.

### WI-2: Azure SQL Database Setup
**Owner:** Gandalf  
**Status:** Not Started  
**Description:**  
- Azure SQL provisioning (or LocalDB for dev).
- EF Core migrations for all tables including AuditLog.
- Connection string configuration.

---

## Phase 2 — Core UI & Workflow

### WI-3: Employee Request Submission Page
**Owner:** Legolas  
**Status:** Not Started  
**Description:**  
- Razor Page form: Destination, Start/End dates, Purpose.
- File upload control (one or more documents).
- On submit: save request as Pending, upload docs to Azure Storage, create AuditLog entry (Action=Submitted).

### WI-4: Document Upload to Azure Storage
**Owner:** Gandalf  
**Status:** Not Started  
**Description:**  
- Azure Blob Storage integration (container per request or naming convention).
- Store blob URL in `RequestDocument` table.
- Dev: use Azurite emulator.

### WI-5: Manager Review Page
**Owner:** Legolas  
**Status:** Not Started  
**Description:**  
- List all Pending requests assigned to the logged-in manager.
- Actions: Approve, Reject, Return for more info (with comments field).
- On action: update status, create AuditLog entry.
- On Approve: record as Assigned Travel (functional spec §3.3).

### WI-6: Employee Resubmission Flow
**Owner:** Legolas  
**Status:** Not Started  
**⚠️ NOTE:** Role-based page access depends on auth decision (AAD vs local Identity).  
**Description:**
- Employee sees Returned requests with manager comments.
- Employee can edit and resubmit → status back to Pending.
- AuditLog entry (Action=Resubmitted).
- Notification triggered to manager (see WI-10).

---

## Phase 3 — Automation & Notifications

### WI-7: Daily Pending Report (Azure Function)
**Owner:** Merry  
**Status:** Not Started  
**Description:**  
- Timer-triggered Azure Function (daily schedule).
- Query pending requests grouped by manager.
- Generate report (email body or log for PoC).
- Must run consistently without manual intervention (non-functional req).

### WI-8: Power Automate Integration
**Owner:** Sam  
**Status:** Not Started  
**Description:**  
- HTTP-triggered flows (Premium licensing confirmed).
- Initial scope: orchestrate approval notification flow.
- Future: SAP/Ariba integration point.

### WI-9: Notification Service (Stub)
**Owner:** Gandalf  
**Status:** Not Started  
**Description:**  
- `INotificationService` interface with stub implementation (logs to console/file for PoC).
- Called on: Reject, Return, Resubmit events.
- Per decisions.md: no real email for PoC (stubbed/logged).

---

## Phase 4 — Audit, Polish & Testing

### WI-10: Audit Log Component ← **NEW**
**Owner:** Gandalf  
**Status:** Not Started  
**Description:**  
- Implement `IAuditLogger` service that writes to `AuditLogEntry` table.
- Actions tracked: Submitted, Approved, Rejected, Returned, Resubmitted.
- Called from request submission, manager review, and resubmission flows.
- Admin/read-only page to view audit trail per request (stretch goal for PoC).

### WI-11: Notification Trigger Points ← **NEW (refines WI-9)**
**Owner:** Legolas (UI hooks) + Gandalf (service)  
**Status:** Not Started  
**Description:**  
- Wire notification calls at correct points per functional spec §3.4:
  - Employee notified on Reject.
  - Employee notified on Return.
  - Manager notified on Resubmit.
- Uses stub from WI-9; ensures correct trigger points are coded even if delivery is logged only.

### WI-12: Integration & E2E Testing
**Owner:** Pippin  
**Status:** Not Started  
**Description:**  
- Verify full request lifecycle: Submit → Review → Approve/Reject/Return → Resubmit.
- Verify audit log entries created at each step.
- Verify daily report function fires and queries correctly.
- Verify document upload/retrieval round-trip.

---

## US ↔ WI Mapping (from Backlog Proposal 2026-08-09)

| US | WI(s) | Notes |
|----|-------|-------|
| US 01 | WI-1, WI-3 | |
| US 02 | WI-3, WI-4 | |
| US 03 | WI-3, WI-6 | Minor gap: no dedicated dashboard WI |
| US 04 | WI-5 | |
| US 05 | WI-5 | |
| US 06 | WI-5, WI-9, WI-11 | |
| US 07 | WI-5, WI-6, WI-9, WI-11 | |
| US 08 | WI-5 | ⚠️ CONFLICT: US says "ViajesAsignados" table; decision says Status=Approved suffices |
| US 09 | WI-7 | |
| US 10 | WI-1 | ⚠️ CONFLICT: US says Azure AD; decision says local ASP.NET Identity |
| US 11 | WI-1, WI-5, WI-6 | |
| — | WI-8, WI-10, WI-12 | No direct US (architecture/spec-driven) |

> **Pending conflicts** require user confirmation — see `.squad/decisions/inbox/aragorn-backlog-reconciliation.md`

---

## Open Questions (from Functional Spec)

| # | Question | Status |
|---|----------|--------|
| OQ-1 | Audit Log: retention policy? (keep forever for PoC, revisit for prod) | **Proposed: keep all, no purge for PoC** |
| OQ-2 | Audit Log: admin UI needed for PoC or just DB-queryable? | Pending Jorgito input |
| OQ-3 | "Notification" means email only, or also in-app (toast/badge)? | **Proposed: log-only stub for PoC; design interface to support both later** |
| OQ-4 | "Assigned Travel" (§3.3) — is this just the Approved status, or a separate entity/table? | **Proposed: same table, status=Approved suffices for PoC** |
