using AcxiomCRM.Authorization;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = RoleNames.Admin)]
public sealed class RolesController(RoleManager<IdentityRole> roleManager, UserManager<ApplicationUser> userManager) : Controller
{
    public async Task<IActionResult> Index()
    {
        var roles = new List<RoleSummaryViewModel>();
        foreach (var name in RoleNames.All)
        {
            var role = await roleManager.FindByNameAsync(name);
            var count = role is null ? 0 : (await userManager.GetUsersInRoleAsync(name)).Count;
            roles.Add(new RoleSummaryViewModel(name, Description(name), count));
        }
        return View(roles);
    }

    private static string Description(string role) => role switch
    {
        RoleNames.Admin => "System administration, user access, and all CRM records.",
        RoleNames.Manager => "Organization-wide CRM visibility and business workflows.",
        _ => "Assigned customers, leads, opportunities, follow-ups, and activities."
    };
}

public sealed record RoleSummaryViewModel(string Name, string Description, int UserCount);
