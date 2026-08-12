using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TravelRequestWF.Infrastructure.Entities;
using TravelRequestWF.Infrastructure.Identity;
using TravelRequestWF.Infrastructure.Services;

namespace TravelRequestWF.Web.Pages.Manager;

[Authorize(Roles = "Manager")]
public class ReviewModel : PageModel
{
    private readonly ITravelRequestService _travelRequestService;
    private readonly UserManager<ApplicationUser> _userManager;

    public ReviewModel(ITravelRequestService travelRequestService, UserManager<ApplicationUser> userManager)
    {
        _travelRequestService = travelRequestService;
        _userManager = userManager;
    }

    public new TravelRequest? Request { get; set; }
    [BindProperty] public string? Comments { get; set; }
    public string? ErrorMessage { get; set; }

    private async Task<(int employeeId, string userId)?> GetManagerInfoAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user?.EmployeeId == null) return null;
        return (user.EmployeeId.Value, user.Id);
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var info = await GetManagerInfoAsync()
            ?? throw new InvalidOperationException("User not linked to Employee.");

        Request = await _travelRequestService.GetRequestByIdAsync(id);
        if (Request == null) return NotFound();
        if (Request.ApproverId != info.employeeId) return Forbid();

        return Page();
    }

    public async Task<IActionResult> OnPostApproveAsync(int id)
        => await HandleActionAsync(id, (req, emp, usr) =>
            _travelRequestService.ApproveRequestAsync(req, emp, usr, Comments));

    public async Task<IActionResult> OnPostRejectAsync(int id)
        => await HandleActionAsync(id, (req, emp, usr) =>
            _travelRequestService.RejectRequestAsync(req, emp, usr, Comments));

    public async Task<IActionResult> OnPostReturnAsync(int id)
        => await HandleActionAsync(id, (req, emp, usr) =>
            _travelRequestService.ReturnRequestAsync(req, emp, usr, Comments));

    private async Task<IActionResult> HandleActionAsync(int id, Func<int, int, string, Task> action)
    {
        var info = await GetManagerInfoAsync()
            ?? throw new InvalidOperationException("User not linked to Employee.");

        try
        {
            await action(id, info.employeeId, info.userId);
            return RedirectToPage("/Manager/Index");
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            Request = await _travelRequestService.GetRequestByIdAsync(id);
            ErrorMessage = ex.Message;
            return Page();
        }
    }
}

