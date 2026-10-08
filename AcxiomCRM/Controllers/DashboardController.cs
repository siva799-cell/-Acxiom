using AcxiomCRM.Authorization;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize]
public sealed class DashboardController(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager) : Controller
{
    public async Task<IActionResult> Index(string? range, DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null || !user.IsActive)
        {
            await signInManager.SignOutAsync();
            return Challenge();
        }

        var roles = await userManager.GetRolesAsync(user);
        var isAdmin = roles.Contains(RoleNames.Admin);
        var customers = dbContext.Customers.AsNoTracking();
        var leads = dbContext.Leads.AsNoTracking();
        var opportunities = dbContext.Opportunities.AsNoTracking();
        var followUps = dbContext.FollowUps.AsNoTracking();
        if (!isAdmin)
        {
            var scope = await CrmAccessScope.UserIdsAsync(dbContext, user.Id, false, cancellationToken);
            customers = customers.Where(x => x.OwnerId != null && scope.Contains(x.OwnerId));
            leads = leads.Where(x => x.AssignedToId != null && scope.Contains(x.AssignedToId));
            opportunities = opportunities.Where(x => x.AssignedToId != null && scope.Contains(x.AssignedToId));
            followUps = followUps.Where(x => x.AssignedToId != null && scope.Contains(x.AssignedToId));
        }

        var now = DateTime.UtcNow;
        DateTime? start = null;
        DateTime? end = null;
        if (range == "today")
        {
            start = now.Date;
            end = now.Date.AddDays(1);
        }
        else if (range == "week")
        {
            start = now.Date.AddDays(-(((int)now.DayOfWeek + 6) % 7));
            end = start.Value.AddDays(7);
        }
        else if (range == "month")
        {
            start = new DateTime(now.Year, now.Month, 1);
            end = start.Value.AddMonths(1);
        }
        else if (range == "custom" && from.HasValue && to.HasValue && from.Value.Date <= to.Value.Date)
        {
            start = from.Value.Date;
            end = to.Value.Date.AddDays(1);
        }
        if (start.HasValue)
        {
            customers = customers.Where(x => x.CreatedDate >= start && x.CreatedDate < end);
            leads = leads.Where(x => x.CreatedDate >= start && x.CreatedDate < end);
            opportunities = opportunities.Where(x => x.CreatedDate >= start && x.CreatedDate < end);
            followUps = followUps.Where(x => x.FollowUpDate >= start && x.FollowUpDate < end);
        }
        ViewBag.Range = range;
        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");
        ViewBag.ChartFrom = start?.ToString("yyyy-MM-dd");
        ViewBag.ChartTo = end?.AddDays(-1).ToString("yyyy-MM-dd");
        var model = new DashboardViewModel
        {
            Name = user.Name,
            Email = user.Email ?? string.Empty,
            Role = roles.FirstOrDefault() ?? "User",
            TotalCustomers = await customers.CountAsync(cancellationToken),
            TotalLeads = await leads.CountAsync(cancellationToken),
            OpenLeads = await leads.CountAsync(x => x.Status != "Converted" && x.Status != "Lost" && x.Status != "Unqualified", cancellationToken),
            TotalOpportunities = await opportunities.CountAsync(cancellationToken),
            OpenOpportunities = await opportunities.CountAsync(x => x.Status == "Open", cancellationToken),
            WonOpportunities = await opportunities.CountAsync(x => x.Status == "Won" || x.Stage == "Won", cancellationToken),
            LostOpportunities = await opportunities.CountAsync(x => x.Status == "Lost" || x.Stage == "Lost", cancellationToken),
            PipelineValue = await opportunities.Where(x => x.Status == "Open").SumAsync(x => x.Amount * x.Probability / 100m, cancellationToken),
            UpcomingFollowUps = await followUps.CountAsync(x => x.Status == "Planned" && x.FollowUpDate >= now, cancellationToken),
            OverdueFollowUps = await followUps.CountAsync(x => x.Status == "Planned" && x.FollowUpDate < now, cancellationToken)
        };
        return View(model);
    }
}
