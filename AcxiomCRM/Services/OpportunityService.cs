using AcxiomCRM.Data;
using AcxiomCRM.Authorization;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public sealed class OpportunityService(ApplicationDbContext dbContext, IAuditService auditService, UserManager<ApplicationUser> userManager)
{
    private static readonly IReadOnlyDictionary<string, string[]> Transitions = new Dictionary<string, string[]>
    {
        ["Qualification"] = ["Proposal", "Lost"],
        ["Proposal"] = ["Negotiation", "Lost"],
        ["Negotiation"] = ["Won", "Lost"]
    };

    public async Task<PagedResult<Opportunity>> GetPageAsync(string? search, string? stage, string userId, bool canReadAll,
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = dbContext.Opportunities.AsNoTracking();
        if (!canReadAll)
        {
            var scope = await CrmAccessScope.UserIdsAsync(dbContext, userId, false, cancellationToken);
            query = query.Where(x => x.AssignedToId != null && scope.Contains(x.AssignedToId));
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.OpportunityName.Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(stage)) query = query.Where(x => x.Stage == stage);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.CreatedDate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<Opportunity>(items, page, pageSize, total);
    }

    public async Task<Opportunity?> GetAsync(int id, string userId, bool canReadAll, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Opportunities.AsNoTracking().Where(x => x.Id == id);
        if (!canReadAll)
        {
            var scope = await CrmAccessScope.UserIdsAsync(dbContext, userId, false, cancellationToken);
            query = query.Where(x => x.AssignedToId != null && scope.Contains(x.AssignedToId));
        }
        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ServiceResult<Opportunity>> CreateAsync(Opportunity input, string userId, CancellationToken cancellationToken = default, bool canManageAll = false, bool canAssign = false)
    {
        var validation = Validate(input);
        if (validation is not null) return ServiceResult<Opportunity>.Failure(validation);
        if (canAssign && !string.IsNullOrWhiteSpace(input.AssignedToId)
            && !await CrmAccessScope.CanAssignToAsync(dbContext, userManager, userId, input.AssignedToId, canManageAll, cancellationToken))
            return ServiceResult<Opportunity>.Failure("Assign the opportunity to an active Sales Executive within your permitted scope.");
        if (!await RelatedRecordsExistAsync(input, userId, canManageAll, cancellationToken))
            return ServiceResult<Opportunity>.Failure("Select related records within your access scope.");
        input.Id = 0;
        input.Stage = "Qualification";
        input.Status = "Open";
        input.CreatedById = userId;
        if (!canAssign || string.IsNullOrWhiteSpace(input.AssignedToId)) input.AssignedToId = userId;
        input.CreatedDate = DateTime.UtcNow;
        dbContext.Opportunities.Add(input);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(userId, "CreateOpportunity", "Opportunities", nameof(Opportunity), input.Id.ToString(), newValue: input.OpportunityName, cancellationToken: cancellationToken);
        return ServiceResult<Opportunity>.Success(input);
    }

    public async Task<ServiceResult<Opportunity>> UpdateAsync(int id, Opportunity input, string userId, bool canManageAll, CancellationToken cancellationToken = default, bool canAssign = false)
    {
        var query = dbContext.Opportunities.AsQueryable();
        if (!canManageAll)
        {
            var scope = await CrmAccessScope.UserIdsAsync(dbContext, userId, false, cancellationToken);
            query = query.Where(x => x.AssignedToId != null && scope.Contains(x.AssignedToId));
        }
        var opportunity = await query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (opportunity is null) return ServiceResult<Opportunity>.Failure("Opportunity not found.");
        var validation = Validate(input);
        if (validation is not null) return ServiceResult<Opportunity>.Failure(validation);
        if (opportunity.Stage != input.Stage && (!Transitions.TryGetValue(opportunity.Stage, out var allowed) || !allowed.Contains(input.Stage)))
            return ServiceResult<Opportunity>.Failure($"Opportunity cannot move from {opportunity.Stage} to {input.Stage}.");
        if (canAssign && !string.IsNullOrWhiteSpace(input.AssignedToId)
            && !await CrmAccessScope.CanAssignToAsync(dbContext, userManager, userId, input.AssignedToId, canManageAll, cancellationToken))
            return ServiceResult<Opportunity>.Failure("Assign the opportunity to an active Sales Executive within your permitted scope.");
        if (!await RelatedRecordsExistAsync(input, userId, canManageAll, cancellationToken))
            return ServiceResult<Opportunity>.Failure("Select related records within your access scope.");

        var oldValue = $"{opportunity.OpportunityName}|{opportunity.Stage}|{opportunity.Amount}";
        opportunity.OpportunityName = input.OpportunityName.Trim();
        opportunity.CustomerId = input.CustomerId;
        opportunity.LeadId = input.LeadId;
        opportunity.Amount = input.Amount;
        opportunity.Probability = input.Probability;
        opportunity.ExpectedCloseDate = input.ExpectedCloseDate;
        opportunity.Stage = input.Stage;
        opportunity.Status = input.Stage is "Won" or "Lost" ? input.Stage : "Open";
        opportunity.ClosedDate = opportunity.Status is "Won" or "Lost" ? DateTime.UtcNow : null;
        opportunity.Notes = input.Notes?.Trim();
        if (canAssign && !string.IsNullOrWhiteSpace(input.AssignedToId)) opportunity.AssignedToId = input.AssignedToId;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(userId, "UpdateOpportunity", "Opportunities", nameof(Opportunity), id.ToString(), oldValue: oldValue,
            newValue: $"{opportunity.OpportunityName}|{opportunity.Stage}|{opportunity.Amount}", cancellationToken: cancellationToken);
        return ServiceResult<Opportunity>.Success(opportunity);
    }

    public async Task<ServiceResult<Opportunity>> DeactivateAsync(int id, string userId, CancellationToken cancellationToken = default)
    {
        var opportunity = await dbContext.Opportunities.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (opportunity is null) return ServiceResult<Opportunity>.Failure("Opportunity not found.");
        if (opportunity.Status != "Open") return ServiceResult<Opportunity>.Failure("Only open opportunities can be deactivated.");
        opportunity.Stage = "Lost";
        opportunity.Status = "Lost";
        opportunity.ClosedDate = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(userId, "DeactivateOpportunity", "Opportunities", nameof(Opportunity), id.ToString(), newValue: "Lost", cancellationToken: cancellationToken);
        return ServiceResult<Opportunity>.Success(opportunity);
    }

    private static string? Validate(Opportunity opportunity)
    {
        if (opportunity.Amount <= 0) return "Opportunity Amount must be greater than 0.";
        if (opportunity.Probability is < 0 or > 100) return "Probability must be between 0 and 100.";
        if (opportunity.Stage is not ("Won" or "Lost") && (!opportunity.ExpectedCloseDate.HasValue || opportunity.ExpectedCloseDate.Value.Date < DateTime.UtcNow.Date))
            return "Expected Close Date cannot be in the past.";
        return null;
    }

    private async Task<bool> RelatedRecordsExistAsync(Opportunity opportunity, string userId, bool canManageAll, CancellationToken cancellationToken)
    {
        var scope = await CrmAccessScope.UserIdsAsync(dbContext, userId, canManageAll, cancellationToken);
        if (opportunity.CustomerId is int customerId && !await dbContext.Customers.AnyAsync(x => x.Id == customerId && (canManageAll || (x.OwnerId != null && scope.Contains(x.OwnerId))), cancellationToken)) return false;
        if (opportunity.LeadId is int leadId && !await dbContext.Leads.AnyAsync(x => x.Id == leadId && (canManageAll || (x.AssignedToId != null && scope.Contains(x.AssignedToId))), cancellationToken)) return false;
        return true;
    }
}
