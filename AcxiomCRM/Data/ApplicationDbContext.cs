using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Models;

namespace AcxiomCRM.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
	public DbSet<Customer> Customers => Set<Customer>();
	public DbSet<Lead> Leads => Set<Lead>();
	public DbSet<Opportunity> Opportunities => Set<Opportunity>();
	public DbSet<FollowUp> FollowUps => Set<FollowUp>();
	public DbSet<CrmActivity> Activities => Set<CrmActivity>();
	public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

	protected override void OnModelCreating(ModelBuilder builder)
	{
		base.OnModelCreating(builder);

		builder.Entity<Customer>().HasIndex(x => x.CustomerCode).IsUnique();
		builder.Entity<Customer>().HasIndex(x => x.Email).IsUnique().HasFilter("[Email] IS NOT NULL");
		builder.Entity<Customer>().HasIndex(x => x.Phone).IsUnique().HasFilter("[Phone] IS NOT NULL");
		builder.Entity<Customer>().HasIndex(x => x.OwnerId);
		builder.Entity<Lead>().HasIndex(x => x.LeadCode).IsUnique();
		builder.Entity<Lead>().HasIndex(x => new { x.Status, x.AssignedToId });
		builder.Entity<Opportunity>().HasIndex(x => new { x.Stage, x.AssignedToId });
		builder.Entity<FollowUp>().HasIndex(x => new { x.FollowUpDate, x.Status });
		builder.Entity<CrmActivity>().HasIndex(x => x.ActivityDate);
		builder.Entity<AuditLog>().HasIndex(x => new { x.CreatedDate, x.UserId, x.Action });
		builder.Entity<ApplicationUser>().HasIndex(x => x.ManagerId);
		builder.Entity<Lead>().Property(x => x.ExpectedValue).HasPrecision(14, 2);
		builder.Entity<Opportunity>().Property(x => x.Amount).HasPrecision(14, 2);

		builder.Entity<Customer>().HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
		builder.Entity<ApplicationUser>().HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ManagerId).OnDelete(DeleteBehavior.Restrict);
		builder.Entity<Lead>().HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AssignedToId).OnDelete(DeleteBehavior.Restrict);
		builder.Entity<Opportunity>().HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AssignedToId).OnDelete(DeleteBehavior.Restrict);
		builder.Entity<Opportunity>().HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
		builder.Entity<Opportunity>().HasOne<Lead>().WithMany().HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.Restrict);
		builder.Entity<Lead>().HasOne<Customer>().WithMany().HasForeignKey(x => x.ConvertedCustomerId).OnDelete(DeleteBehavior.Restrict);
		builder.Entity<Lead>().HasOne<Opportunity>().WithMany().HasForeignKey(x => x.ConvertedOpportunityId).OnDelete(DeleteBehavior.Restrict);

		builder.Entity<FollowUp>().HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
		builder.Entity<FollowUp>().HasOne<Lead>().WithMany().HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.Restrict);
		builder.Entity<FollowUp>().HasOne<Opportunity>().WithMany().HasForeignKey(x => x.OpportunityId).OnDelete(DeleteBehavior.Restrict);
		builder.Entity<CrmActivity>().HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
		builder.Entity<CrmActivity>().HasOne<Lead>().WithMany().HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.Restrict);
		builder.Entity<CrmActivity>().HasOne<Opportunity>().WithMany().HasForeignKey(x => x.OpportunityId).OnDelete(DeleteBehavior.Restrict);
	}

	public override int SaveChanges()
	{
		PreventAuditMutation();
		return base.SaveChanges();
	}

	public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
	{
		PreventAuditMutation();
		return base.SaveChangesAsync(cancellationToken);
	}

	private void PreventAuditMutation()
	{
		if (ChangeTracker.Entries<AuditLog>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
		{
			throw new InvalidOperationException("Audit records are append-only.");
		}
	}
}
