# Legolas — Task Brief: Stage 3 UI — Identity Pages & Authorization

**Assigned to:** Legolas (Frontend Dev)  
**Stage:** 3 — Authentication & Roles  
**Branch:** `dev` (never touch `main`)  
**Date assigned:** 2026-08-11T23:06:00-03:00

---

## Context

We have a Razor Pages app (`TravelRequestWF.Web`, net10.0) with stub pages:
- `Pages/Employee/` — Index, Submit, Detail
- `Pages/Manager/` — Index, Review

**Gandalf is simultaneously** adding `ApplicationUser : IdentityUser`, extending `AppDbContext : IdentityDbContext<ApplicationUser>`, registering Identity in `Program.cs`, and adding NuGet packages. You can write your Razor Pages / PageModel code in parallel, but a final integrated build requires Gandalf's changes to be merged first. **Merge order: Gandalf → then Legolas.**

---

## Your Tasks

### 1. Scaffold or Create Account Pages

Try the scaffolding tool first:
```bash
dotnet aspnet-codegenerator identity \
  --dbContext TravelRequestWF.Infrastructure.Data.AppDbContext \
  --files "Account.Login;Account.Logout;Account.Register" \
  --project TravelRequestWF.Web
```

If the scaffolding tool is not available or fails, manually create minimal pages at:
- `Pages/Account/Login.cshtml` + `Login.cshtml.cs`
- `Pages/Account/Register.cshtml` + `Register.cshtml.cs`
- `Pages/Account/Logout.cshtml` + `Logout.cshtml.cs`
- `Pages/Account/AccessDenied.cshtml` + `AccessDenied.cshtml.cs`

**Login PageModel requirements:**
- `[BindProperty] InputModel Input` with `Email` and `Password` fields
- Uses `SignInManager<ApplicationUser>.PasswordSignInAsync`
- On success: redirect to `returnUrl` or `/`
- On failure: add model error "Invalid credentials"

**Register PageModel requirements:**
- `[BindProperty] InputModel Input` with `Email`, `Password`, `ConfirmPassword`
- Uses `UserManager<ApplicationUser>.CreateAsync` + `SignInManager.SignInAsync`
- On success: redirect to `/`
- Default role for self-registered users: `Employee` (call `userManager.AddToRoleAsync(user, "Employee")` after creation)

**Logout PageModel:**
- POST handler only: `SignInManager.SignOutAsync()` → redirect to `/`

**AccessDenied Page:**
- Simple static message: "You do not have permission to access this page."
- Link back to home (`/`)

### 2. Apply [Authorize] Attributes to Existing PageModels

**Employee pages** (`Pages/Employee/`) — add to each PageModel class:
```csharp
[Authorize(Roles = "Employee")]
public class IndexModel : PageModel { ... }
```
Apply to: `Employee/Index`, `Employee/Submit`, `Employee/Detail`

**Manager pages** (`Pages/Manager/`) — add to each PageModel class:
```csharp
[Authorize(Roles = "Manager")]
public class IndexModel : PageModel { ... }
```
Apply to: `Manager/Index`, `Manager/Review`

Also add `using Microsoft.AspNetCore.Authorization;` to each PageModel file.

### 3. Update _Layout.cshtml Nav

Update `Pages/Shared/_Layout.cshtml` to show authentication state. Inject `SignInManager<ApplicationUser>` and `UserManager<ApplicationUser>` at the top of the file:

```cshtml
@inject Microsoft.AspNetCore.Identity.SignInManager<TravelRequestWF.Infrastructure.Identity.ApplicationUser> SignInManager
@inject Microsoft.AspNetCore.Identity.UserManager<TravelRequestWF.Infrastructure.Identity.ApplicationUser> UserManager
```

In the navbar, replace or augment the existing nav links with:

```cshtml
@if (SignInManager.IsSignedIn(User))
{
    <span class="navbar-text me-2">
        Hello, @User.Identity!.Name
        @if (User.IsInRole("Manager"))
        {
            <span class="badge bg-primary ms-1">Manager</span>
        }
        else
        {
            <span class="badge bg-secondary ms-1">Employee</span>
        }
    </span>
    <form method="post" asp-page="/Account/Logout" class="d-inline">
        <button type="submit" class="btn btn-outline-secondary btn-sm">Logout</button>
    </form>
}
else
{
    <a class="btn btn-outline-primary btn-sm me-1" asp-page="/Account/Login">Login</a>
    <a class="btn btn-outline-secondary btn-sm" asp-page="/Account/Register">Register</a>
}
```

Show role-appropriate nav links:
```cshtml
@if (User.IsInRole("Employee"))
{
    <a class="nav-link" asp-page="/Employee/Index">My Requests</a>
    <a class="nav-link" asp-page="/Employee/Submit">Submit Request</a>
}
@if (User.IsInRole("Manager"))
{
    <a class="nav-link" asp-page="/Manager/Index">Review Requests</a>
}
```

### 4. Add AccessDenied Cookie Path

The cookie is configured in Gandalf's `Program.cs` with `options.AccessDeniedPath = "/Account/AccessDenied"` — make sure your `Pages/Account/AccessDenied.cshtml` exists at that path.

### 5. Home Page (Index) — Handle Redirect After Login

Optionally update `Pages/Index.cshtml` to redirect authenticated users to their role-appropriate page:

```cshtml
@if (User.IsInRole("Manager"))
{
    <p><a asp-page="/Manager/Index" class="btn btn-primary">Go to Manager Dashboard</a></p>
}
else if (User.IsInRole("Employee"))
{
    <p><a asp-page="/Employee/Index" class="btn btn-primary">Go to My Travel Requests</a></p>
}
else
{
    <p><a asp-page="/Account/Login" class="btn btn-outline-primary">Login</a> to get started.</p>
}
```

### 6. Verify Build

```bash
dotnet build TravelRequestWF.sln
```

**Note:** This build will fail if Gandalf's `ApplicationUser` class and `AppDbContext` Identity changes are not yet merged. Coordinate merge order — Gandalf's branch first.

### 7. Commit & Push

```bash
git add -A
git commit -m "feat: Identity UI pages + role-based authorization (Stage 3)

- Account/Login, Register, Logout, AccessDenied pages
- [Authorize(Roles)] on Employee/* and Manager/* PageModels
- _Layout.cshtml: login/logout/register nav + user name + role badge

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
git push origin dev
```

---

## Soft Dependency on Gandalf

| Legolas can do in parallel | Requires Gandalf first |
|---|---|
| Write Login/Register/Logout Razor pages (HTML/cshtml) | Final `dotnet build` to pass |
| Write PageModel skeletons with `SignInManager<ApplicationUser>` references | ApplicationUser class in repo |
| Add `[Authorize(Roles)]` attributes to existing PageModels | AppDbContext change & packages |
| Update _Layout.cshtml nav markup | Program.cs Identity registration |

---

## Acceptance Criteria

- [ ] `Pages/Account/Login`, `Register`, `Logout`, `AccessDenied` pages exist and render
- [ ] `[Authorize(Roles = "Employee")]` on all Employee/* PageModels
- [ ] `[Authorize(Roles = "Manager")]` on all Manager/* PageModels
- [ ] _Layout shows user name + role badge when logged in, Login/Register links when not
- [ ] `dotnet build` succeeds (after Gandalf's changes merged)
- [ ] Unauthenticated navigation to `/Employee/Index` or `/Manager/Index` redirects to `/Account/Login`
- [ ] Authenticated Employee navigating to `/Manager/Index` redirects to `/Account/AccessDenied`
