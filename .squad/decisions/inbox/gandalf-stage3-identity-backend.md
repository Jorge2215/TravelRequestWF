# Gandalf → Team: Stage 3 Identity Backend — Decisions & Notes for Legolas

**Date:** 2026-08-11T23:10:00-03:00  
**From:** Gandalf  
**Relevant to:** Legolas (UI)

## What was built

ASP.NET Identity fully integrated into the backend (Stage 3). Key info Legolas needs:

### ApplicationUser location
- **File:** `src/TravelRequestWF.Infrastructure/Identity/ApplicationUser.cs`
- **Namespace:** `TravelRequestWF.Infrastructure.Identity`
- **Class:** `ApplicationUser : IdentityUser` with `int? EmployeeId` and `Employee? Employee`

### Role names (exact strings for `[Authorize(Roles=...)]`)
- `"Employee"`
- `"Manager"`

### Cookie paths configured
- **LoginPath:** `/Account/Login`  
- **AccessDeniedPath:** `/Account/AccessDenied`

Legolas needs to create these pages (or scaffold them from Identity UI). `Microsoft.AspNetCore.Identity.UI` is already added to the Web project, so scaffolding works out of the box.

### Middleware order in Program.cs
`UseAuthentication()` → `UseAuthorization()` → `MapRazorPages()` — already in the correct order.

### Test credentials (share with Pippin too)
| Email | Password | Role |
|---|---|---|
| employee1@test.com | Employee1!Pass | Employee |
| employee2@test.com | Employee2!Pass | Employee |
| manager1@test.com | Manager1!Pass | Manager |
| manager2@test.com | Manager2!Pass | Manager |

### Seeder note
Seeder runs at app startup (idempotent). After first run, all 4 users + their Employee records exist in `AspNetUsers` and `Employees`. employee1 and employee2 have `SuperiorId` pointing to manager1's Employee row.

### Merge order
My branch must merge to `dev` before Legolas's branch — Legolas's pages will reference `ApplicationUser` which now exists.
