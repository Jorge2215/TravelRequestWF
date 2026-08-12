using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TravelRequestWF.Web.Pages.Employee;

[Authorize(Roles = "Employee")]
public class DetailModel : PageModel
{
    public void OnGet(int id) { }
}
