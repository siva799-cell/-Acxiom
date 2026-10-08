using AcxiomCRM.Authorization;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Api.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/opportunities")]
[Authorize]
public sealed class OpportunitiesApiController(OpportunityService opportunityService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(string? search, string? stage, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await opportunityService.GetPageAsync(search, stage, UserId(), CanReadAll(), page, pageSize, cancellationToken);
        var items = result.Items.Select(ToDto).ToArray();
        return Ok(new ApiResponse<object>(true, "Opportunities retrieved.", new { items, result.Page, result.PageSize, result.TotalCount, result.PageCount }));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var opportunity = await opportunityService.GetAsync(id, UserId(), CanReadAll(), cancellationToken);
        return opportunity is null ? NotFound(new ApiResponse<object>(false, "Opportunity not found.")) : Ok(new ApiResponse<OpportunityDto>(true, "Opportunity retrieved.", ToDto(opportunity)));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager,SalesExecutive")]
    public async Task<IActionResult> Create(OpportunityWriteDto dto, CancellationToken cancellationToken)
    {
        var canAssign = User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.Manager);
        var result = await opportunityService.CreateAsync(Map(dto), UserId(), cancellationToken, User.IsInRole(RoleNames.Admin), canAssign);
        if (!result.Succeeded) return BadRequest(new ApiResponse<object>(false, result.Error!));
        var response = ToDto(result.Data!);
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, new ApiResponse<OpportunityDto>(true, "Opportunity created successfully.", response));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, OpportunityWriteDto dto, CancellationToken cancellationToken)
    {
        var canAssign = User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.Manager);
        var result = await opportunityService.UpdateAsync(id, Map(dto), UserId(), CanReadAll(), cancellationToken, canAssign);
        if (!result.Succeeded)
        {
            if (result.Error == "Opportunity not found.") return NotFound(new ApiResponse<object>(false, result.Error));
            return BadRequest(new ApiResponse<object>(false, result.Error!));
        }
        return Ok(new ApiResponse<OpportunityDto>(true, "Opportunity updated successfully.", ToDto(result.Data!)));
    }

    private string UserId() => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
    private bool CanReadAll() => User.IsInRole(RoleNames.Admin);
    private static Opportunity Map(OpportunityWriteDto dto) => new() { OpportunityName = dto.OpportunityName, CustomerId = dto.CustomerId, LeadId = dto.LeadId, Amount = dto.Amount, Stage = dto.Stage, Probability = dto.Probability, ExpectedCloseDate = dto.ExpectedCloseDate, AssignedToId = dto.AssignedToId, Notes = dto.Notes };
    private static OpportunityDto ToDto(Opportunity item) => new(item.Id, item.OpportunityName, item.CustomerId, item.LeadId, item.Amount, item.Stage, item.Probability, item.ExpectedCloseDate, item.Status, item.WeightedAmount, item.AssignedToId);
}
