# Phase 10 — Connection String Troubleshooting: App Service Not Reading Environment Variables

**Diagnosed by:** Gandalf  
**Date:** 2026-08-15  
**Status:** Root cause identified — Portal configuration naming mistake (no code changes required)

---

## The Symptom

Azure Portal Log Stream shows:

> An error occurred using the connection to database `'TravelRequestWFDb'` on server `'<your-azure-sql-server>.database.windows.net'`

The placeholder text `<your-azure-sql-server>` and database name `TravelRequestWFDb` are the **literal placeholder values from `appsettings.json`** (committed to git). The real Azure SQL database is named `TravelRequestDB` (no "WF", different casing). This proves 100% that the App Service is reading `appsettings.json` and **completely ignoring** the environment variable Jorgito set in the Azure Portal.

---

## Root Cause: Wrong Naming Convention for Nested Config Keys in App Service "Application settings"

### How ASP.NET Core configuration hierarchy works

ASP.NET Core loads configuration sources in this order (later sources override earlier ones):

1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. Environment variables ← **this is where Azure App Service Application settings inject**
4. Command-line arguments

For environment variables to **override** a nested key like `ConnectionStrings:DefaultConnection`, the environment variable name must use a **double underscore (`__`)** as the hierarchy separator, not a colon.

| appsettings.json path | Correct env var name for App Service | ❌ Incorrect (what was likely set) |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` | `ConnectionStrings:DefaultConnection` |
| `AzureStorage:ConnectionString` | `AzureStorage__ConnectionString` | `AzureStorage:ConnectionString` |
| `PowerAutomate:FlowASubmissionUrl` | `PowerAutomate__FlowASubmissionUrl` | `PowerAutomate:FlowASubmissionUrl` |
| `PowerAutomate:FlowBStatusChangeUrl` | `PowerAutomate__FlowBStatusChangeUrl` | `PowerAutomate:FlowBStatusChangeUrl` |

**Why colons fail:** Azure App Service injects Application settings as OS-level environment variables. On Linux (and Windows App Service), a colon in an env var name is either silently ignored or treated as a flat key unrelated to any nested JSON path — .NET's environment variable configuration provider only maps `__` to the `:` hierarchy separator. If Jorgito typed `ConnectionStrings:DefaultConnection` (with a literal colon) as the **Name** in the "Application settings" tab, .NET Core sees it as an unknown flat key, falls through to `appsettings.json`, and reads the placeholder.

### Confirming the bug without Portal access

Jorgito: look at your App Service → **Settings → Environment variables → App settings tab**. Check the **Name** column for each entry you added. If any names contain a `:` (colon) instead of `__` (double underscore), that is the bug.

---

## Fix: Exact Portal Steps

### Option A — Fix the "App settings" tab entries (rename with double underscore)

1. Azure Portal → your App Service → **Settings → Environment variables**
2. Click the **"App settings"** tab (NOT "Connection strings")
3. For each incorrectly-named entry, click the ✏️ edit icon and change the **Name** field:

   | Delete/change this Name | Set it to this Name |
   |---|---|
   | `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` |
   | `AzureStorage:ConnectionString` | `AzureStorage__ConnectionString` |
   | `PowerAutomate:FlowASubmissionUrl` | `PowerAutomate__FlowASubmissionUrl` |
   | `PowerAutomate:FlowBStatusChangeUrl` | `PowerAutomate__FlowBStatusChangeUrl` |

4. Leave the **Value** unchanged (the real connection string / URL you already entered).
5. Click **Save** at the top of the page.
6. Azure will automatically restart the App Service after saving. If it doesn't restart automatically, go to **Overview → Restart** manually.
7. Wait ~30 seconds, then reload the app — the crash should be gone.

---

### Option B — Use the dedicated "Connection strings" tab instead (for SQL only)

Azure App Service has a separate "Connection strings" section specifically for database connection strings. This is an equally valid alternative **for `DefaultConnection` only** (the SQL connection string):

1. Azure Portal → your App Service → **Settings → Environment variables**
2. Click the **"Connection strings"** tab
3. Click **"+ Add"** (or edit the existing entry if one is there)
4. Set:
   - **Name:** `DefaultConnection` ← exact key name matching `appsettings.json`, no prefix
   - **Type:** `SQLAzure`
   - **Value:** your real Azure SQL connection string
5. Click **Save** → App Service restarts.

When you use the "Connection strings" tab with Type=SQLAzure, Azure **automatically** maps it into the `ConnectionStrings:DefaultConnection` configuration path that ASP.NET Core reads — no double-underscore needed. This only applies to the Connection strings tab; the App settings tab always requires `__`.

> **Note:** For `AzureStorage:ConnectionString` and the PowerAutomate URLs, the "Connection strings" tab is NOT appropriate (it's SQL-specific). Those must use the **App settings tab with `__` double-underscore** naming (Option A).

---

## Restart Reminder

Azure App Service usually restarts automatically after you click **Save** on the Environment variables / Configuration page. However:

- If the app is still showing the same error after 1–2 minutes, go to **Overview → Restart** to force a clean restart.
- Configuration changes take effect **only after restart** — in-flight requests use the old config until the worker process recycles.

---

## Code Verification (No Code Change Needed)

`Program.cs` uses the standard runtime pattern:

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
```

`GetConnectionString("DefaultConnection")` is shorthand for `IConfiguration["ConnectionStrings:DefaultConnection"]`. This is evaluated at **runtime** (not build time), so it correctly picks up App Service environment variables — once named correctly with `__`. **No code change is required.**

---

## Summary of All Keys to Fix

Apply Option A (double-underscore in App settings tab) to **all four** keys — do not only fix the SQL one:

| App settings Name (correct) | What it maps to in appsettings.json |
|---|---|
| `ConnectionStrings__DefaultConnection` | Azure SQL connection string |
| `AzureStorage__ConnectionString` | Azure Blob Storage connection string |
| `PowerAutomate__FlowASubmissionUrl` | Flow A HTTP trigger URL |
| `PowerAutomate__FlowBStatusChangeUrl` | Flow B HTTP trigger URL |

---

## Why the Instructions in the Phase 10 Checklist Were Misleading

The Phase 10 checklist (`.squad/files/phase10-web-deploy-workflow-setup.md`) listed the App Service keys using colon notation:

```
- `ConnectionStrings:DefaultConnection`
- `AzureStorage:ConnectionString`
```

These are the correct **appsettings.json paths** but are **NOT** the correct names to type into the Azure Portal "App settings" Name field. That guide will be corrected — but for now, use the `__` names above.

---

*Gandalf — 2026-08-15 | Phase 10 connection string fix*


---

## Round 2 — Double-Underscore Applied But Placeholder STILL Appears (2026-08-15)

**Symptom:** Jorgito renamed env vars to double-underscore notation AND restarted/redeployed. Log stream still shows `<your-azure-sql-server>.database.windows.net` and `TravelRequestWFDb` as the actual server/database names in the EF Core error.

**Code verdict (re-confirmed):** `Program.cs` is clean — `builder.Configuration.GetConnectionString("DefaultConnection")` with no fallback, no `??` coalescing, no custom `ConfigureAppConfiguration` altering precedence. No `appsettings.Production.json`. No bug in code. This is a Portal/infra issue.

**Why the placeholder string in the error IS conclusive proof:** EF Core's connection error message embeds `connection.DataSource` and `connection.Database` from the actual live `SqlConnection` object at runtime — not from any static template. The literal `<your-azure-sql-server>` text in the log stream means the running process genuinely received that string as its connection string. The env var override definitively did NOT take effect.

---

## Prioritized Checklist for Jorgito (Most Likely First)

### 🥇 #1 — DID YOU CLICK THE FINAL "SAVE" BUTTON? (Most Likely Root Cause)

Azure Portal's Environment Variables / Configuration blade has a **two-step save** process:
1. You add/edit an entry — this only stages the change in the list UI.
2. You MUST click the **"Save"** button at the **top of the page** (blue button, usually top-left of the blade).
3. A confirmation dialog may appear — click **"Continue"** / **"OK"**.
4. A green toast notification should appear: "Successfully updated web app settings."
5. The App Service automatically restarts after a successful save.

**If you skipped step 2, nothing was persisted to Azure — the env var was never set.**

Verify: Go to Portal → App Service → Environment variables → App settings tab → look for `ConnectionStrings__DefaultConnection` in the list. If it's there, the value should be your real connection string (click the eye/value field to reveal). If it's NOT there, the save was never completed.

---

### 🥈 #2 — Verify the Exact Name Spelling in the Portal List

The name must be **exactly**: `ConnectionStrings__DefaultConnection`
- Two underscores (`__`), not one (`_`) and not a colon (`:`)
- Capital `C`, capital `S`, capital `D`, capital `C` — case-sensitive on Linux-hosted App Services
- No leading/trailing spaces (easy to accidentally paste one)

Go to Portal → App Service → **Environment variables** → **App settings** tab → find the entry and click the pencil (Edit) icon to see the exact Name field value character by character.

---

### 🥉 #3 — Use Kudu to Verify What the Running Process Actually Sees

Navigate to: `https://<your-app-name>.scm.azurewebsites.net/Env.cshtml`

This shows all environment variables as the live running process sees them. Search the page for `ConnectionStrings`. If `ConnectionStrings__DefaultConnection` appears there with your real value, the env var is set correctly and the problem is something else. If it shows the placeholder or doesn't appear at all, the Portal save did not persist.

Alternative Kudu path: `https://<your-app-name>.scm.azurewebsites.net/DebugConsole` → run `echo %ConnectionStrings__DefaultConnection%` (Windows sandbox) or `printenv ConnectionStrings__DefaultConnection` (Linux).

---

### #4 — Clean Up: Check for Leftover Colon-Named Entries

If Jorgito added the new `__` entries AND kept the old `:` entries (both present), the `:` entries are harmless (they map to an unknown flat key, not `ConnectionStrings:DefaultConnection`), but they're confusing. Remove them to keep settings clean.

Portal → App Service → Environment variables → App settings → look for any entry with a colon in the name (e.g., `ConnectionStrings:DefaultConnection`) → delete it.

---

### #5 — Confirm App Service Actually Restarted After the Save

A successful save auto-restarts the App Service. But if Jorgito manually restarted BEFORE saving (or if the save failed silently), the process may have restarted without the new env vars.

After confirming the save: Portal → App Service → **Overview** → click **Restart** once more → wait ~60 seconds → check Log Stream again.

---

### #6 — Remote Possibility: Wrong Deployment Slot

If the App Service has multiple deployment slots (e.g., "production" slot and a "staging" slot), each slot has its OWN independent set of environment variables. Editing "staging" slot settings while the live traffic runs on "production" slot would produce exactly this symptom.

Check: Portal → App Service → Deployment → Deployment slots. If only one slot exists (the default), this is NOT the issue.

---

### #7 — Alternate Fix Path: Use the "Connection strings" Tab Instead

The Azure Portal's Configuration blade has TWO tabs:
- **App settings** — for general env vars; requires `__` notation for hierarchy
- **Connection strings** — specifically for SQL; Azure automatically maps these into `ConnectionStrings:*` without needing the `__` prefix

As an alternative to the `__` env var approach, Jorgito can:
1. Go to Portal → App Service → Environment variables → **Connection strings** tab
2. Add entry: Name = `DefaultConnection`, Value = `<real connection string>`, Type = `SQLAzure`
3. Click Save → confirm → restart
4. This bypasses the `__` naming entirely and will definitely override `appsettings.json`

---

## Code Confirmation (No Code Bug Found)

`Program.cs` — `AddDbContext` call:
```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
```
- Standard `IConfiguration.GetConnectionString("DefaultConnection")` — reads from `ConnectionStrings:DefaultConnection` at runtime.
- No `??` fallback, no hardcoded placeholder in C# code.
- No custom `ConfigureAppConfiguration` that would change env var precedence.
- No `appsettings.Production.json` that could win over env vars.
- ASP.NET Core default precedence: appsettings.json → appsettings.{Env}.json → **env vars** → cmd args. Env vars WIN over json files in standard setup. Code is correct.

**Conclusion: Fix is 100% in Azure Portal, not in code.**

---

*Gandalf — 2026-08-15 | Phase 10 Round 2 deeper diagnosis*
