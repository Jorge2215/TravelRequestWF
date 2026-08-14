# Stage 5 — Power Automate Setup Guide

**TravelRequestWF — Notification Flows**  
**Author:** Sam (Power Automate Developer)  
**Date:** 2026-08-13  
**Branch:** `dev`

---

## Overview

Two HTTP-triggered Power Automate flows handle all email notifications. The .NET app (Gandalf's `PowerAutomateNotificationService`) resolves email addresses and sends a full payload — the flows just fire the email. No business logic lives in Power Automate.

| Flow | Trigger Event | Email Recipient | When Fired |
|---|---|---|---|
| **Flow A** — `TravelRequest - New Request Submitted (Notify Manager)` | HTTP POST | Manager | On Submit or Resubmit |
| **Flow B** — `TravelRequest - Request Status Changed (Notify Employee)` | HTTP POST | Employee | On Approve, Reject, or Return |

---

## Canonical JSON Payload Contract

Both flows receive the **same JSON shape** via HTTP POST with `Content-Type: application/json`.

```json
{
  "RequestId": "1006",
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

**`EventType` values by flow:**
- Flow A receives: `"Submitted"` or `"Resubmitted"`
- Flow B receives: `"Approved"`, `"Rejected"`, or `"Returned"`

**Important:** All field names are **PascalCase**. Power Automate's schema is case-sensitive — this must match Gandalf's payload exactly.

`Comments` may be `null`. Flow B handles this with an expression (see below).

---

## Flow A — Step-by-Step Build Guide

### Name: `TravelRequest - New Request Submitted (Notify Manager)`

---

### Step 1 — Create the Flow

1. Go to [flow.microsoft.com](https://flow.microsoft.com) and sign in with your Microsoft 365 account.
2. Click **Create** in the left sidebar → **Instant cloud flow**.
3. Name the flow: `TravelRequest - New Request Submitted (Notify Manager)`
4. Under "Choose how to trigger this flow", search for and select **"When a HTTP request is received"** (this is a Premium connector — requires Power Automate per-user plan or Premium licensing, which the team has confirmed).
5. Click **Create**.

---

### Step 2 — Configure the HTTP Trigger

1. The trigger card appears with a **"When a HTTP request is received"** header.
2. Click **"Use sample payload to generate schema"** (link appears inside the trigger card).
3. Paste the following sample payload (use a string for `Comments` — not null — for schema generation; null works fine at runtime):

```json
{
  "RequestId": "1006",
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
  "Comments": "Sample comment"
}
```

4. Click **Done**. Power Automate auto-generates the JSON schema and displays it in the trigger card.
5. **Method** should be `POST` (default — leave as-is).
6. ⚠️ **Do NOT save yet** — the HTTP POST URL only appears after the first save. Build the rest of the flow first, then save once.

**What the generated schema looks like** (for reference — Power Automate generates this automatically):
```json
{
  "type": "object",
  "properties": {
    "RequestId": { "type": "string" },
    "EventType": { "type": "string" },
    "EmployeeName": { "type": "string" },
    "EmployeeEmail": { "type": "string" },
    "ManagerName": { "type": "string" },
    "ManagerEmail": { "type": "string" },
    "Destination": { "type": "string" },
    "StartDate": { "type": "string" },
    "EndDate": { "type": "string" },
    "Purpose": { "type": "string" },
    "Status": { "type": "string" },
    "Comments": { "type": "string" }
  }
}
```

---

### Step 3 — Add "Send an email (V2)" Action

1. Click **+ New step** below the trigger card.
2. Search for **"Send an email"** → select **Office 365 Outlook — Send an email (V2)**.
3. If prompted, click **Sign in** and authenticate with your Office 365 account. This creates a connector connection under your account — the flow will send emails *as you*.

**Fill in the action fields:**

**To:**
- Click inside the **To** field.
- In the dynamic content panel that appears on the right, find and click **ManagerEmail**.
- (If dynamic content doesn't appear, click the lightning bolt icon or type `triggerBody()?['ManagerEmail']` using the expression editor.)

**Subject:**
```
[Travel Request] New request from @{triggerBody()?['EmployeeName']} — Action Required
```
> Paste this directly into the Subject field. The `@{...}` syntax is Power Automate's expression interpolation.

**Body** (click the `</>` HTML toggle to switch to HTML mode, then paste):
```html
<p>A new travel request has been submitted and requires your approval.</p>

<table style="border-collapse: collapse; font-family: Arial, sans-serif;">
  <tr>
    <td style="padding: 6px 12px; font-weight: bold;">Employee:</td>
    <td style="padding: 6px 12px;">@{triggerBody()?['EmployeeName']}</td>
  </tr>
  <tr style="background-color: #f5f5f5;">
    <td style="padding: 6px 12px; font-weight: bold;">Destination:</td>
    <td style="padding: 6px 12px;">@{triggerBody()?['Destination']}</td>
  </tr>
  <tr>
    <td style="padding: 6px 12px; font-weight: bold;">Start Date:</td>
    <td style="padding: 6px 12px;">@{triggerBody()?['StartDate']}</td>
  </tr>
  <tr style="background-color: #f5f5f5;">
    <td style="padding: 6px 12px; font-weight: bold;">End Date:</td>
    <td style="padding: 6px 12px;">@{triggerBody()?['EndDate']}</td>
  </tr>
  <tr>
    <td style="padding: 6px 12px; font-weight: bold;">Purpose:</td>
    <td style="padding: 6px 12px;">@{triggerBody()?['Purpose']}</td>
  </tr>
  <tr style="background-color: #f5f5f5;">
    <td style="padding: 6px 12px; font-weight: bold;">Event:</td>
    <td style="padding: 6px 12px;">@{triggerBody()?['EventType']}</td>
  </tr>
  <tr>
    <td style="padding: 6px 12px; font-weight: bold;">Request ID:</td>
    <td style="padding: 6px 12px;">@{triggerBody()?['RequestId']}</td>
  </tr>
</table>

<p>Please log in to the Travel Request system to approve or reject this request.</p>
```

**Is HTML:** Toggle to **Yes** (if this toggle appears separately from the HTML editor).

---

### Step 4 — Save and Retrieve the HTTP POST URL

1. Click **Save** (top right or bottom of the canvas).
2. After saving successfully, go back to the **trigger card** ("When a HTTP request is received").
3. The **HTTP POST URL** field is now populated. It looks like:
   ```
   https://prod-XX.westus.logic.azure.com:443/workflows/xxxxxxxx.../triggers/manual/paths/invoke?api-version=...&sp=%2Ftriggers%2F...&sv=1.0&sig=...
   ```
4. Click the **copy icon** next to the URL to copy it to clipboard.
5. **This URL is `FlowASubmissionUrl`** — paste it into `TravelRequestWF.Web/appsettings.json` (see the Configuration section below).

---

## Flow B — Step-by-Step Build Guide

### Name: `TravelRequest - Request Status Changed (Notify Employee)`

---

### Step 1 — Create the Flow

Repeat exactly as Flow A Step 1. Name: `TravelRequest - Request Status Changed (Notify Employee)`.

---

### Step 2 — Configure the HTTP Trigger

Repeat Flow A Step 2 exactly — use the **same JSON sample payload**. Both flows share the same payload shape. The generated schema will be identical.

---

### Step 3 — Add "Send an email (V2)" Action

1. Click **+ New step** → **Office 365 Outlook — Send an email (V2)**.
2. Re-use the existing Office 365 connection (it should appear automatically — no need to sign in again).

**To:**
- Dynamic content → **EmployeeEmail**

**Subject:**
```
[Travel Request] Your request to @{triggerBody()?['Destination']} has been @{triggerBody()?['EventType']}
```

**Body** (HTML mode):
```html
<p>Your travel request status has been updated.</p>

<table style="border-collapse: collapse; font-family: Arial, sans-serif;">
  <tr>
    <td style="padding: 6px 12px; font-weight: bold;">Destination:</td>
    <td style="padding: 6px 12px;">@{triggerBody()?['Destination']}</td>
  </tr>
  <tr style="background-color: #f5f5f5;">
    <td style="padding: 6px 12px; font-weight: bold;">Start Date:</td>
    <td style="padding: 6px 12px;">@{triggerBody()?['StartDate']}</td>
  </tr>
  <tr>
    <td style="padding: 6px 12px; font-weight: bold;">End Date:</td>
    <td style="padding: 6px 12px;">@{triggerBody()?['EndDate']}</td>
  </tr>
  <tr style="background-color: #f5f5f5;">
    <td style="padding: 6px 12px; font-weight: bold;">Purpose:</td>
    <td style="padding: 6px 12px;">@{triggerBody()?['Purpose']}</td>
  </tr>
  <tr>
    <td style="padding: 6px 12px; font-weight: bold;">New Status:</td>
    <td style="padding: 6px 12px;"><strong>@{triggerBody()?['Status']}</strong></td>
  </tr>
  <tr style="background-color: #f5f5f5;">
    <td style="padding: 6px 12px; font-weight: bold;">Decision:</td>
    <td style="padding: 6px 12px;">@{triggerBody()?['EventType']}</td>
  </tr>
  <tr>
    <td style="padding: 6px 12px; font-weight: bold;">Manager Comments:</td>
    <td style="padding: 6px 12px;">@{if(empty(triggerBody()?['Comments']), '(No comments provided)', triggerBody()?['Comments'])}</td>
  </tr>
  <tr style="background-color: #f5f5f5;">
    <td style="padding: 6px 12px; font-weight: bold;">Request ID:</td>
    <td style="padding: 6px 12px;">@{triggerBody()?['RequestId']}</td>
  </tr>
</table>

<p>If your request was <strong>returned for revision</strong>, please log in, update the request with the manager's comments in mind, and resubmit.</p>
```

> **How empty Comments are handled:** The expression `@{if(empty(triggerBody()?['Comments']), '(No comments provided)', triggerBody()?['Comments'])}` returns `(No comments provided)` when Comments is `null`, an empty string, or missing. This is a Power Automate `if()` / `empty()` expression — paste it directly in the body; the Power Automate designer will render it as an expression token.

**Is HTML:** Toggle to **Yes**.

---

### Step 4 — Save and Retrieve the HTTP POST URL

Same as Flow A Step 4. Copy the HTTP POST URL from the trigger card. This URL is **`FlowBStatusChangeUrl`**.

---

## Configuring appsettings.json

After creating both flows, update `TravelRequestWF.Web/appsettings.json`:

```json
{
  "PowerAutomate": {
    "FlowASubmissionUrl": "https://prod-XX.region.logic.azure.com:443/workflows/...",
    "FlowBStatusChangeUrl": "https://prod-YY.region.logic.azure.com:443/workflows/..."
  }
}
```

**Where exactly in appsettings.json:** Add the `"PowerAutomate"` block at the top level (alongside `"ConnectionStrings"`, `"Logging"`, etc.).

> ⚠️ **Security note:** Flow URLs contain a SAS signature — they grant anyone who has the URL the ability to trigger the flow. Do **not** commit real URLs to source control if this repository is or becomes public.  
> For production: store the URLs in **Azure App Service Application Settings** (Environment Variables), which override `appsettings.json` at runtime.  
> For local development: use `appsettings.Development.json` (already in `.gitignore` by default for ASP.NET Core projects) or .NET User Secrets.

---

## Validation Steps

These steps map directly to Stage 5, Task 3 of the project prompt.

### Pre-requisite — Test Flows with curl/Postman First

Before running the full app, verify the flows work in isolation. Open a terminal (or Postman) and POST directly to each URL:

**Test Flow A:**
```bash
curl -X POST "<paste FlowASubmissionUrl here>" \
  -H "Content-Type: application/json" \
  -d "{\"RequestId\":\"test-001\",\"EventType\":\"Submitted\",\"EmployeeName\":\"Test Employee\",\"EmployeeEmail\":\"employee@yourdomain.com\",\"ManagerName\":\"Test Manager\",\"ManagerEmail\":\"manager@yourdomain.com\",\"Destination\":\"Madrid\",\"StartDate\":\"2026-09-01\",\"EndDate\":\"2026-09-05\",\"Purpose\":\"Test submission\",\"Status\":\"Pending\",\"Comments\":null}"
```

Expected response: **HTTP 202 Accepted** (Power Automate queues runs asynchronously — there is no response body).  
Then check the manager's inbox — the email should arrive within 1–2 minutes.

**Test Flow B (Approved, with Comments):**
```bash
curl -X POST "<paste FlowBStatusChangeUrl here>" \
  -H "Content-Type: application/json" \
  -d "{\"RequestId\":\"test-002\",\"EventType\":\"Approved\",\"EmployeeName\":\"Test Employee\",\"EmployeeEmail\":\"employee@yourdomain.com\",\"ManagerName\":\"Test Manager\",\"ManagerEmail\":\"manager@yourdomain.com\",\"Destination\":\"Paris\",\"StartDate\":\"2026-09-10\",\"EndDate\":\"2026-09-14\",\"Purpose\":\"Conference\",\"Status\":\"Approved\",\"Comments\":\"Great trip, approved!\"}"
```

**Test Flow B (Returned, null Comments):**
```bash
curl -X POST "<paste FlowBStatusChangeUrl here>" \
  -H "Content-Type: application/json" \
  -d "{\"RequestId\":\"test-003\",\"EventType\":\"Returned\",\"EmployeeName\":\"Test Employee\",\"EmployeeEmail\":\"employee@yourdomain.com\",\"ManagerName\":\"Test Manager\",\"ManagerEmail\":\"manager@yourdomain.com\",\"Destination\":\"Lima\",\"StartDate\":\"2026-10-01\",\"EndDate\":\"2026-10-03\",\"Purpose\":\"Audit\",\"Status\":\"Returned\",\"Comments\":null}"
```

For the null-Comments test: the employee email should show `(No comments provided)` in the Manager Comments row.

---

### Testing via Power Automate Designer (Alternative)

1. Open the flow in Power Automate designer.
2. Click **Test** (top right) → **Manually** → **Test**.
3. Power Automate prompts you to trigger the flow — at this point, run the curl command above (or use Postman) to POST the payload.
4. The designer shows each step executing in real time with green checkmarks (success) or red X (failure).
5. Click any step to see its inputs and outputs.

---

### End-to-End Validation via the App

#### Validate Flow A (Submit → Manager email)

1. Run the TravelRequestWF app (locally or deployed on Azure App Service).
2. Log in as an **Employee** account (from seed data).
3. Submit a new travel request — fill Destination, Start/End Date, Purpose. Optionally attach a document.
4. Check the **Manager's email inbox** — expect subject: `[Travel Request] New request from <EmployeeName> — Action Required`
5. Email should contain the request details in a table format.

#### Validate Flow A (Resubmit → Manager email)

1. As Manager: **Return** a submitted request with a comment.
2. As Employee: Log in, update the request, **Resubmit**.
3. Check Manager's inbox again — expect another email with `EventType: Resubmitted`.

#### Validate Flow B (Approve → Employee email)

1. As Manager: Approve the submitted request.
2. Check Employee's inbox — expect subject: `[Travel Request] Your request to <Destination> has been Approved`
3. Email body should show Status: **Approved** and Comments row showing `(No comments provided)` or the manager's comment.

#### Validate Flow B (Reject → Employee email)

1. Submit a new request as Employee.
2. As Manager: **Reject** with a rejection reason in Comments.
3. Employee inbox — expect email with `EventType: Rejected` and the manager's reason in Manager Comments.

#### Validate Flow B (Return → Employee email)

1. Submit a new request as Employee.
2. As Manager: **Return** with a comment.
3. Employee inbox — expect email with `EventType: Returned`.

---

### Checking Run History for Audit

1. Go to [flow.microsoft.com](https://flow.microsoft.com) → **My flows**.
2. Click the flow name (Flow A or Flow B).
3. Scroll to **28 day run history** on the detail page.
4. Each row shows: start time, duration, status (**Succeeded** in green / **Failed** in red).
5. Click any run row → expands to show each action's input/output.
6. This is how you confirm that every notification fired and diagnose failures.

Power Automate retains 28 days of run history for free/standard flows. This satisfies the audit logging requirement for Stage 5.

---

## Troubleshooting

| Symptom | Likely Cause | Fix |
|---|---|---|
| No run appears in history after submitting | URL is still placeholder in `appsettings.json` | Paste the real flow URL and restart/redeploy the app |
| curl returns HTTP 404 | Flow was deleted or URL was copied incorrectly | Verify URL in Power Automate → re-copy from trigger card |
| curl returns HTTP 400 | Request body malformed or field name mismatch | Check for typos in field names — must be exact PascalCase (`EmployeeName` not `employeeName`) |
| Flow run shows "Failed" — action error on Send email | Office 365 connector not authenticated | Power Automate → **Data** → **Connections** → find Office 365 Outlook → fix/re-authenticate |
| Flow run shows "Failed" — trigger parse error | JSON schema mismatch | Delete the schema from the trigger, re-paste the sample payload, and regenerate |
| Email arrives but dynamic content shows blank | Field name in expression doesn't match schema | Check that the trigger schema was generated from the exact payload above; re-generate if unsure |
| Email arrives but Comments shows blank (not fallback) | `empty()` expression typed incorrectly | Use exactly: `@{if(empty(triggerBody()?['Comments']), '(No comments provided)', triggerBody()?['Comments'])}` |
| "Send an email" action shows "Connection not authorized" | User's O365 session expired | Power Automate → Connections → click the Office 365 Outlook connection → **Fix connection** |
| HTTP 202 received by curl but no email and no run in history | Flow is disabled or in draft state | Open the flow → click **Turn on** button at the top |
| Dynamic content tokens don't appear in the field dropdown | Trigger schema not yet saved | Save the flow once, then re-open and edit the Send email action |

---

## Summary — Configuration Checklist

After completing both flows, verify:

- [ ] Flow A created with name `TravelRequest - New Request Submitted (Notify Manager)`
- [ ] Flow A trigger schema generated from sample payload
- [ ] Flow A "Send an email" action added, To = ManagerEmail, subject and body templated
- [ ] Flow A saved → HTTP POST URL copied → pasted into `appsettings.json` as `PowerAutomate:FlowASubmissionUrl`
- [ ] Flow B created with name `TravelRequest - Request Status Changed (Notify Employee)`
- [ ] Flow B trigger schema generated from same sample payload
- [ ] Flow B "Send an email" action added, To = EmployeeEmail, Comments handled with `if(empty(...))` expression
- [ ] Flow B saved → HTTP POST URL copied → pasted into `appsettings.json` as `PowerAutomate:FlowBStatusChangeUrl`
- [ ] Both URLs validated with curl (HTTP 202 received, emails arrive)
- [ ] Both flows show "Succeeded" in run history after end-to-end app test
