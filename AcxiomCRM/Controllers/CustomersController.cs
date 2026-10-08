using AcxiomCRM.Authorization;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers;

[Authorize]
public sealed class CustomersController(CustomerService customerService) : Controller
{
    public async Task<IActionResult> Index(string? search, string? status, int page = 1, CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
        var canReadAll = User.IsInRole(RoleNames.Admin);
        ViewBag.Search = search;
        ViewBag.Status = status;
        var result = await customerService.GetPageAsync(search, status, null, userId, canReadAll, page, 10, cancellationToken);
        return View(result);
    }

    [HttpGet]
    public IActionResult Create() => View(new Customer());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Customer customer, CancellationToken cancellationToken)
    {
        ModelState.Remove(nameof(Customer.CustomerCode));
        if (!ModelState.IsValid) return View(customer);
        var result = await customerService.CreateAsync(customer, User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(customer);
        }
        TempData["Success"] = "Customer created successfully.";
        return RedirectToAction(nameof(Details), new { id = result.Data!.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var customer = await FindCustomerAsync(id, cancellationToken);
        return customer is null ? NotFound() : View(customer);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var customer = await FindCustomerAsync(id, cancellationToken);
        return customer is null ? NotFound() : View(customer);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Customer input, CancellationToken cancellationToken)
    {
        ModelState.Remove(nameof(Customer.CustomerCode));
        if (!ModelState.IsValid) return View(input);
        var canManageAll = User.IsInRole(RoleNames.Admin);
        var result = await customerService.UpdateAsync(id, input, User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value, canManageAll, cancellationToken);
        if (!result.Succeeded)
        {
            if (result.Error == "Customer not found.") return NotFound();
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(input);
        }
        TempData["Success"] = "Customer updated successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var result = await customerService.DeactivateAsync(id, User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value, canManageAll: true, cancellationToken);
        if (!result.Succeeded) return NotFound();
        TempData["Success"] = "Customer deactivated successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private Task<Customer?> FindCustomerAsync(int id, CancellationToken cancellationToken)
    {
        var canReadAll = User.IsInRole(RoleNames.Admin);
        return customerService.GetAsync(id, User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value, canReadAll, cancellationToken);
    }
}
