using Microsoft.AspNetCore.Identity;
using TravelRequestWF.Infrastructure.Data;
using TravelRequestWF.Infrastructure.Entities;
using TravelRequestWF.Infrastructure.Identity;

namespace TravelRequestWF.Web;

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

        // Seed test users + employee records
        var testUsers = new[]
        {
            new { Email = "manager1@test.com",  Password = "Manager1!Pass",  Role = "Manager",
                  Name = "Carol White",  Department = "Management", SuperiorId = (int?)null },
            new { Email = "manager2@test.com",  Password = "Manager2!Pass",  Role = "Manager",
                  Name = "David Brown",  Department = "Management", SuperiorId = (int?)null },
            new { Email = "employee1@test.com", Password = "Employee1!Pass", Role = "Employee",
                  Name = "Alice Johnson", Department = "Finance", SuperiorId = (int?)null },
            new { Email = "employee2@test.com", Password = "Employee2!Pass", Role = "Employee",
                  Name = "Bob Smith",    Department = "Finance", SuperiorId = (int?)null },
        };

        // Seed managers first so employees can reference their SuperiorId
        int? manager1EmployeeId = null;

        foreach (var u in testUsers)
        {
            if (await userManager.FindByEmailAsync(u.Email) != null)
                continue;

            var emp = db.Employees.FirstOrDefault(e => e.Email == u.Email);
            if (emp == null)
            {
                // For employee roles, link to manager1 as superior
                int? superiorId = u.Role == "Employee" ? manager1EmployeeId : u.SuperiorId;
                emp = new Employee
                {
                    Name = u.Name,
                    Email = u.Email,
                    Department = u.Department,
                    SuperiorId = superiorId
                };
                db.Employees.Add(emp);
                await db.SaveChangesAsync();
            }

            if (u.Email == "manager1@test.com")
                manager1EmployeeId = emp.Id;

            var appUser = new ApplicationUser
            {
                UserName = u.Email,
                Email = u.Email,
                EmailConfirmed = true,
                EmployeeId = emp.Id
            };
            var result = await userManager.CreateAsync(appUser, u.Password);
            if (result.Succeeded)
                await userManager.AddToRoleAsync(appUser, u.Role);
        }
    }
}
