using AcxiomCRM.Data;
using AcxiomCRM.Models;

namespace AcxiomCRM.Services;

public sealed class AuditService(ApplicationDbContext dbContext, IHttpContextAccessor httpContextAccessor) : IAuditService
{
    public async Task LogAsync(string? userId, string action, string? module, string? entityName = null,
        string? recordId = null, string? result = "Success", string? details = null,
        string? oldValue = null, string? newValue = null, CancellationToken cancellationToken = default)
    {
        var audit = new AuditLog
        {
            UserId = userId,
            Action = action,
            Module = module,
            EntityName = entityName,
            RecordId = recordId,
            Result = result ?? "Success",
            Details = details,
            OldValue = oldValue,
            NewValue = newValue,
            IpAddress = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            CreatedDate = DateTime.UtcNow
        };
        dbContext.AuditLogs.Add(audit);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
