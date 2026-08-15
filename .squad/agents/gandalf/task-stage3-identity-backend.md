# Gandalf — Task Brief: Stage 3 Backend — ASP.NET Identity Integration

**Assigned to:** Gandalf (Backend Dev)  
**Stage:** 3 — Authentication & Roles  
**Branch:** `dev` (never touch `main`)  
**Date assigned:** 2026-08-11T23:06:00-03:00

---

## Context

We have a working .NET 10 solution with two projects:
- `TravelRequestWF.Web` — Razor Pages, net10.0
- `TravelRequestWF.Infrastructure` — EF Core class library with `AppDbContext`

`AppDbContext` currently inherits plain `DbContext` and has four entities: `Employee`, `TravelRequest`, `RequestDocument`, `AuditLogEntry`.  
There is NO authentication currently. Stage 1 & 2 migrations are applied to both LocalDB and Azure SQL.

Architecture decisions (from `aragorn-stage3-identity-scope.md`) are firm — implement exactly as described below.

---

## Your Tasks (in order)

### 1. Add NuGet Packages

In `TravelRequestWF.Web` (or `TravelRequestWF.Infrastructure` if Identity is configured there — see step 3):

```
dotnet add package Microsoft.AspNetCore.Identity.EntityFrameworkCore
dotnet add package Microsoft.AspNetCore.Identity.UI
```

`Microsoft.AspNetCore.Identity.EntityFrameworkCore` goes in the Infrastructure project (it owns `AppDbContext`).  
`Microsoft.AspNetCore.Identity.UI` goes in the Web project (it provides the default UI scaffolding and Razor Class Library).

### 2. Create ApplicationUser Class

Create `TravelRequestWF.Infrastructure/Identity/ApplicationUser.cs`:

```csharp
using Microsoft.AspNetCore.Identity;
using TravelRequestWF.Infrastructure.Models;

namespace TravelRequestWF.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }
}
```

### 3. Extend AppDbContext to IdentityDbContext

Modify `AppDbContext` to inherit from `IdentityDbContext<ApplicationUser, IdentityRole, string>`:

```csharp
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using TravelRequestWF.Infrastructure.Identity;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
    // keep existing DbSet<Employee>, DbSet<TravelRequest>, etc.
    // add the navigation FK config for ApplicationUser.EmployeeId
}
```

In `OnModelCreating`, call `base.OnModelCreating(builder)` FIRST (required by Identity), then apply existing entity configs.

Configure the FK:
```csharp
builder.Entity<ApplicationUser>()
    .HasOne(u => u.Employee)
    .WithMany()
    .HasForeignKey(u => u.EmployeeId)
    .IsRequired(false)
    .OnDelete(DeleteBehavior.SetNull);
```

### 4. Configure Program.cs

In `TravelRequestWF.Web/Program.cs`, add after `builder.Services.AddDbContext<AppDbContext>(...)`:

```csharp
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
});

builder.Services.AddAuthentication();
builder.Services.AddAuthorization();
```

Also ensure `app.UseAuthentication()` comes BEFORE `app.UseAuthorization()` in the middleware pipeline.

### 5. Create EF Core Migration

```bash
cd TravelRequestWF.Infrastructure   # or run from solution root with --project flag
dotnet ef migrations add AddIdentityTables --startup-project ../TravelRequestWF.Web
dotnet ef database update --startup-project ../TravelRequestWF.Web
```

Verify the migration file adds: `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens`, `AspNetRoleClaims`, and the `EmployeeId` column on `AspNetUsers`.

**Azure SQL:** Document (in a comment in the migration or in `.squad/files/stage3-azure-sql-deploy-note.md`) the command to apply the migration to Azure SQL:
```bash
dotnet ef database update \
  --startup-project TravelRequestWF.Web \
  --connection "Server=<azure-sql-server>;Database=TravelRequestWF;..."
```

### 6. Write Startup Seeder

Create `TravelRequestWF.Web/SeedData.cs` (or `TravelRequestWF.Infrastructure/Seeding/IdentitySeeder.cs` — your call on placement):

```csharp
public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var db = services.GetRequiredService<AppDbContext>();

        // Seed roles
        foreach (var role in new[] { "Employee", "Manager" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // Seed employees (if not already present) and link to Identity users
        // Employees to seed (adjust IDs to match your existing seeded data if any):
        // Emp #1: Alice Johnson, Finance
        // Emp #2: Bob Smith, Finance (reports to Alice? — see Employee.SuperiorId)
        // Emp #3: Carol White, Management (manager)
        // Emp #4: David Brown, Management (manager)
        // Then create ApplicationUser for each and assign role

        var testUsers = new[]
        {
            new { Email = "employee1@test.com", Password = "Employee1!Pass", Role = "Employee",
                  Name = "Alice Johnson", Department = "Finance", SuperiorId = (int?)null },
            new { Email = "employee2@test.com", Password = "Employee2!Pass", Role = "Employee",
                  Name = "Bob Smith",    Department = "Finance", SuperiorId = (int?)null },
            new { Email = "manager1@test.com",  Password = "Manager1!Pass",  Role = "Manager",
                  Name = "Carol White",  Department = "Management", SuperiorId = (int?)null },
            new { Email = "manager2@test.com",  Password = "Manager2!Pass",  Role = "Manager",
                  Name = "David Brown",  Department = "Management", SuperiorId = (int?)null },
        };

        foreach (var u in testUsers)
        {
            if (await userManager.FindByEmailAsync(u.Email) == null)
            {
                // Ensure Employee record exists
                var emp = db.Employees.FirstOrDefault(e => e.Email == u.Email);
                if (emp == null)
                {
                    emp = new Employee { Name = u.Name, Email = u.Email, Department = u.Department, SuperiorId = u.SuperiorId };
                    db.Employees.Add(emp);
                    await db.SaveChangesAsync();
                }

                var appUser = new ApplicationUser { UserName = u.Email, Email = u.Email, EmployeeId = emp.Id };
                var result = await userManager.CreateAsync(appUser, u.Password);
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(appUser, u.Role);
            }
        }
    }
}
```

In `Program.cs`, call after `app.Build()` but before `app.Run()`:

```csharp
using (var scope = app.Services.CreateScope())
{
    await IdentitySeeder.SeedAsync(scope.ServiceProvider);
}
```

After seeding, optionally link manager employees: set `employee1.SuperiorId` and `employee2.SuperiorId` to `manager1`'s EmployeeId if the seed data needs that hierarchy. Use your judgment for the PoC — the important thing is roles and logins work.

### 7. Verify Build

```bash
dotnet build TravelRequestWF.sln
```

No errors or warnings related to Identity configuration should remain.

### 8. Commit & Push

```bash
git add -A
git commit -m "feat: integrate ASP.NET Identity (Stage 3 backend)

- Add ApplicationUser with EmployeeId FK
- AppDbContext extends IdentityDbContext<ApplicationUser>
- AddIdentityTables EF Core migration
- Program.cs: AddIdentity, cookie config, middleware order
- IdentitySeeder: 2 roles + 4 test users seeded at startup

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
git push origin dev
```

---

## Test Credentials (share these with Pippin)

| Email | Password | Role |
|---|---|---|
| employee1@test.com | Employee1!Pass | Employee |
| employee2@test.com | Employee2!Pass | Employee |
| manager1@test.com | Manager1!Pass | Manager |
| manager2@test.com | Manager2!Pass | Manager |

---

## Parallel Work Note

Legolas is building the UI (Login/Register pages, `[Authorize]` attributes, _Layout nav) in parallel. Your work (NuGet packages, ApplicationUser class, AppDbContext change, Program.cs AddIdentity) must land in `dev` before a final integrated build can pass. If Legolas's branch references `ApplicationUser` that doesn't exist yet, the build will fail until both branches are merged. Coordinate the merge order: **your branch first, then Legolas**.

---

## Acceptance Criteria

- [ ] `dotnet build` succeeds with no errors
- [ ] Migration `AddIdentityTables` exists and has been applied to LocalDB
- [ ] Azure SQL deploy command documented
- [ ] App starts without exception (seeder runs without crashing)
- [ ] `employee1@test.com` and `manager1@test.com` exist in `AspNetUsers` after first run
- [ ] Roles `Employee` and `Manager` exist in `AspNetRoles`
- [ ] `ApplicationUser.EmployeeId` FK is correctly set for each seeded user
