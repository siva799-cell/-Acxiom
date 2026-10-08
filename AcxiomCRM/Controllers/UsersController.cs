using AcxiomCRM.Authorization;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = RoleNames.Admin)]
public sealed class UsersController(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IAuditService auditService) : Controller
{
    public async Task<IActionResult> Index(string? search, int page = 1, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        const int pageSize = 25;
        var query = userManager.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.Name.Contains(term) || (x.Email != null && x.Email.Contains(term)));
        }
        var count = await query.CountAsync(cancellationToken);
        var users = await query.OrderBy(x => x.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var managers = await userManager.GetUsersInRoleAsync(RoleNames.Manager);
        var managerNames = managers.ToDictionary(x => x.Id, x => x.Name);
        var rows = new List<UserListItemViewModel>();
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            rows.Add(new UserListItemViewModel
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email ?? string.Empty,
                Role = roles.FirstOrDefault() ?? "Unassigned",
                ManagerName = user.ManagerId is not null && managerNames.TryGetValue(user.ManagerId, out var managerName) ? managerName : "—",
                IsActive = user.IsActive,
                LockoutEnd = user.LockoutEnd,
                FailedAccessCount = user.AccessFailedCount
            });
        }
        ViewBag.Search = search;
        return View(new PagedResult<UserListItemViewModel>(rows, page, pageSize, count));
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadManagerOptionsAsync();
        return View(new UserFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserFormViewModel model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.Password)) ModelState.AddModelError(nameof(model.Password), "A temporary password is required.");
        if (!RoleNames.All.Contains(model.Role, StringComparer.Ordinal)) ModelState.AddModelError(nameof(model.Role), "Choose one of the supported roles.");
        await ValidateManagerSelectionAsync(model);
        if (!ModelState.IsValid)
        {
            await LoadManagerOptionsAsync();
            return View(model);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = new ApplicationUser
        {
            UserName = model.Email.Trim(),
            Email = model.Email.Trim(),
            EmailConfirmed = true,
            Name = model.Name.Trim(),
            IsActive = model.IsActive,
            CreatedDate = DateTime.UtcNow,
            ManagerId = model.Role == RoleNames.SalesExecutive ? model.ManagerId : null
        };
        var createResult = await userManager.CreateAsync(user, model.Password!);
        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors) ModelState.AddModelError(string.Empty, error.Description);
            await LoadManagerOptionsAsync();
            return View(model);
        }
        var roleResult = await userManager.AddToRoleAsync(user, model.Role);
        if (!roleResult.Succeeded)
        {
            foreach (var error in roleResult.Errors) ModelState.AddModelError(string.Empty, error.Description);
            await LoadManagerOptionsAsync();
            return View(model);
        }
        await auditService.LogAsync(UserId(), "CreateUser", "Users", nameof(ApplicationUser), user.Id, newValue: $"{user.Email}|{model.Role}", cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        TempData["Success"] = "User created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();
        var roles = await userManager.GetRolesAsync(user);
        await LoadManagerOptionsAsync();
        return View(new UserFormViewModel { Id = user.Id, Name = user.Name, Email = user.Email ?? string.Empty, Role = roles.FirstOrDefault() ?? RoleNames.SalesExecutive, IsActive = user.IsActive, ManagerId = user.ManagerId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, UserFormViewModel model, CancellationToken cancellationToken)
    {
        if (!RoleNames.All.Contains(model.Role, StringComparer.Ordinal)) ModelState.AddModelError(nameof(model.Role), "Choose one of the supported roles.");
        await ValidateManagerSelectionAsync(model);
        if (!ModelState.IsValid)
        {
            await LoadManagerOptionsAsync();
            return View(model);
        }
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();
        var currentRoles = await userManager.GetRolesAsync(user);
        if (user.Id == UserId() && (!model.IsActive || !currentRoles.Contains(model.Role)))
        {
            ModelState.AddModelError(string.Empty, "You cannot deactivate yourself or remove your own current role.");
            await LoadManagerOptionsAsync();
            return View(model);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var oldRole = currentRoles.FirstOrDefault();
        var oldActive = user.IsActive;
        var oldManagerId = user.ManagerId;
        user.Name = model.Name.Trim();
        user.Email = model.Email.Trim();
        user.UserName = model.Email.Trim();
        user.IsActive = model.IsActive;
        user.ManagerId = model.Role == RoleNames.SalesExecutive ? model.ManagerId : null;
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            foreach (var error in updateResult.Errors) ModelState.AddModelError(string.Empty, error.Description);
            await LoadManagerOptionsAsync();
            return View(model);
        }
        if (oldRole != model.Role)
        {
            if (currentRoles.Count > 0) await userManager.RemoveFromRolesAsync(user, currentRoles);
            var addRole = await userManager.AddToRoleAsync(user, model.Role);
            if (!addRole.Succeeded)
            {
                foreach (var error in addRole.Errors) ModelState.AddModelError(string.Empty, error.Description);
                await LoadManagerOptionsAsync();
                return View(model);
            }
            await auditService.LogAsync(UserId(), "RoleChange", "Users", nameof(ApplicationUser), id, oldValue: oldRole, newValue: model.Role, cancellationToken: cancellationToken);
        }
        if (oldActive != user.IsActive)
            await auditService.LogAsync(UserId(), user.IsActive ? "UserActivated" : "UserDeactivated", "Users", nameof(ApplicationUser), id, cancellationToken: cancellationToken);
        if (oldManagerId != user.ManagerId)
            await auditService.LogAsync(UserId(), "TeamAssignment", "Users", nameof(ApplicationUser), id, oldValue: oldManagerId, newValue: user.ManagerId, cancellationToken: cancellationToken);
        if (model.IsActive) await userManager.UpdateSecurityStampAsync(user);
        if (!string.IsNullOrWhiteSpace(model.Password))
        {
            var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
            var resetResult = await userManager.ResetPasswordAsync(user, resetToken, model.Password);
            if (!resetResult.Succeeded)
            {
                foreach (var error in resetResult.Errors) ModelState.AddModelError(nameof(model.Password), error.Description);
                await LoadManagerOptionsAsync();
                return View(model);
            }
            await auditService.LogAsync(UserId(), "PasswordReset", "Users", nameof(ApplicationUser), id, cancellationToken: cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        TempData["Success"] = "User updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlock(string id, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound();
        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);
        await auditService.LogAsync(UserId(), "UnlockUser", "Users", nameof(ApplicationUser), id, cancellationToken: cancellationToken);
        TempData["Success"] = "Account unlocked.";
        return RedirectToAction(nameof(Index));
    }

    private string UserId() => userManager.GetUserId(User)!;

    private async Task ValidateManagerSelectionAsync(UserFormViewModel model)
    {
        if (model.Role != RoleNames.SalesExecutive && !string.IsNullOrWhiteSpace(model.ManagerId))
        {
            ModelState.AddModelError(nameof(model.ManagerId), "Only Sales Executives can be assigned to a Manager.");
            return;
        }
        if (string.IsNullOrWhiteSpace(model.ManagerId)) return;
        var manager = await userManager.FindByIdAsync(model.ManagerId);
        if (manager is null || !manager.IsActive || !await userManager.IsInRoleAsync(manager, RoleNames.Manager))
            ModelState.AddModelError(nameof(model.ManagerId), "Choose an active Manager account.");
    }

    private async Task LoadManagerOptionsAsync() => ViewBag.Managers = (await userManager.GetUsersInRoleAsync(RoleNames.Manager))
        .Where(x => x.IsActive).OrderBy(x => x.Name).ToArray();
}
