using AcxiomCRM.Authorization;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Data;

public sealed class DbInitializer(
    ApplicationDbContext dbContext,
    RoleManager<IdentityRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    ILogger<DbInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);

        foreach (var roleName in RoleNames.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(roleName));
                EnsureSucceeded(result, $"Could not create the {roleName} role.");
            }
        }

        if (!environment.IsDevelopment() || !configuration.GetValue<bool>("SeedUsers:Enabled"))
        {
            return;
        }

        await SeedUserAsync("Admin", RoleNames.Admin, cancellationToken);
        await SeedUserAsync("Manager", RoleNames.Manager, cancellationToken);
        await SeedUserAsync("SalesExecutive", RoleNames.SalesExecutive, cancellationToken);
        await AssignDevelopmentManagerAsync(cancellationToken);
        await SeedDemoDataAsync(cancellationToken);
    }

    private async Task AssignDevelopmentManagerAsync(CancellationToken cancellationToken)
    {
        var managerEmail = configuration["SeedUsers:Manager:Email"];
        var salesEmail = configuration["SeedUsers:SalesExecutive:Email"];
        if (string.IsNullOrWhiteSpace(managerEmail) || string.IsNullOrWhiteSpace(salesEmail)) return;
        var manager = await userManager.FindByEmailAsync(managerEmail);
        var sales = await userManager.FindByEmailAsync(salesEmail);
        if (manager is null || sales is null || sales.ManagerId == manager.Id) return;
        sales.ManagerId = manager.Id;
        var result = await userManager.UpdateAsync(sales);
        EnsureSucceeded(result, "Could not assign the development SalesExecutive to the Manager.");
        logger.LogInformation("Configured development reporting line for {SalesUser}.", salesEmail);
    }

    private async Task SeedDemoDataAsync(CancellationToken cancellationToken)
    {
        if (await dbContext.Customers.AnyAsync(x => x.CustomerCode.StartsWith("DEMO-CUS-"), cancellationToken)) return;
        var salesEmail = configuration["SeedUsers:SalesExecutive:Email"];
        var salesUser = string.IsNullOrWhiteSpace(salesEmail) ? null : await userManager.FindByEmailAsync(salesEmail);
        if (salesUser is null)
        {
            logger.LogInformation("CRM demonstration data was skipped because the configured SalesExecutive seed user is unavailable.");
            return;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var customers = Enumerable.Range(1, 12).Select(index => new Customer
        {
            CustomerCode = $"DEMO-CUS-{index:000}",
            CustomerName = $"Demo Customer {index:00}",
            Email = $"contact{index:00}@example.test",
            Phone = $"900000{index:0000}",
            CompanyName = $"Northstar Sample {index:00}",
            City = index % 2 == 0 ? "Pune" : "Bengaluru",
            State = index % 2 == 0 ? "Maharashtra" : "Karnataka",
            Status = index == 12 ? "Inactive" : "Active",
            OwnerId = salesUser.Id,
            CreatedById = salesUser.Id,
            CreatedDate = now.AddDays(-index * 9)
        }).ToList();
        dbContext.Customers.AddRange(customers);
        await dbContext.SaveChangesAsync(cancellationToken);

        var leadStatuses = new[] { "New", "Contacted", "Qualified", "Unqualified", "Lost", "Converted" };
        var leads = Enumerable.Range(1, 18).Select(index => new Lead
        {
            LeadCode = $"DEMO-LEA-{index:000}",
            LeadName = $"Sample Prospect {index:00}",
            Email = $"prospect{index:00}@example.test",
            Phone = $"910000{index:0000}",
            CompanyName = $"Harborline Demo {index:00}",
            Source = index % 3 == 0 ? "Referral" : index % 3 == 1 ? "Website" : "Event",
            Status = leadStatuses[(index - 1) % leadStatuses.Length],
            Priority = index % 4 == 0 ? "High" : "Normal",
            ExpectedValue = 12000 + index * 850,
            AssignedToId = salesUser.Id,
            CreatedById = salesUser.Id,
            CreatedDate = now.AddDays(-index * 4)
        }).ToList();
        dbContext.Leads.AddRange(leads);
        await dbContext.SaveChangesAsync(cancellationToken);

        var stages = new[] { "Qualification", "Proposal", "Negotiation", "Won", "Lost" };
        var opportunities = Enumerable.Range(1, 12).Select(index =>
        {
            var stage = stages[(index - 1) % stages.Length];
            return new Opportunity
            {
                OpportunityName = $"Sample Deal {index:00}",
                CustomerId = customers[(index - 1) % customers.Count].Id,
                LeadId = leads[(index - 1) % leads.Count].Id,
                Amount = 25000 + index * 7250,
                Stage = stage,
                Probability = stage == "Won" ? 100 : stage == "Lost" ? 0 : 20 + index * 5,
                ExpectedCloseDate = now.AddDays(index * 12),
                ClosedDate = stage is "Won" or "Lost" ? now.AddDays(-index) : null,
                Status = stage is "Won" or "Lost" ? stage : "Open",
                AssignedToId = salesUser.Id,
                CreatedById = salesUser.Id,
                CreatedDate = now.AddDays(-index * 13)
            };
        }).ToList();
        dbContext.Opportunities.AddRange(opportunities);
        await dbContext.SaveChangesAsync(cancellationToken);
        for (var index = 0; index < leads.Count; index++)
        {
            if (leads[index].Status == "Converted")
            {
                leads[index].ConvertedCustomerId = customers[index % customers.Count].Id;
                leads[index].ConvertedOpportunityId = opportunities[index % opportunities.Count].Id;
            }
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        var followUps = Enumerable.Range(1, 20).Select(index => new FollowUp
        {
            CustomerId = index % 3 == 0 ? customers[(index - 1) % customers.Count].Id : null,
            LeadId = index % 3 == 1 ? leads[(index - 1) % leads.Count].Id : null,
            OpportunityId = index % 3 == 2 ? opportunities[(index - 1) % opportunities.Count].Id : null,
            FollowUpDate = now.AddDays(index % 4 == 0 ? -index : index),
            FollowUpType = new[] { "Call", "Meeting", "Email", "Task" }[(index - 1) % 4],
            Subject = $"Sample follow-up {index:00}",
            Remarks = "Development demonstration record.",
            Status = index % 5 == 0 ? "Completed" : index % 4 == 0 ? "Missed" : "Planned",
            AssignedToId = salesUser.Id,
            CreatedDate = now.AddDays(-index),
            CompletedDate = index % 5 == 0 ? now.AddDays(-index + 1) : null
        }).ToList();
        var activities = Enumerable.Range(1, 24).Select(index => new CrmActivity
        {
            ActivityType = new[] { "Call", "Meeting", "Email", "Task" }[(index - 1) % 4],
            Subject = $"Sample activity {index:00}",
            Description = "Development demonstration record.",
            ActivityDate = now.AddDays(-index),
            CustomerId = index % 3 == 0 ? customers[(index - 1) % customers.Count].Id : null,
            LeadId = index % 3 == 1 ? leads[(index - 1) % leads.Count].Id : null,
            OpportunityId = index % 3 == 2 ? opportunities[(index - 1) % opportunities.Count].Id : null,
            AssignedToId = salesUser.Id,
            CreatedById = salesUser.Id,
            CreatedDate = now.AddDays(-index)
        }).ToList();
        dbContext.FollowUps.AddRange(followUps);
        dbContext.Activities.AddRange(activities);
        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = salesUser.Id,
            Action = "DevelopmentSeed",
            EntityName = "CRM",
            Module = "System",
            Result = "Success",
            Details = "Fictional demonstration records initialized.",
            CreatedDate = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Initialized fictional CRM demonstration data for {SalesUser}.", salesEmail);
    }

    private async Task SeedUserAsync(string key, string roleName, CancellationToken cancellationToken)
    {
        var email = configuration[$"SeedUsers:{key}:Email"];
        var password = configuration[$"SeedUsers:{key}:Password"];
        var name = configuration[$"SeedUsers:{key}:Name"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation("Development seed user {SeedUser} was skipped because its email or password is not configured.", key);
            return;
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                Name = string.IsNullOrWhiteSpace(name) ? key : name,
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            };
            var createResult = await userManager.CreateAsync(user, password);
            EnsureSucceeded(createResult, $"Could not create configured development user {email}.");
        }

        if (!await userManager.IsInRoleAsync(user, roleName))
        {
            var roleResult = await userManager.AddToRoleAsync(user, roleName);
            EnsureSucceeded(roleResult, $"Could not assign the {roleName} role to {email}.");
        }

        logger.LogInformation("Development seed user {Email} is ready with role {Role}.", email, roleName);
    }

    private static void EnsureSucceeded(IdentityResult result, string message)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"{message} {string.Join(" ", result.Errors.Select(error => error.Description))}");
        }
    }
}
