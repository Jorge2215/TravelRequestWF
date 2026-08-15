using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TravelRequestWF.Infrastructure.Data;
using TravelRequestWF.Infrastructure.Entities;
using TravelRequestWF.Infrastructure.Identity;

namespace TravelRequestWF.Web.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly AppDbContext _db;

    public RegisterModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, AppDbContext db)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<SelectListItem> ManagerOptions { get; set; } = new();

    public async Task OnGetAsync()
    {
        await LoadManagerOptionsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadManagerOptionsAsync();

        if (!ModelState.IsValid)
            return Page();

        var user = new ApplicationUser { UserName = Input.Email, Email = Input.Email };
        var result = await _userManager.CreateAsync(user, Input.Password);

        if (result.Succeeded)
        {
            // Self-registered users are always Employees (not Managers).
            await _userManager.AddToRoleAsync(user, "Employee");

            // Always create a linked Employee row so the submission workflow never crashes.
            var name = string.IsNullOrWhiteSpace(Input.FullName)
                ? Input.Email.Split('@')[0]
                : Input.FullName.Trim();

            var employee = new TravelRequestWF.Infrastructure.Entities.Employee
            {
                Name = name,
                Email = Input.Email,
                Department = "General",
                SuperiorId = Input.ManagerId > 0 ? Input.ManagerId : null
            };
            _db.Employees.Add(employee);
            await _db.SaveChangesAsync();

            user.EmployeeId = employee.Id;
            await _userManager.UpdateAsync(user);

            await _signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToPage("/Index");
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);

        return Page();
    }

    private async Task LoadManagerOptionsAsync()
    {
        // Find all ApplicationUsers in the Manager role and join to their Employee record.
        var managerUsers = await _userManager.GetUsersInRoleAsync("Manager");
        var managerIds = managerUsers.Select(u => u.EmployeeId).Where(id => id != null).Select(id => id!.Value).ToList();

        ManagerOptions = await _db.Employees
            .Where(e => managerIds.Contains(e.Id))
            .OrderBy(e => e.Name)
            .Select(e => new SelectListItem { Value = e.Id.ToString(), Text = e.Name })
            .ToListAsync();
    }

    public class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Full Name")]
        [StringLength(100)]
        public string? FullName { get; set; }

        /// <summary>Selected manager's Employee.Id (optional — null if no managers exist yet).</summary>
        [Display(Name = "Your Manager")]
        public int? ManagerId { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
