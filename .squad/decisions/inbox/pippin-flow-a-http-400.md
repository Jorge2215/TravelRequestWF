# Bug Report — Flow A HTTP 400 from Power Automate

**Filed by:** Pippin  
**Date:** 2026-08-13  
**Fixed by:** Gandalf  
**Fix date:** 2026-08-13  
**Status:** ✅ FIXED — pending live re-verification by Pippin  
**Severity:** High (submission notification not delivered)  
**Component:** `PowerAutomateNotificationService` — Flow A (Submission)

---

## Root Cause (Confirmed by Gandalf)

`NotificationPayload.Comments` was declared `string?` (nullable). Flow A call sites (Submit, Resubmit) assigned `Comments = null`. `System.Text.Json` serializes C# `null` as JSON `null`. Power Automate's HTTP trigger schema (generated from Jorgito's sample JSON which uses `"Comments": ""`) defines the field as `string` — not nullable — so PA rejects `null` with HTTP 400.

**Why Flow B worked (202):** Manager always provides a comment string, so `Comments = comments` was never null on Flow B paths.

---

## Fix Applied

- `NotificationPayload.cs`: `public string? Comments` → `public string Comments { get; set; } = string.Empty`
- `TravelRequestService.cs`:
  - Submit: `Comments = null` → `Comments = string.Empty`
  - Resubmit: `Comments = null` → `Comments = string.Empty`
  - Approve/Reject/Return: `Comments = comments` → `Comments = comments ?? string.Empty`
- Build: 0 errors, 0 warnings confirmed.

---



---

## What I Observed

During the live end-to-end test with real Power Automate HTTP trigger URLs injected via user-secrets, every call to **Flow A** (submission notification) returned **HTTP 400 Bad Request** from Power Automate.

**Log evidence:**
```
Sending HTTP request POST https://defaulteef413388caf401187bfff1c9c425f.9e.environment.api.powerplatform.com/powerautomate/automations/direct/cu/28/workflows/e3a4d82e554843a0a5a32a5c4f475676/triggers/manual/paths/invoke?*
Received HTTP response headers after 946.5072ms - 400
warn: Power Automate Flow A (Submission) returned non-success status 400 for RequestId=2006.
```

Reproduced twice: RequestId=2006 and RequestId=2007 — both 400.

**Flow B (status change) worked fine:** HTTP 202 for Approve, Reject, and Return. This means the general HTTP call infrastructure is correct — the issue is specific to Flow A's endpoint.

---

## What Is NOT Broken

- The app does NOT crash or surface a 500 on Flow A failure — the notification is non-blocking.
- The travel request is saved to DB correctly.
- The audit log is written correctly.
- Flow B works fully.

---

## Likely Root Cause

HTTP 400 from Power Automate's HTTP trigger typically means the **request body does not match the schema the flow expects**. Possible causes:

1. **Payload mismatch:** Flow A was configured in the Power Automate portal with a specific JSON schema (e.g., it defined required fields). Our app sends the full 12-field `NotificationPayload` — but if Flow A's trigger was configured to parse a different schema (or has required fields we're not sending), it will 400.

2. **API key / SAS token in URL:** The trigger URL includes a `sp`, `sv`, `sig` query parameter. If the signature was copied incorrectly or has expired, PA would 400. However, Flow B works fine with a different workflow URL — so the general URL injection mechanism is correct. The issue may be specific to how Flow A's URL was set up.

3. **Flow A trigger schema locked:** In Power Automate, when you define the HTTP trigger schema by parsing a sample JSON, the flow will reject incoming requests that don't match. If Jorgito defined Flow A's trigger schema with different field names or types than what our app sends, that causes 400.

---

## Our App's Payload (confirmed correct by payload capture test)

```json
{
  "RequestId": "2006",
  "EventType": "Submitted",
  "EmployeeName": "Alice Johnson",
  "EmployeeEmail": "employee1@test.com",
  "ManagerName": "Carol White",
  "ManagerEmail": "manager1@test.com",
  "Destination": "Buenos Aires",
  "StartDate": "2026-09-01",
  "EndDate": "2026-09-05",
  "Purpose": "Live E2E Test - Flow A Notification Check",
  "Status": "Pending",
  "Comments": null
}
```

---

## Recommended Actions

### For Jorgito:
1. Open Power Automate portal → check Flow A run history. If NO runs appear for the test, the 400 is PA rejecting before the flow executes (schema/URL issue). If runs appear with input errors, it's a schema mismatch.
2. In Flow A, open the HTTP trigger step → click "Use sample payload to generate schema" → paste the JSON above to ensure PA's expected schema matches our payload.
3. Alternatively, check if the `Comments: null` field is causing issues — some PA schema validators reject null values if the field was defined as required.

### For Gandalf (if schema needs adjustment on our side):
- If Jorgito confirms the flow expects different field names or types, update `NotificationPayload.cs` and the serialization in `PowerAutomateNotificationService.PostToFlowAsync`.
- Do NOT change the payload without Jorgito confirming what Flow A expects.

---

## Impact

- **Submission notifications to manager are NOT being delivered** while this 400 persists.
- **No user-facing impact** — workflow proceeds normally, only the email notification is missing.
- **Flow B (status change notifications to employee) is fully working.**
