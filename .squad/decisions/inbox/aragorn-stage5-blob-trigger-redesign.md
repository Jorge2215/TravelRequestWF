# Aragorn — Stage 5 Blob Trigger Redesign: SUPERSEDES HTTP Trigger Decision

**Date:** 2026-08-13T22:15:00-03:00
**By:** Aragorn
**Stage:** 5b — Power Automate Notifications (Blob Transport Redesign)
**Supersedes:** `.squad/decisions/inbox/aragorn-stage5-notification-scope.md` — specifically Decision 1 (HTTP trigger) and Decision 6 (FlowA/B URL config keys). All other decisions from that document remain in force.

---

## Context — Why This Redesign Is Needed

Jorgito confirmed that his Power Automate plan is **NOT Premium**. The "When an HTTP request is received" (Request connector) is a **Premium-only trigger** — there is no workaround to make it available on a non-premium plan. This invalidates Decision 1 and Decision 6 from the prior Stage 5 scope document.

The SQL Server connector trigger ("When an item is created") is also Premium, ruling out that alternative.

**Research confirmed:** The **Azure Blob Storage connector** is a **Standard connector**, available on non-premium Power Automate plans. Both the trigger ("When a blob is added or modified (properties only)") and the actions ("Get blob content using path", "Create blob", "Delete blob") are Standard. We already have a real Azure Storage Account provisioned (Stage 4, BlobStorageService in TravelRequestWF.Infrastructure).

**Assumption on O365 Outlook:** "Send an email (V2)" via the Office 365 Outlook connector is treated as Standard on M365 Business and E-tier licenses. This assumption is noted but considered safe for Jorgito's environment; if it proves incorrect, the Send Email step is the only action requiring adjustment.

---

## Decision 1 (REVISED) — Trigger Mechanism: Blob Write Instead of HTTP POST

**Chosen:** Instead of `PowerAutomateNotificationService` POSTing JSON to a Power Automate HTTP trigger URL, it will **write a small JSON notification event blob** to a dedicated Azure Storage container. Power Automate triggers on blob creation using the Standard "When a blob is added or modified (properties only)" trigger.

**Transport flow:**
1. `.NET TravelRequestService` calls `INotificationService.NotifySubmittedAsync(...)` / `INotificationService.NotifyStatusChangedAsync(...)` as before.
2. `BlobNotificationService` (renamed from `PowerAutomateNotificationService`) serializes the canonical JSON payload and uploads it as a new blob to the appropriate container.
3. Power Automate's Azure Blob Storage trigger fires on blob creation.
4. Flow reads blob content, parses JSON, sends email via Outlook, deletes the blob.

**Rejected alternatives:** HTTP trigger (Premium — not available), SQL connector polling (Premium + polling latency + requires data gateway for Azure SQL), Logic Apps (separate subscription concern).

---

## Decision 2 (CARRIED FORWARD) — Two Flows

Same as before. Two flows, two separate audiences (Manager for submission/resubmit, Employee for approve/reject/return). No change.

---

## Decision 3 (CARRIED FORWARD) — Recipient Emails Resolved by .NET

Same as before. `EmployeeEmail` and `ManagerEmail` are pre-resolved and written into the blob payload. No lookups inside Power Automate.

---

## Decision 4 (REVISED) — Code: BlobNotificationService replaces PowerAutomateNotificationService

**Chosen:** Rename `PowerAutomateNotificationService` → `BlobNotificationService`. The `INotificationService` interface is **unchanged** (same method signatures). `TravelRequestService` requires no modification.

`BlobNotificationService` uses `Azure.Storage.Blobs` SDK (already a project dependency via `BlobStorageService`) to:
- Resolve the target container name from config.
- Call `CreateIfNotExistsAsync()` with `PublicAccessType.None` on startup/first use.
- Upload the serialized JSON payload as a new blob with a unique name.

**DI registration:** Replace `PowerAutomateNotificationService` registration with `BlobNotificationService` in `Program.cs` / `Startup.cs`.

---

## Decision 5 (CARRIED FORWARD) — Non-Blocking Notifications

Unchanged. Blob write failures are caught in `try/catch`, logged via `ILogger<BlobNotificationService>`, and never throw or roll back the database transaction. The workflow transition always commits regardless of notification outcome.

---

## Decision 6 (REVISED) — Configuration Keys

**Removed config keys (no longer needed):**
- `PowerAutomate:FlowASubmissionUrl`
- `PowerAutomate:FlowBStatusChangeUrl`

**New config keys:**
- `AzureStorage:ConnectionString` — ALREADY EXISTS from Stage 4 (BlobStorageService). Reused. No new secret needed.
- `PowerAutomate:SubmissionContainerName` — default value: `"notification-submitted"` (can be overridden per environment)
- `PowerAutomate:StatusChangeContainerName` — default value: `"notification-status-changed"`

Container names follow Azure Blob Storage naming rules (lowercase, hyphens allowed). The service will call `CreateIfNotExistsAsync()` so containers do not need to be manually pre-provisioned (though they will also be created by Power Automate's trigger configuration in the portal, whichever comes first).

**No placeholder URLs needed.** This eliminates the "paste the URL" manual step entirely.

---

## Decision 7 (NEW) — One Container per Flow vs. Shared Container

**Chosen: TWO SEPARATE CONTAINERS** — `notification-submitted` and `notification-status-changed`.

**Rejected: One shared container (`notification-events`).**

**Rationale:** With a single shared container, BOTH Power Automate flows would trigger on EVERY blob write (both submission and status-change events). Each flow would then need a "Condition" action at the top to check `EventType` and terminate early if the blob is not for that flow. This adds complexity inside each flow and means each flow processes blobs it doesn't own.

With two containers, each flow's trigger is scoped to exactly the events it handles — no conditional early-exit logic required, flows are simpler and independently testable. The operational overhead of two containers vs. one is negligible (both auto-created by the service; same Storage Account, same connection string).

**The two containers:**
| Container | Flow | EventType values |
|---|---|---|
| `notification-submitted` | Flow A — notifies Manager | `Submitted`, `Resubmitted` |
| `notification-status-changed` | Flow B — notifies Employee | `Approved`, `Rejected`, `Returned` |

---

## Decision 8 (NEW) — Blob Naming Convention

**Chosen:** `{RequestId}-{EventType}-{UtcTicks}.json`

Example: `3f8a1b2c-0000-0000-0000-d1e2f3a4b5c6-Submitted-638589123456789012.json`

**Rationale:** Guarantees uniqueness per event even if the same request fires the same event twice in rapid succession (unlikely but defensive). `RequestId` prefix makes the blob immediately identifiable in portal/logs. `UtcTicks` (`DateTime.UtcNow.Ticks`) provides ordering without a separate timestamp field. `.json` extension ensures correct MIME type handling.

---

## Decision 9 (NEW) — Blob Cleanup After Email

**Chosen:** Power Automate flows **delete the blob** after successfully sending the email (using the Standard "Delete blob" action).

**Rationale:** The blob is a transient message-passing mechanism, not a durable record. The `AuditLogEntry` table in Azure SQL is the authoritative, durable audit trail for all workflow events. Leaving blobs indefinitely would accumulate storage costs, risk re-triggering on blob modifications in edge cases, and complicate operations. Deleting after successful email processing keeps the containers clean and the purpose clear.

**If email send fails:** The flow should let the error propagate (not swallow it), leaving the blob in place for inspection/retry. Power Automate's built-in retry policy on failed flow runs handles this.

---

## JSON Payload Contract (UNCHANGED)

The canonical payload shape from the prior decision remains identical. The only change is the transport: blob write instead of HTTP POST.

```json
{
  "RequestId": "guid-string",
  "EventType": "Submitted | Resubmitted | Approved | Rejected | Returned",
  "EmployeeName": "string",
  "EmployeeEmail": "string",
  "ManagerName": "string",
  "ManagerEmail": "string",
  "Destination": "string",
  "StartDate": "yyyy-MM-dd",
  "EndDate": "yyyy-MM-dd",
  "Purpose": "string",
  "Status": "string",
  "Comments": "string or null"
}
```

---

## Summary of Changes vs. Prior Decision

| Aspect | Prior Decision (HTTP) | This Decision (Blob) |
|---|---|---|
| Trigger | HTTP POST to Power Automate URL | Blob write to Azure Storage container |
| PA trigger type | "When an HTTP request is received" (Premium ❌) | "When a blob is added or modified" (Standard ✅) |
| Service class | `PowerAutomateNotificationService` | `BlobNotificationService` |
| Interface | `INotificationService` (unchanged) | `INotificationService` (unchanged) |
| Transport dependency | `HttpClient` | `Azure.Storage.Blobs` SDK (already present) |
| Config keys | `FlowASubmissionUrl`, `FlowBStatusChangeUrl` | `SubmissionContainerName`, `StatusChangeContainerName` |
| Connection secret | New HTTP endpoint secret | Reuse existing `AzureStorage:ConnectionString` |
| Containers | N/A | Two: `notification-submitted`, `notification-status-changed` |
| Blob cleanup | N/A | Flow deletes blob after email send |
| Non-blocking | ✅ | ✅ (carried forward) |
| TravelRequestService changes | None needed | None needed |
