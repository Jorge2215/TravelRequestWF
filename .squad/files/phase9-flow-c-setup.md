# Phase 9 — Flow C: Daily Pending Requests Digest — Power Automate Setup Guide

**TravelRequestWF — Daily Manager Digest Flow**  
**Author:** Sam (Power Automate Developer)  
**Date:** 2026-08-15  
**Branch:** `dev`

---

## Overview

Flow C is an HTTP-triggered Power Automate flow that receives a JSON payload from the `DailyPendingReportFunction` Azure Function once per manager per day, then sends a single digest email listing all of that manager's pending travel requests.

The Azure Function (Merry's code) runs on a timer trigger at **08:00 UTC daily**, groups pending requests by manager, and POSTs one payload per manager to Flow C's HTTP trigger URL. Flow C's only responsibility is to build an HTML table from the array of requests and send the email.

| Flow | Trigger | Recipient | Fired By |
|---|---|---|---|
| **Flow C** — `Daily Pending Requests Digest` | HTTP POST (from Azure Function) | Manager | `DailyPendingReportFunction` — daily 08:00 UTC |

This is the same push-based HTTP trigger architecture as Phase 5 (Flow A / Flow B). If you built those flows successfully, this one follows the exact same pattern with one addition: an array loop to build an HTML table.

---

## Prerequisites

- Power Automate per-user plan or Microsoft 365 license that includes Power Automate (same license used for Flow A and Flow B in Phase 5 — no new license required).
- Access to the **Office 365 Outlook** connector (same connector and connection used for Flow A / Flow B — no new sign-in needed if the connection already exists in your tenant).
- **"When a HTTP request is received"** trigger (Premium connector) — already used and confirmed working in Phase 5.
- Flow C URL will need to be stored in:
  - **Local dev:** `src/TravelRequestWF.Functions/local.settings.json` → `Values` section (gitignored)
  - **Azure:** Function App Configuration → Application Settings in the Azure Portal

---

## Canonical JSON Payload Contract (Flow C)

The Azure Function POSTs this JSON body once per manager. The shape differs from Flow A/B — `PendingRequests` is an **array of objects**, one element per pending travel request.

```json
{
  "ManagerName": "Carol White",
  "ManagerEmail": "carol.white@company.com",
  "PendingRequests": [
    {
      "RequestId": 1,
      "EmployeeName": "Alice Johnson",
      "Destination": "Buenos Aires",
      "StartDate": "2026-09-01",
      "EndDate": "2026-09-05",
      "Status": "Pending"
    },
    {
      "RequestId": 3,
      "EmployeeName": "Bob Smith",
      "Destination": "Mendoza",
      "StartDate": "2026-09-10",
      "EndDate": "2026-09-12",
      "Status": "Pending"
    }
  ]
}
```

**Field names are case-sensitive** (PascalCase). These must match Merry's Azure Function payload exactly:

| Field | Type | Notes |
|---|---|---|
| `ManagerName` | string | Manager's display name |
| `ManagerEmail` | string | Manager's email address (To field) |
| `PendingRequests` | array | One element per pending request |
| `PendingRequests[].RequestId` | integer | Int PK from the database |
| `PendingRequests[].EmployeeName` | string | Employee's display name |
| `PendingRequests[].Destination` | string | Trip destination |
| `PendingRequests[].StartDate` | string | Format: `yyyy-MM-dd` |
| `PendingRequests[].EndDate` | string | Format: `yyyy-MM-dd` |
| `PendingRequests[].Status` | string | Always `"Pending"` in this digest |

---

## Step 1 — Create Flow C

1. Go to [flow.microsoft.com](https://flow.microsoft.com) and sign in with your Microsoft 365 account.
2. Click **Create** in the left sidebar → **Instant cloud flow**.
3. Name the flow: `Daily Pending Requests Digest`
4. Under "Choose how to trigger this flow", search for **"When a HTTP request is received"** and select it.
5. Click **Create**.

> ⚠️ **If "When a HTTP request is received" doesn't appear in the trigger gallery:** This is a known UI quirk — the same one encountered during Phase 5. Workarounds (try in order):
> - Skip the trigger selection dialog entirely by clicking **Skip** or **Create** without choosing, then on the canvas click **Add a trigger** and search the exact phrase: `When a HTTP request is received`
> - If it still doesn't appear inside a Solution: create the flow **outside any Solution** (go to **My flows** → **New flow** → **Instant cloud flow**)
> - The trigger is a Premium connector — confirm your Power Automate license is active

---

## Step 2 — Configure the HTTP Trigger

1. The trigger card appears: **"When a HTTP request is received"**.
2. Click **"Use sample payload to generate schema"** (link appears inside the trigger card, below the Request Body JSON Schema field).
3. Paste the following sample payload **exactly as shown** (the nested array is what generates the correct schema for `PendingRequests`):

```json
{
  "ManagerName": "Carol White",
  "ManagerEmail": "carol.white@company.com",
  "PendingRequests": [
    {
      "RequestId": 1,
      "EmployeeName": "Alice Johnson",
      "Destination": "Buenos Aires",
      "StartDate": "2026-09-01",
      "EndDate": "2026-09-05",
      "Status": "Pending"
    },
    {
      "RequestId": 3,
      "EmployeeName": "Bob Smith",
      "Destination": "Mendoza",
      "StartDate": "2026-09-10",
      "EndDate": "2026-09-12",
      "Status": "Pending"
    }
  ]
}
```

4. Click **Done**. Power Automate auto-generates the JSON schema. You will see the schema appear in the trigger card — it should include `ManagerName`, `ManagerEmail`, and `PendingRequests` as an array type.
5. **Method** stays `POST` (default — leave as-is).
6. ⚠️ **Do NOT save yet** — the HTTP POST URL only appears after the first save. Build all actions first, then save once at the end.

**What the generated schema should look like** (Power Automate generates this automatically from your sample — shown here only for verification):

```json
{
  "type": "object",
  "properties": {
    "ManagerName":  { "type": "string" },
    "ManagerEmail": { "type": "string" },
    "PendingRequests": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "RequestId":    { "type": "integer" },
          "EmployeeName": { "type": "string" },
          "Destination":  { "type": "string" },
          "StartDate":    { "type": "string" },
          "EndDate":      { "type": "string" },
          "Status":       { "type": "string" }
        }
      }
    }
  }
}
```

If the schema looks correct (array of objects with all six item-level fields), proceed.

---

## Step 3 — Initialize the HTML Table Variable

1. Click **+ New step** below the trigger card.
2. Search for **"Initialize variable"** → select **Variables — Initialize variable**.
3. Configure the action:
   - **Name:** `varEmailBody`
   - **Type:** `String`
   - **Value:** Paste the following HTML exactly (this is the table header that will be prepended to the email body):

```html
<table border="1" cellpadding="6" cellspacing="0" style="border-collapse:collapse;font-family:Arial,sans-serif;">
  <thead>
    <tr style="background:#0078d4;color:white;">
      <th>Request ID</th>
      <th>Employee</th>
      <th>Destination</th>
      <th>Start Date</th>
      <th>End Date</th>
      <th>Status</th>
    </tr>
  </thead>
  <tbody>
```

> **What you'll see:** After filling in the Name and Type fields, click inside the Value field. A multi-line text box appears. Paste the HTML directly into it. Power Automate may show it as plain text in this field — that is expected; it will render as HTML in the email.

---

## Step 4 — Add "Apply to each" Loop Over PendingRequests

1. Click **+ New step** → search for **"Apply to each"** → select **Control — Apply to each**.
2. In the **"Select an output from previous steps"** field, click inside it — the dynamic content panel appears on the right.
3. Under the trigger's dynamic content section, find and select **PendingRequests**. (If you don't see it, type `PendingRequests` — it should appear as an array token from the HTTP trigger.)

> **What you'll see:** The Apply to each card wraps itself around an empty inner area. The header shows `PendingRequests` as the loop source.

### Step 4a — Inside the loop: Append one table row per request

1. Click **Add an action** inside the Apply to each card.
2. Search for **"Append to string variable"** → select **Variables — Append to string variable**.
3. Configure:
   - **Name:** `varEmailBody` (select from dropdown)
   - **Value:** Paste the following HTML. Where you see `@{...}` tokens, you can either type them directly OR use the dynamic content picker to insert each field:

```html
<tr>
  <td style="padding:6px 12px;">@{items('Apply_to_each')?['RequestId']}</td>
  <td style="padding:6px 12px;">@{items('Apply_to_each')?['EmployeeName']}</td>
  <td style="padding:6px 12px;">@{items('Apply_to_each')?['Destination']}</td>
  <td style="padding:6px 12px;">@{items('Apply_to_each')?['StartDate']}</td>
  <td style="padding:6px 12px;">@{items('Apply_to_each')?['EndDate']}</td>
  <td style="padding:6px 12px;">@{items('Apply_to_each')?['Status']}</td>
</tr>
```

> **Tip — using dynamic content inside the Value field:** After pasting, Power Automate may auto-resolve the `@{items('Apply_to_each')?['FieldName']}` expressions to visual tokens (colored pills). This is correct — each pill represents that field's value from the current loop iteration. If a token shows "unknown" or red, check the field name spelling against the schema.

> **If the action is renamed:** Power Automate may rename "Apply to each" to "Apply_to_each_1" or similar. If the expressions show red after pasting, delete and re-insert each dynamic token from the dynamic content picker (under "Apply to each" in the panel) to auto-generate the correct `items(...)` expression for your specific loop name.

---

## Step 5 — Close the HTML Table (After the Loop)

1. Click **+ New step** (this step must be **outside** the Apply to each loop — click the `+` that appears below/after the loop card, not inside it).
2. Search for **"Append to string variable"** → select **Variables — Append to string variable**.
3. Configure:
   - **Name:** `varEmailBody`
   - **Value:**

```html

  </tbody>
</table>
```

> **How to confirm this step is outside the loop:** The Append action card should appear at the same indentation level as the Apply to each card, not nested inside it. If it ends up inside, drag it out or delete and re-add it using the `+` below the loop.

---

## Step 6 — Send the Digest Email

1. Click **+ New step** → search for **"Send an email"** → select **Office 365 Outlook — Send an email (V2)**.
2. The existing Office 365 connection from Phase 5 should appear automatically. If not, click **Sign in** and authenticate with your Microsoft 365 account.

**Fill in the fields:**

### To
- Click inside the **To** field.
- In the dynamic content panel, find **ManagerEmail** (under the HTTP trigger section) and click it.
- The field will show a `ManagerEmail` token (blue pill).

### Subject

The subject line should include today's date and the count of pending requests. Because the count requires an expression, the full subject must be built as a mix of plain text and expressions.

**Option A — Paste directly (recommended):**

Click inside the **Subject** field. Switch to the expression editor (click the `fx` button or select the "Expression" tab in the formula bar). Enter the full subject as an expression:

```
concat('Daily Travel Request Digest — ', utcNow('yyyy-MM-dd'), ' (', string(length(triggerBody()?['PendingRequests'])), ' pending)')
```

Click **OK**. The subject field will show this as a single expression token.

**Option B — Mixed text and tokens:**

Type `Daily Travel Request Digest — ` directly in the Subject field, then add each dynamic piece:
1. Type the leading text.
2. To insert the date: click the dynamic content panel → switch to **Expression** tab → type `utcNow('yyyy-MM-dd')` → click **OK**.
3. Type ` (`.
4. To insert the count: click **Expression** tab → type `string(length(triggerBody()?['PendingRequests']))` → click **OK**.
5. Type ` pending)`.

> **Why `string(length(...))` instead of just `length(...)`:** The Subject field expects a string. `length()` returns an integer; wrapping it with `string()` converts it. Without this, Power Automate may throw a type mismatch error at runtime.

**Resulting subject at runtime example:** `Daily Travel Request Digest — 2026-09-15 (2 pending)`

### Body

1. Click inside the **Body** field.
2. Look for an **`</>`** (code/HTML) toggle button at the top of the Body toolbar — click it to switch to HTML mode. (In some Power Automate UI versions this is labeled "Is HTML" or appears as a toggle in the action's advanced options.)
3. Paste the following as the body content:

```html
<p>Hi @{triggerBody()?['ManagerName']},</p>
<p>The following travel requests are pending your approval as of today (@{utcNow('yyyy-MM-dd')}):</p>
@{variables('varEmailBody')}
<p style="margin-top:16px;">Please log in to the Travel Request system to review and approve or reject each request.</p>
<p style="color:#888;font-size:12px;">This is an automated daily digest. You will receive one email per day while pending requests remain in the queue.</p>
```

> **What you'll see in the designer:** Power Automate resolves `@{triggerBody()?['ManagerName']}`, `@{utcNow('yyyy-MM-dd')}`, and `@{variables('varEmailBody')}` into visual tokens. If the tokens don't resolve, check that the trigger schema was generated correctly (Step 2) and the variable was initialized (Step 3).

### Is HTML

- If the **"Is HTML"** toggle appears as a separate field in "Send an email (V2)": set it to **Yes**.
- If it doesn't appear: click **Show advanced options** at the bottom of the Send an email card — the toggle is typically there.
- This toggle is required for the HTML table to render correctly in Outlook. Without it, the recipient sees raw HTML tags as plain text.

---

## Step 7 — Save and Retrieve the HTTP POST URL

1. Click **Save** (top right of the canvas, or the Save button at the bottom).
2. After saving successfully, go back to the **trigger card** ("When a HTTP request is received").
3. The **HTTP POST URL** field is now populated. It looks like:
   ```
   https://prod-XX.westus.logic.azure.com:443/workflows/xxxxxxxx.../triggers/manual/paths/invoke?api-version=...&sp=%2Ftriggers%2F...&sv=1.0&sig=...
   ```
4. Click the **copy icon** next to the URL to copy it to clipboard.

> ⚠️ **Security:** This URL contains a SAS signature — anyone with the URL can trigger the flow. Treat it as a secret. **Never commit it to source control.** Store it only in `local.settings.json` (gitignored) locally and in Azure Function App Configuration in the portal.

---

## Step 8 — Wire the URL into the Azure Function

### Local development

Open `src/TravelRequestWF.Functions/local.settings.json` (this file is gitignored — safe to store the real URL here). Add the key under `Values`:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "SqlConnectionString": "<your connection string>",
    "PowerAutomate:FlowASubmissionUrl": "<Flow A URL>",
    "PowerAutomate:FlowBStatusChangeUrl": "<Flow B URL>",
    "PowerAutomate:FlowCDailyDigestUrl": "<paste Flow C URL here>"
  }
}
```

The exact key name is: **`PowerAutomate:FlowCDailyDigestUrl`** — do not alter casing or the colon separator.

### Azure Portal (Production)

1. Go to the **Azure Portal** → navigate to your **Function App** resource.
2. In the left menu: **Settings** → **Environment variables** (or **Configuration** → **Application settings** depending on portal version).
3. Click **+ Add** → enter:
   - **Name:** `PowerAutomate:FlowCDailyDigestUrl`
   - **Value:** paste the Flow C HTTP POST URL
4. Click **Save** → **Continue** to confirm.

> This is the exact same process used for `PowerAutomate:FlowASubmissionUrl` and `PowerAutomate:FlowBStatusChangeUrl` in Phase 5.

### Share the URL with the Coordinator

Paste the Flow C HTTP POST URL directly into the team chat. **Aragorn (Coordinator)** will record it as team configuration. The URL goes into `local.settings.json` locally and Azure Function App Configuration in the portal — **never in a committed file**.

---

## Step 9 — Testing

### Option A — Manual "Test" button in Power Automate Designer

1. Open Flow C in the Power Automate designer.
2. Click **Test** (top right corner) → select **Manually** → click **Test**.
3. Power Automate waits for you to trigger the flow. Open a terminal and POST the sample payload with curl:

```bash
curl -X POST "<paste Flow C HTTP POST URL here>" \
  -H "Content-Type: application/json" \
  -d "{\"ManagerName\":\"Carol White\",\"ManagerEmail\":\"carol.white@company.com\",\"PendingRequests\":[{\"RequestId\":1,\"EmployeeName\":\"Alice Johnson\",\"Destination\":\"Buenos Aires\",\"StartDate\":\"2026-09-01\",\"EndDate\":\"2026-09-05\",\"Status\":\"Pending\"},{\"RequestId\":3,\"EmployeeName\":\"Bob Smith\",\"Destination\":\"Mendoza\",\"StartDate\":\"2026-09-10\",\"EndDate\":\"2026-09-12\",\"Status\":\"Pending\"}]}"
```

Expected response from the curl command: **HTTP 202 Accepted** (empty body — Power Automate runs asynchronously).

4. Back in the designer: each action step shows a green checkmark (success) or red X (failure). Click any step to inspect its inputs and outputs.
5. Check `carol.white@company.com` inbox (or your own email if you substituted your address in the test payload) — the digest email should arrive within 1–2 minutes.

### Option B — Wait for the Real Timer Trigger

Once Merry's Azure Function is deployed and `PowerAutomate:FlowCDailyDigestUrl` is set in Azure Function App Configuration:

- The function fires automatically at **08:00 UTC daily** and POSTs to Flow C for each manager with pending requests.
- Check **Power Automate run history** (see below) to confirm runs after 08:00 UTC.

### Option C — Local Function Test with Real Database

If you want to test locally before deploying:

1. Ensure `local.settings.json` has a valid `SqlConnectionString` pointing at your Azure SQL database.
2. Ensure `PowerAutomate:FlowCDailyDigestUrl` is set to the real Flow C URL in `local.settings.json`.
3. In a terminal, navigate to `src/TravelRequestWF.Functions/` and run:
   ```
   func start
   ```
4. The timer won't fire at 08:00 unless you wait for it. To trigger manually, use the Azure Functions Core Tools HTTP admin endpoint (or simply wait for the scheduled time if the DB has live pending requests).

### Checking Run History

1. Go to [flow.microsoft.com](https://flow.microsoft.com) → **My flows**.
2. Click **Daily Pending Requests Digest**.
3. Scroll to **28 day run history** on the flow detail page.
4. Each row shows start time, duration, and status (**Succeeded** in green / **Failed** in red).
5. Click any run to inspect each action's inputs and outputs — useful for confirming the HTML table was built correctly and verifying the email recipient and subject.

---

## Validation Checklist (for Pippin when validation phase begins)

These items map to the Phase 9 validation criteria from the architecture decisions:

- [ ] Flow C created with name `Daily Pending Requests Digest`
- [ ] HTTP trigger schema generated from the sample JSON payload above (includes `PendingRequests` as array)
- [ ] `Initialize variable` action creates `varEmailBody` (String) with HTML table header
- [ ] `Apply to each` loop iterates over `PendingRequests` from the trigger body
- [ ] Inside the loop: `Append to string variable` adds one `<tr>` row per request with all six fields
- [ ] After the loop: `Append to string variable` closes `</tbody></table>`
- [ ] `Send an email (V2)` action: **To** = `ManagerEmail`, **Is HTML** = Yes
- [ ] Subject includes `utcNow('yyyy-MM-dd')` and `length(triggerBody()?['PendingRequests'])` count
- [ ] Body includes greeting with `ManagerName` and the `varEmailBody` HTML table
- [ ] Flow saved → HTTP POST URL copied
- [ ] `PowerAutomate:FlowCDailyDigestUrl` set in `local.settings.json` (local) and Azure Function App Configuration (Azure)
- [ ] Manual test via curl returns HTTP 202 and email arrives at the To address
- [ ] Email HTML table renders correctly in Outlook (not raw HTML tags)
- [ ] Subject line shows correct date and correct pending count
- [ ] Each manager receives exactly one digest email listing only **their** pending requests (grouping is correct in the Azure Function — this is Merry's concern, but verify by seeding 2 managers with distinct pending requests and confirming separate emails)
- [ ] A manager with zero pending requests receives no email (the Azure Function skips empty groups — no Flow C call is made for them)
- [ ] Power Automate run history shows green/Succeeded for each manager's run
- [ ] No run appears for a manager who has no pending requests (Flow C was never called for them)

---

## Troubleshooting

| Symptom | Likely Cause | Fix |
|---|---|---|
| No run appears in history after Function fires | `FlowCDailyDigestUrl` is still placeholder | Set the real URL in `local.settings.json` / Azure App Settings and redeploy/restart |
| curl returns HTTP 404 | Flow URL copied incorrectly or flow was deleted | Verify in Power Automate → re-copy URL from trigger card |
| curl returns HTTP 400 | Request body malformed or field name mismatch | Check JSON is valid; field names must be exact PascalCase (`PendingRequests`, not `pendingRequests`) |
| Flow run shows "Failed" — trigger parse error | Schema mismatch (e.g. `PendingRequests` not typed as array) | Delete schema from trigger → re-paste sample payload → regenerate |
| Flow run shows "Failed" — Apply to each error | `PendingRequests` not resolved from trigger body | Check schema was generated correctly; re-run schema generation |
| Email arrives but table shows empty rows | `items('Apply_to_each')` expression references wrong loop name | Delete the Append action inside the loop and re-add using dynamic content picker to auto-generate the correct expression |
| Email shows raw HTML (tags visible) | "Is HTML" toggle is off | Find the toggle in Send an email (V2) advanced options → set to Yes |
| Subject shows type error at runtime | `length()` not wrapped in `string()` | Use `string(length(triggerBody()?['PendingRequests']))` in the subject expression |
| Dynamic content tokens don't appear | Trigger schema not saved | Save the flow once, then re-open and edit actions |
| "Send an email" shows "Connection not authorized" | Office 365 session expired | Power Automate → **Data** → **Connections** → fix Office 365 Outlook connection |
| HTTP 202 from curl but no email, no run in history | Flow is disabled or in draft | Open flow → click **Turn on** at the top |
| `varEmailBody` always empty | Initialize variable step is skipped or disabled | Check that Initialize variable step is enabled (not turned off) and comes before the loop |

---

## Summary — Flow C Configuration Checklist

After completing Flow C, verify and check off each item:

- [ ] Flow C created: `Daily Pending Requests Digest`
- [ ] HTTP trigger schema generated from sample payload (array of PendingRequests objects)
- [ ] Initialize variable: `varEmailBody`, String, HTML table header as initial value
- [ ] Apply to each: loops over `PendingRequests`; inside → Append to string variable adds `<tr>` per item
- [ ] After loop: Append to string variable closes `</tbody></table>`
- [ ] Send an email (V2): To = `ManagerEmail`, Subject with date + count, Body = HTML with `varEmailBody`, Is HTML = Yes
- [ ] Flow saved → HTTP POST URL copied
- [ ] `PowerAutomate:FlowCDailyDigestUrl` added to `local.settings.json` (local, gitignored)
- [ ] `PowerAutomate:FlowCDailyDigestUrl` added to Azure Function App → Application Settings
- [ ] Manual curl test → HTTP 202 → email arrives → HTML table renders correctly
- [ ] Flow run history shows Succeeded
