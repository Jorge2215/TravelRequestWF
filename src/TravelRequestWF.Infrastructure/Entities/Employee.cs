namespace TravelRequestWF.Infrastructure.Entities;

public class Employee
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;

    public int? SuperiorId { get; set; }
    public Employee? Superior { get; set; }
    public ICollection<Employee> Subordinates { get; set; } = new List<Employee>();

    public ICollection<TravelRequest> TravelRequests { get; set; } = new List<TravelRequest>();
    public ICollection<TravelRequest> ApprovalRequests { get; set; } = new List<TravelRequest>();
}
