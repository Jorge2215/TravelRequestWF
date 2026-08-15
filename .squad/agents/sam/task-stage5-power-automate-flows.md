# Sam — Task Brief: Stage 5 Power Automate Flows

**Assigned to:** Sam (Power Automate Developer)  
**Stage:** 5 — Power Automate Notifications  
**Branch:** `dev`  
**Date issued:** 2026-08-13T21:27:04-03:00  
**Issued by:** Aragorn

---

## Context

Stage 1–4 of TravelRequestWF is complete (Razor Pages app with full Employee/Manager workflow, Azure SQL, Azure Blob Storage). Stage 5 adds Power Automate notification flows.

Aragorn has defined all architecture decisions in `.squad/decisions/inbox/aragorn-stage5-notification-scope.md`. Gandalf's task brief (`.squad/agents/gandalf/task-stage5-notification-integration.md`) defines the exact JSON payload contract. **You can start immediately** — Aragorn has locked the payload contract in this brief. You do not need to wait for Gandalf's code to be written.

**Your constraint:** We do not have live access to the Power Automate tenant via automation tools. You cannot provision flows programmatically. Your deliverable is a **thorough, step-by-step setup guide** at `.squad/files/stage5-power-automate-setup.md` that Jorgito can follow in the Power Automate designer at [flow.microsoft.com](https://flow.microsoft.com) to create both flows himself.

This mirrors the pattern from prior stages: Gandalf documented exact Azure CLI/EF commands for Jorgito to run manually — you do the equivalent for Power Automate.

---

## Your Deliverable

**File:** `.squad/files/stage5-power-automate-setup.md`

Write a complete, self-contained guide covering everything below.

---

## What to Document

### Overview

Brief intro: two flows, what each does, who receives email in each case.

| Flow | Trigger | Sender | Recipient | When |
|---|---|---|---|---|
| Flow A — New Request Submitted | HTTP POST | .NET app | Manager | On Submit or Resubmit |
| Flow B — Request Status Changed | HTTP POST | .NET app | Employee | On Approve, Reject, or Return |

---

### Canonical JSON Payload Contract

Aragorn has locked this. Both flows receive HTTP POST with `Content-Type: application/json`. The payload shape is **identical** for both flows (same fields, PascalCase). Include this schema verbatim in the guide so Jorgito can paste it into the Power Automate "Generate from sample" dialog:

```json
{
  "RequestId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "EventType": "Submitted",
  "EmployeeName": "Ana López",
  "EmployeeEmail": "ana.lopez@company.com",
  "ManagerName": "Carlos Ruiz",
  "ManagerEmail": "carlos.ruiz@company.com",
  "Destination": "Buenos Aires",
  "StartDate": "2026-08-20",
  "EndDate": "2026-08-25",
  "Purpose": "Client meeting",
  "Status": "Pending",
  "Comments": null
}
```

`EventType` values:
- Flow A receives: `"Submitted"` or `"Resubmitted"`
- Flow B receives: `"Approved"`, `"Rejected"`, or `"Returned"`

`Comments` may be `null` — handle with an `if(empty(...), '', triggerBody()?['Comments'])` expression or coalesce in email body.

---

### Flow A — Step-by-Step Build Guide

**Name the flow:** `TravelRequest - New Request Submitted (Notify Manager)`

#### Step 1: Create the flow

1. Go to [flow.microsoft.com](https://flow.microsoft.com) → **Create** → **Instant cloud flow**  
   *(Or: My flows → New flow → Instant cloud flow)*
2. Name the flow: `TravelRequest - New Request Submitted (Notify Manager)`
3. Trigger: search for and select **"When a HTTP request is received"** (HTTP trigger, requires Premium or Power Automate per-user plan)
4. Click **Create**

#### Step 2: Configure the HTTP trigger

1. In the trigger card, click **"Use sample payload to generate schema"**
2. Paste the JSON sample above (with all fields including `Comments: null` replaced with `"sample comment"` for schema generation — null is fine at runtime but use a string for schema inference)
3. Click **Done** — Power Automate generates the JSON Schema automatically
4. Set **Method** to `POST` (default)
5. After saving the flow, Power Automate will display the **HTTP POST URL** — copy this URL (it looks like `https://prod-XX.westus.logic.azure.com:443/workflows/...`). This is `FlowASubmissionUrl` — paste it into `appsettings.json` under `PowerAutomate:FlowASubmissionUrl`.

#### Step 3: Add "Parse JSON" action *(optional but recommended for reliability)*

1. Click **+ New step** → search **"Parse JSON"** → select the Data Operations **Parse JSON** action
2. **Content:** click in the field → select **Body** from dynamic content (the raw HTTP body)
3. **Schema:** paste the generated JSON schema from Step 2 (or re-generate from the same sample payload)

> If you skip Parse JSON, you can reference trigger body fields directly via `triggerBody()?['FieldName']` expressions in subsequent steps. The guide should explain both approaches.

#### Step 4: Add "Send an email (V2)" action

1. Click **+ New step** → search **"Send an email"** → select **Office 365 Outlook — Send an email (V2)**
2. Sign in with your Office 365 account when prompted

**To:** (dynamic content) `ManagerEmail`  
**Subject:**
```
[Travel Request] New request from @{triggerBody()?['EmployeeName']} — Action Required
```

**Body (HTML recommended):**
```html
<p>A new travel request has been submitted and requires your approval.</p>
<table>
  <tr><td><strong>Employee:</strong></td><td>@{triggerBody()?['EmployeeName']}</td></tr>
  <tr><td><strong>Destination:</strong></td><td>@{triggerBody()?['Destination']}</td></tr>
  <tr><td><strong>Start Date:</strong></td><td>@{triggerBody()?['StartDate']}</td></tr>
  <tr><td><strong>End Date:</strong></td><td>@{triggerBody()?['EndDate']}</td></tr>
  <tr><td><strong>Purpose:</strong></td><td>@{triggerBody()?['Purpose']}</td></tr>
  <tr><td><strong>Status:</strong></td><td>@{triggerBody()?['Status']}</td></tr>
  <tr><td><strong>Event:</strong></td><td>@{triggerBody()?['EventType']}</td></tr>
</table>
<p>Please log in to the Travel Request system to approve or reject this request.</p>
<p>Request ID: @{triggerBody()?['RequestId']}</p>
```

#### Step 5: Save and retrieve the URL

1. Click **Save**
2. Go back to the trigger card — the **HTTP POST URL** is now visible. Copy it.
3. Paste it into `TravelRequestWF.Web/appsettings.json`:
   ```json
   "PowerAutomate": {
     "FlowASubmissionUrl": "<paste URL here>",
     "FlowBStatusChangeUrl": "PLACEHOLDER_FLOW_B_URL"
   }
   ```

---

### Flow B — Step-by-Step Build Guide

**Name the flow:** `TravelRequest - Request Status Changed (Notify Employee)`

#### Step 1: Create the flow

Same process as Flow A Step 1. Name: `TravelRequest - Request Status Changed (Notify Employee)`.

#### Step 2: Configure the HTTP trigger

Same as Flow A Step 2 — use the **same JSON sample payload** (both flows share the same shape). Power Automate will generate the same schema.

#### Step 3: Add "Send an email (V2)" action

**To:** (dynamic content) `EmployeeEmail`  
**Subject:**
```
[Travel Request] Your request to @{triggerBody()?['Destination']} has been @{triggerBody()?['EventType']}
```

**Body (HTML):**
```html
<p>Your travel request status has been updated.</p>
<table>
  <tr><td><strong>Destination:</strong></td><td>@{triggerBody()?['Destination']}</td></tr>
  <tr><td><strong>Start Date:</strong></td><td>@{triggerBody()?['StartDate']}</td></tr>
  <tr><td><strong>End Date:</strong></td><td>@{triggerBody()?['EndDate']}</td></tr>
  <tr><td><strong>Purpose:</strong></td><td>@{triggerBody()?['Purpose']}</td></tr>
  <tr><td><strong>New Status:</strong></td><td><strong>@{triggerBody()?['Status']}</strong></td></tr>
  <tr><td><strong>Decision:</strong></td><td>@{triggerBody()?['EventType']}</td></tr>
  <tr><td><strong>Manager Comments:</strong></td><td>@{if(empty(triggerBody()?['Comments']), '(no comments)', triggerBody()?['Comments'])}</td></tr>
</table>
<p>If your request was returned, please log in, make the requested changes, and resubmit.</p>
<p>Request ID: @{triggerBody()?['RequestId']}</p>
```

#### Step 4: Save and retrieve the URL

Copy the HTTP POST URL from the trigger card. Paste into `appsettings.json`:
```json
"PowerAutomate": {
  "FlowASubmissionUrl": "<Flow A URL already pasted>",
  "FlowBStatusChangeUrl": "<paste Flow B URL here>"
}
```

---

### Configuring `appsettings.json` in the .NET app

After creating both flows, Jorgito must update `TravelRequestWF.Web/appsettings.json`:

```json
"PowerAutomate": {
  "FlowASubmissionUrl": "https://prod-XX.region.logic.azure.com:443/workflows/...",
  "FlowBStatusChangeUrl": "https://prod-YY.region.logic.azure.com:443/workflows/..."
}
```

Do NOT commit real flow URLs to source control if this is a public repo. Consider using `appsettings.Production.json` (excluded via `.gitignore`) or Azure App Service Application Settings for production values.

---

### Validation Steps (mirrors Stage 5 Task 3 from the prompt)

#### Test Flow A (Submit → Manager email)

1. Run the TravelRequestWF app locally (or deployed)
2. Log in as an **Employee** account
3. Submit a new travel request (fill in Destination, Dates, Purpose, optionally upload a document)
4. Check the **Manager's email inbox** — expect an email with subject `[Travel Request] New request from <EmployeeName> — Action Required` containing request details
5. In Power Automate portal → **My flows** → select `TravelRequest - New Request Submitted (Notify Manager)` → **28 day run history** — verify a successful run appears (green checkmark). Click the run to inspect inputs/outputs.

#### Test Flow B (Approve → Employee email)

1. Log in as the **Manager** account
2. Find the submitted request from Test Flow A → click **Approve**
3. Check the **Employee's email inbox** — expect an email with subject `[Travel Request] Your request to <Destination> has been Approved`
4. In Power Automate → `TravelRequest - Request Status Changed (Notify Employee)` → run history → verify successful run

#### Test Flow B (Reject)

1. Submit another request as Employee
2. Manager → **Reject** (enter a rejection reason in Comments)
3. Employee email should arrive with Status=Rejected and the manager's comment in "Manager Comments"
4. Verify run history in Power Automate

#### Test Flow B (Return)

1. Submit another request as Employee
2. Manager → **Return** (enter return reason in Comments)
3. Employee email should arrive with Status=Returned and the comment
4. Employee can resubmit → this triggers Flow A again (EventType="Resubmitted") → Manager receives another email

#### Verify Run History (audit)

- Power Automate portal → **My flows** → select each flow → **Run history** tab
- Each run shows: start time, duration, status (Succeeded/Failed), trigger inputs, action outputs
- This satisfies "Log flow runs for audit purposes" from the Stage 5 prompt

---

### Troubleshooting

| Symptom | Likely Cause | Fix |
|---|---|---|
| No flow triggered (no run in history) | URL still placeholder in appsettings | Paste the real flow URL and restart/redeploy the app |
| Flow triggered but email not received | Email in spam / wrong email address in seed data | Check spam folder; verify Employee.Email and Approver.Email in the database |
| Flow failed — "Invalid JSON" | Payload field mismatch | Compare the trigger schema in Power Automate against the canonical payload in this guide |
| Flow failed — "Connection not authorized" | Office 365 connector sign-in expired | Re-authenticate the Office 365 Outlook connection in Power Automate → Connections |
| HTTP 404 from .NET log | Flow URL wrong or flow deleted | Recreate the flow and update URL in appsettings |
| HTTP 400 from .NET log | JSON schema mismatch | Verify PascalCase field names match exactly |

---

### Notes for Jorgito

- Power Automate HTTP trigger URLs are **secret** — treat them like API keys. Anyone with the URL can POST to your flow.
- Flow URLs do not expire by default but are invalidated if you delete and recreate the flow.
- You can test the trigger manually using `curl` or Postman with the sample JSON payload before wiring up the .NET app, to verify the flow and email work end-to-end first.

Example curl test for Flow A:
```bash
curl -X POST "<FlowASubmissionUrl>" \
  -H "Content-Type: application/json" \
  -d '{
    "RequestId": "test-001",
    "EventType": "Submitted",
    "EmployeeName": "Test Employee",
    "EmployeeEmail": "employee@yourdomain.com",
    "ManagerName": "Test Manager",
    "ManagerEmail": "manager@yourdomain.com",
    "Destination": "Madrid",
    "StartDate": "2026-09-01",
    "EndDate": "2026-09-05",
    "Purpose": "Test submission",
    "Status": "Pending",
    "Comments": null
  }'
```

Expected response: HTTP 202 Accepted (Power Automate queues the run asynchronously).
