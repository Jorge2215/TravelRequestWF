using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TravelRequestWF.Web.Pages.Employee;

[Authorize(Roles = "Employee")]
public class SubmitModel : PageModel
{
    public void OnGet() { }
}
