using Microsoft.AspNetCore.Identity;
using TravelRequestWF.Infrastructure.Entities;

namespace TravelRequestWF.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }
}
