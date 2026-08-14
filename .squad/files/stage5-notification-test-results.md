# Stage 5 — Notification Integration Test Results

**Tester:** Pippin  
**Date:** 2026-08-13  
**Branch:** `dev`  
**Scope:** PowerAutomateNotificationService — non-blocking validation, payload capture, contract cross-check

---

## 1. Build

```
dotnet build TravelRequestWF.slnx
Build succeeded.  0 Warning(s)  0 Error(s)
```
✅ **PASS** — 0 errors, 0 warnings.

---

## 2. Placeholder URL Handling (Code Review)

`PowerAutomateNotificationService.PostToFlowAsync()` contains:

```csharp
if (string.IsNullOrWhiteSpace(url) || url.StartsWith("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
{
    _logger.LogInformation("Power Automate {FlowName} URL not configured — skipping notification for RequestId={RequestId}.", ...);
    return;
}
```

Additionally, any non-placeholder URL that fails is wrapped in `try/catch(Exception)` which only logs and never re-throws — making every notification call non-blocking.

✅ **PASS** — Placeholder detection is explicit and correct. HTTP failures are also silently caught.

---

## 3. Submit Workflow — Non-Blocking Test (placeholder URLs)

Tested configuration:  
- `FlowASubmissionUrl`: temporarily set to `http://localhost:9999/` (mock listener)  
- `FlowBStatusChangeUrl`: `PLACEHOLDER_FLOW_B_URL`

**Action:** Logged in as `employee1@test.com`, submitted a new travel request (Destination=Madrid, 2026-09-01→2026-09-05).

**Result:**  
- Submission succeeded → redirected to `/Employee` dashboard  
- Status in DB: `Pending` ✅  
- App log entry for Flow A: `Power Automate Flow A (Submission) notified successfully for RequestId=1006.` ✅  
- No crash, no 500 ✅

---

## 4. Approve Workflow — Non-Blocking Test (placeholder Flow B URL)

**Action:** Logged in as `manager1@test.com`, approved request #1006 with comment "Approved via test".

**Result:**  
- Approval succeeded → redirected to `/Manager` dashboard  
- App log entry: `Power Automate Flow B (Status Change) URL not configured — skipping notification for RequestId=1006.` ✅  
- No crash, no 500, no transaction rollback ✅

✅ **CONFIRMED NON-BLOCKING** — core workflow (submit/approve) succeeds correctly even when notification URLs are missing or placeholder.

---

## 5. Payload Capture — Mock HTTP Listener

A `System.Net.HttpListener` was run on `http://localhost:9999/` and `FlowASubmissionUrl` temporarily pointed to it during the submit test. The listener captured the following JSON exactly as sent by the app:

```json
{
  "RequestId": "1006",
  "EventType": "Submitted",
  "EmployeeName": "Alice Johnson",
  "EmployeeEmail": "employee1@test.com",
  "ManagerName": "Carol White",
  "ManagerEmail": "manager1@test.com",
  "Destination": "Madrid",
  "StartDate": "2026-09-01",
  "EndDate": "2026-09-05",
  "Purpose": "Client Meeting - Payload Test",
  "Status": "Pending",
  "Comments": null
}
```

The mock listener returned HTTP 202. After the test, `FlowASubmissionUrl` was reverted to `PLACEHOLDER_FLOW_A_URL`. ✅

---

## 6. Contract Cross-Check: Sam's Guide vs Gandalf's Payload

### Fields — field-by-field comparison

| Field | Sam's Schema | Gandalf's NotificationPayload | Match? |
|---|---|---|---|
| RequestId | string | `request.Id.ToString()` | ✅ (see note) |
| EventType | string | `"Submitted"` / `"Resubmitted"` / `"Approved"` / `"Rejected"` / `"Returned"` | ✅ |
| EmployeeName | string | employee.Name | ✅ |
| EmployeeEmail | string | employee.Email | ✅ |
| ManagerName | string | request.Approver.Name | ✅ |
| ManagerEmail | string | request.Approver.Email | ✅ |
| Destination | string | request.Destination | ✅ |
| StartDate | string (ISO 8601) | `request.StartDate.ToString("yyyy-MM-dd")` | ✅ |
| EndDate | string (ISO 8601) | `request.EndDate.ToString("yyyy-MM-dd")` | ✅ |
| Purpose | string | request.Purpose | ✅ |
| Status | string | `request.Status.ToString()` | ✅ |
| Comments | string (nullable) | nullable string | ✅ |

All 12 fields present, all names PascalCase, all types string/nullable string. **Overall contract: MATCH.**

### ⚠️ Minor Documentation Discrepancy — RequestId Format

Sam's guide uses a UUID example for RequestId (`"3fa85f64-5717-4562-b3fc-2c963f66afa6"`).

The actual value is an **integer as string** (`"1006"`) because `TravelRequest.Id` is an `int` primary key.

Power Automate schema type is `"string"` — it will accept any string value, so this does **NOT break the integration**. But the sample payload in Sam's guide implies UUID format, which could mislead someone generating test payloads.

**Recommendation:** Update Sam's `stage5-power-automate-setup.md` sample payload to use an integer string for RequestId (e.g., `"42"`) or add a note clarifying the actual format is an integer string.

---

## 7. Process Cleanup

- Mock HTTP listener runspace: closed before test completed.
- App process (PID 6984): stopped via `Stop-Process -Id 6984`.
- `appsettings.Development.json`: reverted to `PLACEHOLDER_FLOW_A_URL`.
- Temp files (app_output.txt, app_error.txt, cookies.txt): removed.

---

## Overall Stage 5 Verdict

| Item | Status |
|---|---|
| Build (0 errors) | ✅ PASS |
| Placeholder detection and graceful skip | ✅ PASS |
| Submit workflow non-blocking | ✅ PASS |
| Approve workflow non-blocking | ✅ PASS |
| Log entries visible (not silently swallowed) | ✅ PASS |
| Payload capture — correct JSON shape | ✅ PASS |
| Contract cross-check (Sam vs Gandalf) | ✅ MATCH with minor doc note |
| Live email delivery | ⏸ DEFERRED — Jorgito must create real Power Automate flows and paste URLs into appsettings |

**Stage 5 integration is solid.** The .NET notification layer is correctly wired, non-blocking, and produces the correct payload. Live email delivery validation is blocked only on Jorgito creating the actual flows in the Power Automate portal.
