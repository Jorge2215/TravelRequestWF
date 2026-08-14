# Pippin → Coordinator: RequestId Format Mismatch in Sam's Setup Guide

**Date:** 2026-08-13  
**From:** Pippin (Tester)  
**To:** Coordinator → Sam (Power Automate Developer)  
**Severity:** Low — not a runtime bug, documentation only

---

## What Was Found

During Stage 5 payload capture testing, the actual JSON sent by `PowerAutomateNotificationService` was captured via a local HTTP listener. The `RequestId` field value is an **integer as a string** (e.g., `"1006"`), because `TravelRequest.Id` is an `int` primary key:

```csharp
// NotificationPayload construction in TravelRequestService.cs
RequestId = request.Id.ToString()  // produces "1006", "42", etc.
```

Sam's `stage5-power-automate-setup.md` setup guide shows the following example in the JSON payload section:

```json
{
  "RequestId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  ...
}
```

This example uses a UUID/GUID format, which does not match the actual runtime value.

---

## Impact

**Runtime impact: None.** Power Automate's generated schema defines `RequestId` as `"type": "string"` — it accepts any string value, whether UUID or integer. The flow will work correctly at runtime.

**Documentation impact: Minor.** If Jorgito or Sam copy-pastes the sample payload from the guide to test a Flow manually via curl/Postman, the RequestId format won't match real data. This is confusing but harmless.

---

## Recommended Fix

In `stage5-power-automate-setup.md`, update the sample payload's `RequestId` value from the UUID example to an integer string:

```json
{
  "RequestId": "42",
  ...
}
```

Or add a note alongside the sample:

> **Note:** `RequestId` is an integer string (e.g., `"42"`) — the UUID shown above is illustrative only. Power Automate accepts any string value here.

---

## Routing

Sam should update the setup guide. No code change required.
