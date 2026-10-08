using AcxiomCRM.Data;
using AcxiomCRM.Authorization;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public sealed class FollowUpService(ApplicationDbContext dbContext, IAuditService auditService, UserManager<ApplicationUser> userManager)
{
    public async Task<PagedResult<FollowUp>> GetPageAsync(string? status, string userId, bool canReadAll, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = dbContext.FollowUps.AsNoTracking();
        if (!canReadAll)
        {
            var scope = await CrmAccessScope.UserIdsAsync(dbContext, userId, false, cancellationToken);
            query = query.Where(x => x.AssignedToId != null && scope.Contains(x.AssignedToId));
        }
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.FollowUpDate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<FollowUp>(items, page, pageSize, total);
    }

    public async Task<ServiceResult<FollowUp>> CreateAsync(FollowUp input, string userId, CancellationToken cancellationToken = default, bool canManageAll = false, bool canAssign = false)
    {
        if (canAssign && !string.IsNullOrWhiteSpace(input.AssignedToId)
            && !await CrmAccessScope.CanAssignToAsync(dbContext, userManager, userId, input.AssignedToId, canManageAll, cancellationToken))
            return ServiceResult<FollowUp>.Failure("Assign the follow-up to an active Sales Executive within your permitted scope.");
        var error = await ValidateAsync(input, userId, canManageAll, cancellationToken);
        if (error is not null) return ServiceResult<FollowUp>.Failure(error);
        input.Id = 0;
        input.Status = "Planned";
        if (!canAssign || string.IsNullOrWhiteSpace(input.AssignedToId)) input.AssignedToId = userId;
        input.CreatedDate = DateTime.UtcNow;
        dbContext.FollowUps.Add(input);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(userId, "CreateFollowUp", "FollowUps", nameof(FollowUp), input.Id.ToString(), newValue: input.Subject, cancellationToken: cancellationToken);
        return ServiceResult<FollowUp>.Success(input);
    }

    public async Task<ServiceResult<FollowUp>> RescheduleAsync(int id, DateTime date, string userId, bool canManageAll, CancellationToken cancellationToken = default)
    {
        var followUp = await GetEditableAsync(id, userId, canManageAll, cancellationToken);
        if (followUp is null) return ServiceResult<FollowUp>.Failure("Follow-up not found.");
        if (followUp.Status != "Planned") return ServiceResult<FollowUp>.Failure("Only planned follow-ups can be rescheduled.");
        if (date.Date < DateTime.UtcNow.Date) return ServiceResult<FollowUp>.Failure("Follow-up date cannot be earlier than today.");
        var oldDate = followUp.FollowUpDate.ToString("O");
        followUp.FollowUpDate = date;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(userId, "RescheduleFollowUp", "FollowUps", nameof(FollowUp), id.ToString(), oldValue: oldDate, newValue: date.ToString("O"), cancellationToken: cancellationToken);
        return ServiceResult<FollowUp>.Success(followUp);
    }

    public async Task<ServiceResult<FollowUp>> SetStatusAsync(int id, string status, string userId, bool canManageAll, CancellationToken cancellationToken = default)
    {
        if (status is not ("Completed" or "Cancelled" or "Missed")) return ServiceResult<FollowUp>.Failure("Invalid follow-up status.");
        var followUp = await GetEditableAsync(id, userId, canManageAll, cancellationToken);
        if (followUp is null) return ServiceResult<FollowUp>.Failure("Follow-up not found.");
        if (followUp.Status != "Planned") return ServiceResult<FollowUp>.Failure("Only planned follow-ups can be updated.");
        followUp.Status = status;
        followUp.CompletedDate = status == "Completed" ? DateTime.UtcNow : null;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(userId, status == "Completed" ? "CompleteFollowUp" : "CancelFollowUp", "FollowUps", nameof(FollowUp), id.ToString(), cancellationToken: cancellationToken);
        return ServiceResult<FollowUp>.Success(followUp);
    }

    private async Task<string?> ValidateAsync(FollowUp followUp, string userId, bool canManageAll, CancellationToken cancellationToken)
    {
        if (followUp.FollowUpType is not ("Call" or "Meeting" or "Email" or "Task")) return "Choose a valid follow-up type.";
        if (followUp.FollowUpDate.Date < DateTime.UtcNow.Date) return "Follow-up date cannot be earlier than today.";
        var targets = (followUp.CustomerId.HasValue ? 1 : 0) + (followUp.LeadId.HasValue ? 1 : 0) + (followUp.OpportunityId.HasValue ? 1 : 0);
        if (targets != 1) return "Choose exactly one related customer, lead, or opportunity.";
        var scope = await CrmAccessScope.UserIdsAsync(dbContext, userId, canManageAll, cancellationToken);
        if (followUp.CustomerId is int customerId && !await dbContext.Customers.AnyAsync(x => x.Id == customerId && (canManageAll || (x.OwnerId != null && scope.Contains(x.OwnerId))), cancellationToken)) return "Related customer was not found in your access scope.";
        if (followUp.LeadId is int leadId && !await dbContext.Leads.AnyAsync(x => x.Id == leadId && (canManageAll || (x.AssignedToId != null && scope.Contains(x.AssignedToId))), cancellationToken)) return "Related lead was not found in your access scope.";
        if (followUp.OpportunityId is int opportunityId && !await dbContext.Opportunities.AnyAsync(x => x.Id == opportunityId && (canManageAll || (x.AssignedToId != null && scope.Contains(x.AssignedToId))), cancellationToken)) return "Related opportunity was not found in your access scope.";
        return null;
    }

    private async Task<FollowUp?> GetEditableAsync(int id, string userId, bool canManageAll, CancellationToken cancellationToken)
    {
        var query = dbContext.FollowUps.AsQueryable();
        if (!canManageAll)
        {
            var scope = await CrmAccessScope.UserIdsAsync(dbContext, userId, false, cancellationToken);
            query = query.Where(x => x.AssignedToId != null && scope.Contains(x.AssignedToId));
        }
        return await query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }
}
