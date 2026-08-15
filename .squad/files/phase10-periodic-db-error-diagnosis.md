# Phase 10 — Periodic DB Error Diagnosis

**Date:** 2026-08-15  
**Author:** Gandalf (Backend Dev)  
**Symptom:** Log Stream repeatedly shows EF Core connection errors with placeholder DB name `TravelRequestWFDb` and server `<your-azure-sql-server>.database.windows.net`, even when no users are browsing the app.

---

## Full Code Audit Results

### TravelRequestWF.Web — Search Results

| Pattern | Found? | Location |
|---------|--------|----------|
| `IHostedService` implementation | ❌ NO | — |
| `BackgroundService` implementation | ❌ NO | — |
| `AddHealthChecks` / `AddDbContextCheck` | ❌ NO | — |
| `PeriodicTimer` / `while(true)` loop | ❌ NO | — |
| Second `AddDbContext<AppDbContext>` | ❌ NO | — |
| `new SqlConnection(...)` with hardcoded string | ❌ NO | — |
| `EnableRetryOnFailure` | ❌ NO (was missing — now FIXED) | — |
| `OnConfiguring` override in AppDbContext | ❌ NO | — |

**There is exactly ONE `AddDbContext` registration** in the Web project (in `Program.cs`) and it uses `builder.Configuration.GetConnectionString("DefaultConnection")` — the standard IConfiguration-based path that reads from App Service config overrides at runtime.

### TravelRequestWF.Functions — Audit

- Has its own `AddDbContext<AppDbContext>` using key `configuration["SqlConnectionString"]` — a DIFFERENT config key from the Web app
- Has a `TimerTrigger("0 0 8 * * *")` — fires ONCE daily at 08:00 UTC, NOT "frequently periodic"
- The Function App's connection string (from `local.settings.json`) references `TravelRequestDB` on `azure-sql-pampa.database.windows.net` — DIFFERENT names than what appears in the error message
- Uses `UseAzureMonitorExporter()` (OpenTelemetry) — telemetry goes to Azure Monitor/Application Insights, NOT to the App Service Log Stream
- **Therefore: this error is NOT from the Function App** — the Function App's error messages would show `TravelRequestDB`, not `TravelRequestWFDb`

---

## Root Cause Diagnosis

### Why `TravelRequestWFDb` / `<your-azure-sql-server>` appear (placeholder proof)

These values are verbatim from `appsettings.json` → `ConnectionStrings:DefaultConnection`. EF Core embeds the actual `SqlConnection.DataSource` and `.Database` values in its error log messages — not a template. Seeing those placeholder strings **proves** the running process used the `appsettings.json` fallback connection string at the moment of each error.

### Why it appears "periodically" with no users

The web app has **no background services or timers**. The only code that touches the DB at startup (outside of HTTP requests) is `IdentitySeeder.SeedAsync`, called unconditionally in `Program.cs` right after `app = builder.Build()`.

**The periodic pattern is caused by a crash-restart loop:**

1. App cold-starts (Azure App Service recycled, or scaled, or restarted)
2. `IdentitySeeder.SeedAsync` executes — makes DB queries (`db.Employees.FirstOrDefault`, `db.SaveChangesAsync`, Role checks)
3. If the DB connection fails (transient Azure SQL blip, or placeholder connection string still in effect for this process instance), the seeder throws an unhandled exception
4. The exception propagates out of `Program.cs` — the ASP.NET Core host terminates
5. **Azure App Service automatically restarts the app** (this is standard Azure App Service behavior)
6. Go to step 1 — repeat → **periodic errors in Log Stream, no user involved**

### Why login/register works simultaneously

If Jorgito successfully logged in/registered, that confirms the App Service `Connection strings` tab value (Name=`DefaultConnection`, Type=SQLAzure) IS being applied for HTTP-request-scoped DI resolutions. The login page uses `AppDbContext` resolved from DI during an HTTP request, after the app finished starting.

**The apparent contradiction resolves as follows:**  
The crash-restart loop errors were generated BEFORE the connection string fix took full effect (e.g., errors logged during the startup of a process instance that was still running with the old placeholder), OR they are from a brief period during which the App Service had restarted but the new config value had not yet propagated to the running process.

After deploying this fix (see below), the crash-restart loop is broken: even if the seeder fails transiently, the app continues to run and the restart loop stops.

---

## Application Insights / Log Stream Contamination — Ruled Out

- Log Stream in Azure Portal is strictly scoped per App Service resource (streams stdout/stderr via Kudu). It CANNOT mix logs from `TravelRequestApp` (Web) and `TravelReqFunction` (Functions) — those are separate App Service resources.
- The Function App sends telemetry via OpenTelemetry to Azure Monitor — this appears in Application Insights, not Log Stream.
- The placeholder DB name `TravelRequestWFDb` is ONLY present in the Web project's `appsettings.json` — conclusively proving the error originates from the Web app process.

---

## Fix Applied

**File:** `src/TravelRequestWF.Web/Program.cs`

### Change 1 — Add `EnableRetryOnFailure` to `UseSqlServer`

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null)));
```

- Directly addresses the error message recommendation
- Handles transient Azure SQL connectivity blips (network hiccup, brief throttling, cold-start latency) without propagating as exceptions
- Retries up to 5 times with up to 30s delay before giving up

### Change 2 — Seeder wrapped in try-catch

```csharp
using (var scope = app.Services.CreateScope())
{
    var seederLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        await IdentitySeeder.SeedAsync(scope.ServiceProvider);
    }
    catch (Exception ex)
    {
        seederLogger.LogWarning(ex,
            "IdentitySeeder failed on this startup — likely a transient DB connectivity issue. " +
            "The app will continue; seeding will be retried on the next cold-start.");
    }
}
```

- **Breaks the crash-restart loop**: a seeder failure now logs a Warning and lets the app continue, instead of crashing the host
- The seeder is idempotent (checks `FindByEmailAsync` / `RoleExistsAsync` before creating) so retrying on the next cold-start is safe
- Core functionality (login, pages, user requests) is unaffected if seeding is skipped once

---

## Verification Steps for Jorgito

1. **Deploy this fix** — push `dev` → trigger the App Service deploy pipeline
2. After deploy, watch Log Stream: you should see `warn: Program[0] IdentitySeeder failed...` messages IF there are still transient DB blips, but **no more `fail:` EF Core connection errors** and no more crash-restart loops
3. If errors completely stop: the seeder was the culprit and `EnableRetryOnFailure` resolved it cleanly
4. If the exact same `TravelRequestWFDb` errors continue appearing even AFTER this deploy: something in Azure Portal is still sending the placeholder string. In that case, open Kudu → Debug Console → run `printenv | grep -i connection` to see the live environment variables of the running process

---

## Residual Risk

The `appsettings.json` placeholder connection string should remain as-is (it's a sentinel value for local development when real credentials aren't configured). The App Service config override is the correct production mechanism. No change needed in `appsettings.json`.
