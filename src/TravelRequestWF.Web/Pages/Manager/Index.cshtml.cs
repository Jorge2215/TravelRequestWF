using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TravelRequestWF.Web.Pages.Manager;

[Authorize(Roles = "Manager")]
public class IndexModel : PageModel
{
    public void OnGet() { }
}
