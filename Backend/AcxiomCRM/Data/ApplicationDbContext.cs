
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Data;

public class ApplicationDbContext
    : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Lead> Leads => Set<Lead>();

    public DbSet<Opportunity> Opportunities =>
        Set<Opportunity>();

    public DbSet<FollowUp> FollowUps => Set<FollowUp>();

    public DbSet<Activity> Activities => Set<Activity>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Unique customer code and email
        builder.Entity<Customer>()
            .HasIndex(c => c.CustomerCode)
            .IsUnique();

        builder.Entity<Customer>()
            .HasIndex(c => c.Email)
            .IsUnique();

        // Unique lead code
        builder.Entity<Lead>()
            .HasIndex(l => l.LeadCode)
            .IsUnique();

        // Customer relationships
        builder.Entity<Opportunity>()
            .HasOne(o => o.Customer)
            .WithMany()
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Opportunity>()
            .HasOne(o => o.Lead)
            .WithMany()
            .HasForeignKey(o => o.LeadId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<FollowUp>()
            .HasOne(f => f.Customer)
            .WithMany()
            .HasForeignKey(f => f.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<FollowUp>()
            .HasOne(f => f.Lead)
            .WithMany()
            .HasForeignKey(f => f.LeadId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Activity>()
            .HasOne(a => a.Customer)
            .WithMany()
            .HasForeignKey(a => a.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Activity>()
            .HasOne(a => a.Lead)
            .WithMany()
            .HasForeignKey(a => a.LeadId)
            .OnDelete(DeleteBehavior.Restrict);

        // Decimal precision
        builder.Entity<Opportunity>()
            .Property(o => o.Amount)
            .HasPrecision(18, 2);

        builder.Entity<Lead>()
            .Property(l => l.ExpectedValue)
            .HasPrecision(18, 2);
    }
}
