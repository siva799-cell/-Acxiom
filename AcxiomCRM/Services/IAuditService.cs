namespace AcxiomCRM.Services;

public interface IAuditService
{
    Task LogAsync(string? userId, string action, string? module, string? entityName = null,
        string? recordId = null, string? result = "Success", string? details = null,
        string? oldValue = null, string? newValue = null, CancellationToken cancellationToken = default);
}
