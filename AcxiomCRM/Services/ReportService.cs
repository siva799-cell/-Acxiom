using AcxiomCRM.Data;
using AcxiomCRM.Authorization;
using AcxiomCRM.Models;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public sealed record ReportResult(string Title, string[] Columns, IReadOnlyList<string[]> Rows, int Page, int PageSize, int TotalCount)
{
    public int PageCount => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

public sealed class ReportService(ApplicationDbContext dbContext)
{
    public async Task<ReportResult> BuildAsync(string report, string userId, bool canReadAll, string? search,
        string? status, string? ownerId, DateTime? from, DateTime? to, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var start = from?.Date;
        var end = to?.Date.AddDays(1);
        var term = search?.Trim();

        switch (report.ToLowerInvariant())
        {
            case "customers":
            {
                var query = dbContext.Customers.AsNoTracking();
                if (!canReadAll)
                {
                    var scope = await CrmAccessScope.UserIdsAsync(dbContext, userId, false, cancellationToken);
                    query = query.Where(x => x.OwnerId != null && scope.Contains(x.OwnerId));
                }
                if (!string.IsNullOrWhiteSpace(ownerId)) query = query.Where(x => x.OwnerId == ownerId);
                if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
                if (start.HasValue) query = query.Where(x => x.CreatedDate >= start);
                if (end.HasValue) query = query.Where(x => x.CreatedDate < end);
                if (!string.IsNullOrWhiteSpace(term)) query = query.Where(x => x.CustomerName.Contains(term) || (x.CompanyName != null && x.CompanyName.Contains(term)) || (x.Email != null && x.Email.Contains(term)));
                var total = await query.CountAsync(cancellationToken);
                var items = await query.OrderBy(x => x.CustomerName).Skip((page - 1) * pageSize).Take(pageSize)
                    .Select(x => new { x.CustomerName, x.Status, x.OwnerId, x.CreatedDate }).ToListAsync(cancellationToken);
                return Make("Customers", ["Customer", "Status", "Owner", "Created date"], items.Select(x => new[] { x.CustomerName, x.Status, x.OwnerId ?? "", x.CreatedDate.ToString("yyyy-MM-dd") }).ToArray(), page, pageSize, total);
            }
            case "leads":
            {
                var query = dbContext.Leads.AsNoTracking();
                if (!canReadAll)
                {
                    var scope = await CrmAccessScope.UserIdsAsync(dbContext, userId, false, cancellationToken);
                    query = query.Where(x => x.AssignedToId != null && scope.Contains(x.AssignedToId));
                }
                if (!string.IsNullOrWhiteSpace(ownerId)) query = query.Where(x => x.AssignedToId == ownerId);
                if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
                if (start.HasValue) query = query.Where(x => x.CreatedDate >= start);
                if (end.HasValue) query = query.Where(x => x.CreatedDate < end);
                if (!string.IsNullOrWhiteSpace(term)) query = query.Where(x => x.LeadName.Contains(term) || (x.CompanyName != null && x.CompanyName.Contains(term)) || (x.Source != null && x.Source.Contains(term)));
                var total = await query.CountAsync(cancellationToken);
                var items = await query.OrderByDescending(x => x.CreatedDate).Skip((page - 1) * pageSize).Take(pageSize)
                    .Select(x => new { x.LeadName, x.Source, x.Status, x.AssignedToId, Converted = x.Status == "Converted", x.CreatedDate }).ToListAsync(cancellationToken);
                return Make("Leads", ["Lead", "Source", "Status", "Owner", "Conversion", "Created date"], items.Select(x => new[] { x.LeadName, x.Source ?? "", x.Status, x.AssignedToId ?? "", x.Converted ? "Yes" : "No", x.CreatedDate.ToString("yyyy-MM-dd") }).ToArray(), page, pageSize, total);
            }
            case "followups":
            {
                var query = dbContext.FollowUps.AsNoTracking();
                if (!canReadAll)
                {
                    var scope = await CrmAccessScope.UserIdsAsync(dbContext, userId, false, cancellationToken);
                    query = query.Where(x => x.AssignedToId != null && scope.Contains(x.AssignedToId));
                }
                if (!string.IsNullOrWhiteSpace(ownerId)) query = query.Where(x => x.AssignedToId == ownerId);
                if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
                if (start.HasValue) query = query.Where(x => x.FollowUpDate >= start);
                if (end.HasValue) query = query.Where(x => x.FollowUpDate < end);
                if (!string.IsNullOrWhiteSpace(term)) query = query.Where(x => x.Subject.Contains(term));
                var total = await query.CountAsync(cancellationToken);
                var items = await query.OrderBy(x => x.FollowUpDate).Skip((page - 1) * pageSize).Take(pageSize)
                    .Select(x => new { x.FollowUpDate, x.Subject, x.Status, x.AssignedToId, x.CustomerId, x.LeadId }).ToListAsync(cancellationToken);
                return Make("Follow-ups", ["Date", "Subject", "Status", "Owner", "Customer/Lead"], items.Select(x => new[] { x.FollowUpDate.ToString("yyyy-MM-dd HH:mm"), x.Subject, x.Status, x.AssignedToId ?? "", x.CustomerId is not null ? $"Customer #{x.CustomerId}" : x.LeadId is not null ? $"Lead #{x.LeadId}" : "Opportunity" }).ToArray(), page, pageSize, total);
            }
            case "opportunities":
            {
                var query = dbContext.Opportunities.AsNoTracking();
                if (!canReadAll)
                {
                    var scope = await CrmAccessScope.UserIdsAsync(dbContext, userId, false, cancellationToken);
                    query = query.Where(x => x.AssignedToId != null && scope.Contains(x.AssignedToId));
                }
                if (!string.IsNullOrWhiteSpace(ownerId)) query = query.Where(x => x.AssignedToId == ownerId);
                if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Stage == status);
                if (start.HasValue) query = query.Where(x => x.CreatedDate >= start);
                if (end.HasValue) query = query.Where(x => x.CreatedDate < end);
                if (!string.IsNullOrWhiteSpace(term)) query = query.Where(x => x.OpportunityName.Contains(term));
                var total = await query.CountAsync(cancellationToken);
                var items = await query.OrderByDescending(x => x.CreatedDate).Skip((page - 1) * pageSize).Take(pageSize)
                    .Select(x => new { x.OpportunityName, x.Stage, x.Amount, x.Probability, x.ExpectedCloseDate, x.AssignedToId, x.Status }).ToListAsync(cancellationToken);
                return Make("Opportunities", ["Opportunity", "Stage", "Amount", "Probability", "Expected close", "Owner", "Status"], items.Select(x => new[] { x.OpportunityName, x.Stage, x.Amount.ToString("N2"), x.Probability.ToString(), x.ExpectedCloseDate?.ToString("yyyy-MM-dd") ?? "", x.AssignedToId ?? "", x.Status }).ToArray(), page, pageSize, total);
            }
            case "pipeline":
            {
                var query = dbContext.Opportunities.AsNoTracking().Where(x => x.Status == "Open");
                if (!canReadAll)
                {
                    var scope = await CrmAccessScope.UserIdsAsync(dbContext, userId, false, cancellationToken);
                    query = query.Where(x => x.AssignedToId != null && scope.Contains(x.AssignedToId));
                }
                if (start.HasValue) query = query.Where(x => x.CreatedDate >= start);
                if (end.HasValue) query = query.Where(x => x.CreatedDate < end);
                var items = await query.GroupBy(x => x.Stage).Select(x => new { Stage = x.Key, Count = x.Count(), Amount = x.Sum(o => o.Amount), Weighted = x.Sum(o => o.Amount * o.Probability / 100m) }).OrderBy(x => x.Stage).ToListAsync(cancellationToken);
                return Make("Pipeline", ["Stage", "Opportunity count", "Total amount", "Weighted pipeline"], items.Select(x => new[] { x.Stage, x.Count.ToString(), x.Amount.ToString("N2"), x.Weighted.ToString("N2") }).ToArray(), 1, Math.Max(1, items.Count), items.Count);
            }
            case "conversion":
            {
                var query = dbContext.Leads.AsNoTracking();
                if (!canReadAll) query = query.Where(x => x.AssignedToId == userId);
                if (start.HasValue) query = query.Where(x => x.CreatedDate >= start);
                if (end.HasValue) query = query.Where(x => x.CreatedDate < end);
                var total = await query.CountAsync(cancellationToken);
                var qualified = await query.CountAsync(x => x.Status == "Qualified" || x.Status == "Converted", cancellationToken);
                var converted = await query.CountAsync(x => x.Status == "Converted", cancellationToken);
                var lost = await query.CountAsync(x => x.Status == "Lost" || x.Status == "Unqualified", cancellationToken);
                var rate = total == 0 ? 0 : converted * 100m / total;
                return Make("Lead conversion", ["Total leads", "Qualified leads", "Converted leads", "Lost leads", "Conversion rate"], [[total.ToString(), qualified.ToString(), converted.ToString(), lost.ToString(), $"{rate:N1}%"]], 1, 1, 1);
            }
            case "useractivity":
            {
                var query = dbContext.AuditLogs.AsNoTracking();
                if (!canReadAll)
                {
                    var scope = await CrmAccessScope.UserIdsAsync(dbContext, userId, false, cancellationToken);
                    query = query.Where(x => x.UserId != null && scope.Contains(x.UserId));
                }
                if (!string.IsNullOrWhiteSpace(ownerId)) query = query.Where(x => x.UserId == ownerId);
                if (start.HasValue) query = query.Where(x => x.CreatedDate >= start);
                if (end.HasValue) query = query.Where(x => x.CreatedDate < end);
                var total = await query.Select(x => x.UserId).Distinct().CountAsync(cancellationToken);
                var items = await query.GroupBy(x => x.UserId).Select(x => new { UserId = x.Key, ActionCount = x.Count(), RecentAction = x.Max(a => a.CreatedDate) }).OrderByDescending(x => x.ActionCount).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
                return Make("User activity", ["User ID", "Action count", "Latest event"], items.Select(x => new[] { x.UserId ?? "", x.ActionCount.ToString(), x.RecentAction.ToString("yyyy-MM-dd HH:mm") }).ToArray(), page, pageSize, total);
            }
            case "audit":
            {
                var query = dbContext.AuditLogs.AsNoTracking();
                if (!string.IsNullOrWhiteSpace(ownerId)) query = query.Where(x => x.UserId == ownerId);
                if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Result == status);
                if (start.HasValue) query = query.Where(x => x.CreatedDate >= start);
                if (end.HasValue) query = query.Where(x => x.CreatedDate < end);
                if (!string.IsNullOrWhiteSpace(term)) query = query.Where(x => x.Action.Contains(term) || (x.Module != null && x.Module.Contains(term)));
                var total = await query.CountAsync(cancellationToken);
                var items = await query.OrderByDescending(x => x.CreatedDate).Skip((page - 1) * pageSize).Take(pageSize)
                    .Select(x => new { x.UserId, x.Action, x.Module, x.EntityName, x.RecordId, x.CreatedDate, x.Result }).ToListAsync(cancellationToken);
                return Make("Audit report", ["User", "Action", "Module", "Entity", "Record", "Timestamp", "Result"], items.Select(x => new[] { x.UserId ?? "", x.Action, x.Module ?? "", x.EntityName ?? "", x.RecordId ?? "", x.CreatedDate.ToString("yyyy-MM-dd HH:mm"), x.Result }).ToArray(), page, pageSize, total);
            }
            default:
                throw new ArgumentException("Unknown report.", nameof(report));
        }
    }

    private static ReportResult Make(string title, string[] columns, IReadOnlyList<string[]> rows, int page, int pageSize, int total) =>
        new(title, columns, rows, page, pageSize, total);
}
