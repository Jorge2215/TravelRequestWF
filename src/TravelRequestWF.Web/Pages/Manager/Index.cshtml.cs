using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TravelRequestWF.Infrastructure.Entities;
using TravelRequestWF.Infrastructure.Identity;
using TravelRequestWF.Infrastructure.Services;

namespace TravelRequestWF.Web.Pages.Manager;

[Authorize(Roles = "Manager")]
public class IndexModel : PageModel
{
    private readonly ITravelRequestService _travelRequestService;
    private readonly UserManager<ApplicationUser> _userManager;

    public IndexModel(ITravelRequestService travelRequestService, UserManager<ApplicationUser> userManager)
    {
        _travelRequestService = travelRequestService;
        _userManager = userManager;
    }

    public IReadOnlyList<TravelRequest> Requests { get; set; } = Array.Empty<TravelRequest>();

    public async Task OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user?.EmployeeId == null)
            throw new InvalidOperationException("User not linked to Employee.");

        Requests = await _travelRequestService.GetRequestsForManagerAsync(user.EmployeeId.Value);
    }
}

