using Microsoft.EntityFrameworkCore;
using TravelRequestWF.Infrastructure.Entities;

namespace TravelRequestWF.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<TravelRequest> TravelRequests => Set<TravelRequest>();
    public DbSet<RequestDocument> RequestDocuments => Set<RequestDocument>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Employee self-reference: Superior → Subordinates
        modelBuilder.Entity<Employee>()
            .HasOne(e => e.Superior)
            .WithMany(e => e.Subordinates)
            .HasForeignKey(e => e.SuperiorId)
            .OnDelete(DeleteBehavior.Restrict);

        // TravelRequest → Employee (requester)
        modelBuilder.Entity<TravelRequest>()
            .HasOne(t => t.Employee)
            .WithMany(e => e.TravelRequests)
            .HasForeignKey(t => t.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        // TravelRequest → Employee (approver) — separate FK to avoid cascade cycles
        modelBuilder.Entity<TravelRequest>()
            .HasOne(t => t.Approver)
            .WithMany(e => e.ApprovalRequests)
            .HasForeignKey(t => t.ApproverId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
