# Phase 9 — "Sync Triggers BadRequest" Troubleshooting Guide

**Date:** 2026-08-15  
**Symptom:** `func azure functionapp publish` reports "Deployment completed successfully" but then:
```
[2026-08-15T13:24:06.550Z] Syncing triggers...
... (retries) ...
Error calling sync triggers (BadRequest). Request ID = '8ce939c8-...'
```

---

## Most Likely Root Cause: Program.cs Throws at Host Startup

**ROOT CAUSE CONFIRMED (code-level):** `Program.cs` previously had:
```csharp
var connectionString = builder.Configuration["SqlConnectionString"]
    ?? throw new InvalidOperationException("SqlConnectionString is not configured...");
```

This `throw` executes **eagerly at host startup** — before any function is invoked. Azure's "Sync Triggers" step requires the Functions worker process to start successfully so Azure can introspect and register the function triggers. If the worker crashes during startup (because `SqlConnectionString` is not yet set in Azure Application Settings), Azure cannot enumerate the triggers and returns `BadRequest`.

Since you were told to set Application Settings *after* publishing, but "sync triggers" runs *as part of* publish, the worker crashed on first start → `BadRequest`.

---

## Code Fix Applied

`Program.cs` — changed from eager-throw to deferred/null-safe resolution:

```csharp
// BEFORE (crashes host startup if SqlConnectionString is missing):
var connectionString = builder.Configuration["SqlConnectionString"]
    ?? throw new InvalidOperationException("SqlConnectionString is not configured...");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// AFTER (defers config lookup to first DI resolution; host starts cleanly even if unset):
builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = configuration["SqlConnectionString"] ?? string.Empty;
    options.UseSqlServer(connectionString);
});
```

`DailyPendingReportFunction.cs` — added runtime guard at the top of `RunAsync`:
```csharp
if (string.IsNullOrWhiteSpace(_sqlConnectionString))
{
    _logger.LogWarning("[DailyReport] SqlConnectionString is not configured. Set it in Azure Portal → Application settings. Skipping run.");
    return;
}
```

This means:
- **Deploy-time:** Host starts cleanly, sync triggers succeeds. ✅
- **Run-time (timer fires but no connection string):** Function logs a clear warning and exits gracefully — no unhandled exception. ✅
- **Run-time (connection string set correctly):** Function operates normally. ✅

---

## Other Candidates to Verify in the Azure Portal

Even with the code fix applied, verify these in the Portal to rule out secondary causes:

### 1. Confirm Runtime Stack = .NET 8 Isolated
Portal → **Function App → Settings → Configuration → General settings** tab:
- **Runtime stack:** `.NET`
- **Version:** `8 (LTS) Isolated`  
  *(If it shows "8 In-Process" or "6", change to "8 Isolated" and Save.)*
- **FUNCTIONS_EXTENSION_VERSION** app setting should be `~4`

### 2. Confirm `AzureWebJobsStorage` is set and valid
Portal → **Function App → Settings → Configuration → Application settings**:
- `AzureWebJobsStorage` must exist and point to a valid Storage Account connection string.  
  If missing, the Functions host cannot start at all. If you created the Function App via the Portal wizard, this is usually auto-populated — but verify.

### 3. Set `SqlConnectionString` Application Setting  
Portal → **Function App → Settings → Configuration → Application settings → + New application setting**:
- **Name:** `SqlConnectionString`
- **Value:** Your Azure SQL connection string (same one used by the Web App)  
  Example: `Server=tcp:<server>.database.windows.net,1433;Initial Catalog=<db>;Authentication=Active Directory Default;`

> ⚠️ With the code fix, the function will now *deploy* even without this value set. But the timer trigger will log a warning and skip its work until you set a real connection string.

### 4. Set `PowerAutomate:FlowCDailyDigestUrl` Application Setting
Portal → **Function App → Settings → Configuration → Application settings → + New application setting**:
- **Name:** `PowerAutomate:FlowCDailyDigestUrl`
- **Value:** The HTTP trigger URL from Sam's Flow C (or `PLACEHOLDER_FLOW_C_URL` temporarily)

> The function already has a PLACEHOLDER guard — it will log "skipping digest" and continue without error if this is a placeholder value.

### 5. Verify OS matches (Windows)
Portal → **Function App → Overview** — confirm **Operating System: Windows**.  
The `func azure functionapp publish --dotnet-isolated` command publishes a Windows build by default. If the Function App was created as Linux, there's an OS mismatch. If Linux, add `--os-type Linux` to the publish command.

---

## Re-Publish Command

After applying the fixes and verifying Portal settings:

```powershell
# From the Functions project directory:
cd src\TravelRequestWF.Functions

func azure functionapp publish <YOUR-FUNCTION-APP-NAME> --dotnet-isolated
```

Replace `<YOUR-FUNCTION-APP-NAME>` with the actual name from `az functionapp list --output table`.

---

## Package Versions Note

Current `TravelRequestWF.Functions.csproj` package versions:
- `Microsoft.Azure.Functions.Worker` 2.52.0 — current/recent ✅
- `Microsoft.Azure.Functions.Worker.Extensions.Timer` 4.3.1 — current ✅
- `Microsoft.Azure.Functions.Worker.Sdk` 2.0.7 — current ✅

No package version issues detected.

---

## Summary

| Check | Expected | Action if Wrong |
|---|---|---|
| Code fix (eager throw) | Removed — applied in this PR | Already done |
| Runtime stack | .NET 8 Isolated | Change in General settings |
| `AzureWebJobsStorage` | Present, valid Storage Account | Add/fix in Application settings |
| `SqlConnectionString` | Set to Azure SQL connection string | Add in Application settings |
| `PowerAutomate:FlowCDailyDigestUrl` | Set (real URL or PLACEHOLDER) | Add in Application settings |
| OS | Windows | Recreate Function App or add `--os-type` flag |
