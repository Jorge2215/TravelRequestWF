using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TravelRequestWF.Infrastructure.Entities;
using TravelRequestWF.Infrastructure.Identity;
using TravelRequestWF.Infrastructure.Services;

namespace TravelRequestWF.Web.Pages.Employee;

[Authorize(Roles = "Employee")]
public class DetailModel : PageModel
{
    private readonly ITravelRequestService _travelRequestService;
    private readonly UserManager<ApplicationUser> _userManager;

    public DetailModel(ITravelRequestService travelRequestService, UserManager<ApplicationUser> userManager)
    {
        _travelRequestService = travelRequestService;
        _userManager = userManager;
    }

    public new TravelRequest? Request { get; set; }
    public bool CanResubmit => Request?.Status == TravelRequestStatus.Returned;
    public string? ErrorMessage { get; set; }

    private async Task<int?> GetEmployeeIdAsync() =>
        (await _userManager.GetUserAsync(User))?.EmployeeId;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var employeeId = await GetEmployeeIdAsync()
            ?? throw new InvalidOperationException("User not linked to Employee.");

        Request = await _travelRequestService.GetRequestByIdAsync(id);
        if (Request == null) return NotFound();
        if (Request.EmployeeId != employeeId) return Forbid();

        return Page();
    }

    public async Task<IActionResult> OnPostResubmitAsync(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user?.EmployeeId == null)
            throw new InvalidOperationException("User not linked to Employee.");

        try
        {
            await _travelRequestService.ResubmitRequestAsync(id, user.EmployeeId.Value, user.Id);
            return RedirectToPage(new { id });
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            Request = await _travelRequestService.GetRequestByIdAsync(id);
            ErrorMessage = ex.Message;
            return Page();
        }
    }
}

