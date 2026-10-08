using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Authorization;

public static class CrmAccessScope
{
    public static async Task<List<string>> UserIdsAsync(ApplicationDbContext dbContext, string userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        if (isAdmin) return [];
        var userIds = await dbContext.Users.Where(user => user.ManagerId == userId)
            .Select(user => user.Id).ToListAsync(cancellationToken);
        userIds.Add(userId);
        return userIds;
    }

    public static async Task<bool> CanAssignToAsync(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager,
        string actorId, string? targetUserId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(targetUserId)) return false;
        var target = await dbContext.Users.FirstOrDefaultAsync(user => user.Id == targetUserId && user.IsActive, cancellationToken);
        if (target is null || !await userManager.IsInRoleAsync(target, RoleNames.SalesExecutive)) return false;
        return isAdmin || target.ManagerId == actorId;
    }
}
