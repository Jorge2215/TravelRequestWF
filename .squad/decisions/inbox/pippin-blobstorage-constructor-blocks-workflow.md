# Pippin — Bug Report: BlobStorageService Constructor Blocks All Stage 4 Workflow Pages

**Date:** 2026-08-12T20:03:51-03:00  
**By:** Pippin  
**Stage:** 4  
**Severity:** Critical (complete blocker)

---

## Decision Needed

**Route to Gandalf for fix before Stage 4 testing can proceed.**

---

## Bug Description

`BlobStorageService` validates the Azure Storage connection string in its **constructor**. The service is registered as **Scoped**. This means the constructor runs on every incoming request that resolves `ITravelRequestService` (which depends on `IBlobStorageService`). With the placeholder connection string `"YOUR_AZURE_STORAGE_CONNECTION_STRING_HERE"` in `appsettings.Development.json`, every authenticated request to any workflow page returns **HTTP 500**.

### Affected Pages
- `GET /Employee/Index`
- `GET /Employee/Submit`
- `POST /Employee/Submit`
- `GET /Employee/Detail/{id}`
- `POST /Employee/Detail/{id}?handler=Resubmit`
- `GET /Manager/Index`
- `GET /Manager/Review/{id}`
- `POST /Manager/Review/{id}?handler=Approve`
- `POST /Manager/Review/{id}?handler=Reject`
- `POST /Manager/Review/{id}?handler=Return`

### Error (captured via HTTP response body in dev mode)
```
System.InvalidOperationException: Azure Storage connection string is not configured.
  Set AzureStorage:ConnectionString in appsettings.json.
  at TravelRequestWF.Infrastructure.Services.BlobStorageService..ctor(IOptions`1 options)
    in BlobStorageService.cs:line 17
  ...
```

---

## Intended Behavior (per `aragorn-stage4-workflow-scope.md` Decision 1)

> "The app will throw a clear `InvalidOperationException` at startup or on first upload attempt if the connection string is the placeholder value — NOT a silent stub."

The intent: the app should work for non-upload operations; only the actual file upload call should fail when Azure Storage is not configured.

---

## Root Cause

```csharp
// BlobStorageService.cs — constructor (CURRENT, BROKEN)
public BlobStorageService(IOptions<AzureStorageOptions> options)
{
    _options = options.Value;
    if (_options.ConnectionString == "YOUR_AZURE_STORAGE_CONNECTION_STRING_HERE" ||
        string.IsNullOrWhiteSpace(_options.ConnectionString))
    {
        throw new InvalidOperationException("Azure Storage connection string is not configured...");
    }
}
```

The validation fires at DI resolution time, not at upload time.

---

## Recommended Fix

Move the validation into `UploadDocumentAsync`:

```csharp
// BlobStorageService.cs — FIXED
public BlobStorageService(IOptions<AzureStorageOptions> options)
{
    _options = options.Value; // just store, no validation here
}

public async Task<string> UploadDocumentAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct = default)
{
    if (_options.ConnectionString == "YOUR_AZURE_STORAGE_CONNECTION_STRING_HERE" ||
        string.IsNullOrWhiteSpace(_options.ConnectionString))
    {
        throw new InvalidOperationException(
            "Azure Storage connection string is not configured. Set AzureStorage:ConnectionString in appsettings.json.");
    }
    // ... rest of upload logic unchanged
}
```

**File:** `src/TravelRequestWF.Infrastructure/Services/BlobStorageService.cs`  
**Effort:** ~5 minute fix (move 6 lines).

---

## Impact of Fix

Once fixed, all 13 blocked test cases (TC2–TC13) can be re-run. TC14 (file upload with real Azure Storage) remains DEFERRED until Jorgito provides the connection string — this is the ONLY expected deferral.
