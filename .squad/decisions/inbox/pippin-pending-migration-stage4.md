# Bug Report: Stage4WorkflowFields Migration Not Applied to Azure SQL

**From:** Pippin (Tester)  
**Date:** 2026-08-12T23:47:34-03:00  
**Severity:** Blocking — all Stage 4 runtime tests cannot execute

---

## Summary

EF Core migration `20260812231909_Stage4WorkflowFields` is in source control but has **not been applied** to the Azure SQL database. This causes a `SqlException` on every request that queries `TravelRequests` or `AuditLogEntries`.

## Error

```
Microsoft.Data.SqlClient.SqlException: Invalid column name 'SubmittedAt'.
```

Stack trace leads to:  
`TravelRequestService.GetRequestsForEmployeeAsync` → `TravelRequests` query → SQL Server rejects `SubmittedAt` column.

## Migration Details

Migration: `20260812231909_Stage4WorkflowFields`  
Status: `(Pending)` per `dotnet ef migrations list`

Columns missing from DB:
- `TravelRequests.SubmittedAt` — `datetime2`, not nullable, default `0001-01-01`
- `AuditLogEntries.Details` — `nvarchar(max)`, nullable

## Impact

All workflow pages fail with HTTP 500 immediately after authentication. None of the 13 Stage 4 test cases can execute. This is the **second consecutive blocker** preventing Stage 4 validation (the first was the BlobStorageService constructor bug, now fixed).

## Fix Required (Gandalf)

Apply the pending migration to the Azure SQL database:

```bash
dotnet ef database update --project src/TravelRequestWF.Infrastructure --startup-project src/TravelRequestWF.Web
```

Or configure the app to auto-apply pending migrations on startup (e.g., `context.Database.MigrateAsync()` in `Program.cs`).

## Context

This migration was added as part of Stage 4 development but the DB update step was apparently skipped before the Gandalf → Pippin handoff.
