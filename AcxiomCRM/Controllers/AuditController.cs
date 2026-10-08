using AcxiomCRM.Authorization;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = RoleNames.Admin)]
public sealed class AuditController(ApplicationDbContext dbContext) : Controller
{
    public async Task<IActionResult> Index(string? userId, string? module, string? action, string? result,
        string? recordId, DateTime? from, DateTime? to, int page = 1, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        const int pageSize = 25;
        var query = dbContext.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(userId)) query = query.Where(x => x.UserId == userId);
        if (!string.IsNullOrWhiteSpace(module)) query = query.Where(x => x.Module == module);
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(x => x.Action.Contains(action));
        if (!string.IsNullOrWhiteSpace(result)) query = query.Where(x => x.Result == result);
        if (!string.IsNullOrWhiteSpace(recordId)) query = query.Where(x => x.RecordId == recordId);
        if (from.HasValue) query = query.Where(x => x.CreatedDate >= from.Value);
        if (to.HasValue) query = query.Where(x => x.CreatedDate < to.Value.Date.AddDays(1));
        var count = await query.CountAsync(cancellationToken);
        var records = await query.OrderByDescending(x => x.CreatedDate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        ViewBag.UserId = userId;
        ViewBag.Module = module;
        ViewBag.Action = action;
        ViewBag.Result = result;
        ViewBag.RecordId = recordId;
        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");
        return View(new PagedResult<AuditLog>(records, page, pageSize, count));
    }
}
