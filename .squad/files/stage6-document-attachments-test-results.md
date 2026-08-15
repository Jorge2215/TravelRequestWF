# Stage 6 — Document Attachments: Test Results

**Tested by:** Pippin  
**Date:** 2026-08-15  
**Branch:** dev  
**Commit:** 26c02e5 (Phase 6: file upload validation + per-environment blob container)  
**Environment:** Development (localhost:5199, LocalDB `TravelRequestWFDb_Dev`, Azure Storage `travelrequeststorage`, container `travel-documents-dev`)

---

## Pre-flight Checks

| Check | Result | Evidence |
|---|---|---|
| `dotnet build` | ✅ PASS | 0 errors, 0 warnings |
| `dotnet ef migrations list` | ✅ PASS | 5 migrations applied, **0 pending** — no schema change for Phase 6 as expected |
| Container `travel-documents-dev` exists | ✅ PASS | `az storage container list` confirms all 3 containers: `travel-documents`, `travel-documents-dev`, `travel-documents-prod` |
| `appsettings.Development.json` ContainerName | ✅ PASS | `"travel-documents-dev"` |
| `appsettings.json` ContainerName | ✅ PASS | `"travel-documents-prod"` |

---

## Test Cases

| TC | Scenario | Expected | Actual | Result |
|---|---|---|---|---|
| TC-1 | Build: `dotnet build TravelRequestWF.slnx` | 0 errors | 0 errors, 0 warnings | ✅ PASS |
| TC-2 | Migrations: `dotnet ef migrations list` | No pending | 5 applied, 0 pending | ✅ PASS |
| TC-A | Upload valid PDF (small, ~49 bytes, `.pdf` extension + `application/pdf` content-type) | HTTP 302 → `/Employee`, request created with Status=Pending | HTTP 302 → `/Employee`, RequestId=3006, Status=0 (Pending), 1 RequestDocument row created | ✅ PASS |
| TC-B | Upload rejected file type (`.exe`, `application/octet-stream`) | HTTP 200 (stays on page), error message shown, no request created | HTTP 200, error: `"File 'bad_file.exe' has an unsupported type. Allowed: PDF, DOCX, JPG, JPEG, PNG, GIF."` No request created in DB. | ✅ PASS |
| TC-C | Upload oversized file (~11 MB `.pdf`) | HTTP 200 (stays on page), error message referencing size, no request created | HTTP 200, error: `"File 'big_file.pdf' exceeds the 10 MB size limit."` No request created. | ✅ PASS |
| TC-D | Upload 2 valid files (`.pdf` + `.docx`) simultaneously | HTTP 302, both files in RequestDocuments linked to same TravelRequestId | HTTP 302, RequestId=3007, 2 RequestDocument rows (DocId 3 + 4) both with TravelRequestId=3007 | ✅ PASS |
| TC-5 | Blob storage verification: uploaded blobs land in `travel-documents-dev` (NOT `travel-documents`) | Blobs in `travel-documents-dev` with `BlobUrl` pointing to dev container | `az storage blob list` on `travel-documents-dev` returned 3 blobs: `..._valid_doc.pdf` (TC-A), `..._valid_doc.pdf` + `..._valid_doc2.docx` (TC-D). All BlobUrls: `https://travelrequeststorage.blob.core.windows.net/travel-documents-dev/...` | ✅ PASS |
| TC-6 | DB record integrity: `RequestDocument` rows have correct `FileName`, `BlobUrl`, `TravelRequestId` FK | All columns correctly populated | sqlcmd confirms: `FileName=valid_doc.pdf`, `BlobUrl=https://travelrequeststorage.blob.core.windows.net/travel-documents-dev/…`, `TravelRequestId` matching TravelRequest row | ✅ PASS |
| TC-7 | Ownership check regression: employee2 cannot access employee1's request | Redirect to `/Account/AccessDenied` | HTTP 200 on `/Account/AccessDenied?ReturnUrl=%2FEmployee%2FDetail%2F3006` — `<title>Access Denied</title>` | ✅ PASS |
| TC-8 | Frontend hint: `accept` attribute + help text on file input | `accept=".pdf,.docx,.jpg,.jpeg,.png,.gif"` and help text visible | `accept=".pdf,.docx,.jpg,.jpeg,.png,.gif"` confirmed; help text: `"Accepted file types: PDF, DOCX, JPG, PNG, GIF. Max 10 MB per file."` | ✅ PASS |
| TC-9 | Flow A triggered on valid submission (expected side effect) | Flow A HTTP call made; non-blocking | TC-A: Flow A → HTTP 202 ✅; TC-D: Flow A → HTTP 202 ✅ (Flow A HTTP 400 bug from Stage 5 now fixed) |  ✅ PASS (bonus) |

---

## TC-B Behavior Deep-Dive: Does rejection block the entire submission?

**Code behavior:** `ValidateDocuments()` is called **before** any DB writes (`SaveChangesAsync`). It loops over ALL documents first. If **any** file fails validation, an `InvalidOperationException` is thrown immediately. The `SubmitModel.OnPostAsync()` catches this and returns the page with `ErrorMessage` set.

**Result:** Rejection **blocks the entire submission**. No `TravelRequest` is created, no documents are uploaded to blob storage, and no audit entries are written. This is correct and intentional behavior — atomic all-or-nothing validation.

---

## Observations / Notes

1. **Per-environment container switch is working correctly.** All blobs from this test session landed in `travel-documents-dev`, confirming the `appsettings.Development.json` → `ContainerName: "travel-documents-dev"` config is being picked up at runtime.

2. **Validation is purely extension-based.** The `ValidateDocuments()` method checks `Path.GetExtension(fileName)` against a `HashSet`. It does NOT inspect actual file content (magic bytes). A malicious user could rename a `.exe` to `.pdf` and bypass this check. This is acceptable for a PoC but worth noting for future hardening.

3. **Max file size check uses `stream.Length`.** For `IFormFile` streams, `Length` reflects the actual uploaded size. The 11 MB test file was correctly rejected (stream.Length = 11,534,336 > 10,485,760).

4. **Flow A HTTP 400 bug is now fixed** (Stage 5 regression was that `Comments` was null; fix in commit `1d78451` sends empty string). Both TC-A and TC-D Flow A calls returned HTTP 202.

5. **AzureStorage:ConnectionString is still a placeholder in `appsettings.Development.json`** (committed as `YOUR_AZURE_STORAGE_CONNECTION_STRING_HERE`). Pippin set the real value via `dotnet user-secrets` for this test run. This is correct security practice — real credentials should never be committed. Jorgito/developers must run `dotnet user-secrets set "AzureStorage:ConnectionString" "<real-connection-string>"` on each dev machine.

---

## Cleanup

- Test blobs deleted from `travel-documents-dev` via `az storage blob delete`.
- Test `TravelRequest` rows (3006, 3007) and their `RequestDocument` rows (2, 3, 4) deleted from LocalDB via sqlcmd.
- Test files directory `test-files-phase6/` removed.

---

## Summary

**Phase 6 fully validated. All 10 test cases PASS. No bugs found.**

The three Phase 6 gaps identified by Aragorn are all correctly implemented:
1. ✅ File type/size validation — blocks invalid types and oversized files with friendly error messages; entire submission is atomic (rejected if any file fails).
2. ✅ Container-per-environment — `travel-documents-dev` in Development, `travel-documents-prod` in production.
3. ✅ Frontend hint — `accept` attribute and help text on file input.
