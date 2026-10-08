using System.Text;
using AcxiomCRM.Authorization;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers;

[Authorize]
public sealed class ReportsController(ReportService reportService) : Controller
{
    public IActionResult Index() => View();

    public Task<IActionResult> Customers(string? search, string? status, string? ownerId, DateTime? from, DateTime? to, int page = 1, string? format = null, CancellationToken cancellationToken = default) =>
        ShowAsync("customers", search, status, ownerId, from, to, page, format, cancellationToken);

    public Task<IActionResult> Leads(string? search, string? status, string? ownerId, DateTime? from, DateTime? to, int page = 1, string? format = null, CancellationToken cancellationToken = default) =>
        ShowAsync("leads", search, status, ownerId, from, to, page, format, cancellationToken);

    public Task<IActionResult> FollowUps(string? search, string? status, string? ownerId, DateTime? from, DateTime? to, int page = 1, string? format = null, CancellationToken cancellationToken = default) =>
        ShowAsync("followups", search, status, ownerId, from, to, page, format, cancellationToken);

    public Task<IActionResult> Opportunities(string? search, string? status, string? ownerId, DateTime? from, DateTime? to, int page = 1, string? format = null, CancellationToken cancellationToken = default) =>
        ShowAsync("opportunities", search, status, ownerId, from, to, page, format, cancellationToken);

    public Task<IActionResult> Pipeline(string? search, string? status, string? ownerId, DateTime? from, DateTime? to, int page = 1, string? format = null, CancellationToken cancellationToken = default) =>
        ShowAsync("pipeline", search, status, ownerId, from, to, page, format, cancellationToken);

    public Task<IActionResult> Conversion(string? search, string? status, string? ownerId, DateTime? from, DateTime? to, int page = 1, string? format = null, CancellationToken cancellationToken = default) =>
        ShowAsync("conversion", search, status, ownerId, from, to, page, format, cancellationToken);

    public Task<IActionResult> UserActivity(string? search, string? status, string? ownerId, DateTime? from, DateTime? to, int page = 1, string? format = null, CancellationToken cancellationToken = default) =>
        ShowAsync("useractivity", search, status, ownerId, from, to, page, format, cancellationToken);

    [Authorize(Roles = RoleNames.Admin)]
    public Task<IActionResult> Audit(string? search, string? status, string? ownerId, DateTime? from, DateTime? to, int page = 1, string? format = null, CancellationToken cancellationToken = default) =>
        ShowAsync("audit", search, status, ownerId, from, to, page, format, cancellationToken);

    private async Task<IActionResult> ShowAsync(string report, string? search, string? status, string? ownerId,
        DateTime? from, DateTime? to, int page, string? format, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
        var canReadAll = User.IsInRole(RoleNames.Admin);
        var result = await reportService.BuildAsync(report, userId, canReadAll, search, status, ownerId, from, to, page, 25, cancellationToken);
        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var csv = new StringBuilder();
            csv.AppendLine(string.Join(',', result.Columns.Select(EscapeCsv)));
            foreach (var row in result.Rows) csv.AppendLine(string.Join(',', row.Select(EscapeCsv)));
            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"acxiomcrm-{report}.csv");
        }
        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.OwnerId = ownerId;
        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");
        ViewBag.ActionName = ControllerContext.RouteData.Values["action"]?.ToString();
        return View("Table", result);
    }

    private static string EscapeCsv(string value)
    {
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t') value = "'" + value;
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
