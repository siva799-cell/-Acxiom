using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Diagnostics;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;

namespace AcxiomCRM.Controllers;

public class HomeController(ILogger<HomeController> logger) : Controller
{
    public IActionResult Index()
    {
        return User.Identity?.IsAuthenticated == true
            ? RedirectToAction("Index", "Dashboard")
            : RedirectToAction("Login", "Account");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        var error = HttpContext.Features.Get<IExceptionHandlerPathFeature>()?.Error;
        if (error is not null) logger.LogError(error, "An unhandled application exception occurred.");
        if (HttpContext.Features.Get<IExceptionHandlerPathFeature>()?.Path?.StartsWith("/api", StringComparison.OrdinalIgnoreCase) == true)
            return StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<object>(false, "An unexpected error occurred."));
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [AllowAnonymous]
    public IActionResult StatusError(int code)
    {
        Response.StatusCode = code;
        ViewData["StatusCode"] = code;
        return View("StatusCode");
    }
}
