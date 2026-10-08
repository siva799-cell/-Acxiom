using AcxiomCRM.Authorization;
using AcxiomCRM.Data;
using AcxiomCRM.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Api.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/reports")]
[Authorize]
public sealed class ReportsApiController(ApplicationDbContext dbContext) : ControllerBase
{
    [HttpGet("lead-status")]
    public async Task<IActionResult> LeadStatus(DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
        var leads = dbContext.Leads.AsNoTracking();
        if (from.HasValue) leads = leads.Where(x => x.CreatedDate >= from.Value.Date);
        if (to.HasValue) leads = leads.Where(x => x.CreatedDate < to.Value.Date.AddDays(1));
        if (!User.IsInRole(RoleNames.Admin))
        {
            var scope = await CrmAccessScope.UserIdsAsync(dbContext, userId, false, cancellationToken);
            leads = leads.Where(x => x.AssignedToId != null && scope.Contains(x.AssignedToId));
        }
        var data = await leads.GroupBy(x => x.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .OrderBy(x => x.Status).ToListAsync(cancellationToken);
        return Ok(new ApiResponse<object>(true, "Lead status report retrieved.", data));
    }

    [HttpGet("pipeline")]
    public async Task<IActionResult> Pipeline(DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
        var opportunities = dbContext.Opportunities.AsNoTracking();
        if (from.HasValue) opportunities = opportunities.Where(x => x.CreatedDate >= from.Value.Date);
        if (to.HasValue) opportunities = opportunities.Where(x => x.CreatedDate < to.Value.Date.AddDays(1));
        if (!User.IsInRole(RoleNames.Admin))
        {
            var scope = await CrmAccessScope.UserIdsAsync(dbContext, userId, false, cancellationToken);
            opportunities = opportunities.Where(x => x.AssignedToId != null && scope.Contains(x.AssignedToId));
        }
        var data = await opportunities.GroupBy(x => x.Stage).Select(group => new
        {
            Stage = group.Key,
            OpportunityCount = group.Count(),
            TotalAmount = group.Sum(x => x.Amount),
            WeightedPipeline = group.Sum(x => x.Amount * x.Probability / 100m)
        }).OrderBy(x => x.Stage).ToListAsync(cancellationToken);
        return Ok(new ApiResponse<object>(true, "Pipeline report retrieved.", data));
    }

    [HttpGet("monthly-sales")]
    public async Task<IActionResult> MonthlySales(int months = 6, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        months = Math.Clamp(months, 1, 12);
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
        var currentMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var start = from.HasValue ? new DateTime(from.Value.Year, from.Value.Month, 1) : currentMonth.AddMonths(1 - months);
        var endMonth = to.HasValue ? new DateTime(to.Value.Year, to.Value.Month, 1) : currentMonth;
        if ((endMonth.Year - start.Year) * 12 + endMonth.Month - start.Month > 11) start = endMonth.AddMonths(-11);
        var opportunities = dbContext.Opportunities.AsNoTracking()
            .Where(x => x.Status == "Won" && x.ClosedDate >= start && x.ClosedDate < endMonth.AddMonths(1));
        if (!User.IsInRole(RoleNames.Admin))
        {
            var scope = await CrmAccessScope.UserIdsAsync(dbContext, userId, false, cancellationToken);
            opportunities = opportunities.Where(x => x.AssignedToId != null && scope.Contains(x.AssignedToId));
        }
        var totals = await opportunities.GroupBy(x => new { x.ClosedDate!.Value.Year, x.ClosedDate!.Value.Month })
            .Select(group => new { group.Key.Year, group.Key.Month, Amount = group.Sum(x => x.Amount), Count = group.Count() })
            .ToListAsync(cancellationToken);
        var monthCount = Math.Clamp((endMonth.Year - start.Year) * 12 + endMonth.Month - start.Month + 1, 1, 12);
        var data = Enumerable.Range(0, monthCount).Select(offset => start.AddMonths(offset)).Select(month =>
        {
            var total = totals.FirstOrDefault(x => x.Year == month.Year && x.Month == month.Month);
            return new { Month = month.ToString("yyyy-MM"), WonAmount = total?.Amount ?? 0m, WonCount = total?.Count ?? 0 };
        }).ToArray();
        return Ok(new ApiResponse<object>(true, "Monthly sales report retrieved.", data));
    }
}
