using AcxiomCRM.Authorization;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Api.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/leads")]
[Authorize]
public sealed class LeadsApiController(LeadService leadService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(string? search, string? status, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await leadService.GetPageAsync(search, status, UserId(), CanReadAll(), page, pageSize, cancellationToken);
        var items = result.Items.Select(ToDto).ToArray();
        return Ok(new ApiResponse<object>(true, "Leads retrieved.", new { items, result.Page, result.PageSize, result.TotalCount, result.PageCount }));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var lead = await leadService.GetAsync(id, UserId(), CanReadAll(), cancellationToken);
        return lead is null ? NotFound(new ApiResponse<object>(false, "Lead not found.")) : Ok(new ApiResponse<LeadDto>(true, "Lead retrieved.", ToDto(lead)));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager,SalesExecutive")]
    public async Task<IActionResult> Create(LeadWriteDto dto, CancellationToken cancellationToken)
    {
        var lead = Map(dto);
        var canAssign = User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.Manager);
        var result = await leadService.CreateAsync(lead, UserId(), cancellationToken, canAssign, User.IsInRole(RoleNames.Admin));
        if (!result.Succeeded) return BadRequest(new ApiResponse<object>(false, result.Error!));
        var response = ToDto(result.Data!);
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, new ApiResponse<LeadDto>(true, "Lead created successfully.", response));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, LeadWriteDto dto, CancellationToken cancellationToken)
    {
        var canAssign = User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.Manager);
        var result = await leadService.UpdateAsync(id, Map(dto), UserId(), CanReadAll(), cancellationToken, canAssign);
        if (!result.Succeeded)
        {
            if (result.Error == "Lead not found.") return NotFound(new ApiResponse<object>(false, result.Error));
            return BadRequest(new ApiResponse<object>(false, result.Error!));
        }
        return Ok(new ApiResponse<LeadDto>(true, "Lead updated successfully.", ToDto(result.Data!)));
    }

    [HttpPost("{id:int}/convert")]
    public async Task<IActionResult> Convert(int id, CancellationToken cancellationToken)
    {
        var result = await leadService.ConvertAsync(id, UserId(), CanReadAll(), cancellationToken);
        if (!result.Succeeded) return BadRequest(new ApiResponse<object>(false, result.Error!));
        return Ok(new ApiResponse<LeadDto>(true, "Lead converted successfully.", ToDto(result.Data!)));
    }

    private string UserId() => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
    private bool CanReadAll() => User.IsInRole(RoleNames.Admin);
    private static Lead Map(LeadWriteDto dto) => new() { LeadName = dto.LeadName, Email = dto.Email, Phone = dto.Phone, CompanyName = dto.CompanyName, Source = dto.Source, Status = dto.Status, Priority = dto.Priority, ExpectedValue = dto.ExpectedValue, AssignedToId = dto.AssignedToId, Notes = dto.Notes };
    private static LeadDto ToDto(Lead lead) => new(lead.Id, lead.LeadCode, lead.LeadName, lead.CompanyName, lead.Email, lead.Phone, lead.Status, lead.Priority, lead.ExpectedValue, lead.AssignedToId, lead.CreatedDate);
}
