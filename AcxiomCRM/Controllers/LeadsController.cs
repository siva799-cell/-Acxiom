using AcxiomCRM.Authorization;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize]
public sealed class LeadsController(LeadService leadService, UserManager<ApplicationUser> userManager) : Controller
{
    public async Task<IActionResult> Index(string? search, string? status, int page = 1, CancellationToken cancellationToken = default)
    {
        ViewBag.Search = search;
        ViewBag.Status = status;
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
        var canReadAll = User.IsInRole(RoleNames.Admin);
        return View(await leadService.GetPageAsync(search, status, userId, canReadAll, page, 10, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadAssigneesAsync();
        return View(new Lead());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Lead lead, CancellationToken cancellationToken)
    {
        ModelState.Remove(nameof(Lead.LeadCode));
        if (!ModelState.IsValid)
        {
            await LoadAssigneesAsync();
            return View(lead);
        }
        var canAssign = User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.Manager);
        var result = await leadService.CreateAsync(lead, User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value, cancellationToken, canAssign, User.IsInRole(RoleNames.Admin));
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            await LoadAssigneesAsync();
            return View(lead);
        }
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Lead created successfully." : result.Error;
        return result.Succeeded ? RedirectToAction(nameof(Details), new { id = result.Data!.Id }) : View(lead);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var lead = await FindAsync(id, cancellationToken);
        if (lead is null) return NotFound();
        await LoadAssigneesAsync();
        return View(lead);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var lead = await FindAsync(id, cancellationToken);
        return lead is null ? NotFound() : View(lead);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Lead input, CancellationToken cancellationToken)
    {
        ModelState.Remove(nameof(Lead.LeadCode));
        if (!ModelState.IsValid)
        {
            await LoadAssigneesAsync();
            return View(input);
        }
        var canManageAll = User.IsInRole(RoleNames.Admin);
        var canAssign = User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.Manager);
        var result = await leadService.UpdateAsync(id, input, User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value, canManageAll, cancellationToken, canAssign);
        if (!result.Succeeded)
        {
            if (result.Error == "Lead not found.") return NotFound();
            ModelState.AddModelError(nameof(Lead.Status), result.Error!);
            await LoadAssigneesAsync();
            return View(input);
        }
        TempData["Success"] = "Lead updated successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Convert(int id, CancellationToken cancellationToken)
    {
        var canManageAll = User.IsInRole(RoleNames.Admin);
        var result = await leadService.ConvertAsync(id, User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value, canManageAll, cancellationToken);
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Details), new { id });
        }
        TempData["Success"] = "Lead converted to a customer and opportunity.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await leadService.DeactivateAsync(id, User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value, cancellationToken);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Lead closed as lost." : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    private Task<Lead?> FindAsync(int id, CancellationToken cancellationToken)
    {
        var canReadAll = User.IsInRole(RoleNames.Admin);
        return leadService.GetAsync(id, User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value, canReadAll, cancellationToken);
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
