using AcxiomCRM.Authorization;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Api.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/customers")]
[Authorize]
public sealed class CustomersApiController(CustomerService customerService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(string? search, string? status, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
        var canReadAll = User.IsInRole(RoleNames.Admin);
        var result = await customerService.GetPageAsync(search, status, null, userId, canReadAll, page, pageSize, cancellationToken);
        var items = result.Items.Select(ToDto).ToArray();
        return Ok(new ApiResponse<object>(true, "Customers retrieved.", new { items, result.Page, result.PageSize, result.TotalCount, result.PageCount }));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
        var customer = await customerService.GetAsync(id, userId, CanReadAll(), cancellationToken);
        return customer is null ? NotFound(new ApiResponse<object>(false, "Customer not found.")) : Ok(new ApiResponse<CustomerDto>(true, "Customer retrieved.", ToDto(customer)));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager,SalesExecutive")]
    public async Task<IActionResult> Create(CustomerWriteDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
        var customer = new Customer
        {
            CustomerName = dto.CustomerName,
            Email = dto.Email,
            Phone = dto.Phone,
            CompanyName = dto.CompanyName,
            Address = dto.Address,
            City = dto.City,
            State = dto.State,
            Notes = dto.Notes
        };
        var result = await customerService.CreateAsync(customer, userId, cancellationToken);
        if (!result.Succeeded) return Conflict(new ApiResponse<object>(false, result.Error!));
        var response = ToDto(result.Data!);
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, new ApiResponse<CustomerDto>(true, "Customer created successfully.", response));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CustomerWriteDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
        var entity = new Customer
        {
            CustomerName = dto.CustomerName,
            Email = dto.Email,
            Phone = dto.Phone,
            CompanyName = dto.CompanyName,
            Address = dto.Address,
            City = dto.City,
            State = dto.State,
            Status = dto.Status,
            Notes = dto.Notes
        };
        var result = await customerService.UpdateAsync(id, entity, userId, CanReadAll(), cancellationToken);
        if (!result.Succeeded)
        {
            if (result.Error == "Customer not found.") return NotFound(new ApiResponse<object>(false, result.Error));
            if (result.Error!.StartsWith("A customer with", StringComparison.Ordinal)) return Conflict(new ApiResponse<object>(false, result.Error));
            return BadRequest(new ApiResponse<object>(false, result.Error));
        }
        return Ok(new ApiResponse<CustomerDto>(true, "Customer updated successfully.", ToDto(result.Data!)));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await customerService.DeactivateAsync(id, User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value, true, cancellationToken);
        return result.Succeeded
            ? Ok(new ApiResponse<CustomerDto>(true, "Customer deactivated successfully.", ToDto(result.Data!)))
            : NotFound(new ApiResponse<object>(false, result.Error!));
    }

    private bool CanReadAll() => User.IsInRole(RoleNames.Admin);

    private static CustomerDto ToDto(Customer customer) => new(customer.Id, customer.CustomerCode, customer.CustomerName,
        customer.Email, customer.Phone, customer.CompanyName, customer.Status, customer.OwnerId, customer.CreatedDate);
}
