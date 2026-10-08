using AcxiomCRM.Data;
using AcxiomCRM.Authorization;
using AcxiomCRM.Models;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public sealed class CustomerService(ApplicationDbContext dbContext, IAuditService auditService)
{
    public async Task<PagedResult<Customer>> GetPageAsync(string? search, string? status, string? ownerId,
        string userId, bool canReadAll, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = dbContext.Customers.AsNoTracking();
        if (!canReadAll)
        {
            var scope = await CrmAccessScope.UserIdsAsync(dbContext, userId, false, cancellationToken);
            query = query.Where(x => x.OwnerId != null && scope.Contains(x.OwnerId));
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.CustomerName.Contains(term)
                || (x.Email != null && x.Email.Contains(term))
                || (x.Phone != null && x.Phone.Contains(term))
                || (x.CompanyName != null && x.CompanyName.Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        if (canReadAll && !string.IsNullOrWhiteSpace(ownerId)) query = query.Where(x => x.OwnerId == ownerId);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.CustomerName).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<Customer>(items, page, pageSize, totalCount);
    }

    public async Task<Customer?> GetAsync(int id, string userId, bool canReadAll, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Customers.AsNoTracking().Where(x => x.Id == id);
        if (!canReadAll)
        {
            var scope = await CrmAccessScope.UserIdsAsync(dbContext, userId, false, cancellationToken);
            query = query.Where(x => x.OwnerId != null && scope.Contains(x.OwnerId));
        }
        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ServiceResult<Customer>> CreateAsync(Customer customer, string userId, CancellationToken cancellationToken = default)
    {
        customer.Status = "Active";
        if (await IsDuplicateAsync(customer, null, cancellationToken))
            return ServiceResult<Customer>.Failure("A customer with the same email, phone, or company/name already exists.");

        customer.CustomerCode = $"CUS-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        customer.CreatedById = userId;
        customer.OwnerId ??= userId;
        customer.CreatedDate = DateTime.UtcNow;
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(userId, "CreateCustomer", "Customers", nameof(Customer), customer.Id.ToString(), newValue: customer.CustomerName, cancellationToken: cancellationToken);
        return ServiceResult<Customer>.Success(customer);
    }

    public async Task<ServiceResult<Customer>> UpdateAsync(int id, Customer input, string userId, bool canManageAll, CancellationToken cancellationToken = default)
    {
        if (input.Status is not ("Active" or "Inactive")) return ServiceResult<Customer>.Failure("Choose a valid customer status.");
        if (!canManageAll && input.Status == "Inactive") return ServiceResult<Customer>.Failure("Only an administrator can deactivate a customer.");
        var query = dbContext.Customers.AsQueryable();
        if (!canManageAll)
        {
            var scope = await CrmAccessScope.UserIdsAsync(dbContext, userId, false, cancellationToken);
            query = query.Where(x => x.OwnerId != null && scope.Contains(x.OwnerId));
        }
        var customer = await query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (customer is null) return ServiceResult<Customer>.Failure("Customer not found.");
        if (await IsDuplicateAsync(input, id, cancellationToken))
            return ServiceResult<Customer>.Failure("A customer with the same email, phone, or company/name already exists.");

        var oldName = customer.CustomerName;
        customer.CustomerName = input.CustomerName.Trim();
        customer.Email = input.Email?.Trim();
        customer.Phone = input.Phone?.Trim();
        customer.CompanyName = input.CompanyName?.Trim();
        customer.Address = input.Address?.Trim();
        customer.City = input.City?.Trim();
        customer.State = input.State?.Trim();
        customer.Status = input.Status;
        customer.Notes = input.Notes?.Trim();
        if (canManageAll && !string.IsNullOrWhiteSpace(input.OwnerId)) customer.OwnerId = input.OwnerId;
        customer.ModifiedById = userId;
        customer.ModifiedDate = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.LogAsync(userId, "UpdateCustomer", "Customers", nameof(Customer), id.ToString(), oldValue: oldName, newValue: customer.CustomerName, cancellationToken: cancellationToken);
        return ServiceResult<Customer>.Success(customer);
    }

    public async Task<ServiceResult<Customer>> DeactivateAsync(int id, string userId, bool canManageAll, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Customers.AsQueryable();
        if (!canManageAll)
        {
            var scope = await CrmAccessScope.UserIdsAsync(dbContext, userId, false, cancellationToken);
            query = query.Where(x => x.OwnerId != null && scope.Contains(x.OwnerId));
        }
        var customer = await query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (customer is null) return ServiceResult<Customer>.Failure("Customer not found.");
        if (customer.Status != "Inactive")
        {
            customer.Status = "Inactive";
            customer.ModifiedById = userId;
            customer.ModifiedDate = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            await auditService.LogAsync(userId, "DeactivateCustomer", "Customers", nameof(Customer), id.ToString(), cancellationToken: cancellationToken);
        }
        return ServiceResult<Customer>.Success(customer);
    }

    private Task<bool> IsDuplicateAsync(Customer customer, int? exceptId, CancellationToken cancellationToken)
    {
        var email = customer.Email?.Trim().ToLower();
        var phone = customer.Phone?.Trim();
        var name = customer.CustomerName.Trim().ToLower();
        var company = customer.CompanyName?.Trim().ToLower();
        return dbContext.Customers.AnyAsync(existing => existing.Id != exceptId
            && ((email != null && existing.Email != null && existing.Email.ToLower() == email)
                || (phone != null && existing.Phone == phone)
                || (company != null && existing.CompanyName != null && existing.CompanyName.ToLower() == company
                    && existing.CustomerName.ToLower() == name)), cancellationToken);
    }
}
