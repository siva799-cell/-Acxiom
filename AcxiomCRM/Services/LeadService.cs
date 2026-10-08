using AcxiomCRM.Data;
using AcxiomCRM.Authorization;
using AcxiomCRM.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace AcxiomCRM.Services;

public sealed class LeadService(ApplicationDbContext dbContext, IAuditService auditService, UserManager<ApplicationUser> userManager)
{
    private static readonly IReadOnlyDictionary<string, string[]> Transitions = new Dictionary<string, string[]>
    {
        ["New"] = ["Contacted"],
        ["Contacted"] = ["Qualified", "Unqualified", "Lost"],
        ["Qualified"] = ["Lost"]
    };

    public async Task<PagedResult<Lead>> GetPageAsync(string? search, string? status, string userId, bool canReadAll,
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = dbContext.Leads.AsNoTracking();
        if (!canReadAll)
        {
            var scope = await CrmAccessScope.UserIdsAsync(dbContext, userId, false, cancellationToken);
            query = query.Where(x => x.AssignedToId != null && scope.Contains(x.AssignedToId));
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.LeadName.Contains(term) || (x.CompanyName != null && x.CompanyName.Contains(term))
                || (x.Email != null && x.Email.Contains(term)) || (x.Source != null && x.Source.Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.CreatedDate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<Lead>(items, page, pageSize, total);
    }

    public async Task<Lead?> GetAsync(int id, string userId, bool canReadAll, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Leads.AsNoTracking().Where(x => x.Id == id);
        if (!canReadAll)
        {
            var scope = await CrmAccessScope.UserIdsAsync(dbContext, userId, false, cancellationToken);
            query = query.Where(x => x.AssignedToId != null && scope.Contains(x.AssignedToId));
        }
        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ServiceResult<Lead>> CreateAsync(Lead lead, string userId, CancellationToken cancellationToken = default, bool canAssign = false, bool isAdmin = false)
    {
        if (lead.Priority is not ("Low" or "Normal" or "High")) return ServiceResult<Lead>.Failure("Choose a valid lead priority.");
        if (canAssign && !string.IsNullOrWhiteSpace(lead.AssignedToId)
            && !await CrmAccessScope.CanAssignToAsync(dbContext, userManager, userId, lead.AssignedToId, isAdmin, cancellationToken))
            return ServiceResult<Lead>.Failure("Assign the lead to an active Sales Executive within your permitted scope.");
        lead.LeadCode = $"LEA-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        lead.CreatedById = userId;
        if (!canAssign || string.IsNullOrWhiteSpace(lead.AssignedToId)) lead.AssignedToId = userId;
        lead.CreatedDate = DateTime.UtcNow;
        lead.Status = "New";
        dbContext.Leads.Add(lead);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(userId, "CreateLead", "Leads", nameof(Lead), lead.Id.ToString(), newValue: lead.LeadName, cancellationToken: cancellationToken);
        return ServiceResult<Lead>.Success(lead);
    }

    public async Task<ServiceResult<Lead>> UpdateAsync(int id, Lead input, string userId, bool canManageAll, CancellationToken cancellationToken = default, bool canAssign = false)
    {
        var query = dbContext.Leads.AsQueryable();
        if (!canManageAll)
        {
            var scope = await CrmAccessScope.UserIdsAsync(dbContext, userId, false, cancellationToken);
            query = query.Where(x => x.AssignedToId != null && scope.Contains(x.AssignedToId));
        }
        var lead = await query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (lead is null) return ServiceResult<Lead>.Failure("Lead not found.");
        if (input.Priority is not ("Low" or "Normal" or "High")) return ServiceResult<Lead>.Failure("Choose a valid lead priority.");
        if (lead.Status != input.Status && (!Transitions.TryGetValue(lead.Status, out var allowed) || !allowed.Contains(input.Status)))
            return ServiceResult<Lead>.Failure($"Lead cannot move from {lead.Status} to {input.Status}.");
        if (canAssign && !string.IsNullOrWhiteSpace(input.AssignedToId)
            && !await CrmAccessScope.CanAssignToAsync(dbContext, userManager, userId, input.AssignedToId, canManageAll, cancellationToken))
            return ServiceResult<Lead>.Failure("Assign the lead to an active Sales Executive within your permitted scope.");

        var oldState = $"{lead.LeadName}|{lead.Status}";
        lead.LeadName = input.LeadName.Trim();
        lead.Email = input.Email?.Trim();
        lead.Phone = input.Phone?.Trim();
        lead.CompanyName = input.CompanyName?.Trim();
        lead.Source = input.Source?.Trim();
        lead.Priority = input.Priority;
        lead.ExpectedValue = input.ExpectedValue;
        lead.Notes = input.Notes?.Trim();
        lead.Status = input.Status;
        if (canAssign && !string.IsNullOrWhiteSpace(input.AssignedToId)) lead.AssignedToId = input.AssignedToId;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(userId, "UpdateLead", "Leads", nameof(Lead), id.ToString(), oldValue: oldState, newValue: $"{lead.LeadName}|{lead.Status}", cancellationToken: cancellationToken);
        return ServiceResult<Lead>.Success(lead);
    }

    public async Task<ServiceResult<Lead>> ConvertAsync(int id, string userId, bool canManageAll, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var query = dbContext.Leads.AsQueryable();
        if (!canManageAll)
        {
            var scope = await CrmAccessScope.UserIdsAsync(dbContext, userId, false, cancellationToken);
            query = query.Where(x => x.AssignedToId != null && scope.Contains(x.AssignedToId));
        }
        var lead = await query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (lead is null) return ServiceResult<Lead>.Failure("Lead not found.");
        if (lead.Status != "Qualified") return ServiceResult<Lead>.Failure("Only a qualified lead can be converted.");
        if (lead.ExpectedValue <= 0) return ServiceResult<Lead>.Failure("Set an expected value greater than 0 before converting this lead.");

        Customer? customer = null;
        if (!string.IsNullOrWhiteSpace(lead.Email))
            customer = await dbContext.Customers.FirstOrDefaultAsync(x => x.Email == lead.Email, cancellationToken);
        if (customer is null && !string.IsNullOrWhiteSpace(lead.Phone))
            customer = await dbContext.Customers.FirstOrDefaultAsync(x => x.Phone == lead.Phone, cancellationToken);
        if (customer is null)
        {
            customer = new Customer
            {
                CustomerCode = $"CUS-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
                CustomerName = lead.LeadName,
                Email = lead.Email,
                Phone = lead.Phone,
                CompanyName = lead.CompanyName,
                OwnerId = lead.AssignedToId ?? userId,
                CreatedById = userId,
                CreatedDate = DateTime.UtcNow
            };
            dbContext.Customers.Add(customer);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var opportunity = new Opportunity
        {
            OpportunityName = $"{lead.CompanyName ?? lead.LeadName} - New opportunity",
            CustomerId = customer.Id,
            LeadId = lead.Id,
            Amount = lead.ExpectedValue,
            Stage = "Qualification",
            Probability = 10,
            Status = "Open",
            CreatedById = userId,
            AssignedToId = lead.AssignedToId ?? userId
        };
        dbContext.Opportunities.Add(opportunity);
        lead.Status = "Converted";
        await dbContext.SaveChangesAsync(cancellationToken);
        lead.ConvertedCustomerId = customer.Id;
        lead.ConvertedOpportunityId = opportunity.Id;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(userId, "LeadConversion", "Leads", nameof(Lead), id.ToString(), newValue: $"Customer:{customer.Id};Opportunity:{opportunity.Id}", cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ServiceResult<Lead>.Success(lead);
    }

    public async Task<ServiceResult<Lead>> DeactivateAsync(int id, string userId, CancellationToken cancellationToken = default)
    {
        var lead = await dbContext.Leads.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (lead is null) return ServiceResult<Lead>.Failure("Lead not found.");
        if (lead.Status is "Converted" or "Lost" or "Unqualified") return ServiceResult<Lead>.Failure("This lead is already closed and cannot be deactivated.");
        if (lead.Status == "New") lead.Status = "Contacted";
        lead.Status = "Lost";
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(userId, "DeleteLead", "Leads", nameof(Lead), id.ToString(), newValue: "Lost", cancellationToken: cancellationToken);
        return ServiceResult<Lead>.Success(lead);
    }
}
