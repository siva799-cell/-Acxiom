using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthApiController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IAuditService auditService,
    IAntiforgery antiforgery) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("csrf")]
    public IActionResult GetCsrfToken()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new ApiResponse<object>(true, "Anti-forgery token issued.", new { requestToken = tokens.RequestToken }));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(ApiLoginDto dto)
    {
        var user = await userManager.FindByEmailAsync(dto.Email);
        if (user is null || !user.IsActive)
        {
            await auditService.LogAsync(user?.Id, "LoginFailed", "Authentication", result: "Failure", details: "API sign-in credentials were not accepted.");
            return Unauthorized(new ApiResponse<object>(false, "Sign-in could not be completed."));
        }

        var result = await signInManager.PasswordSignInAsync(dto.Email, dto.Password, dto.RememberMe, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            await auditService.LogAsync(user.Id, result.IsLockedOut ? "Lockout" : "LoginFailed", "Authentication", result: "Failure", details: "API sign-in credentials were not accepted.");
            return Unauthorized(new ApiResponse<object>(false, "Sign-in could not be completed."));
        }

        await auditService.LogAsync(user.Id, "LoginSucceeded", "Authentication");
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new ApiResponse<object>(true, "Signed in successfully.", new { user.Id, user.Email, user.Name, csrfToken = tokens.RequestToken }));
    }

    [Authorize]
    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await auditService.LogAsync(userManager.GetUserId(User), "Logout", "Authentication");
        await signInManager.SignOutAsync();
        return Ok(new ApiResponse<object>(true, "Signed out successfully."));
    }
}
