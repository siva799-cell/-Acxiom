using AcxiomCRM.Authorization;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize]
public sealed class FollowUpsController(FollowUpService followUpService, UserManager<ApplicationUser> userManager) : Controller
{
    public async Task<IActionResult> Index(string? status, int page = 1, CancellationToken cancellationToken = default)
    {
        ViewBag.Status = status;
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
        var canReadAll = User.IsInRole(RoleNames.Admin);
        return View(await followUpService.GetPageAsync(status, userId, canReadAll, page, 10, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadAssigneesAsync();
        return View(new FollowUp { FollowUpDate = DateTime.UtcNow.Date.AddDays(1).AddHours(9) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FollowUp followUp, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadAssigneesAsync();
            return View(followUp);
        }
        var canAssign = User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.Manager);
        var result = await followUpService.CreateAsync(followUp, User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value, cancellationToken, User.IsInRole(RoleNames.Admin), canAssign);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            await LoadAssigneesAsync();
            return View(followUp);
        }
        TempData["Success"] = "Follow-up scheduled successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id, CancellationToken cancellationToken) => await SetStatusAsync(id, "Completed", cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken) => await SetStatusAsync(id, "Cancelled", cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reschedule(int id, DateTime followUpDate, CancellationToken cancellationToken)
    {
        var result = await followUpService.RescheduleAsync(id, followUpDate, User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value,
            User.IsInRole(RoleNames.Admin), cancellationToken);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Follow-up rescheduled." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> SetStatusAsync(int id, string status, CancellationToken cancellationToken)
    {
        var result = await followUpService.SetStatusAsync(id, status, User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value,
            User.IsInRole(RoleNames.Admin), cancellationToken);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? $"Follow-up {status.ToLowerInvariant()}." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadAssigneesAsync()
    {
        ViewData["CanAssign"] = User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.Manager);
        if (User.IsInRole(RoleNames.Admin))
        {
            ViewBag.Assignees = (await userManager.GetUsersInRoleAsync(RoleNames.SalesExecutive)).Where(x => x.IsActive).OrderBy(x => x.Name).ToArray();
            return;
        }
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
        ViewBag.Assignees = await userManager.Users.Where(x => x.ManagerId == userId && x.IsActive).OrderBy(x => x.Name).ToArrayAsync();
    }
}
