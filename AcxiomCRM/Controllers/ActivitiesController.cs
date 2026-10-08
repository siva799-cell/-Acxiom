using AcxiomCRM.Authorization;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers;

[Authorize]
public sealed class ActivitiesController(ActivityService activityService) : Controller
{
    public async Task<IActionResult> Index(string? type, string? status, int page = 1, CancellationToken cancellationToken = default)
    {
        ViewBag.Type = type;
        ViewBag.Status = status;
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
        var canReadAll = User.IsInRole(RoleNames.Admin);
        return View(await activityService.GetPageAsync(type, status, userId, canReadAll, page, 10, cancellationToken));
    }

    [HttpGet]
    public IActionResult Create() => View(new CrmActivity { ActivityDate = DateTime.UtcNow });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CrmActivity activity, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(activity);
        var result = await activityService.CreateAsync(activity, User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value, cancellationToken, User.IsInRole(RoleNames.Admin));
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(activity);
        }
        TempData["Success"] = "Activity recorded successfully.";
        return RedirectToAction(nameof(Index));
    }
}
