using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TravelRequestWF.Infrastructure.Entities;
using TravelRequestWF.Infrastructure.Identity;
using TravelRequestWF.Infrastructure.Services;

namespace TravelRequestWF.Web.Pages.Employee;

[Authorize(Roles = "Employee")]
public class SubmitModel : PageModel
{
    private readonly ITravelRequestService _travelRequestService;
    private readonly UserManager<ApplicationUser> _userManager;

    public SubmitModel(ITravelRequestService travelRequestService, UserManager<ApplicationUser> userManager)
    {
        _travelRequestService = travelRequestService;
        _userManager = userManager;
    }

    [BindProperty] public string Destination { get; set; } = "";
    [BindProperty] public DateOnly StartDate { get; set; }
    [BindProperty] public DateOnly EndDate { get; set; }
    [BindProperty] public string Purpose { get; set; } = "";
    [BindProperty] public List<IFormFile> Documents { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public IActionResult OnGet() => Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        if (StartDate > EndDate)
        {
            ModelState.AddModelError(nameof(StartDate), "Start date must be on or before end date.");
            return Page();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user?.EmployeeId == null)
            throw new InvalidOperationException("User not linked to Employee.");

        var employeeId = user.EmployeeId.Value;
        var userId = user.Id;

        var docTuples = Documents
            .Select(f => (f.OpenReadStream(), f.FileName, f.ContentType))
            .ToList<(Stream Stream, string FileName, string ContentType)>();

        var dto = new SubmitRequestDto(Destination, StartDate, EndDate, Purpose, docTuples);

        try
        {
            await _travelRequestService.SubmitRequestAsync(employeeId, userId, dto);
            return RedirectToPage("/Employee/Index");
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
            return Page();
        }
    }
}

