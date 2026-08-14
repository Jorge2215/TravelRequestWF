# Stage 5 — Notification Integration Test Results

**Tester:** Pippin  
**Date:** 2026-08-13  
**Branch:** `dev`  
**Scope:** PowerAutomateNotificationService — non-blocking validation, payload capture, contract cross-check

> ⚠️ **INTERRUPTED RUN (2026-08-13T23:22):** Jorgito requested stop before any further scenarios were started. All scenarios already executed are fully recorded below. The live E2E section (real PA URLs) is complete — no partial scenarios were in-flight at stop time. dotnet process was already stopped.

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

---

## Live End-to-End Test — Real Power Automate URLs (2026-08-13)

**Tester:** Pippin  
**Context:** Jorgito created two real Power Automate flows using HTTP-trigger connectors (Premium plan confirmed). Gandalf stored the real trigger URLs via .NET user-secrets (NOT committed to repo). Flows injected automatically in Development mode.

### Build

```
dotnet build TravelRequestWF.slnx
Build succeeded.  0 Warning(s)  0 Error(s)
```
✅ **PASS** — 0 errors, 0 warnings.

### Environment Confirmation

Console startup banner confirmed:
```
info: Microsoft.Hosting.Lifetime[0]
      Hosting environment: Development
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5199
```
✅ Development mode confirmed. User-secrets injected automatically.

---

### Flow A — Submission Notification

**Action:** Logged in as `employee1@test.com`, submitted new travel request (Buenos Aires, 2026-09-01→2026-09-05). RequestId assigned: **2006**.  
**Second submission** (Berlin, 2026-10-20→2026-10-25, ISO date format). RequestId assigned: **2007**.

**Console log (RequestId=2006):**
```
Sending HTTP request POST https://defaulteef413388caf401187bfff1c9c425f.9e.environment.api.powerplatform.com/powerautomate/automations/direct/cu/28/workflows/e3a4d82e554843a0a5a32a5c4f475676/triggers/manual/paths/invoke?*
Received HTTP response headers after 946.5072ms - 400
warn: Power Automate Flow A (Submission) returned non-success status 400 for RequestId=2006.
```

**Console log (RequestId=2007):**
```
Sending HTTP request POST https://defaulteef413388caf401187bfff1c9c425f.9e.environment.api.powerplatform.com/powerautomate/automations/direct/cu/28/workflows/e3a4d82e554843a0a5a32a5c4f475676/triggers/manual/paths/invoke?*
Received HTTP response headers after 872.5632ms - 400
warn: Power Automate Flow A (Submission) returned non-success status 400 for RequestId=2007.
```

**Result: ❌ FAIL — HTTP 400 from Power Automate on BOTH submission attempts.**

- ✅ Confirmed: HTTP call IS being made to the real Power Automate endpoint (URL reached, not skipped)  
- ✅ Confirmed: Request was saved to DB (RequestId=2006 and 2007 created, status=Pending)  
- ✅ Confirmed: App does NOT crash or raise a 500 — notification failure is non-blocking  
- ❌ Power Automate responded with 400 Bad Request — payload mismatch or flow schema mismatch (see bug report `pippin-flow-a-http-400.md`)

**Needs Jorgito to verify:** Check Power Automate run history for Flow A — are any run records present? If not, the 400 might indicate the trigger URL itself is correct but the request body doesn't satisfy the flow's schema definition.

---

### Flow B — Status Change Notification (Approve)

**Action:** Logged in as `manager1@test.com`, approved RequestId=2006 with comment "Approved - live E2E test".

**Console log:**
```
Sending HTTP request POST https://defaulteef413388caf401187bfff1c9c425f.9e.environment.api.powerplatform.com/powerautomate/automations/direct/cu/03/workflows/5621461b6d93422ba67490d6fad760e4/triggers/manual/paths/invoke?*
Received HTTP response headers after 1066.6414ms - 202
info: Power Automate Flow B (Status Change) notified successfully for RequestId=2006.
```

**Result: ✅ SUCCESS — HTTP 202 Accepted from Power Automate.**

- ✅ Request 2006 status updated to Approved in DB  
- ✅ Audit log entry created  
- ✅ Power Automate returned 202 — flow triggered  

**Needs Jorgito to verify:** Check Power Automate run history for Flow B (Approve) and confirm email received by employee1@test.com.

---

### Flow B — Status Change Notification (Return for More Info)

**Action:** Manager1 returned RequestId=6 with comment "Return for more info - live E2E test".

**Console log:**
```
Sending HTTP request POST https://defaulteef413388caf401187bfff1c9c425f.9e.environment.api.powerplatform.com/powerautomate/automations/direct/cu/03/workflows/5621461b6d93422ba67490d6fad760e4/triggers/manual/paths/invoke?*
Received HTTP response headers after 564.307ms - 202
info: Power Automate Flow B (Status Change) notified successfully for RequestId=6.
```

**Result: ✅ SUCCESS — HTTP 202 Accepted from Power Automate.**

- ✅ Request 6 status updated to Returned in DB  
- ✅ Power Automate returned 202 — flow triggered  

**Needs Jorgito to verify:** Power Automate run history for Flow B (Return) and email to employee.

---

### Flow B — Status Change Notification (Reject)

**Action:** Manager1 rejected RequestId=2007 with comment "REJECT - live Flow B test".

**Console log:**
```
Sending HTTP request POST https://defaulteef413388caf401187bfff1c9c425f.9e.environment.api.powerplatform.com/powerautomate/automations/direct/cu/03/workflows/5621461b6d93422ba67490d6fad760e4/triggers/manual/paths/invoke?*
Received HTTP response headers after 1139.2642ms - 202
info: Power Automate Flow B (Status Change) notified successfully for RequestId=2007.
```

**Result: ✅ SUCCESS — HTTP 202 Accepted from Power Automate.**

- ✅ Request 2007 status updated to Rejected in DB  
- ✅ Power Automate returned 202 — flow triggered  

**Needs Jorgito to verify:** Power Automate run history for Flow B (Reject) and email to employee.

---

### Core Workflow — Regression Check

All DB state transitions confirmed correct with live notifications active:

| Action | DB State Updated | Notification | Regression? |
|--------|-----------------|--------------|-------------|
| Submit | ✅ Pending | ❌ Flow A 400 | None — app non-blocking |
| Approve | ✅ Approved | ✅ Flow B 202 | None |
| Reject | ✅ Rejected | ✅ Flow B 202 | None |
| Return | ✅ Returned | ✅ Flow B 202 | None |

✅ **No regressions found.** Live notification calls do not affect core workflow correctness.

---

### Live Test Summary

| Item | Status | Evidence |
|------|--------|----------|
| Build (0 errors) | ✅ | Console output |
| Environment = Development | ✅ | Startup banner |
| Flow A called (real HTTP, not skipped) | ✅ | HttpClient logs |
| Flow A HTTP 202 | ❌ Got 400 | Bug report filed |
| Flow B — Approve HTTP 202 | ✅ | Log: `notified successfully for RequestId=2006` |
| Flow B — Reject HTTP 202 | ✅ | Log: `notified successfully for RequestId=2007` |
| Flow B — Return HTTP 202 | ✅ | Log: `notified successfully for RequestId=6` |
| App non-blocking on Flow A failure | ✅ | Workflow continued, no 500 |
| Core DB transitions correct | ✅ | Status verified via employee page |
| No regressions | ✅ | All actions complete normally |

### What Jorgito Must Verify Manually

1. **Power Automate portal → Flow A run history**: Were any runs recorded? If yes, the body was received but failed flow validation. If no runs at all, the 400 means PA rejected before running.  
2. **Power Automate portal → Flow B run history**: 3 runs should appear (Approve, Reject, Return).  
3. **Email inbox (employee1@test.com or the configured recipient)**: Did notification emails arrive for the 3 Flow B triggers?  
4. **Flow A fix**: Once the 400 root cause is identified (likely payload schema mismatch), Gandalf should update the payload or Jorgito adjusts the flow's expected schema.

### Process Cleanup

- App process (PID 3520): stopped via `Stop-Process` (PID already exited after `stop_powershell`).
- No temp files created in working directory.
