using AcxiomCRM.Authorization;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize]
public sealed class OpportunitiesController(OpportunityService opportunityService, UserManager<ApplicationUser> userManager) : Controller
{
    public async Task<IActionResult> Index(string? search, string? stage, int page = 1, CancellationToken cancellationToken = default)
    {
        ViewBag.Search = search;
        ViewBag.Stage = stage;
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
        var canReadAll = User.IsInRole(RoleNames.Admin);
        return View(await opportunityService.GetPageAsync(search, stage, userId, canReadAll, page, 10, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadAssigneesAsync();
        return View(new Opportunity { ExpectedCloseDate = DateTime.UtcNow.Date.AddDays(30) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Opportunity opportunity, CancellationToken cancellationToken)
    {
        ModelState.Remove(nameof(Opportunity.CreatedDate));
        if (!ModelState.IsValid)
        {
            await LoadAssigneesAsync();
            return View(opportunity);
        }
        var canAssign = User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.Manager);
        var result = await opportunityService.CreateAsync(opportunity, User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value, cancellationToken, User.IsInRole(RoleNames.Admin), canAssign);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            await LoadAssigneesAsync();
            return View(opportunity);
        }
        TempData["Success"] = "Opportunity created successfully.";
        return RedirectToAction(nameof(Details), new { id = result.Data!.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var opportunity = await FindAsync(id, cancellationToken);
        if (opportunity is null) return NotFound();
        await LoadAssigneesAsync();
        return View(opportunity);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var opportunity = await FindAsync(id, cancellationToken);
        return opportunity is null ? NotFound() : View(opportunity);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Opportunity input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadAssigneesAsync();
            return View(input);
        }
        var canManageAll = User.IsInRole(RoleNames.Admin);
        var canAssign = User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.Manager);
        var result = await opportunityService.UpdateAsync(id, input, User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value, canManageAll, cancellationToken, canAssign);
        if (!result.Succeeded)
        {
            if (result.Error == "Opportunity not found.") return NotFound();
            ModelState.AddModelError(string.Empty, result.Error!);
            await LoadAssigneesAsync();
            return View(input);
        }
        TempData["Success"] = "Opportunity updated successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await opportunityService.DeactivateAsync(id, User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value, cancellationToken);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Opportunity closed as lost." : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    private Task<Opportunity?> FindAsync(int id, CancellationToken cancellationToken)
    {
        var canReadAll = User.IsInRole(RoleNames.Admin);
        return opportunityService.GetAsync(id, User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value, canReadAll, cancellationToken);
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
