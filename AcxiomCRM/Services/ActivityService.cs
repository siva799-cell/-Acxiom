using AcxiomCRM.Data;
using AcxiomCRM.Authorization;
using AcxiomCRM.Models;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public sealed class ActivityService(ApplicationDbContext dbContext, IAuditService auditService)
{
    public async Task<PagedResult<CrmActivity>> GetPageAsync(string? type, string? status, string userId, bool canReadAll,
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = dbContext.Activities.AsNoTracking();
        if (!canReadAll)
        {
            var scope = await CrmAccessScope.UserIdsAsync(dbContext, userId, false, cancellationToken);
            query = query.Where(x => x.AssignedToId != null && scope.Contains(x.AssignedToId));
        }
        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(x => x.ActivityType == type);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.ActivityDate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<CrmActivity>(items, page, pageSize, total);
    }

    public async Task<ServiceResult<CrmActivity>> CreateAsync(CrmActivity activity, string userId, CancellationToken cancellationToken = default, bool canManageAll = false)
    {
        if (activity.ActivityType is not ("Call" or "Meeting" or "Email" or "Task")) return ServiceResult<CrmActivity>.Failure("Choose a valid activity type.");
        var targetCount = (activity.CustomerId.HasValue ? 1 : 0) + (activity.LeadId.HasValue ? 1 : 0) + (activity.OpportunityId.HasValue ? 1 : 0);
        if (targetCount > 1) return ServiceResult<CrmActivity>.Failure("An activity can be linked to only one customer, lead, or opportunity.");
        var scope = await CrmAccessScope.UserIdsAsync(dbContext, userId, canManageAll, cancellationToken);
        if (activity.CustomerId is int customerId && !await dbContext.Customers.AnyAsync(x => x.Id == customerId && (canManageAll || (x.OwnerId != null && scope.Contains(x.OwnerId))), cancellationToken)) return ServiceResult<CrmActivity>.Failure("Related customer is outside your access scope.");
        if (activity.LeadId is int leadId && !await dbContext.Leads.AnyAsync(x => x.Id == leadId && (canManageAll || (x.AssignedToId != null && scope.Contains(x.AssignedToId))), cancellationToken)) return ServiceResult<CrmActivity>.Failure("Related lead is outside your access scope.");
        if (activity.OpportunityId is int opportunityId && !await dbContext.Opportunities.AnyAsync(x => x.Id == opportunityId && (canManageAll || (x.AssignedToId != null && scope.Contains(x.AssignedToId))), cancellationToken)) return ServiceResult<CrmActivity>.Failure("Related opportunity is outside your access scope.");
        activity.Id = 0;
        activity.CreatedById = userId;
        activity.AssignedToId ??= userId;
        activity.CreatedDate = DateTime.UtcNow;
        dbContext.Activities.Add(activity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(userId, "CreateActivity", "Activities", nameof(CrmActivity), activity.Id.ToString(), newValue: activity.Subject, cancellationToken: cancellationToken);
        return ServiceResult<CrmActivity>.Success(activity);
    }
}
