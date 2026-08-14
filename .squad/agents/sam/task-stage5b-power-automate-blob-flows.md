# Sam — Task: Stage 5b Power Automate Blob-Triggered Flows

**Assigned to:** Sam
**Requested by:** Aragorn
**Date:** 2026-08-13T22:15:00-03:00
**Branch:** `dev`
**Supersedes:** `.squad/agents/sam/task-stage5-power-automate-flows.md`

---

## Context

Jorgito's Power Automate plan is **non-Premium**. The HTTP trigger ("When an HTTP request is received") is Premium-only and cannot be used. The entire flow design has been redesigned to use **Azure Blob Storage Standard connector triggers** instead.

Gandalf's task (`task-stage5b-blob-notification-redesign.md`) will update the .NET infrastructure to write JSON blobs to two Azure Storage containers:
- `notification-submitted` — written when a request is submitted or resubmitted (triggers email to Manager)
- `notification-status-changed` — written when a request is approved, rejected, or returned (triggers email to Employee)

Your job is to **rewrite** `.squad/files/stage5-power-automate-setup.md` with new step-by-step instructions for setting up both flows using Standard connectors only.

---

## Your Task

Rewrite `.squad/files/stage5-power-automate-setup.md` in its entirety. Add a note at the top stating it supersedes the previous version (HTTP trigger approach), explaining the licensing reason.

Do NOT write any .NET code. Your entire deliverable is the updated markdown guide.

---

## Guide Content Requirements

### Section 0 — Supersession Notice

Add a prominent notice at the top:

> **⚠️ This guide supersedes the original Stage 5 setup guide (HTTP trigger approach).**
> The "When an HTTP request is received" trigger is a Premium connector and is not available on the current Power Automate plan. This guide redesigns both flows to use the Azure Blob Storage Standard connector trigger instead.

---

### Section 1 — Prerequisites

List what must be in place before building the flows:
- Azure Storage Account (already provisioned in Stage 4 — same account used for document uploads).
- The Storage Account connection string is already in `appsettings.json` as `AzureStorage:ConnectionString`. Sam does NOT need this value — it's for the .NET app.
- For Power Automate: the user needs to connect to the **same** Azure Storage Account in Power Automate portal using the Azure Blob Storage connector. This requires either:
  - The Storage Account **connection string**, OR
  - The Storage Account **name + access key** (found in Azure Portal → Storage Account → Access Keys).
- Two containers will be auto-created by the .NET app (`BlobNotificationService`) on first run. They can also be verified or pre-created manually in Azure Portal → Storage Account → Containers.
- M365/O365 account for Outlook "Send an email (V2)" connector.

---

### Section 2 — Verify / Pre-Create the Two Containers

Step-by-step instructions:

1. Navigate to the Azure Portal (portal.azure.com).
2. Open the Storage Account used by TravelRequestWF (Stage 4).
3. Go to **Containers** in the left menu.
4. Check if `notification-submitted` and `notification-status-changed` exist. If not, create them:
   - Click **+ Container**.
   - Name: `notification-submitted` → Public access level: **Private (no anonymous access)** → Create.
   - Repeat for `notification-status-changed`.
5. Note: the .NET app will also auto-create these containers on first use — this step is optional but useful for testing before the app is deployed.

---

### Section 3 — Build Flow A: "Travel Request Submitted — Notify Manager"

Explain: this flow fires when a new blob appears in `notification-submitted`.

#### Step 3.1 — Create the Flow

1. Go to [make.powerautomate.com](https://make.powerautomate.com).
2. Click **+ Create** → **Automated cloud flow**.
3. Name: `TravelRequest - Notify Manager on Submission`.
4. Search for trigger: `When a blob is added or modified (properties only)`.
5. Select the **Azure Blob Storage** connector (verify it shows as **Standard**, not Premium).
6. Click **Create**.

#### Step 3.2 — Configure the Trigger

1. **Storage Account connection:** click **Sign in** or **New connection**.
   - Connection name: `TravelRequestWF-Storage`
   - Enter the Storage Account name and access key (from Azure Portal → Storage Account → Access Keys → Key 1).
2. **Container:** select or type `/notification-submitted`.
3. Leave all other trigger settings at defaults.

#### Step 3.3 — Add "Get blob content using path" Action

1. Click **+ New step**.
2. Search for `Get blob content using path` — select **Azure Blob Storage** connector.
3. Use the same connection created in Step 3.2.
4. **Blob path:** use dynamic content → select **Path** from the trigger output.
   - This resolves to the full blob path of the newly created blob.
5. **Infer content type:** Yes.

#### Step 3.4 — Add "Parse JSON" Action

1. Click **+ New step** → search `Parse JSON` (Data Operations connector — Standard).
2. **Content:** use dynamic content → select **File Content** from the "Get blob content" step.
3. **Schema:** click **Generate from sample** and paste the following sample payload:

```json
{
  "RequestId": "3f8a1b2c-0000-0000-0000-d1e2f3a4b5c6",
  "EventType": "Submitted",
  "EmployeeName": "Jane Doe",
  "EmployeeEmail": "jane.doe@example.com",
  "ManagerName": "John Manager",
  "ManagerEmail": "john.manager@example.com",
  "Destination": "Buenos Aires",
  "StartDate": "2026-08-20",
  "EndDate": "2026-08-25",
  "Purpose": "Client meeting",
  "Status": "Pending",
  "Comments": null
}
```

4. Click **Done** — Power Automate generates the schema automatically.

#### Step 3.5 — Add "Send an email (V2)" Action

1. Click **+ New step** → search `Send an email (V2)` → select **Office 365 Outlook** connector (Standard).
2. Sign in with the M365 account if prompted.
3. Configure:
   - **To:** dynamic content → `ManagerEmail` (from Parse JSON).
   - **Subject:** `New Travel Request from [EmployeeName] — Action Required`
     - Use dynamic content: `New Travel Request from ` + `EmployeeName` + ` — Action Required`
   - **Body (HTML):** compose the email body using dynamic content fields. Example:

```
Hello <strong>[ManagerName]</strong>,<br><br>
A new travel request has been submitted and requires your approval.<br><br>
<strong>Employee:</strong> [EmployeeName] ([EmployeeEmail])<br>
<strong>Destination:</strong> [Destination]<br>
<strong>Travel Dates:</strong> [StartDate] to [EndDate]<br>
<strong>Purpose:</strong> [Purpose]<br>
<strong>Status:</strong> [Status]<br>
<strong>Request ID:</strong> [RequestId]<br><br>
Please log in to the TravelRequestWF application to review and approve or reject this request.<br><br>
Thank you.
```

   Replace each `[Field]` with the corresponding dynamic content token from Parse JSON.
   - For `Comments`: wrap in a conditional check — if Comments is not null/empty, add a line `<strong>Comments:</strong> [Comments]<br>`.
     Use a **Condition** action before Send Email if desired, or simply include Comments unconditionally (empty string renders as blank).
   - **Importance:** Normal.

#### Step 3.6 — Add "Delete blob" Action

1. Click **+ New step** → search `Delete blob` → select **Azure Blob Storage** (Standard).
2. Use the same connection.
3. **Blob:** use dynamic content → select **Path** from the trigger output (same as Step 3.3).
4. This runs after email is sent successfully — if email fails, flow fails before reaching this step, leaving the blob for inspection/retry.

#### Step 3.7 — Save and Test Flow A

1. Click **Save**.
2. Manual test: see Section 6 (manual testing via Azure Portal blob upload).

---

### Section 4 — Build Flow B: "Travel Request Status Changed — Notify Employee"

Explain: this flow fires when a new blob appears in `notification-status-changed`. The structure is identical to Flow A except the container, the email recipient (Employee), and the subject/body copy.

#### Step 4.1 — Create the Flow

1. Click **+ Create** → **Automated cloud flow**.
2. Name: `TravelRequest - Notify Employee on Status Change`.
3. Trigger: `When a blob is added or modified (properties only)` → Azure Blob Storage (Standard).
4. Click **Create**.

#### Step 4.2 — Configure the Trigger

1. Use the same connection (`TravelRequestWF-Storage`) or create it again if not available.
2. **Container:** `/notification-status-changed`.

#### Step 4.3 — Get Blob Content, Parse JSON

Repeat Steps 3.3 and 3.4 exactly (same action types, same connection, same JSON schema).

#### Step 4.4 — Send Email to Employee

1. Add "Send an email (V2)" (Office 365 Outlook).
2. Configure:
   - **To:** dynamic content → `EmployeeEmail`.
   - **Subject:** dynamic based on EventType. Options:
     - Simplest approach: `Your travel request to [Destination] has been updated (Status: [Status])`.
     - More explicit: Use a **Switch** action on `EventType` before Send Email with three branches (Approved/Rejected/Returned), each with a tailored subject and body. This is optional — a single generic subject with Status in it is acceptable for PoC.
   - **Body (HTML)** example:

```
Hello <strong>[EmployeeName]</strong>,<br><br>
Your travel request has been updated by your manager.<br><br>
<strong>Destination:</strong> [Destination]<br>
<strong>Travel Dates:</strong> [StartDate] to [EndDate]<br>
<strong>Purpose:</strong> [Purpose]<br>
<strong>New Status:</strong> [Status]<br>
<strong>Decision by:</strong> [ManagerName]<br>
<strong>Request ID:</strong> [RequestId]<br>
[If Comments not null: <strong>Manager Comments:</strong> [Comments]<br>]<br>
Please log in to the TravelRequestWF application to view your request details.<br><br>
Thank you.
```

#### Step 4.5 — Delete Blob

Repeat Step 3.6 using the trigger's **Path** dynamic content.

#### Step 4.6 — Save Flow B.

---

### Section 5 — No URL Configuration Needed

Because the redesign uses blob-based triggers, **there are no Power Automate flow URLs to copy or configure**. The `.NET app` does not need to be given any URL. The connection between the app and Power Automate is purely through the shared Azure Storage Account.

The only configuration the .NET app needs (already set by Gandalf's task) is:
- `AzureStorage:ConnectionString` — existing key, no change.
- `PowerAutomate:SubmissionContainerName` = `notification-submitted`
- `PowerAutomate:StatusChangeContainerName` = `notification-status-changed`

---

### Section 6 — Manual Testing (Without the Full App Running)

Explain how to manually trigger each flow for validation, by uploading a sample blob directly:

#### Test Flow A (Submission):

1. In Azure Portal → Storage Account → Containers → `notification-submitted`.
2. Click **Upload**.
3. Create a local file named `test-submission.json` with content:

```json
{
  "RequestId": "00000000-0000-0000-0000-000000000001",
  "EventType": "Submitted",
  "EmployeeName": "Test Employee",
  "EmployeeEmail": "your-real-email@example.com",
  "ManagerName": "Test Manager",
  "ManagerEmail": "your-real-manager-email@example.com",
  "Destination": "Test City",
  "StartDate": "2026-09-01",
  "EndDate": "2026-09-05",
  "Purpose": "Testing Power Automate flow",
  "Status": "Pending",
  "Comments": null
}
```

   Use real email addresses you can access for `EmployeeEmail` and `ManagerEmail`.
4. Upload the file to the container.
5. Go to Power Automate → **My flows** → `TravelRequest - Notify Manager on Submission` → **Run history**.
6. Within ~1-2 minutes, a new run should appear. Verify it succeeded.
7. Check the Manager email inbox for the notification.
8. Verify the blob was deleted from the container after the flow completed.

#### Test Flow B (Status Change):

Repeat with a blob uploaded to `notification-status-changed`:

```json
{
  "RequestId": "00000000-0000-0000-0000-000000000001",
  "EventType": "Approved",
  "EmployeeName": "Test Employee",
  "EmployeeEmail": "your-real-email@example.com",
  "ManagerName": "Test Manager",
  "ManagerEmail": "your-real-manager-email@example.com",
  "Destination": "Test City",
  "StartDate": "2026-09-01",
  "EndDate": "2026-09-05",
  "Purpose": "Testing Power Automate flow",
  "Status": "Approved",
  "Comments": "Looks good, approved."
}
```

---

### Section 7 — End-to-End Validation (Full App)

Steps to validate the complete integration after both flows are built and the app is deployed (or running locally with an accessible Azure Storage Account):

1. Ensure `appsettings.json` (or environment variables in App Service) has:
   - `AzureStorage:ConnectionString` — the real connection string (already set from Stage 4).
   - `PowerAutomate:SubmissionContainerName` = `notification-submitted`.
   - `PowerAutomate:StatusChangeContainerName` = `notification-status-changed`.
2. Log in as an Employee and submit a travel request.
3. Verify: blob appears briefly in `notification-submitted` container (may disappear quickly after flow processes it).
4. Verify: Manager receives email notification within ~1-2 minutes.
5. Log in as Manager and approve/reject/return the request.
6. Verify: blob appears briefly in `notification-status-changed` container.
7. Verify: Employee receives email notification within ~1-2 minutes.
8. Verify: both containers are empty after successful flow runs (blobs deleted).
9. Check Power Automate run history for both flows — all runs should show as Succeeded.

---

### Section 8 — Troubleshooting

Common issues and resolutions:

| Issue | Likely Cause | Resolution |
|---|---|---|
| Flow doesn't trigger | Container name mismatch | Verify the container name in the trigger exactly matches what the app writes to (case-sensitive) |
| Flow triggers but "Get blob content" fails | Blob deleted before flow reads it | Unlikely with unique names; check blob path dynamic content is from trigger, not hardcoded |
| "Parse JSON" fails | JSON malformed or schema mismatch | Check blob content in Azure Portal; verify schema matches actual payload |
| "Send email" fails | Outlook connection expired or wrong account | Re-authenticate the O365 Outlook connection in Power Automate → Connections |
| Blobs accumulating and not deleted | "Delete blob" step not reached (email failed) | Check flow run history for error; fix email step first |
| App writes blob but no email received | Flow trigger delay | Azure Blob Storage trigger can take up to 5 minutes on first connection; test again after a few minutes |

---

### Section 9 — Connector Types Reference (Confirmation)

| Connector | Action/Trigger | Type | Notes |
|---|---|---|---|
| Azure Blob Storage | When a blob is added or modified (properties only) | **Standard** ✅ | Safe on non-Premium plans |
| Azure Blob Storage | Get blob content using path | **Standard** ✅ | |
| Azure Blob Storage | Delete blob | **Standard** ✅ | |
| Data Operations | Parse JSON | **Standard** ✅ | Built-in, no connector license needed |
| Office 365 Outlook | Send an email (V2) | **Standard** ✅ | Requires M365 Business/E-tier license (assumption noted) |
| Request (HTTP) | When an HTTP request is received | **Premium** ❌ | NOT used — requires Premium license |

---

## Commit Instructions

After completing the guide, commit and push to `dev`:

```
docs: rewrite stage5-power-automate-setup.md for blob-trigger approach (Stage 5b)

- Supersedes HTTP trigger version (Premium connector, unavailable on current plan)
- Both flows now use Azure Blob Storage Standard connector trigger
- Flow A: notification-submitted container → Manager email
- Flow B: notification-status-changed container → Employee email
- Includes manual testing steps (blob upload via Azure Portal)
- Includes end-to-end validation and troubleshooting guide

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
```

Push to `dev`. Do not touch `main`.

---

## Out of Scope

- Do NOT write any .NET code.
- Do NOT modify any `.cs` files.
- Do NOT create or modify `appsettings.json`.
- The containers will be auto-created by `BlobNotificationService` (Gandalf's task) — you only need to document the optional manual pre-creation step.
- Do NOT build a Logic Apps solution — Power Automate only.
