using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers;

public sealed class AccountController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IAuditService auditService,
    ILogger<AccountController> logger) : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user is not null && !user.IsActive)
        {
            var check = await signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);
            var action = check.IsLockedOut ? "Lockout" : "LoginFailed";
            await auditService.LogAsync(user.Id, action, "Authentication", result: "Failure", details: "Sign-in rejected for an inactive account.");
            ModelState.AddModelError(string.Empty, "Sign-in could not be completed. Check your details or contact an administrator.");
            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded && user is not null)
        {
            await auditService.LogAsync(user.Id, "LoginSucceeded", "Authentication");
            logger.LogInformation("User {UserId} signed in successfully.", user.Id);
            return LocalRedirect(Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl! : Url.Action("Index", "Dashboard")!);
        }

        var eventName = result.IsLockedOut ? "Lockout" : "LoginFailed";
        await auditService.LogAsync(user?.Id, eventName, "Authentication", result: "Failure", details: "Credentials were not accepted.");
        ModelState.AddModelError(string.Empty, "Sign-in could not be completed. Check your details or contact an administrator.");
        return View(model);
    }

    [Authorize]
    [HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        var result = await userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }
        await signInManager.RefreshSignInAsync(user);
        await auditService.LogAsync(user.Id, "PasswordChanged", "Authentication");
        TempData["Success"] = "Password changed successfully.";
        return RedirectToAction("Index", "Dashboard");
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var userId = userManager.GetUserId(User);
        await auditService.LogAsync(userId, "Logout", "Authentication");
        await signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied() => View();
}
